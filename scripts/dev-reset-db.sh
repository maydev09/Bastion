#!/usr/bin/env bash
# Deletes the local database with all its data and the local raw captures, then creates the
# database again from the migrations.
set -euo pipefail

repo="$(cd "$(dirname "$0")/.." && pwd)"

if [[ "${1:-}" != "--yes" ]]; then
  read -r -p "Delete the local database and raw captures with all data and create the database again? [y/N] " answer
  [[ "$answer" == [yY] ]] || exit 1
fi

docker compose --project-directory "$repo" down --volumes
rm -rf "$repo/src/Host/Bastion.Host/data/raw"
"$repo/scripts/dev-up.sh"
