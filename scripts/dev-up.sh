#!/usr/bin/env bash
set -euo pipefail

repo="$(cd "$(dirname "$0")/.." && pwd)"

secret() {
  local key="$1" label="$2" value
  if ! value="$(secret-tool lookup service Bastion account "$key")" || [[ -z "$value" ]]; then
    value="$(openssl rand -hex 24)"
    printf '%s' "$value" | secret-tool store --label "$label" service Bastion account "$key"
    echo "Created secret '$key' in the local secret store." >&2
  fi
  printf '%s' "$value"
}

DB_POSTGRES_PASSWORD="$(secret db-postgres-password "postgres password")"
DB_OWNER_PASSWORD="$(secret db-owner-password "bastion_owner password")"
DB_APP_PASSWORD="$(secret db-app-password "bastion_app password")"
export DB_POSTGRES_PASSWORD DB_OWNER_PASSWORD DB_APP_PASSWORD

docker compose --project-directory "$repo" up --detach --wait db
docker compose --project-directory "$repo" run --rm migrate

dotnet user-secrets set "ConnectionStrings:Bastion" \
  "Host=localhost;Port=5432;Database=bastion;Username=bastion_app;Password=$DB_APP_PASSWORD" \
  --project "$repo/src/Host/Bastion.Host" >/dev/null
echo "Stored the connection string in the .NET user secrets of Bastion.Host." >&2
