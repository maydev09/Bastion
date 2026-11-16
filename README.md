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
scripts/install-playwright-firefox.sh                                                       # Linux
pwsh src/BuildingBlocks/Bastion.BuildingBlocks.Scraping/bin/Debug/net10.0/playwright.ps1 install firefox   # Windows
```

**Database**

```bash
scripts/dev-up.sh
```

Starts PostgreSQL 18 on `127.0.0.1:5432` (database `bastion`) and applies all migrations.
It is safe to run again: applied migrations are skipped. On the first run it generates random
passwords for the roles `postgres`, `bastion_owner` (migrations) and `bastion_app` (application)
and keeps them in your keyring (`secret-tool lookup app bastion key db-app`); no password is
written to a file.

- Stop: `docker compose stop`. Delete the database: `docker compose down -v`.
- Roles and privileges come from `db/init/01-roles.sh`, which PostgreSQL runs only when it
  creates an empty database. After changing it, delete the database and run `scripts/dev-up.sh` again.
- Migrations: plain SQL files in each module's `Infrastructure/Migrations/`, named
  `YYYYMMDDHHMMSS_description.sql` with `-- migrate:up` and `-- migrate:down` sections.
  They are found automatically; the schema is the module folder name in snake_case
  (`src/Modules/FlatOffers` → `flat_offers`), and each schema has its own `schema_migrations` table.

**Run**

| What | Command | URL |
|---|---|---|
| Backend | `scripts/dev-run.sh` | http://localhost:5080 (Swagger: `/swagger`) |
| UI with hot reload | `npm run dev` | http://localhost:5173 |
| UI served by the backend | `npm run build`, then the backend | http://localhost:5080 |

`scripts/dev-run.sh` reads the application role's password from the keyring and passes the
connection string to the backend as the environment variable `ConnectionStrings__Bastion`.
Without it the backend still starts, and the home page says the database is not configured.
In containers the same key comes from the file `/run/secrets/ConnectionStrings__Bastion`.

In Rider: run configuration **API + Web** starts both. Rider only passes the connection
string on if it was started through the script, so close Rider and run
`scripts/dev-run.sh rider .` (with the Toolbox, the `rider` launcher is in
`~/.local/share/JetBrains/Toolbox/scripts/`).

**Tests**: `dotnet test Bastion.slnx` and `npm test`; lint: `npm run lint`. Backend tests need
a running Docker: database tests start their own PostgreSQL with Testcontainers, set up the roles
and run the migrations exactly as Compose does (same images, read from `compose.yaml`).

**Configuration**: `src/Host/Bastion.Host/bastion.toml` (not `appsettings.json`); local overrides
in the git-ignored `bastion.local.toml`; environment variables win, e.g. `Modules__JobOffers__Enabled=false`.

**Windows**: everything runs except live scraping, because the VPN check uses Linux `ip route`
and fails closed. Use Linux (or WSL) for the OtoDom smoke test.

## License

Bastion is free software licensed under the [GNU Affero General Public License v3.0](LICENSE).
You may use, modify and host it. If you run a modified version for other people over a
network, you must make your source code available to them under the same license.
