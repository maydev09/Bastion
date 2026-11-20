#!/usr/bin/env bash
# Updates the server to the newest master in one step: waits for the image CI publishes for it,
# backs up the database, moves the checkout and restarts Bastion, then waits until it answers.
# Run with sudo from /opt/bastion. Until the new image exists nothing changes on the server.
set -euo pipefail

repo=/opt/bastion
url=http://localhost:8080/api/platform/database
[[ $EUID -eq 0 ]] || { echo "Run with sudo." >&2; exit 1; }
cd "$repo"

git fetch --quiet origin master
current="$(git rev-parse HEAD)"
target="$(git rev-parse origin/master)"
if [[ "$current" == "$target" ]]; then
  echo "Already at $target." >&2
  exit 0
fi

# CI publishes the image a few minutes after a merge; pulling it now also shortens the restart.
image="ghcr.io/maydev09/bastion:$target"
echo "Waiting for $image (up to 20 minutes)..." >&2
for attempt in $(seq 1 40); do
  if docker pull --quiet "$image" >/dev/null 2>&1; then
    break
  fi
  if (( attempt == 40 )); then
    echo "No image for $target after 20 minutes; the server was not changed. Check CI on GitHub." >&2
    exit 1
  fi
  sleep 30
done

# The backup needs the running database; when Bastion is down, the newest dump is the nightly one.
if systemctl is-active --quiet bastion; then
  systemctl start bastion-backup
else
  echo "Bastion is not running; skipping the backup (the nightly dump is the newest)." >&2
fi

# Also leaves a rollback (a checked-out older commit) and returns to master.
git checkout --quiet master
git merge --quiet --ff-only "$target"
"$repo/scripts/server-setup.sh"

# Migrations run before the app starts, so the first answer means the update is complete.
echo "Waiting for Bastion to answer (up to 5 minutes)..." >&2
for attempt in $(seq 1 60); do
  if curl --silent --fail --output /dev/null "$url"; then
    echo "Bastion ${target:0:7} is running: http://$(hostname):8080" >&2
    exit 0
  fi
  sleep 5
done

echo "Bastion did not answer within 5 minutes." >&2
echo "Logs: journalctl -u bastion -f" >&2
echo "Roll back: cd $repo && git checkout ${current:0:7} && systemctl restart bastion" >&2
exit 1
