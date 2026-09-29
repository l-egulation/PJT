#!/bin/sh
# Turns HTTP Basic Auth on the SPA shell on or off based on env vars supplied
# at container start. nginx's own entrypoint runs every script under
# /docker-entrypoint.d/ before starting nginx, so this needs no extra wiring.
#
# BASIC_AUTH_ENABLED  - "true" to require basic auth, anything else disables it
# BASIC_AUTH_USERNAME - login name (default: hanjjak)
# BASIC_AUTH_PASSWORD - login password, required when enabled
set -eu

AUTH_DIR=/etc/nginx/auth
CONF_FILE="$AUTH_DIR/basic-auth.conf"
HTPASSWD_FILE="$AUTH_DIR/.htpasswd"

mkdir -p "$AUTH_DIR"

if [ "${BASIC_AUTH_ENABLED:-false}" = "true" ]; then
  if [ -z "${BASIC_AUTH_PASSWORD:-}" ]; then
    echo "40-basic-auth.sh: BASIC_AUTH_ENABLED=true but BASIC_AUTH_PASSWORD is empty" >&2
    exit 1
  fi
  username="${BASIC_AUTH_USERNAME:-hanjjak}"
  printf '%s:%s\n' "$username" "$(openssl passwd -apr1 "$BASIC_AUTH_PASSWORD")" > "$HTPASSWD_FILE"
  # nginx worker processes drop to an unprivileged user, so the hash file
  # (root-owned, since this script runs before that drop) must stay world-readable.
  chmod 644 "$HTPASSWD_FILE"
  cat > "$CONF_FILE" <<EOF
auth_basic "hanjjak";
auth_basic_user_file $HTPASSWD_FILE;
EOF
  echo "40-basic-auth.sh: basic auth enabled for user '$username'"
else
  rm -f "$HTPASSWD_FILE"
  : > "$CONF_FILE"
fi
