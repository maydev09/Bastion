# 0010. Secrets with systemd credentials and Docker secrets

- **Status:** Accepted
- **Date:** 2026-11-15

## Context

Bastion needs a few secrets: database passwords for the PostgreSQL superuser, the
migration role and the application role, later perhaps API keys. Configuration files
(ADR 0008) are committed and must not contain them.

The legacy projects kept secrets in `.env` files: plain text on disk, easy to leak
through a wrong commit, a backup or any tool that reads the project folder.

Bastion runs in Docker Compose, locally and on a headless home server (no desktop
environment). Following the "focus on the core problem" rule, we want no extra
infrastructure such as a secret management server.

## Decision

Secrets are stored encrypted outside the repository, delivered to containers as
**Docker secrets** and read by the application as files.

### Inside the containers (same everywhere)
- Compose mounts every secret as a file `/run/secrets/<name>`. Secrets are never
  passed to containers as environment variables.
- Bastion reads `/run/secrets` with the official key-per-file configuration provider
  (`Microsoft.Extensions.Configuration.KeyPerFile`). The file name is the key, with
  `__` as the section separator, e.g. `ConnectionStrings__Bastion`. Code only sees
  `IConfiguration` and does not know where a value came from.
- PostgreSQL uses `POSTGRES_PASSWORD_FILE`. Role setup scripts and dbmate read
  their passwords from `/run/secrets` as well.

### On the server
- Secrets are encrypted with **systemd-creds** (key bound to the host, and to TPM2
  when available) and stored in `/etc/credstore.encrypted/`.
- A systemd service starts Docker Compose. It loads the secrets with
  `LoadCredentialEncrypted=`; systemd decrypts them into a private in-memory
  directory (`$CREDENTIALS_DIRECTORY`) that exists only while the service runs.
  A server-specific Compose override takes the Docker secrets from that directory.
- Encrypted files are useless on another machine, so a stolen disk or backup does
  not reveal them.

### On a developer machine
- Secret values come from the developer's local secret store. A dev script passes
  them to Compose as environment variables of the `docker compose` process only
  (Compose `secrets: ... environment:`); they are never written to files.

### Rules
- No `.env` files with real values. No secrets in TOML files, Compose files or the repository.
- Exact service files and scripts are created together with the deployment setup.

## Alternatives considered

- **`.env` files:** simple and common, but plain text on disk (legacy approach).
- **Plain files with strict permissions** (`chmod 600`, e.g. deployed by Ansible):
  the common small-scale standard, but still plain text on disk and in backups.
- **HashiCorp Vault / OpenBao, Infisical:** industry standard for many servers and
  teams, but a separate service to run, back up and unseal. Not needed for one server.
- **SOPS + age:** encrypted secret files in the repository, but an extra tool, the
  age key still lies on disk, and TOML is not supported.
- **Passwordless database login** (`peer` over a Unix socket): no secret at all, but
  impractical across the Docker container boundary.

## Consequences

- No plain-text secret on disk in normal operation, with no new infrastructure.
- Containers see secrets the same way locally and on the server.
- Setting up a new server includes encrypting its secrets with `systemd-creds`;
  secrets have to be re-encrypted when moving to another machine.
- Secrets cannot be read accidentally by tools scanning the project folder.
