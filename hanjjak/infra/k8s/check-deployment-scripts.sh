#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"

for script in "$SCRIPT_DIR"/*.sh; do
  bash -n "$script"
done

export EXTERNAL_SMOKE_URL=https://example.invalid/healthz
export OFFLINE_REWARD_TEST_ALL_ACCOUNTS=true
export OFFLINE_REWARD_TEST_REWARD_MULTIPLIER=0.5
export OFFLINE_REWARD_TEST_GRACE_PERIOD_SECONDS=0
export OFFLINE_REWARD_TEST_BUCKET_SECONDS=60
export OFFLINE_REWARD_TEST_ACCRUAL_CAP_SECONDS=28800
source "$SCRIPT_DIR/deployment-common.sh"
validate_release regression-check
declare -F restore_annotated_offline_policy >/dev/null
grep -q 'restore_annotated_offline_policy' "$SCRIPT_DIR/rollback.sh"
grep -q 'previous-offline-bucket' "$SCRIPT_DIR/deployment-common.sh"

render_manifest kafka.yaml --image-tag regression-check --event-consumers-image-name web >/dev/null
render_manifest migration-job.yaml --image-tag regression-check --job-name migration-regression-check >/dev/null
render_manifest backfill-job.yaml --image-tag regression-check --job-name backfill-regression-check >/dev/null

base_manifest="$(render_manifest base.yaml --image-tag regression-check)"
python3 -c '
import sys
import yaml

documents = list(yaml.safe_load_all(sys.stdin))
policies = {item["metadata"]["name"]: item for item in documents if item.get("kind") == "NetworkPolicy"}
postgres_sources = policies["allow-postgres"]["spec"]["ingress"][0]["from"]
assert any(source.get("podSelector", {}).get("matchLabels", {}).get("app.kubernetes.io/name") == "backfill" for source in postgres_sources)
job_selector = policies["allow-database-jobs"]["spec"]["podSelector"]["matchExpressions"]
assert any(expression.get("key") == "app.kubernetes.io/name" and "backfill" in expression.get("values", []) for expression in job_selector)
' <<<"$base_manifest"
render_manifest smoke-pod.yaml >/dev/null

fake_kubectl() {
  [[ "$1" == get ]] || return 1
  case "$2" in
    deployment)
      case "$3" in
        game-api) printf '%s\n' '{"spec":{"replicas":2,"template":{"spec":{"containers":[{"name":"game-api","image":"docker.io/hanjjak/game-api:current"}]}}}}' ;;
        admin-console) printf '%s\n' '{"spec":{"replicas":2,"template":{"spec":{"containers":[{"name":"admin-console","image":"docker.io/hanjjak/admin-console:current"}]}}}}' ;;
        cloudflared) printf '%s\n' '{"spec":{"replicas":2}}' ;;
        event-consumers) printf '%s\n' '{"spec":{"replicas":1,"template":{"spec":{"containers":[{"name":"event-consumers","image":"docker.io/hanjjak/web:current-event-consumers"}]}}}}' ;;
        *) return 1 ;;
      esac
      ;;
    deployment/web-green)
      printf '%s\n' '{"spec":{"replicas":0,"template":{"spec":{"containers":[{"name":"web","image":"docker.io/hanjjak/web:previous"}]}}}}'
      ;;
    configmap)
      printf '%s\n' '{"data":{"PUBLIC_ORIGIN":"https://example.invalid","POSTGRES_DB":"hanjjak","POSTGRES_USER":"hanjjak","OFFLINE_REWARD_TEST_ALL_ACCOUNTS":"true","OFFLINE_REWARD_TEST_REWARD_MULTIPLIER":"1","OFFLINE_REWARD_TEST_GRACE_PERIOD_SECONDS":"0","OFFLINE_REWARD_TEST_BUCKET_SECONDS":"10","OFFLINE_REWARD_TEST_ACCRUAL_CAP_SECONDS":"80","BASIC_AUTH_ENABLED":"true","BASIC_AUTH_USERNAME":"regression-user"}}'
      ;;
    service)
      [[ "$3" == web ]]
      printf 'blue'
      ;;
    *) return 1 ;;
  esac
}

acquire_maintenance_lock() { :; }
release_maintenance_lock() { :; }
KUBECTL=(fake_kubectl)
begin_operation regression-check
trap - EXIT

[[ "$active_slot" == blue ]]
[[ "$inactive_slot" == green ]]
[[ "$previous_offline_multiplier" == 1 ]]
[[ "$previous_offline_grace" == 0 ]]
[[ "$previous_offline_bucket" == 10 ]]
[[ "$previous_offline_cap" == 80 ]]
[[ "$previous_basic_auth_enabled" == true ]]
[[ "$previous_basic_auth_username" == regression-user ]]

printf 'Deployment script regression checks passed\n'
