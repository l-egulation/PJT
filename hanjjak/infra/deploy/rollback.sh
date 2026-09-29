#!/usr/bin/env bash
set -euo pipefail

: "${ROLLBACK_IMAGE_TAG:?ROLLBACK_IMAGE_TAG must identify a previously verified image set}"
[[ "$ROLLBACK_IMAGE_TAG" != "latest" ]] || { echo "ROLLBACK_IMAGE_TAG must not be latest" >&2; exit 1; }
export IMAGE_TAG="$ROLLBACK_IMAGE_TAG"

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
"$SCRIPT_DIR/deploy.sh"
