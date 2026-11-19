# Disaster recovery

How to get the production data back from a dump, and how to test that a dump can be restored.
Sections A and B run on the server, from `/opt/bastion`, as the admin who set it up;
do not start an import while you restore. Section C runs on any other machine.

## Which dump

- Dumps are in `/var/backups/bastion` on the server and on every machine that copied them
  with `scripts/backup-pull.sh` (by default `~/bastion-backups`), named `bastion-<date>-<time>.dump`.
- After a bad deployment: the dump made by the update command just before it (README).
- Otherwise: the newest one.

## A. A deployment or a migration broke the data

The server is fine, only the data or the schema is wrong.

```bash
cd /opt/bastion
dump=/var/backups/bastion/bastion-YYYYMMDD-HHMMSS.dump   # the dump you chose

# 1. Freeze the app, so it neither writes nor exits while its database is replaced. Stopping it
#    would make systemd restart the whole stack, database included (bastion.service).
sudo docker pause bastion-app-1

# 2. Replace the database with the dump (drops the current one, including newer tables).
sudo docker exec bastion-db-1 dropdb --username postgres --force bastion
sudo docker exec -i bastion-db-1 pg_restore --username postgres --create --dbname postgres < "$dump"

# 3. Run the code that matches the dump: the commit that was deployed before.
sudo docker unpause bastion-app-1
sudo git checkout <commit before the deployment>
sudo systemctl restart bastion
```

Later, to update again: `sudo git checkout master`, then the normal update from README.

## B. The server is gone (dead disk, new machine)

1. Install Debian and run the setup from README ("Running on a server"). It creates new
   passwords; a dump holds no passwords, so old ones are not needed. Get a new WireGuard
   key from the VPN provider.
2. Wait until Bastion runs (`sudo docker ps`: app, vpn and db are up). It starts with an
   empty database.
3. From the machine that holds the copies, send the newest dump to the server:

   ```bash
   scp ~/bastion-backups/bastion-YYYYMMDD-HHMMSS.dump <user>@<server>:/tmp/
   ```

4. On the server, restore it as in A, steps 1 and 2 (with `dump=/tmp/bastion-…dump`), then
   `sudo docker unpause bastion-app-1 && sudo systemctl restart bastion`. You lose what changed after that dump.

## Check after A or B

Open the UI: the offers and the audit are back. `sudo journalctl -u bastion -n 50` shows
the migrations ran without errors.

## C. Test a backup

A backup counts only once it has been restored. Restore a dump on a machine with Docker and a
checkout of this repository, in a separate Compose project, and run the image the server runs.
Do it after changes to backups, migrations or the database version, and from time to time.

Needs bash (not fish or zsh syntax), port 5432 free on localhost, and the dump on that machine
(`scripts/backup-pull.sh <user>@<server>`). Run all steps in one shell: the passwords exist only
there, and the roles keep the passwords they were created with.

```bash
dump=~/bastion-backups/bastion-YYYYMMDD-HHMMSS.dump
git checkout <commit checked out in /opt/bastion on the server>
export DB_POSTGRES_PASSWORD=$(openssl rand -hex 24) DB_OWNER_PASSWORD=$(openssl rand -hex 24) \
  DB_APP_PASSWORD=$(openssl rand -hex 24)

# The same restore as in A, step 2, then the migrations of that commit.
docker compose -p bastion-restore up --detach --wait db
docker exec bastion-restore-db-1 dropdb --username postgres --force bastion
docker exec -i bastion-restore-db-1 pg_restore --username postgres --create --dbname postgres < "$dump"
docker compose -p bastion-restore run --rm migrate

docker run --rm --network bastion-restore_default -p 127.0.0.1:8081:8080 \
  -e ConnectionStrings__Bastion="Host=db;Port=5432;Database=bastion;Username=bastion_app;Password=$DB_APP_PASSWORD" \
  ghcr.io/maydev09/bastion:$(git rev-parse HEAD)
```

Passed when `http://localhost:8081` shows the offers and
`curl -s localhost:8081/api/housing/offers` reports the same `totalCount` as production.
Imports fail there: the machine has no VPN container.

Clean up: stop the app (`Ctrl+C`), then `docker compose -p bastion-restore down --volumes`.
Always with `-p bastion-restore`: without it the command deletes the local development database.
