#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
K8S_NAMESPACE="${K8S_NAMESPACE:-hanjjak}"
: "${IMAGE_TAG:?IMAGE_TAG is required to validate deployable images}"
KUBECTL=(kubectl)
[[ -z "${KUBE_CONTEXT:-}" ]] || KUBECTL+=(--context "$KUBE_CONTEXT")
for manifest in base.yaml kafka.yaml migration-job.yaml backfill-job.yaml smoke-pod.yaml; do
  python3 "$SCRIPT_DIR/render.py" --input "$SCRIPT_DIR/$manifest" --namespace "$K8S_NAMESPACE" \
    --image-tag "$IMAGE_TAG" --event-consumers-image-name web \
    | "${KUBECTL[@]}" apply --dry-run=server -f - >/dev/null
done

can_i() {
  local expected="$1" verb="$2" resource="$3" answer status=0
  local target="${resource%%/*}"
  local extra=()
  [[ "$resource" != */* ]] || extra+=(--subresource="${resource#*/}")
  answer="$("${KUBECTL[@]}" auth can-i --as="system:serviceaccount:${K8S_NAMESPACE}:deployer" \
    "$verb" "$target" "${extra[@]}" -n "$K8S_NAMESPACE")" || status=$?
  [[ "$answer" == "$expected" && ( "$status" == 0 || ( "$expected" == no && "$status" == 1 ) ) ]] || {
    echo "Unexpected deployer permission: $verb $resource => $answer (exit=$status), expected $expected" >&2
    return 1
  }
}
for verb in get list watch create patch delete; do
  can_i yes "$verb" configmaps
done
for verb in get list watch create delete; do
  can_i yes "$verb" pods
  can_i yes "$verb" jobs
done
can_i yes get pods/log
can_i yes get services
can_i yes patch services
can_i yes get deployments
can_i yes watch deployments
can_i yes patch deployments
can_i yes patch deployments/scale
can_i yes get statefulsets
can_i yes watch statefulsets
can_i yes get cronjobs
can_i no get secrets
can_i no create roles
can_i no create rolebindings
can_i no create pods/exec
can_i no '*' '*'
echo "Rendered K3s manifests and deployer permissions passed server-side validation"
