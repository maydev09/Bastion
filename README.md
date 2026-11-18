# Bastion

## Where Bastion came from

Bastion grew out of two tools I built for myself to automate my own searches:

- **JobOffersFinder** (Python) collected job offers from several job boards. It ran on my home server and genuinely helped me land a job.
- **FlatFinder** (C#) did the same for apartment listings.

Both did their job well, but they shared one limitation: no user interface. I browsed the data directly with SQL queries. That gave me full control, but it didn't surface the most interesting part — the patterns and trends you can't see by looking at individual records.

Bastion is the next step:

- **One platform** instead of two separate projects in different tech stacks.
- **A frontend** that clearly presents the insights hidden in the collected data.
- **A modular architecture** where the scrapers are just the first modules. The long-term goal is a personal headquarters: a hub of tools that automate everyday tasks for a single person.

## Running locally

**Requirements**
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 24 LTS](https://nodejs.org/) with npm 12 (`npm install -g npm@12`)
- A VPN for live scraping (Proton, WireGuard or any tunnel)
- Database (Linux): [Docker Engine](https://docs.docker.com/engine/install/) with the Compose plugin
  (tested with Compose 5.7), `secret-tool` (Debian: `apt install libsecret-tools`) with a running
  desktop keyring, and `openssl`. PostgreSQL and dbmate run in containers, nothing else to install.

**First run** (repository root)

```bash
npm install
dotnet build Bastion.slnx
```

Playwright Firefox, needed for live scraping (the User-Agent is read from it):

```bash
scripts/install-playwright-firefox.sh
```

**Database**

```bash
scripts/dev-up.sh
```

Starts PostgreSQL 18 on `127.0.0.1:5432` (database `bastion`) and applies all migrations.
It is safe to run again: applied migrations are skipped. On the first run it generates random
passwords for the roles `postgres`, `bastion_owner` (migrations) and `bastion_app` (application)
and keeps them in your keyring (`secret-tool lookup service Bastion account db-app-password`).
It also stores the backend's connection string in the .NET user secrets of `Bastion.Host`
(`dotnet user-secrets list --project src/Host/Bastion.Host`), which the backend reads in Development.

- Stop: `docker compose stop`. Delete the database: `docker compose down -v`.
- Recreate it from scratch (all data lost, local raw captures too): `scripts/dev-reset-db.sh`. Useful to check that the
  migrations alone build the schema you expect, e.g. after renaming tables in an unmerged migration.
- Roles and privileges come from `db/init/01-roles.sh`, which PostgreSQL runs only when it
  creates an empty database. After changing it, delete the database and run `scripts/dev-up.sh` again.
- Migrations: plain SQL files in each module's Infrastructure project
  (`src/Modules/Housing/Bastion.Modules.Housing.Infrastructure/Migrations/`), named
  `YYYYMMDDHHMMSS_description.sql` with `-- migrate:up` and `-- migrate:down` sections.
  They are found automatically; the schema is the module folder name in snake_case
  (`src/Modules/Housing` → `housing`), and each schema has its own `schema_migrations` table.

**Run**

| What | Command | URL |
|---|---|---|
| Backend | `dotnet run --project src/Host/Bastion.Host` | http://localhost:5080 (Swagger: `/swagger`) |
| UI with hot reload | `npm run dev` | http://localhost:5173 |
| UI served by the backend | `npm run build`, then the backend | http://localhost:5080 |

Without a connection string the backend stops at startup and says to run `scripts/dev-up.sh`.
In containers the same key comes from the file `/run/secrets/ConnectionStrings__Bastion`.

In Rider: run configuration **API + Web** starts both.

**Tests**: `dotnet test Bastion.slnx` and `npm test`; lint: `npm run lint`. Backend tests need
a running Docker: database tests start their own PostgreSQL with Testcontainers, set up the roles
and run the migrations exactly as Compose does (same images, read from `compose.yaml`).

**Raw data**: every page fetched by an import is kept, with personal data anonymized as `***`, as a Zstandard file for 30 days, locally in
`src/Host/Bastion.Host/data/raw/housing/<day>/<id>.zst` (git-ignored; open with `zstd -dc <id>.zst`).
The folder is `[Modules.Housing.RawCaptures] Path` in the configuration.

**Configuration**: `src/Host/Bastion.Host/bastion.toml` (not `appsettings.json`); local overrides
in the git-ignored `bastion.local.toml`; environment variables win, e.g. `Modules__JobOffers__Enabled=false`.

**Windows** is not documented or tested. Bastion can run there too, but the scripts (bash,
`secret-tool`) would have to be rewritten (e.g. PowerShell with the Windows Credential Manager),
or run in WSL 2 with a Secret Service such as `gnome-keyring`. Live scraping also needs the VPN
check (`ip route`) to see the VPN interface, which WSL 2 does not by default.

## Running on a server

Bastion runs on a small home server with Debian (no desktop, SSH only), started by systemd.
Its scraping traffic goes through a VPN container (ADR 0019); secrets are encrypted with
`systemd-creds` (ADR 0010).

1. Clone the repository to `/opt/bastion` and run `sudo scripts/server-setup.sh`. It installs
   Docker, asks for the WireGuard private key of your VPN provider and starts the `bastion` service.
2. Open `http://<server>:8080` from the home network. Do not forward the port to the internet:
   Bastion has no login.
3. **Update:** `cd /opt/bastion && sudo git pull && sudo systemctl restart bastion`. The server
   pulls the image CI published for that commit; it never builds one.
4. **Roll back:** `sudo git checkout <older commit>` and restart.
5. **Logs:** `sudo journalctl -u bastion -f`.

## License

Bastion is free software licensed under the [GNU Affero General Public License v3.0](LICENSE).
You may use, modify and host it. If you run a modified version for other people over a
network, you must make your source code available to them under the same license.
