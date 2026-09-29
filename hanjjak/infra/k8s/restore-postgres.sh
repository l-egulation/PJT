#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
K8S_NAMESPACE="${K8S_NAMESPACE:-hanjjak}"
KUBECTL=(kubectl)
[[ -z "${KUBE_CONTEXT:-}" ]] || KUBECTL+=(--context "$KUBE_CONTEXT")
KUBECTL+=(--namespace "$K8S_NAMESPACE")

# Restore and deployment/registry pruning share this lock.  The local
# confirmation and file checks intentionally precede lock acquisition so a
# rejected invocation never mutates cluster state.
# shellcheck source=/dev/null
source "$SCRIPT_DIR/maintenance-lock.sh"
[[ "${RESTORE_CONFIRM:-}" == "YES" ]] || { echo "Set RESTORE_CONFIRM=YES to restore" >&2; exit 1; }
BACKUP_FILE="${1:?usage: RESTORE_CONFIRM=YES restore-postgres.sh BACKUP_FILE}"
[[ -s "$BACKUP_FILE" ]] || { echo "Backup file is missing or empty: $BACKUP_FILE" >&2; exit 1; }

RESTORE_TOKEN="$(date -u +%Y%m%dT%H%M%SZ)-$$-${RANDOM}"
MAINTENANCE_LOCK_OWNER="restore:${RESTORE_TOKEN}"
acquire_maintenance_lock "$MAINTENANCE_LOCK_OWNER"

RESTORE_SUCCEEDED=0
WORKLOADS_SHUTDOWN_STARTED=0
WORKLOADS_STOPPED=0
CRONJOB_SUSPENDED=0
REMOTE_DUMP="/tmp/hanjjak-restore-${RESTORE_TOKEN}.dump"
SCRATCH_DB="hanjjak_restore_${RESTORE_TOKEN//[^a-zA-Z0-9_]/_}"
die() {
  echo "$*" >&2
  exit 1
}

cleanup() {
  local status=$?
  trap - EXIT

  # Leave the uploaded dump in place on every failure for forensic recovery.
  # It is removed only after the final restore and all preceding commands have
  # succeeded; the caller's original file is never modified or removed.
  if (( status == 0 && RESTORE_SUCCEEDED == 1 )); then
    "${KUBECTL[@]}" exec postgres-0 -- sh -eu -c 'rm -f -- "$1"' sh "$REMOTE_DUMP" || status=1
  fi

  release_maintenance_lock "$MAINTENANCE_LOCK_OWNER" || status=1

  if (( WORKLOADS_STOPPED == 1 )); then
    if (( RESTORE_SUCCEEDED == 1 && status == 0 )); then
      printf 'PostgreSQL restore succeeded; application workloads remain stopped and postgres-backup remains suspended. Resume only with an explicit approved deployment, then explicitly unsuspend the backup CronJob if required.\n'
    else
      printf 'PostgreSQL restore failed; application workloads remain stopped and postgres-backup remains suspended. Inspect the preserved backup and rerun this restore or an approved deployment explicitly; this script never resumes writers.\n' >&2
    fi
  elif (( WORKLOADS_SHUTDOWN_STARTED == 1 )); then
    printf 'PostgreSQL restore did not complete; workload shutdown may be partial and postgres-backup remains suspended. Inspect cluster state and recover explicitly; this script never resumes writers.\n' >&2
  elif (( CRONJOB_SUSPENDED == 1 )); then
    printf 'PostgreSQL restore did not complete; application workloads were not stopped and postgres-backup remains suspended. Inspect cluster state and recover explicitly; this script never resumes writers.\n' >&2
  fi
  exit "$status"
}
trap cleanup EXIT
for resource in statefulset/postgres deployment/game-api deployment/admin-console deployment/web-blue deployment/web-green deployment/cloudflared cronjob/postgres-backup; do
  "${KUBECTL[@]}" get "$resource" >/dev/null 2>&1 || die "$resource is missing; refusing restore"
done


"${KUBECTL[@]}" rollout status statefulset/postgres --timeout=300s
"${KUBECTL[@]}" cp "$BACKUP_FILE" "postgres-0:$REMOTE_DUMP"

# Validate the archive and perform a complete restore in a uniquely named
# scratch database before any application is stopped or the real database is
# dropped.  The scratch database is the only database this block removes.
"${KUBECTL[@]}" exec postgres-0 -- sh -eu -c '
  dump_path="$1"
  scratch_db="$2"
  export PGPASSWORD="$(cat /var/run/hanjjak-secrets/db-password)"
  pg_restore --list "$dump_path" >/dev/null
  # Never drop an existing database: a collision means the generated name was
  # not unique and must fail closed rather than destroy an unrelated DB.
  createdb --username="$POSTGRES_USER" "$scratch_db"
  cleanup_scratch() {
    status=$?
    trap - EXIT
    dropdb --if-exists --force --username="$POSTGRES_USER" "$scratch_db" >/dev/null 2>&1 || :
    exit "$status"
  }
  trap cleanup_scratch EXIT
  pg_restore --exit-on-error --single-transaction --no-owner --no-privileges \
    --username="$POSTGRES_USER" --dbname="$scratch_db" "$dump_path"
  dropdb --if-exists --force --username="$POSTGRES_USER" "$scratch_db"
  trap - EXIT
