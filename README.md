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

Requires the .NET 10 SDK.

```bash
dotnet run --project src/Host/Bastion.Host --launch-profile http
```

The API listens on http://localhost:5080 (`/api/platform/modules` lists the enabled modules);
Swagger UI is at http://localhost:5080/swagger in Development.

Live scraping reads the browser User-Agent from Playwright's Firefox; install it once:

```bash
scripts/install-playwright-firefox.sh
```

The web UI needs Node.js 24 LTS; run from the repository root:

```bash
npm install
npm run dev
```

It runs at http://localhost:5173 and forwards `/api` to the backend.
In Rider, the shared run configuration **API + Web** (`.run/`) starts the backend and Vite together.
`npm run build` puts the UI into the Host, which then serves it at http://localhost:5080.
Configuration is in TOML, not `appsettings.json`: `src/Host/Bastion.Host/bastion.toml`, with
overrides in `bastion.<Environment>.toml` and a git-ignored `bastion.local.toml`.
Environment variables override files, e.g. `Modules__JobOffers__Enabled=false`.

## License

Bastion is free software licensed under the [GNU Affero General Public License v3.0](LICENSE).
You may use, modify and host it. If you run a modified version for other people over a
network, you must make your source code available to them under the same license.
