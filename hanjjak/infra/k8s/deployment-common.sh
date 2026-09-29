#!/usr/bin/env bash
# Shared by deployment and application rollback; neither operation restores SQL.
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
K8S_NAMESPACE="${K8S_NAMESPACE:-hanjjak}"
WEB_DRAIN_SECONDS="${WEB_DRAIN_SECONDS:-30}"
EVENT_CONSUMERS_DEPLOY_ENABLED="${EVENT_CONSUMERS_DEPLOY_ENABLED:-false}"
KUBECTL=(kubectl)
[[ -z "${KUBE_CONTEXT:-}" ]] || KUBECTL+=(--context "$KUBE_CONTEXT")
KUBECTL+=(--namespace "$K8S_NAMESPACE")
source "$SCRIPT_DIR/maintenance-lock.sh"

die() { echo "$*" >&2; exit 1; }
validate_release() {
  [[ "$1" =~ ^[A-Za-z0-9_][A-Za-z0-9_.-]{0,127}$ && "$1" != latest && "$1" != bootstrap ]] || die "A concrete release image tag is required"
  [[ "$WEB_DRAIN_SECONDS" =~ ^[0-9]+$ ]] || die "WEB_DRAIN_SECONDS must be an integer"
  [[ "$EVENT_CONSUMERS_DEPLOY_ENABLED" == true || "$EVENT_CONSUMERS_DEPLOY_ENABLED" == false ]] || die "EVENT_CONSUMERS_DEPLOY_ENABLED must be true or false"
  [[ "${BASIC_AUTH_ENABLED:-false}" == true || "${BASIC_AUTH_ENABLED:-false}" == false ]] || die "BASIC_AUTH_ENABLED must be true or false"
  [[ -n "${EXTERNAL_SMOKE_URL:-}" ]] || die "EXTERNAL_SMOKE_URL is required"
  [[ "${OFFLINE_REWARD_TEST_ALL_ACCOUNTS:-false}" == true || "${OFFLINE_REWARD_TEST_ALL_ACCOUNTS:-false}" == false ]] || die "OFFLINE_REWARD_TEST_ALL_ACCOUNTS must be true or false"
  if [[ "${OFFLINE_REWARD_TEST_ALL_ACCOUNTS:-false}" == true ]]; then
    [[ "${OFFLINE_REWARD_TEST_REWARD_MULTIPLIER:-}" == 0.5 ]] || die "Global offline test mode requires multiplier 0.5"
    [[ "${OFFLINE_REWARD_TEST_GRACE_PERIOD_SECONDS:-}" == 0 ]] || die "Global offline test mode requires grace period 0"
    [[ "${OFFLINE_REWARD_TEST_BUCKET_SECONDS:-}" == 60 ]] || die "Global offline test mode requires bucket 60"
    [[ "${OFFLINE_REWARD_TEST_ACCRUAL_CAP_SECONDS:-}" == 28800 ]] || die "Global offline test mode requires accrual cap 28800"
  fi
}
render_manifest() {
  local input="$1"; shift
  python3 "$SCRIPT_DIR/render.py" --input "$SCRIPT_DIR/$input" \
    --namespace "$K8S_NAMESPACE" "$@"
}
wait_completion() {
  local resource="$1" seconds="$2" deadline=$((SECONDS + $2)) state
  while (( SECONDS < deadline )); do
    state="$("${KUBECTL[@]}" get "$resource" -o json)" || return 1
    if [[ "$resource" == pod/* ]]; then
      case "$(jq -r '.status.phase' <<<"$state")" in
        Succeeded) return 0 ;;
        Failed) break ;;
      esac
    else
      if jq -e '.status.conditions[]? | select(.type == "Complete" and .status == "True")' <<<"$state" >/dev/null; then return 0; fi
      if jq -e '.status.conditions[]? | select(.type == "Failed" and .status == "True")' <<<"$state" >/dev/null; then break; fi
    fi
    sleep 2
  done
  "${KUBECTL[@]}" logs "$resource" >&2 || true
  echo "$resource did not complete successfully within ${seconds}s" >&2
  return 1
}
run_smoke() {
  local mode="$1" command ips
  "${KUBECTL[@]}" delete pod deployment-smoke --ignore-not-found --wait=true >/dev/null || return 1
  if [[ "$mode" == service ]]; then
    render_manifest smoke-pod.yaml | "${KUBECTL[@]}" apply -f - >/dev/null || return 1
  else
    if [[ "$mode" == api ]]; then
      command='set -eu; curl --fail --silent --show-error --connect-timeout 3 --max-time 10 --retry 2 --retry-all-errors --retry-delay 1 http://game-api:8080/actuator/health/readiness >/dev/null'
    else
      ips="$("${KUBECTL[@]}" get pods -l "app.kubernetes.io/name=web,app.kubernetes.io/slot=$mode" -o json | jq -r '.items[] | select(.metadata.deletionTimestamp == null) | .status.podIP // empty')" || return 1
      [[ -n "$ips" ]] || return 1
      command='set -eu;'
      for ip in $ips; do
        command+=" curl --fail --silent --show-error --connect-timeout 3 --max-time 10 --retry 2 --retry-all-errors --retry-delay 1 http://${ip}:8080/healthz >/dev/null;"
        command+=" curl --fail --silent --show-error --connect-timeout 3 --max-time 10 --retry 2 --retry-all-errors --retry-delay 1 http://${ip}:8080/api/v1/auth/session >/dev/null;"
      done
    fi
    "${KUBECTL[@]}" run deployment-smoke --labels=app.kubernetes.io/name=deployment-smoke \
      --image="curlimages/curl:8.22.0" --restart=Never --dry-run=client -o json \
      --command -- sh -c "$command" | jq '.spec.automountServiceAccountToken = false' \
      | "${KUBECTL[@]}" create -f - >/dev/null || return 1
  fi
  wait_completion pod/deployment-smoke 120 || return 1
  "${KUBECTL[@]}" delete pod deployment-smoke --wait=true >/dev/null
}
select_web() {
  "${KUBECTL[@]}" patch service web --type=merge \
    -p "$(jq -cn --arg slot "$1" '{spec:{selector:{"app.kubernetes.io/name":"web","app.kubernetes.io/slot":$slot}}}')" >/dev/null
}
set_web_image() {
  "${KUBECTL[@]}" set image "deployment/web-$1" "web=$2" "web-assets=$2" >/dev/null
}
restore_release() {
  local failed=0
  # Never remove the new slot if the Service could still point to it.
  if ! select_web "$active_slot"; then
    echo "Cannot restore service/web selector; both slots retained for manual recovery" >&2
    return 1
  fi
  "${KUBECTL[@]}" patch configmap application-config --type=merge \
    -p "$(jq -cn --arg origin "$previous_origin" --arg all "$previous_offline_all" --arg ids "$previous_offline_ids" --arg multiplier "$previous_offline_multiplier" --arg grace "$previous_offline_grace" --arg bucket "$previous_offline_bucket" --arg cap "$previous_offline_cap" --arg basicAuthEnabled "$previous_basic_auth_enabled" --arg basicAuthUsername "$previous_basic_auth_username" '{data:{PUBLIC_ORIGIN:$origin,OFFLINE_REWARD_TEST_ALL_ACCOUNTS:$all,OFFLINE_REWARD_TEST_ACCOUNT_IDS:$ids,OFFLINE_REWARD_TEST_REWARD_MULTIPLIER:$multiplier,OFFLINE_REWARD_TEST_GRACE_PERIOD_SECONDS:$grace,OFFLINE_REWARD_TEST_BUCKET_SECONDS:$bucket,OFFLINE_REWARD_TEST_ACCRUAL_CAP_SECONDS:$cap,BASIC_AUTH_ENABLED:$basicAuthEnabled,BASIC_AUTH_USERNAME:$basicAuthUsername}}')" >/dev/null || failed=1
  "${KUBECTL[@]}" set image deployment/game-api "game-api=$previous_api_image" >/dev/null || failed=1
  "${KUBECTL[@]}" scale deployment/game-api --replicas="$previous_api_replicas" >/dev/null || failed=1
  "${KUBECTL[@]}" set image deployment/admin-console "admin-console=$previous_admin_image" >/dev/null || failed=1
  "${KUBECTL[@]}" scale deployment/admin-console --replicas="$previous_admin_replicas" >/dev/null || failed=1
  if [[ "$EVENT_CONSUMERS_DEPLOY_ENABLED" == true ]]; then
    "${KUBECTL[@]}" set image deployment/event-consumers "event-consumers=$previous_consumer_image" >/dev/null || failed=1
    "${KUBECTL[@]}" scale deployment/event-consumers --replicas="$previous_consumer_replicas" >/dev/null || failed=1
  fi
  if (( previous_api_replicas > 0 )); then
    "${KUBECTL[@]}" rollout status deployment/game-api --timeout=300s || failed=1
  fi
  if (( previous_admin_replicas > 0 )); then
    "${KUBECTL[@]}" rollout status deployment/admin-console --timeout=180s || failed=1
  fi
  if [[ "$EVENT_CONSUMERS_DEPLOY_ENABLED" == true ]] && (( previous_consumer_replicas > 0 )); then
    "${KUBECTL[@]}" rollout status deployment/event-consumers --timeout=180s || failed=1
  fi
  set_web_image "$inactive_slot" "$previous_inactive_image" || failed=1
  "${KUBECTL[@]}" scale "deployment/web-$inactive_slot" --replicas="$previous_inactive_replicas" >/dev/null || failed=1
  "${KUBECTL[@]}" scale deployment/cloudflared --replicas="$previous_tunnel_replicas" >/dev/null || failed=1
  if (( failed )); then
    echo "Application recovery incomplete; inspect workloads before retrying" >&2
    return 1
  fi
  echo "Previous selector, application images and replica counts restored; database unchanged" >&2
}
finish_operation() {
  local status=$?
  trap - EXIT
  if (( status != 0 && release_changed == 1 )); then
    restore_release || status=1
  fi
  release_maintenance_lock "$LOCK_OWNER" || status=1
  exit "$status"
}
begin_operation() {
  LOCK_OWNER="$1:${CI_PIPELINE_ID:-manual}:$$:$(date -u +%Y%m%dT%H%M%SZ)"
  release_changed=0
  acquire_maintenance_lock "$LOCK_OWNER"
  trap finish_operation EXIT
  local api admin web tunnel consumer config
  api="$("${KUBECTL[@]}" get deployment game-api -o json)"
  admin="$("${KUBECTL[@]}" get deployment admin-console -o json)"
  tunnel="$("${KUBECTL[@]}" get deployment cloudflared -o json)"
  config="$("${KUBECTL[@]}" get configmap application-config -o json)"
  active_slot="$("${KUBECTL[@]}" get service web -o jsonpath='{.spec.selector.app\.kubernetes\.io/slot}')"
  case "$active_slot" in blue) inactive_slot=green ;; green) inactive_slot=blue ;; *) die "Invalid active web slot: $active_slot" ;; esac
  web="$("${KUBECTL[@]}" get "deployment/web-$inactive_slot" -o json)"
  previous_api_image="$(jq -er '.spec.template.spec.containers[] | select(.name=="game-api") | .image' <<<"$api")"
  previous_api_replicas="$(jq -er '.spec.replicas' <<<"$api")"
  previous_admin_image="$(jq -er '.spec.template.spec.containers[] | select(.name=="admin-console") | .image' <<<"$admin")"
  previous_admin_replicas="$(jq -er '.spec.replicas' <<<"$admin")"
  consumer="$(${KUBECTL[@]} get deployment event-consumers -o json)"
  previous_consumer_image="$(jq -er '.spec.template.spec.containers[] | select(.name=="event-consumers") | .image' <<<"$consumer")"
  previous_consumer_replicas="$(jq -er '.spec.replicas' <<<"$consumer")"
  previous_tunnel_replicas="$(jq -er '.spec.replicas' <<<"$tunnel")"
  previous_inactive_image="$(jq -er '.spec.template.spec.containers[] | select(.name=="web") | .image' <<<"$web")"
  previous_inactive_replicas="$(jq -er '.spec.replicas' <<<"$web")"
  previous_origin="$(jq -er '.data.PUBLIC_ORIGIN' <<<"$config")"
  previous_offline_all="$(jq -er '.data.OFFLINE_REWARD_TEST_ALL_ACCOUNTS // "false"' <<<"$config")"
  previous_offline_ids="$(jq -er '.data.OFFLINE_REWARD_TEST_ACCOUNT_IDS // ""' <<<"$config")"
  previous_offline_multiplier="$(jq -er '.data.OFFLINE_REWARD_TEST_REWARD_MULTIPLIER // ""' <<<"$config")"
  previous_offline_grace="$(jq -er '.data.OFFLINE_REWARD_TEST_GRACE_PERIOD_SECONDS // ""' <<<"$config")"
  previous_offline_bucket="$(jq -er '.data.OFFLINE_REWARD_TEST_BUCKET_SECONDS // ""' <<<"$config")"
  previous_offline_cap="$(jq -er '.data.OFFLINE_REWARD_TEST_ACCRUAL_CAP_SECONDS // ""' <<<"$config")"
  previous_basic_auth_enabled="$(jq -er '.data.BASIC_AUTH_ENABLED // "false"' <<<"$config")"
  previous_basic_auth_username="$(jq -er '.data.BASIC_AUTH_USERNAME // "hanjjak"' <<<"$config")"
  configured_db="$(jq -er '.data.POSTGRES_DB' <<<"$config")"
  configured_user="$(jq -er '.data.POSTGRES_USER' <<<"$config")"
}
restore_annotated_offline_policy() {
  local service all ids multiplier grace bucket cap basicAuthEnabled basicAuthUsername
  service="$("${KUBECTL[@]}" get service web -o json)"
  all="$(jq -er '.metadata.annotations["hanjjak.dev/previous-offline-all"]' <<<"$service")"
  ids="$(jq -er '.metadata.annotations["hanjjak.dev/previous-offline-ids"]' <<<"$service")"
  multiplier="$(jq -er '.metadata.annotations["hanjjak.dev/previous-offline-multiplier"]' <<<"$service")"
  grace="$(jq -er '.metadata.annotations["hanjjak.dev/previous-offline-grace"]' <<<"$service")"
  bucket="$(jq -er '.metadata.annotations["hanjjak.dev/previous-offline-bucket"]' <<<"$service")"
  cap="$(jq -er '.metadata.annotations["hanjjak.dev/previous-offline-cap"]' <<<"$service")"
  basicAuthEnabled="$(jq -er '.metadata.annotations["hanjjak.dev/previous-basic-auth-enabled"]' <<<"$service")"
  basicAuthUsername="$(jq -er '.metadata.annotations["hanjjak.dev/previous-basic-auth-username"]' <<<"$service")"
  "${KUBECTL[@]}" patch configmap application-config --type=merge \
    -p "$(jq -cn --arg all "$all" --arg ids "$ids" --arg multiplier "$multiplier" --arg grace "$grace" --arg bucket "$bucket" --arg cap "$cap" --arg basicAuthEnabled "$basicAuthEnabled" --arg basicAuthUsername "$basicAuthUsername" '{data:{OFFLINE_REWARD_TEST_ALL_ACCOUNTS:$all,OFFLINE_REWARD_TEST_ACCOUNT_IDS:$ids,OFFLINE_REWARD_TEST_REWARD_MULTIPLIER:$multiplier,OFFLINE_REWARD_TEST_GRACE_PERIOD_SECONDS:$grace,OFFLINE_REWARD_TEST_BUCKET_SECONDS:$bucket,OFFLINE_REWARD_TEST_ACCRUAL_CAP_SECONDS:$cap,BASIC_AUTH_ENABLED:$basicAuthEnabled,BASIC_AUTH_USERNAME:$basicAuthUsername}}')" >/dev/null
}

apply_release() {
  local tag="$1"
  release_changed=1
  "${KUBECTL[@]}" set image deployment/game-api "game-api=docker.io/hanjjak/game-api:$tag" >/dev/null
  "${KUBECTL[@]}" scale deployment/game-api --replicas=2 >/dev/null
  "${KUBECTL[@]}" rollout status deployment/game-api --timeout=300s
  run_smoke api
  "${KUBECTL[@]}" set image deployment/admin-console "admin-console=docker.io/hanjjak/admin-console:$tag" >/dev/null
  "${KUBECTL[@]}" scale deployment/admin-console --replicas=2 >/dev/null
  "${KUBECTL[@]}" rollout status deployment/admin-console --timeout=180s
  if [[ "$EVENT_CONSUMERS_DEPLOY_ENABLED" == true ]]; then
    "${KUBECTL[@]}" set image deployment/event-consumers \
      "event-consumers=docker.io/hanjjak/web:${tag}-event-consumers" >/dev/null
    "${KUBECTL[@]}" scale deployment/event-consumers --replicas=1 >/dev/null
    "${KUBECTL[@]}" rollout status deployment/event-consumers --timeout=180s
  fi
  set_web_image "$inactive_slot" "docker.io/hanjjak/web:$tag"
  "${KUBECTL[@]}" scale "deployment/web-$inactive_slot" --replicas=2 >/dev/null
  "${KUBECTL[@]}" rollout status "deployment/web-$inactive_slot" --timeout=180s
  run_smoke "$inactive_slot"
  select_web "$inactive_slot"
  run_smoke service
  "${KUBECTL[@]}" scale deployment/cloudflared --replicas=2 >/dev/null
  curl --fail --silent --show-error --connect-timeout 3 --max-time 10 \
    --retry 12 --retry-all-errors --retry-delay 2 "$EXTERNAL_SMOKE_URL" >/dev/null
  sleep "$WEB_DRAIN_SECONDS"
  # Commit metadata before retiring the old slot; subsequent failures leave both available.
  "${KUBECTL[@]}" annotate service web "hanjjak.dev/previous-slot=$active_slot" \
    "hanjjak.dev/previous-api-image=$previous_api_image" "hanjjak.dev/previous-admin-image=$previous_admin_image" \
    "hanjjak.dev/previous-offline-all=$previous_offline_all" "hanjjak.dev/previous-offline-ids=$previous_offline_ids" \
    "hanjjak.dev/previous-offline-multiplier=$previous_offline_multiplier" "hanjjak.dev/previous-offline-grace=$previous_offline_grace" \
    "hanjjak.dev/previous-offline-bucket=$previous_offline_bucket" "hanjjak.dev/previous-offline-cap=$previous_offline_cap" \
    "hanjjak.dev/previous-basic-auth-enabled=$previous_basic_auth_enabled" "hanjjak.dev/previous-basic-auth-username=$previous_basic_auth_username" --overwrite >/dev/null
  release_changed=0
  "${KUBECTL[@]}" scale "deployment/web-$active_slot" --replicas=0 >/dev/null
}
