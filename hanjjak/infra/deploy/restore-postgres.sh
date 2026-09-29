#!/usr/bin/env bash
set -euo pipefail

[[ "${RESTORE_CONFIRM:-}" == "YES" ]] || { echo "Set RESTORE_CONFIRM=YES to restore" >&2; exit 1; }
BACKUP_FILE="${1:?usage: RESTORE_CONFIRM=YES restore-postgres.sh BACKUP_FILE}"
[[ -s "$BACKUP_FILE" ]] || { echo "Backup file is missing or empty: $BACKUP_FILE" >&2; exit 1; }

DEPLOY_ENV_FILE="${DEPLOY_ENV_FILE:-/etc/hanjjak/production.env}"
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
COMPOSE=(docker compose --env-file "$DEPLOY_ENV_FILE" -f "$SCRIPT_DIR/compose.yaml")

"${COMPOSE[@]}" up -d --wait postgres
"${COMPOSE[@]}" stop game-api web cloudflared 2>/dev/null || true
"${COMPOSE[@]}" exec -T postgres sh -c 'dropdb --if-exists --force --username="$POSTGRES_USER" "$POSTGRES_DB" && createdb --username="$POSTGRES_USER" "$POSTGRES_DB"'
"${COMPOSE[@]}" exec -T postgres sh -c 'exec pg_restore --exit-on-error --no-owner --no-privileges --username="$POSTGRES_USER" --dbname="$POSTGRES_DB"' < "$BACKUP_FILE"
"${COMPOSE[@]}" --profile tunnel up -d --wait
"$SCRIPT_DIR/smoke.sh"
