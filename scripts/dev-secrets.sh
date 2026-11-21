# Sourced by the dev scripts: exports the local database passwords that Compose needs, taken from
# the keyring and generated there on the first use.

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
