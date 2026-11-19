#!/usr/bin/env bash
# Copies the server's database dumps to this machine (ADR 0020). Dumps already copied are
# kept, also after the server deletes them.
# Usage: scripts/backup-pull.sh <user@server>
set -euo pipefail

server="${1:?Usage: scripts/backup-pull.sh <user@server>}"
target="${BASTION_BACKUP_DIR:-$HOME/bastion-backups}"

mkdir -p "$target"
# A dump that is still being written is a .tmp file; it is not a backup.
rsync --archive --ignore-existing --exclude '*.tmp' "$server:/var/backups/bastion/" "$target/"
echo "Newest dump: $(ls -1t "$target" | sed -n 1p)" >&2
