# 0008. Configuration in TOML

- **Status:** Accepted
- **Date:** 2026-11-15

## Context

Bastion needs technical configuration from day one: which modules are enabled
(`Modules:<Name>:Enabled`, ADR 0007), connection strings, delays, timeouts and
the VPN check (`Scraping:RequireVpn`). User preferences such as search criteria are
not configuration; they are data managed in the UI (SPEC, Security and privacy).

The .NET default is `appsettings.json`. JSON has no comments, and nested sections
get noisy quickly. The maintainer prefers TOML, which is easy to read and edit by hand.

## Decision

- Configuration files use **TOML** instead of `appsettings.json`.
- TOML is plugged into the standard .NET `IConfiguration` as one more configuration
  provider. The rest of the code only sees `IConfiguration` and the options pattern,
  so it does not know the file format.
- Files in the Host project:
  - `bastion.toml`: committed defaults, no secrets.
  - `bastion.<Environment>.toml` (e.g. `bastion.Development.toml`): committed
    overrides per environment, no secrets.
  - `bastion.local.toml`: local overrides, git-ignored.
- Environment variables override files, using the standard .NET form
  (`Modules__Housing__Enabled=false`). Secrets (passwords, connection strings
  with passwords) never go into TOML files; they come from secret files (ADR 0010).
- TOML tables map to configuration sections, so keys stay the same as in the other ADRs:

```toml
[Modules.Housing]
Enabled = true

[Scraping]
RequireVpn = true   # turn off only locally, see SPEC
```

## Alternatives considered

- **`appsettings.json`:** built in and familiar, but no comments and harder to read.
- **YAML:** has comments, but meaningful indentation and implicit typing cause
  surprising bugs.
- **Environment variables only:** fine for containers, poor for
  structured defaults that should be versioned and documented.

## Consequences

- One extra package for the TOML provider, referenced only by the Host.
- Developers used to `appsettings.json` have to look for `bastion.toml` instead.
  The README says so.
- `.gitignore` must contain `bastion.local.toml`.
