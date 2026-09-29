#!/usr/bin/env bash
set -euo pipefail

: "${K3S_VERSION:?K3S_VERSION must be pinned, for example v1.36.4+k3s1}"
BACKUP_HOST_PATH="${BACKUP_HOST_PATH:-/var/backups/hanjjak}"

[[ "$K3S_VERSION" =~ ^v[0-9]+\.[0-9]+\.[0-9]+\+k3s[0-9]+$ ]] || {
  echo "K3S_VERSION must be an exact pinned +k3s release" >&2
  exit 1
}
sudo install -d -m 0750 "$BACKUP_HOST_PATH"

if sudo systemctl is-active --quiet k3s; then
  installed="$(sudo k3s --version | awk 'NR == 1 { print $3 }')"
  [[ "$installed" == "$K3S_VERSION" ]] || {
    echo "Installed K3s $installed differs from requested $K3S_VERSION; upgrade explicitly" >&2
    exit 1
  }
else
  curl --fail --silent --show-error --location --connect-timeout 5 --max-time 60 \
    https://get.k3s.io | INSTALL_K3S_VERSION="$K3S_VERSION" sh -s - server \
      --disable traefik \
      --secrets-encryption
fi
sudo k3s kubectl wait --for=condition=Ready node --all --timeout=180s >/dev/null
for image in apache/kafka:4.0.0 postgres:17.11-alpine cloudflare/cloudflared:2026.8.3 curlimages/curl:8.22.0; do
  sudo k3s ctr images pull "docker.io/$image" >/dev/null
done
printf 'K3s %s is ready; fixed support images and Kafka are available in containerd\n' "$K3S_VERSION"
