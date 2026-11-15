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

**Run**

| What | Command | URL |
|---|---|---|
| Backend | `dotnet run --project src/Host/Bastion.Host --launch-profile http` | http://localhost:5080 (Swagger: `/swagger`) |
| UI with hot reload | `npm run dev` | http://localhost:5173 |
| UI served by the backend | `npm run build`, then the backend | http://localhost:5080 |

In Rider: run configuration **API + Web** starts both.

**Tests**: `dotnet test Bastion.slnx` and `npm test`; lint: `npm run lint`.

**Configuration**: `src/Host/Bastion.Host/bastion.toml` (not `appsettings.json`); local overrides
in the git-ignored `bastion.local.toml`; environment variables win, e.g. `Modules__JobOffers__Enabled=false`.

**Windows**: everything runs except live scraping, because the VPN check uses Linux `ip route`
and fails closed. Use Linux (or WSL) for the OtoDom smoke test.

## License

Bastion is free software licensed under the [GNU Affero General Public License v3.0](LICENSE).
You may use, modify and host it. If you run a modified version for other people over a
network, you must make your source code available to them under the same license.
