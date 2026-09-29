#!/usr/bin/env bash
set -euo pipefail

DEPLOY_ENV_FILE="${DEPLOY_ENV_FILE:-/etc/hanjjak/production.env}"
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
COMPOSE=(docker compose --env-file "$DEPLOY_ENV_FILE" -f "$SCRIPT_DIR/compose.yaml")

"${COMPOSE[@]}" exec -T web wget -q -O /dev/null http://127.0.0.1:8080/healthz
"${COMPOSE[@]}" exec -T web wget -q -O /dev/null http://game-api:8080/actuator/health/readiness
"${COMPOSE[@]}" exec -T web sh -c '
  if [ "${BASIC_AUTH_ENABLED:-false}" = "true" ]; then
    wget -q --http-user="$BASIC_AUTH_USERNAME" --http-password="$BASIC_AUTH_PASSWORD" -O /dev/null http://127.0.0.1:8080/
  else
    wget -q -O /dev/null http://127.0.0.1:8080/
  fi
'
"${COMPOSE[@]}" exec -T web wget -q -O /dev/null http://127.0.0.1:8080/admin/
"${COMPOSE[@]}" exec -T admin-console wget -q -O /dev/null http://127.0.0.1:8080/healthz
"${COMPOSE[@]}" exec -T admin-console wget -q -O /dev/null http://127.0.0.1:8080/
printf 'deployment smoke passed\n'
