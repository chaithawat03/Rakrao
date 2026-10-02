# รากเรา / RAKRAO

RAKRAO (Our Roots) is a private-first family relationship platform. This repository currently contains the Phase 1 Milestone 1 local development foundation. Business workflows, authentication, family records and tree views are planned for later milestones.

The backend is a modular ASP.NET Core monolith with Domain, Application, Infrastructure and API projects. PostgreSQL is the database. The web shell uses React and TypeScript. See [docs/architecture.md](docs/architecture.md), [docs/product-decisions.md](docs/product-decisions.md) and [docs/phase-1-plan.md](docs/phase-1-plan.md) for the approved design.

## Repository layout

| Path | Purpose |
|---|---|
| `src/RakRao.Domain` | Domain boundary; no infrastructure dependency |
| `src/RakRao.Application` | Application behavior and contracts |
| `src/RakRao.Infrastructure` | EF Core and PostgreSQL integration |
| `src/RakRao.Api` | HTTP composition, health checks and logging |
| `apps/web` | React and TypeScript shell (`rakrao-web`) |
| `tests` | Backend smoke and unit tests |
| `infra/postgres` | Local database initialization |
| `.github/workflows` | Build and test CI |

## Prerequisites

- .NET SDK 10.0 and a compatible ASP.NET Core 10 runtime
- Node.js 22.12 or later and npm 10
- Docker with Compose for local PostgreSQL

No Google Cloud access is needed for this milestone. The passwords in `compose.yaml` and `appsettings.Development.json` are disposable local-only defaults. Keep real credentials outside the repository. Override the connection string with `ConnectionStrings__Default` or .NET user secrets when needed.

## Start local PostgreSQL

From the repository root in PowerShell:

```powershell
docker compose up -d postgres
docker compose ps
```

The first start creates `rakrao_dev` and the separate `rakrao_test` database. The database listens on `127.0.0.1:5432`. Stop it with `docker compose down`.

To **delete both local databases and start fresh**, run `docker compose down --volumes`, then `docker compose up -d postgres`. This removes the named local PostgreSQL volume. The initialization SQL only runs when that volume is empty.

## Backend setup and migrations

```powershell
dotnet restore RakRao.sln
dotnet tool restore
dotnet build RakRao.sln --no-restore
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet tool run dotnet-ef database update --project src/RakRao.Infrastructure --startup-project src/RakRao.Api
dotnet run --project src/RakRao.Api --urls http://127.0.0.1:5080
```

In another terminal, check `http://127.0.0.1:5080/health` for API liveness and `/health/ready` for database connectivity. The initial migration creates EF Core migration tracking only. It has no business tables or backfill.

When a later milestone has an approved schema change, create a migration with:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet tool run dotnet-ef migrations add DescriptiveName --project src/RakRao.Infrastructure --startup-project src/RakRao.Api --output-dir Persistence/Migrations
```

Review the migration and data/backfill strategy before applying it. Do not use `EnsureCreated` against a migrated database.

## Frontend setup

```powershell
cd apps/web
npm ci
npm run typecheck
npm run lint
npm run build
npm test
npm run dev
```

The shell is served at the URL printed by Vite. Its API base URL defaults to relative `/api`. The local Vite proxy forwards `/api` to `http://127.0.0.1:5080`; the proxy target lives in one development config file, not in components. Copy `.env.example` to `.env.local` only if you need local overrides. Never place credentials in `VITE_` variables because Vite exposes them to the browser.

On this workstation, the npm user config forces offline mode and its proxy entries return HTTP 400. The existing approved environment proxy works with a command-local, empty npm user config. If the same condition occurs, run this from the repository root; `.npmrc.local` and `.npm-cache` are ignored by Git:

```powershell
New-Item -ItemType File -Path .npmrc.local -Force | Out-Null
cd apps/web
npm ci --offline=false --userconfig ..\..\.npmrc.local --cache ..\..\.npm-cache
```

This does not change system proxy settings or disable TLS validation. Coordinate with IT before changing the corporate npm configuration itself.

## Backend tests

With PostgreSQL running, set the separate test database connection string and run all tests:

```powershell
$env:RAKRAO_TEST_CONNECTION_STRING = 'Host=localhost;Port=5432;Database=rakrao_test;Username=rakrao;Password=rakrao_local_dev_only'
dotnet test RakRao.sln
```

Without PostgreSQL, run the unit and API health smoke tests:

```powershell
dotnet test RakRao.sln --filter 'Category!=Database'
```

CI starts an isolated PostgreSQL service and runs the full backend suite. Frontend CI installs from the committed lockfile with `npm ci`, then runs typecheck, lint, formatting, build and tests. CI has no deployment job or cloud credentials.
