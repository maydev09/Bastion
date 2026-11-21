#!/usr/bin/env bash
set -euo pipefail

repo="$(cd "$(dirname "$0")/.." && pwd)"
source "$repo/scripts/dev-secrets.sh"

docker compose --project-directory "$repo" up --detach --wait db
docker compose --project-directory "$repo" run --rm migrate

dotnet user-secrets set "ConnectionStrings:Bastion" \
  "Host=localhost;Port=5432;Database=bastion;Username=bastion_app;Password=$DB_APP_PASSWORD" \
  --project "$repo/src/Host/Bastion.Host" >/dev/null
echo "Stored the connection string in the .NET user secrets of Bastion.Host." >&2
