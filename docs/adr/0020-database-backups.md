# 0020. Database backups with pg_dump, pulled to another machine

- **Status:** Accepted
- **Date:** 2026-11-19, updated 2026-11-19 (any machine can hold the copies)

## Context

The server holds the only copy of the production data: offers, their history and the audit.
A failed migration, a bad deployment or a dead disk would lose it. Phase 4 (ROADMAP) needs a
backup before a deployment and a check of the new image on another machine against a copy
of the production data. One user, one small server, no second site and no cloud account for Bastion.

## Decision

The server dumps the database with **`pg_dump`** (custom format) to `/var/backups/bastion`, and
**another machine pulls** the dumps over SSH with rsync (for example the one Bastion is
developed on).

- `bastion-backup.timer` runs `deploy/backup.sh` **every night** at 03:00 while Bastion runs.
- **Before a deployment** the admin runs the same dump on demand (`systemctl start bastion-backup`,
  part of the update command in README), so a broken migration can go back to that day's data.
- The server keeps the newest 30 dumps. The directory belongs to the admin's group (setgid,
  files `0640`), so `scripts/backup-pull.sh <user@server>` copies them without root. The
  pulling machine keeps every dump it has copied.
- Only the database. Raw captures in `/data/raw` (ADR 0013) are for debugging and re-parsing;
  losing them does not change what the UI shows.
- A dump holds no passwords: roles are created by `db/init` on each machine with its own secrets.
- Restore drops the database and recreates it from the dump (`pg_restore --create`), so tables
  added by newer migrations go too: docs/disaster-recovery.md.

## Alternatives considered

- **A dump before every start of the service:** no deployment without a backup, but it adds
  logic to every start, a full disk stops Bastion, and restart retries leave several dumps
  where only the first one is from before the migration.
- **USB disk in the server:** automatic, but in the same place as the server (fire, theft).
- **Cloud (rclone to a drive):** off-site and automatic, but needs a new tool, an account and
  one more secret on the server; worth it only if a pulled copy turns out to be not enough.
- **Copy of the volume files or PostgreSQL WAL archiving:** point-in-time recovery, but more
  setup and tied to the PostgreSQL version; a logical dump is enough for this data size.

## Consequences

- The off-server copy is only as fresh as the last `backup-pull.sh` run.
- The backup before a deployment is a manual step; skipping it means going back to the
  last nightly dump.
- The pulling machine has the production data, which is what the backup test needs
  (docs/disaster-recovery.md, section C).
