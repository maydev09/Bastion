#!/usr/bin/env bash
# Docker Compose for bastion.service (ADR 0010). The decrypted credentials are readable only by
# root, and the app and PostgreSQL do not run as root, so they are copied as readable files to
# /run/bastion (RuntimeDirectory: in memory, mode 0700, removed when the service stops) and
# mounted from there. Secrets passed through the environment would end up in the containers'
# layers on disk.
set -euo pipefail

secrets=/run/bastion
# systemd sets CREDENTIALS_DIRECTORY for ExecStartPre/ExecStart but not for ExecStop; `down`
# needs no secrets, and the copies in /run/bastion still exist at that point.
if [[ -n "${CREDENTIALS_DIRECTORY:-}" ]]; then
  for name in db-postgres-password db-owner-password db-app-password wireguard-private-key; do
    install -m 0444 "$CREDENTIALS_DIRECTORY/$name" "$secrets/$name"
  done
  (
    umask 0222
    printf 'Host=db;Port=5432;Database=bastion;Username=bastion_app;Password=%s' \
      "$(cat "$CREDENTIALS_DIRECTORY/db-app-password")" >"$secrets/db-connection-string.tmp"
    mv "$secrets/db-connection-string.tmp" "$secrets/db-connection-string"
  )
fi

# The server runs the image CI built from the commit that is checked out here, so code, Compose
# files and image always match. Deploy = git pull, rollback = git checkout <commit>; then restart.
BASTION_COMMIT="$(git -C "$(dirname "$0")/.." rev-parse HEAD)"
export BASTION_COMMIT

exec docker compose "$@"
