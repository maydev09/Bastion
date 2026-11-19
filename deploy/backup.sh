#!/usr/bin/env bash
# Dumps the database to /var/backups/bastion and keeps the newest 30 dumps (ADR 0020).
# Run by bastion-backup.service: nightly from its timer, and on demand before a deployment
# (`systemctl start bastion-backup`). Needs the db container running.
set -euo pipefail

dir=/var/backups/bastion
keep=30
file="$dir/bastion-$(date +%Y%m%d-%H%M%S).dump"

# Readable for the admin's group (set on the directory by server-setup.sh), so the desktop
# can copy the dumps over SSH without root.
umask 0027
trap 'rm -f "$file.tmp"' EXIT
"$(dirname "$0")/compose.sh" exec -T db pg_dump --username postgres --format custom bastion >"$file.tmp"
mv "$file.tmp" "$file"
echo "Backup: $file" >&2

ls -1t "$dir"/bastion-*.dump | tail -n +$((keep + 1)) | xargs --no-run-if-empty rm --
