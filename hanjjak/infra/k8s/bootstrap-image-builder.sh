#!/usr/bin/env bash
set -euo pipefail

NERDCTL_VERSION="${NERDCTL_VERSION:-2.3.5}"
NERDCTL_FULL_SHA256="${NERDCTL_FULL_SHA256:-b697295c623639734aaab737523c808fd3cc8d3046039fd94fff1744e4c317aa}"
archive="$(mktemp --suffix=.tar.gz /tmp/nerdctl-full.XXXXXX)"
cleanup() { rm -f "$archive"; }
trap cleanup EXIT

curl --fail --silent --show-error --location \
  "https://github.com/containerd/nerdctl/releases/download/v${NERDCTL_VERSION}/nerdctl-full-${NERDCTL_VERSION}-linux-amd64.tar.gz" \
  --output "$archive"
printf '%s  %s\n' "$NERDCTL_FULL_SHA256" "$archive" | sha256sum --check --status
sudo tar -xzf "$archive" -C /usr/local
sudo install -d -m 0755 /etc/buildkit /run/buildkit-k3s.io
sudo install -d -m 0700 /var/lib/buildkit-k3s
cat <<'CONFIG' | sudo tee /etc/buildkit/buildkitd-k3s.toml >/dev/null
[worker.oci]
  enabled = false
[worker.containerd]
  enabled = true
  address = "/run/k3s/containerd/containerd.sock"
  namespace = "k8s.io"
  snapshotter = "overlayfs"
CONFIG
cat <<'UNIT' | sudo tee /etc/systemd/system/buildkit-k3s.service >/dev/null
[Unit]
Description=BuildKit for K3s containerd
After=k3s.service
Requires=k3s.service

[Service]
ExecStart=/usr/local/bin/buildkitd --config /etc/buildkit/buildkitd-k3s.toml --addr unix:///run/buildkit-k3s.io/buildkitd.sock --root /var/lib/buildkit-k3s
Restart=on-failure

[Install]
WantedBy=multi-user.target
UNIT
sudo install -m 0755 "$PWD/infra/k8s/k3s-image-build" /usr/local/bin/k3s-image-build
cat <<'SUDOERS' | sudo tee /etc/sudoers.d/gitlab-runner-k3s-build >/dev/null
gitlab-runner ALL=(root) NOPASSWD: /usr/local/bin/k3s-image-build *, /usr/local/bin/k3s ctr images list *
SUDOERS
sudo chmod 0440 /etc/sudoers.d/gitlab-runner-k3s-build
sudo visudo -cf /etc/sudoers.d/gitlab-runner-k3s-build >/dev/null
sudo systemctl enable --now buildkit-k3s
sudo buildctl --addr unix:///run/buildkit-k3s.io/buildkitd.sock debug workers >/dev/null
printf 'Direct K3s containerd image builder is ready\n'
