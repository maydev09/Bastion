#!/usr/bin/env bash
# Replaces all local development data with a snapshot of production taken now: the database
# (ADR 0020) and the raw captures of every module (ADR 0013).
#
# All or nothing: the snapshot is downloaded, restored into a separate database and migrated with
# this branch's migrations while the local data stays untouched. Only when all of that worked are
# the database (one transaction) and the raw captures (one rename per module) swapped. Any error
# stops the script and leaves the local data as it was.
#
# The raw capture folders come from the app's TOML configuration (scripts/raw-capture-paths.py):
# locally the Development layers, on the server the files of the deployed checkout, which match
# its image (deploy/compose.sh).
#
# Usage: scripts/dev-restore-prod.sh <user@server>
# Asks for the sudo password on the server. Stop the local backend first.
set -euo pipefail

server="${1:?Usage: scripts/dev-restore-prod.sh <user@server>}"
repo="$(cd "$(dirname "$0")/.." && pwd)"
host="$repo/src/Host/Bastion.Host"
backups="${BASTION_BACKUP_DIR:-$HOME/bastion-backups}"
db=bastion-db-1

# On the server (README, "Running on a server"). The copy of the raw captures sits next to the
# dumps, readable for the admin's group like them (server-setup.sh).
server_checkout=/opt/bastion
server_host=$server_checkout/src/Host/Bastion.Host
server_app=bastion-app-1
server_root=/app
server_copy=/var/backups/bastion/raw

