#!/usr/bin/env bash
set -euo pipefail

repo="$(cd "$(dirname "$0")/.." && pwd)"

secret() {
  local key="$1" value
  if ! value="$(secret-tool lookup app bastion key "$key")" || [[ -z "$value" ]]; then
    value="$(openssl rand -hex 24)"
    printf '%s' "$value" | secret-tool store --label "Bastion $key" app bastion key "$key"
    echo "Created secret '$key' in the local secret store." >&2
  fi
  printf '%s' "$value"
}

DB_POSTGRES_PASSWORD="$(secret db-postgres)"
DB_OWNER_PASSWORD="$(secret db-owner)"
DB_APP_PASSWORD="$(secret db-app)"
export DB_POSTGRES_PASSWORD DB_OWNER_PASSWORD DB_APP_PASSWORD

docker compose --project-directory "$repo" up --detach --wait db
docker compose --project-directory "$repo" run --rm migrate
