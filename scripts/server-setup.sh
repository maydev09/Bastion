#!/usr/bin/env bash
# Turns a clean Debian into the Bastion server. Run as root from the repository cloned to
# /opt/bastion. Safe to run again: every step skips what is already done.
set -euo pipefail

repo="$(cd "$(dirname "$0")/.." && pwd)"
[[ $EUID -eq 0 ]] || { echo "Run as root." >&2; exit 1; }
# The admin who runs sudo gets read access to the backups (see below).
[[ -n "${SUDO_USER:-}" ]] || { echo "Run with sudo from your own account." >&2; exit 1; }
[[ "$repo" == /opt/bastion ]] || { echo "Clone the repository to /opt/bastion." >&2; exit 1; }

# Docker from the official repository (Debian's docker.io lags behind Compose features we use).
if ! command -v docker >/dev/null; then
  apt-get update
  apt-get install --yes ca-certificates curl openssl
  install -m 0755 -d /etc/apt/keyrings
  curl -fsSL https://download.docker.com/linux/debian/gpg -o /etc/apt/keyrings/docker.asc
  cat >/etc/apt/sources.list.d/docker.sources <<EOF
Types: deb
URIs: https://download.docker.com/linux/debian
Suites: $(. /etc/os-release && echo "$VERSION_CODENAME")
Components: stable
Signed-By: /etc/apt/keyrings/docker.asc
EOF
  apt-get update
  apt-get install --yes docker-ce docker-ce-cli containerd.io docker-compose-plugin
fi

# Debian's security fixes install themselves; they patch the same versions, so the risk is low.
# Docker (its own repository), reboots and Bastion itself stay manual.
apt-get install --yes unattended-upgrades
if [[ ! -f /etc/apt/apt.conf.d/20auto-upgrades ]]; then
  cat >/etc/apt/apt.conf.d/20auto-upgrades <<EOF
APT::Periodic::Update-Package-Lists "1";
APT::Periodic::Unattended-Upgrade "1";
EOF
fi

# The default json-file driver never rotates logs; the "local" driver does.
if [[ ! -f /etc/docker/daemon.json ]]; then
  echo '{ "log-driver": "local" }' >/etc/docker/daemon.json
  systemctl restart docker
fi

# systemd-creds binds secrets to the TPM only when these libraries are present.
apt-get install --yes libtss2-esys-3.0.2-0t64 libtss2-rc0t64 libtss2-mu-4.0.1-0t64

# Passwords are generated once and only ever stored encrypted. Each file is written
# through a temporary name, so a failed encryption never leaves a file that a rerun would skip.
mkdir -p /etc/credstore.encrypted
for name in db-postgres-password db-owner-password db-app-password; do
  file="/etc/credstore.encrypted/$name"
  if [[ ! -f "$file" ]]; then
    openssl rand -hex 24 | systemd-creds encrypt --name="$name" - "$file.tmp"
    mv "$file.tmp" "$file"
    echo "Created secret '$name'." >&2
  fi
done

# The WireGuard key comes from the VPN provider's account, so it is pasted, not generated.
file=/etc/credstore.encrypted/wireguard-private-key
if [[ ! -f "$file" ]]; then
  read -r -s -p "WireGuard private key (PrivateKey from the provider's config): " key
  echo
  printf '%s' "$key" | systemd-creds encrypt --name=wireguard-private-key - "$file.tmp"
  mv "$file.tmp" "$file"
  echo "Stored secret 'wireguard-private-key'." >&2
fi

# Database dumps. The directory belongs to the group of the admin who ran sudo, and
# new dumps inherit that group (setgid), so another machine copies them with rsync, without root.
apt-get install --yes rsync
install -d -m 2750 -g "$(id -gn "$SUDO_USER")" /var/backups/bastion

install -m 0644 "$repo/deploy/bastion.service" "$repo/deploy/bastion-backup.service" \
  "$repo/deploy/bastion-backup.timer" /etc/systemd/system/
systemctl daemon-reload
systemctl enable bastion.service
systemctl enable --now bastion-backup.timer
# Clears the retry limit, so an update can start a service that stopped after failed retries.
systemctl reset-failed bastion.service
systemctl restart bastion.service
echo "Bastion is running. Logs: journalctl -u bastion -f" >&2
