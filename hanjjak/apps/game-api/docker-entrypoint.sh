#!/usr/bin/env bash
set -euo pipefail

if [[ -n "${DB_PASSWORD_FILE:-}" ]]; then
  DB_PASSWORD="$(<"$DB_PASSWORD_FILE")"
  export DB_PASSWORD
fi

exec java -jar /app.jar "$@"