# Runs on the server with sudo: <app container> <checkout> <copy folder> (<module> <container path>)...
# Stops unless the app runs the image of the checked-out commit, so the paths read from the
# checkout are the ones the app uses. A module without a folder yet gets an empty copy.
server_snapshot=$(cat <<'REMOTE'
set -euo pipefail
app="$1" checkout="$2" copy="$3"
shift 3

stop() {
  echo "$*" >&2
  exit 1
}

# The host folder behind a container path: the longest mount that contains it.
host_path() {
  local path="$1" mounts dest src best="" best_src=""
  mounts="$(sudo docker inspect --format '{{range .Mounts}}{{.Destination}}{{"\t"}}{{.Source}}{{"\n"}}{{end}}' "$app")"
  while IFS=$'\t' read -r dest src; do
    if [[ -n "$dest" && ("$path" == "$dest" || "$path" == "$dest"/*) && ${#dest} -gt ${#best} ]]; then
      best="$dest" best_src="$src"
    fi
  done <<<"$mounts"
  [[ -n "$best" ]] || stop "$path is not on a volume of $app."
  printf '%s\n' "$best_src${path#"$best"}"
}

[[ "$(sudo docker inspect --format '{{.State.Running}}' "$app")" == true ]] || stop "$app is not running."
image="$(sudo docker inspect --format '{{.Config.Image}}' "$app")"
commit="$(sudo git -C "$checkout" rev-parse HEAD)"
[[ "${image##*:}" == "$commit" ]] || stop "$app runs $image, but $checkout is at $commit; restart Bastion first."

declare -A sources
while (($#)); do
  sources["$1"]="$(host_path "$2")"
  shift 2
done

sudo systemctl start bastion-backup
sudo mkdir -p "$copy"
for module in "${!sources[@]}"; do
  source="${sources[$module]}"
  sudo mkdir -p "$copy/$module"
  if sudo test -d "$source"; then
    sudo rsync --recursive --times --delete "$source/" "$copy/$module/"
  else
    sudo find "$copy/$module" -mindepth 1 -delete
  fi
done
sudo chgrp -R --reference="$(dirname "$copy")" "$copy"
sudo chmod -R g+rX,o= "$copy"
REMOTE
)

fail() {
  echo "$*" >&2
  exit 1
}

sql() {
  docker exec -i "$db" psql --username postgres --dbname postgres --quiet --no-psqlrc \
    --set ON_ERROR_STOP=1 "$@"
}

newest_dump() {
  ls -1t "$backups"/bastion-*.dump 2>/dev/null | sed -n 1p || true
}

# Fills the named associative array with module -> raw capture folder.
read_paths() {
  local -n paths="$1"
  local output module path
  output="$(python3 "$repo/scripts/raw-capture-paths.py" "${@:2}")"
  while IFS=$'\t' read -r module path; do
    [[ -n "$module" ]] || continue
    paths["$module"]="$path"
  done <<<"$output"
}

declare -A local_raw server_raw
swapped=()

# Puts the staged raw captures of every module in place; on failure every module goes back.
swap_raw() {
  local module path
  for module in "${!local_raw[@]}"; do
    path="${local_raw[$module]}"
    if [[ -e "$path" ]] && ! mv "$path" "$path.previous"; then
      unswap_raw
      return 1
    fi
    if ! mv "$path.restore" "$path"; then
      if [[ -e "$path.previous" ]]; then mv "$path.previous" "$path"; fi
      unswap_raw
      return 1
    fi
    swapped+=("$path")
  done
}

unswap_raw() {
  local path
  for path in "${swapped[@]}"; do
    mv "$path" "$path.restore"
    if [[ -e "$path.previous" ]]; then mv "$path.previous" "$path"; fi
  done
}

# Runs on every exit: removes what this run prepared and did not swap in.
cleanup() {
  local path
  sql --command 'DROP DATABASE IF EXISTS bastion_restore' >/dev/null 2>&1 || true
  for path in "${local_raw[@]}"; do rm -rf "$path.restore"; done
  rm -rf "${config:-}"
}

config=""
trap cleanup EXIT

# Checks before anything is downloaded or changed.
read_paths local_raw "$host" "$host/bastion.toml" "$host/bastion.Development.toml" "$host/bastion.local.toml"
config="$(mktemp -d)"
ssh "$server" "cd $server_host && tar --create --file - bastion*.toml" | tar --extract --file - --directory "$config"
read_paths server_raw "$server_root" "$config/bastion.toml" "$config/bastion.Production.toml"
[[ "$(printf '%s\n' "${!local_raw[@]}" | sort)" == "$(printf '%s\n' "${!server_raw[@]}" | sort)" ]] ||
  fail "Modules with raw captures differ: here ${!local_raw[*]}, on the server ${!server_raw[*]}."

source "$repo/scripts/dev-secrets.sh"
docker compose --project-directory "$repo" up --detach --wait db
[[ "$(sql --tuples-only --no-align --command "SELECT count(*) FROM pg_stat_activity WHERE datname = 'bastion'")" == 0 ]] ||
  fail "Something is connected to the local database 'bastion'; stop the backend first."
[[ -z "$(sql --tuples-only --no-align --command "SELECT 1 FROM pg_database WHERE datname = 'bastion_previous'")" ]] ||
  fail "The database bastion_previous is left from an earlier run; look at it and drop it first."
for path in "${local_raw[@]}"; do
  [[ ! -e "$path.previous" ]] || fail "$path.previous is left from an earlier run; look at it and remove it first."
done
# Leftovers of an earlier run that failed or was stopped.
cleanup

# 1. Snapshot on the server: a dump, then the raw captures (files newer than the dump only add
#    files that no row points to). Each module's folder is mirrored with rsync from the app's volume
#    on the host, so a later run copies only new files.
args=("$server_app" "$server_checkout" "$server_copy")
for module in "${!server_raw[@]}"; do args+=("$module" "${server_raw[$module]}"); done
before="$(newest_dump)"
ssh -t "$server" "bash -c $(printf '%q' "$server_snapshot") snapshot $(printf '%q ' "${args[@]}")"

# 2. Download.
"$repo/scripts/backup-pull.sh" "$server"
dump="$(newest_dump)"
[[ -n "$dump" && "$dump" != "$before" ]] || fail "No new dump arrived from the server."
mkdir -p "$backups/raw"
rsync --recursive --times --delete "$server:$server_copy/" "$backups/raw/"

# 3. Prepare next to the local data: raw captures in staging folders, the database as
#    bastion_restore with the same database privileges as db/init/01-roles.sh gives bastion.
for module in "${!local_raw[@]}"; do
  path="${local_raw[$module]}"
  mkdir -p "$(dirname "$path")"
  cp --archive --reflink=auto "$backups/raw/$module" "$path.restore"
done
sql --command 'CREATE DATABASE bastion_restore OWNER bastion_owner TEMPLATE template0'
sql --command 'REVOKE ALL ON DATABASE bastion_restore FROM PUBLIC' \
  --command 'GRANT CONNECT ON DATABASE bastion_restore TO bastion_app'
docker exec -i "$db" pg_restore --username postgres --dbname bastion_restore \
  --exit-on-error --single-transaction <"$dump"
docker compose --project-directory "$repo" run --rm --env DB_NAME=bastion_restore migrate

# 4. Swap. Ctrl+C, a closed terminal and kill are ignored from here on, so the swap is not left
#    half done.
trap '' INT HUP TERM
sql <<'SQL'
BEGIN;
ALTER DATABASE bastion RENAME TO bastion_previous;
ALTER DATABASE bastion_restore RENAME TO bastion;
COMMIT;
SQL
if ! swap_raw; then
  sql <<'SQL' || fail "Could not move the raw captures in place, nor rename the databases back: \
'bastion' is the production copy, 'bastion_previous' your old database, the raw captures are \
your old ones. Stop what is connected, then rename bastion to bastion_restore and bastion_previous to bastion."
BEGIN;
ALTER DATABASE bastion RENAME TO bastion_restore;
ALTER DATABASE bastion_previous RENAME TO bastion;
COMMIT;
SQL
  fail "Could not move the raw captures in place; the local data is as before."
fi

# 5. The old local data is no longer needed.
sql --command 'DROP DATABASE bastion_previous'
for path in "${local_raw[@]}"; do rm -rf "$path.previous"; done
echo "Restored $(basename "$dump") and the raw captures of: ${!local_raw[*]}." >&2
