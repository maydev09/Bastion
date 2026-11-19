# Disaster recovery

How to get the production data back from a dump (ADR 0020). Run the commands on the server,
from `/opt/bastion`, as the admin who set it up. Do not start an import while you restore.

## Which dump

- Dumps are in `/var/backups/bastion` on the server and in `~/bastion-backups` on the desktop,
  named `bastion-<date>-<time>.dump`.
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
3. From the desktop, copy the newest dump to the server:

   ```bash
   scp ~/bastion-backups/bastion-YYYYMMDD-HHMMSS.dump <user>@<server>:/tmp/
   ```

4. On the server, restore it as in A, steps 1 and 2 (with `dump=/tmp/bastion-…dump`), then
   `sudo docker unpause bastion-app-1 && sudo systemctl restart bastion`. You lose what changed after that dump.

## Check

Open the UI: the offers and the audit are back. `sudo journalctl -u bastion -n 50` shows
the migrations ran without errors.
