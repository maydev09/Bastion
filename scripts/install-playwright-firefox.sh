#!/usr/bin/env bash
set -euo pipefail

repo="$(cd "$(dirname "$0")/.." && pwd)"
dotnet build "$repo/src/BuildingBlocks/Bastion.BuildingBlocks.Scraping" --nologo -v quiet
playwright="$repo/src/BuildingBlocks/Bastion.BuildingBlocks.Scraping/bin/Debug/net10.0/.playwright"
"$playwright/node/linux-x64/node" "$playwright/package/cli.js" install firefox
