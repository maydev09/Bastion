#!/usr/bin/env bash
set -euo pipefail

psql --username postgres --dbname "$POSTGRES_DB" --set ON_ERROR_STOP=1 \
  --set db="$POSTGRES_DB" \
  --set owner_password="$(< /run/secrets/db_owner_password)" \
  --set app_password="$(< /run/secrets/db_app_password)" <<'SQL'
CREATE ROLE bastion_owner LOGIN PASSWORD :'owner_password';
CREATE ROLE bastion_app LOGIN PASSWORD :'app_password';

ALTER DATABASE :"db" OWNER TO bastion_owner;
REVOKE ALL ON DATABASE :"db" FROM PUBLIC;
GRANT CONNECT ON DATABASE :"db" TO bastion_app;
REVOKE ALL ON SCHEMA public FROM PUBLIC;

ALTER DEFAULT PRIVILEGES FOR ROLE bastion_owner GRANT USAGE ON SCHEMAS TO bastion_app;
ALTER DEFAULT PRIVILEGES FOR ROLE bastion_owner GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO bastion_app;
ALTER DEFAULT PRIVILEGES FOR ROLE bastion_owner GRANT USAGE ON SEQUENCES TO bastion_app;
SQL
