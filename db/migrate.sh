#!/bin/sh
set -eu

password="$(cat /run/secrets/db_owner_password)"
export DATABASE_URL="postgres://bastion_owner:${password}@db:5432/bastion?sslmode=disable"

# /modules/Housing/Bastion.Modules.Housing.Infrastructure/Migrations -> schema housing
for dir in /modules/*/*.Infrastructure/Migrations/; do
  [ -d "$dir" ] || continue
  module="$(echo "$dir" | cut -d/ -f3)"
  schema="$(echo "$module" | sed -E 's/([a-z0-9])([A-Z])/\1_\2/g' | tr '[:upper:]' '[:lower:]')"
  echo "== $schema"
  dbmate --migrations-dir "$dir" --migrations-table "$schema.schema_migrations" --no-dump-schema "$@"
done