' sh "$REMOTE_DUMP" "$SCRATCH_DB"

writer_jobs() {
  local query
  query=".items[]
    | select(
        ((.metadata.labels[\"app.kubernetes.io/name\"] // \"\") == \"migration\") or
        ((.metadata.labels[\"app.kubernetes.io/name\"] // \"\") == \"postgres-backup\") or
        ((.metadata.labels[\"app.kubernetes.io/name\"] // \"\") == \"backfill\") or
        ((.spec.template.metadata.labels[\"app.kubernetes.io/name\"] // \"\") == \"migration\") or
        ((.spec.template.metadata.labels[\"app.kubernetes.io/name\"] // \"\") == \"postgres-backup\") or
        ((.spec.template.metadata.labels[\"app.kubernetes.io/name\"] // \"\") == \"backfill\")
      )
    | select((([.status.conditions[]? | select(.status == \"True\") | .type] | index(\"Complete\")) == null) and
            (([.status.conditions[]? | select(.status == \"True\") | .type] | index(\"Failed\")) == null))
    | .metadata.name"
  "${KUBECTL[@]}" get jobs -o json | jq -r "$query"
}

writer_pods() {
  "${KUBECTL[@]}" get pods -l 'app.kubernetes.io/name in (migration,postgres-backup,backfill)' -o json |
    jq -r '.items[] | select(.status.phase == "Pending" or .status.phase == "Running" or .status.phase == "Unknown") | .metadata.name'
}

ensure_no_active_writer_jobs() {
  local active_jobs active_pods
  if ! active_jobs="$(writer_jobs)"; then
    echo "Unable to inspect migration/backup jobs; refusing destructive restore" >&2
    return 1
  fi
  if [[ -n "$active_jobs" ]]; then
    echo "Active migration/backup/backfill jobs exist; refusing destructive restore: $active_jobs" >&2
    return 1
  fi
  if ! active_pods="$(writer_pods)"; then
    echo "Unable to inspect migration/backup pods; refusing destructive restore" >&2
    return 1
  fi
  if [[ -n "$active_pods" ]]; then
    echo "Migration/backup/backfill pods are still active; refusing destructive restore: $active_pods" >&2
    return 1
  fi
}

wait_for_pods_terminated() {
  local selector="$1"
  local deadline=$(( $(date +%s) + 180 ))
  while :; do
    local pods
    if ! pods="$("${KUBECTL[@]}" get pods -l "$selector" -o name)"; then
      echo "Unable to inspect application pods ($selector); refusing destructive restore" >&2
      return 1
    fi
    [[ -z "$pods" ]] && return 0
    if (( $(date +%s) >= deadline )); then
      echo "Timed out waiting for application pods to terminate ($selector): $pods" >&2
      return 1
    fi
    sleep 2
  done
}

# Check before suspension as well as immediately before dropping the real DB.
# No unrelated Job is deleted; an active writer is a fail-safe refusal.
ensure_no_active_writer_jobs
"${KUBECTL[@]}" patch cronjob postgres-backup --type=merge -p '{"spec":{"suspend":true}}' >/dev/null
CRONJOB_SUSPENDED=1
ensure_no_active_writer_jobs

WORKLOADS_SHUTDOWN_STARTED=1
"${KUBECTL[@]}" scale deployment/game-api deployment/admin-console deployment/web-blue deployment/web-green deployment/cloudflared --replicas=0
wait_for_pods_terminated 'app.kubernetes.io/name=game-api'
wait_for_pods_terminated 'app.kubernetes.io/name=admin-console'
wait_for_pods_terminated 'app.kubernetes.io/name=web'
wait_for_pods_terminated 'app.kubernetes.io/name=cloudflared'
WORKLOADS_STOPPED=1
ensure_no_active_writer_jobs


# All identifiers are passed as arguments to PostgreSQL client utilities, not
# interpolated into SQL.  The real database is touched only after validation,
# writer shutdown, and the final active-job check have all succeeded.
"${KUBECTL[@]}" exec postgres-0 -- sh -eu -c '
  dump_path="$1"
  export PGPASSWORD="$(cat /var/run/hanjjak-secrets/db-password)"
  dropdb --if-exists --force --username="$POSTGRES_USER" "$POSTGRES_DB"
  createdb --username="$POSTGRES_USER" "$POSTGRES_DB"
  pg_restore --exit-on-error --single-transaction --no-owner --no-privileges \
    --username="$POSTGRES_USER" --dbname="$POSTGRES_DB" "$dump_path"
' sh "$REMOTE_DUMP"
RESTORE_SUCCEEDED=1
