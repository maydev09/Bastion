# One image with the backend and the frontend (ADR 0011).

FROM node:24.22.3-trixie-slim AS web
WORKDIR /src
COPY package.json package-lock.json .npmrc ./
RUN npm ci
COPY tsconfig.json vite.config.ts ./
COPY src/Web src/Web
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0.404-noble AS backend
WORKDIR /src
COPY . .
COPY --from=web /src/src/Host/Bastion.Host/wwwroot src/Host/Bastion.Host/wwwroot
# The OpenAPI document is generated and checked in CI, not needed here.
RUN dotnet publish src/Host/Bastion.Host --configuration Release --runtime linux-x64 --self-contained false \
    -p:OpenApiGenerateDocuments=false --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0.15-noble
# iproute2: the VPN check runs `ip route get` (ADR 0019).
RUN apt-get update && apt-get install --yes --no-install-recommends iproute2 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=backend /app .
# Firefox is started once to read its User-Agent (ADR 0016). The NuGet package makes Playwright's
# node executable only for its owner (root), but the app runs as `app`.
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
RUN chmod 0755 .playwright/node/linux-x64/node \
    && .playwright/node/linux-x64/node .playwright/package/cli.js install --with-deps firefox \
    && rm -rf /var/lib/apt/lists/*
RUN mkdir /data && chown app:app /data
USER app
VOLUME /data
ENTRYPOINT ["dotnet", "Bastion.Host.dll"]
