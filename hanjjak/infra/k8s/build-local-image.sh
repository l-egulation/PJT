#!/usr/bin/env bash
set -euo pipefail

: "${IMAGE_TAG:?IMAGE_TAG is required}"
: "${IMAGE_NAME:?IMAGE_NAME is required}"
: "${DOCKERFILE:?DOCKERFILE is required}"
[[ "$IMAGE_NAME" == game-api || "$IMAGE_NAME" == web || "$IMAGE_NAME" == admin-console || "$IMAGE_NAME" == event-consumers ]] || {
  echo "IMAGE_NAME must be game-api, web, admin-console, or event-consumers" >&2
  exit 1
}
[[ "$IMAGE_TAG" =~ ^[A-Za-z0-9][A-Za-z0-9_.-]*$ && "$IMAGE_TAG" != latest && "$IMAGE_TAG" != bootstrap ]] || {
  echo "IMAGE_TAG must be a concrete tag other than latest or bootstrap" >&2
  exit 1
}
[[ -f "$DOCKERFILE" ]] || { echo "Dockerfile is missing: $DOCKERFILE" >&2; exit 1; }
command -v k3s-image-build >/dev/null || { echo "k3s-image-build is required" >&2; exit 1; }

context="$(pwd -P)"
dockerfile="$context/$DOCKERFILE"
image="docker.io/hanjjak/$IMAGE_NAME:$IMAGE_TAG"

sudo k3s-image-build "$IMAGE_NAME" "$IMAGE_TAG" "$dockerfile" "$context"
sudo k3s ctr images list -q | grep -Fx "$image" >/dev/null || {
  echo "Direct K3s build did not create $image" >&2
  exit 1
}
printf 'Built directly in K3s containerd: %s\n' "$image"
