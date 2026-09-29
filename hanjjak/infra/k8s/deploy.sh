#!/usr/bin/env bash
set -euo pipefail
source "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)/deployment-common.sh"

: "${IMAGE_TAG:?IMAGE_TAG is required}"
: "${PUBLIC_ORIGIN:?PUBLIC_ORIGIN is required}"
validate_release "$IMAGE_TAG"
render_manifest kafka.yaml --image-tag "$IMAGE_TAG" --event-consumers-image-name web \
  | "${KUBECTL[@]}" apply -f - >/dev/null
"${KUBECTL[@]}" rollout status statefulset/kafka --timeout=300s
begin_operation deploy
[[ "${POSTGRES_DB:-$configured_db}" == "$configured_db" ]] || die "POSTGRES_DB differs from bootstrap; migrate database configuration separately"
[[ "${POSTGRES_USER:-$configured_user}" == "$configured_user" ]] || die "POSTGRES_USER differs from bootstrap; migrate database configuration separately"
"${KUBECTL[@]}" rollout status statefulset/postgres --timeout=300s

job_suffix="$(date -u +%Y%m%d%H%M%S)-$$"
backup_job="predeploy-backup-$job_suffix"
"${KUBECTL[@]}" create job --from=cronjob/postgres-backup "$backup_job" >/dev/null
wait_completion "job/$backup_job" 300
migration_job="migration-$job_suffix"
render_manifest migration-job.yaml --image-tag "$IMAGE_TAG" --job-name "$migration_job" \
  | "${KUBECTL[@]}" create -f - >/dev/null
wait_completion "job/$migration_job" 300
release_changed=1
offline_patch="$(jq -cn \
  --arg origin "$PUBLIC_ORIGIN" \
  --arg all "${OFFLINE_REWARD_TEST_ALL_ACCOUNTS:-true}" \
  --arg ids "${OFFLINE_REWARD_TEST_ACCOUNT_IDS:-}" \
  --arg multiplier "${OFFLINE_REWARD_TEST_REWARD_MULTIPLIER:-0.5}" \
  --arg grace "${OFFLINE_REWARD_TEST_GRACE_PERIOD_SECONDS:-0}" \
  --arg bucket "${OFFLINE_REWARD_TEST_BUCKET_SECONDS:-60}" \
  --arg cap "${OFFLINE_REWARD_TEST_ACCRUAL_CAP_SECONDS:-28800}" \
  --arg basicAuthEnabled "${BASIC_AUTH_ENABLED:-false}" \
  --arg basicAuthUsername "${BASIC_AUTH_USERNAME:-hanjjak}" \
  '{data:{PUBLIC_ORIGIN:$origin,OFFLINE_REWARD_TEST_ALL_ACCOUNTS:$all,OFFLINE_REWARD_TEST_ACCOUNT_IDS:$ids,OFFLINE_REWARD_TEST_REWARD_MULTIPLIER:$multiplier,OFFLINE_REWARD_TEST_GRACE_PERIOD_SECONDS:$grace,OFFLINE_REWARD_TEST_BUCKET_SECONDS:$bucket,OFFLINE_REWARD_TEST_ACCRUAL_CAP_SECONDS:$cap,BASIC_AUTH_ENABLED:$basicAuthEnabled,BASIC_AUTH_USERNAME:$basicAuthUsername}}')"
"${KUBECTL[@]}" patch configmap application-config --type=merge -p "$offline_patch" >/dev/null
apply_release "$IMAGE_TAG"
backfill_job="backfill-$job_suffix"
render_manifest backfill-job.yaml --image-tag "$IMAGE_TAG" --job-name "$backfill_job" \
  | "${KUBECTL[@]}" create -f - >/dev/null
wait_completion "job/$backfill_job" 900
printf 'K3s deployment succeeded: image=%s web-slot=%s\n' "$IMAGE_TAG" "$inactive_slot"
