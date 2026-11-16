#!/usr/bin/env bash
set -euo pipefail

repo="$(cd "$(dirname "$0")/.." && pwd)"

if ! password="$(secret-tool lookup app bastion key db-app)" || [[ -z "$password" ]]; then
  echo "No database password in the local secret store. Run scripts/dev-up.sh first." >&2
  exit 1
fi
export ConnectionStrings__Bastion="Host=localhost;Port=5432;Database=bastion;Username=bastion_app;Password=$password"

cd "$repo"
if (( $# == 0 )); then
  set -- dotnet run --project src/Host/Bastion.Host --launch-profile http
fi
exec "$@"
