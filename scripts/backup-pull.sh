#!/usr/bin/env bash
# Copies the server's database dumps to this machine (ADR 0020). Dumps already copied are
# kept, also after the server deletes them.
# Usage: scripts/backup-pull.sh <user@server>
set -euo pipefail

server="${1:?Usage: scripts/backup-pull.sh <user@server>}"
target="${BASTION_BACKUP_DIR:-$HOME/bastion-backups}"

mkdir -p "$target"
# A dump that is still being written is a .tmp file; it is not a backup. raw/ is the copy of
# the raw captures for scripts/dev-restore-prod.sh, which pulls it itself.
rsync --archive --ignore-existing --exclude '*.tmp' --exclude '/raw/' "$server:/var/backups/bastion/" "$target/"
echo "Newest dump: $(ls -1t "$target"/bastion-*.dump 2>/dev/null | sed -n 1p | xargs --no-run-if-empty basename)" >&2
