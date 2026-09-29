#!/usr/bin/env bash
set -euo pipefail

DEPLOY_ENV_FILE="${DEPLOY_ENV_FILE:-/etc/hanjjak/production.env}"
BACKUP_DIR="${BACKUP_DIR:-/var/backups/hanjjak}"
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
COMPOSE=(docker compose --env-file "$DEPLOY_ENV_FILE" -f "$SCRIPT_DIR/compose.yaml" --profile tunnel)

: "${IMAGE_TAG:?IMAGE_TAG is required}"
[[ "$IMAGE_TAG" != "latest" ]] || { echo "IMAGE_TAG must not be latest" >&2; exit 1; }
for image in game-api web admin-console; do
  docker image inspect "hanjjak/${image}:${IMAGE_TAG}" >/dev/null || {
    echo "Required local image hanjjak/${image}:${IMAGE_TAG} is not available on this Docker daemon" >&2
    exit 1
  }
done

"${COMPOSE[@]}" config --quiet
"$SCRIPT_DIR/backup-postgres.sh"
"${COMPOSE[@]}" up -d --wait --remove-orphans --pull never
"$SCRIPT_DIR/smoke.sh"
