#!/usr/bin/env bash
# Shared serialization for operations that can scale or replace workloads.
# Callers must define KUBECTL as the fully scoped kubectl array (including namespace).

acquire_maintenance_lock() {
  local owner="${1:-${MAINTENANCE_LOCK_OWNER:-}}"
  if [[ -z "$owner" ]]; then
    owner="${HOSTNAME:-unknown}:$$:$(date -u +%Y%m%dT%H%M%SZ)"
  fi
  [[ -n "${KUBECTL+x}" ]] || {
    echo "KUBECTL array must be defined before acquiring the maintenance lock" >&2
    return 1
  }
  MAINTENANCE_LOCK_OWNER="$owner"
  export MAINTENANCE_LOCK_OWNER
  # create (rather than apply) is an atomic API operation: an existing lock is
  # never replaced or silently adopted by a second destructive operation.
  "${KUBECTL[@]}" create configmap deployment-maintenance-lock \
    --from-literal=owner="$owner" \
    --from-literal=acquired-at="$(date -u +%Y-%m-%dT%H:%M:%SZ)" >/dev/null || {
      echo "maintenance lock is already held; refusing concurrent operation" >&2
      return 1
    }
}

release_maintenance_lock() {
  local owner="${1:-${MAINTENANCE_LOCK_OWNER:-}}"
  [[ -n "$owner" ]] || return 0
  [[ -n "${KUBECTL+x}" ]] || {
    echo "KUBECTL array must be defined before releasing the maintenance lock" >&2
    return 1
  }

  local holder
  holder="$("${KUBECTL[@]}" get configmap deployment-maintenance-lock \
    -o jsonpath='{.data.owner}' 2>/dev/null)" || return 0
  if [[ "$holder" != "$owner" ]]; then
    echo "maintenance lock is held by '$holder'; refusing release by '$owner'" >&2
    return 1
  fi
  "${KUBECTL[@]}" delete configmap deployment-maintenance-lock --ignore-not-found=true >/dev/null
}
