#!/usr/bin/env bash
set -euo pipefail

DEPLOY_ENV_FILE="${DEPLOY_ENV_FILE:-/etc/hanjjak/production.env}"
BACKUP_DIR="${BACKUP_DIR:-/var/backups/hanjjak}"
BACKUP_RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-14}"
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
COMPOSE=(docker compose --env-file "$DEPLOY_ENV_FILE" -f "$SCRIPT_DIR/compose.yaml")

mkdir -p "$BACKUP_DIR"
if ! "${COMPOSE[@]}" ps --status running --services | grep -qx postgres; then
  echo "postgres is not running; skipping pre-deploy backup"
  exit 0
fi

backup="$BACKUP_DIR/hanjjak-$(date -u +%Y%m%dT%H%M%SZ).dump"
tmp="$backup.tmp"
trap 'rm -f "$tmp"' EXIT
"${COMPOSE[@]}" exec -T postgres sh -c 'exec pg_dump --format=custom --no-owner --no-privileges --username="$POSTGRES_USER" --dbname="$POSTGRES_DB"' > "$tmp"
test -s "$tmp"
mv "$tmp" "$backup"
find "$BACKUP_DIR" -maxdepth 1 -type f -name 'hanjjak-*.dump' -mtime "+$BACKUP_RETENTION_DAYS" -delete
trap - EXIT
printf '%s\n' "$backup"
