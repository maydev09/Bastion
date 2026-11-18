#!/usr/bin/env bash
# Docker Compose for bastion.service (ADR 0010). The decrypted credentials are readable only by
# root, and the app and PostgreSQL do not run as root, so they are copied as readable files to
# /run/bastion (RuntimeDirectory: in memory, mode 0700, removed when the service stops) and
# mounted from there. Secrets passed through the environment would end up in the containers'
# layers on disk.
set -euo pipefail

secrets=/run/bastion
for name in db-postgres-password db-owner-password db-app-password wireguard-private-key; do
  install -m 0444 "$CREDENTIALS_DIRECTORY/$name" "$secrets/$name"
done
(
  umask 0222
  printf 'Host=db;Port=5432;Database=bastion;Username=bastion_app;Password=%s' \
    "$(cat "$CREDENTIALS_DIRECTORY/db-app-password")" >"$secrets/db-connection-string.tmp"
  mv "$secrets/db-connection-string.tmp" "$secrets/db-connection-string"
)

exec docker compose "$@"
