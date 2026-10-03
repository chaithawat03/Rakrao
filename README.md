# รากเรา / RAKRAO

RAKRAO (Our Roots) is a private-first family relationship platform. The repository contains the Phase 1 Milestone 1 foundation and the Milestone 2 authenticated User and audit baseline. Family, Person and tree workflows belong to later milestones.

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

For local emulator development, no live Google Cloud access is needed. The passwords in `compose.yaml` and `appsettings.Development.json` are disposable local-only defaults. Keep real credentials outside the repository. Override the connection string with `ConnectionStrings__Default` or .NET user secrets when needed.

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

In another terminal, check `http://127.0.0.1:5080/health` for API liveness and `/health/ready` for database connectivity. The initial migration creates EF Core migration tracking only. The Milestone 2 migration adds `users` and `audit_events`. No backfill is needed because Milestone 1 had no business rows. Apply migrations to an existing development database before testing `/api/v1/me`.

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

The shell is served at the URL printed by Vite. Its API base URL defaults to relative `/api`. The local Vite proxy forwards `/api` to `http://127.0.0.1:5080`; the proxy target lives in one development config file, not in components. Copy `apps/web/.env.example` to `apps/web/.env.local` and supply the Firebase Web app configuration. `VITE_` values are public browser configuration; never put secrets in them.

## Firebase Authentication setup

The existing `rakrao-dev` Google Cloud project is associated with Firebase and has one Web app, `RAKRAO Web Dev` (`1:723651832622:web:2719448385db17471e68bf`). Google and Phone sign-in are enabled. Authorized domains are `rakrao-dev.firebaseapp.com`, `rakrao-dev.web.app` and `127.0.0.1`; SMS is restricted to Thailand (`TH`). One fictional +66 test phone number is configured in Firebase Console, with its value and code kept private. The Google provider is represented in `firebase.json`, which preserves the Auth Emulator configuration; deploy only that provider configuration with `firebase deploy --only auth --project rakrao-dev` when it changes.

For live local development, obtain the Web app's public `apiKey`, `authDomain`, `projectId` and `appId` with `firebase apps:sdkconfig WEB 1:723651832622:web:2719448385db17471e68bf --project rakrao-dev` and put them in ignored `apps/web/.env.local`. Do not commit that file. The backend reads `Firebase__ProjectId` (Development defaults to `rakrao-dev`) and uses Application Default Credentials with the official Firebase Admin SDK. Run `gcloud auth application-default login` locally for development; do not use a downloaded service-account key in the repository. Keep fictional Phone test numbers and codes out of source control.

For isolated local development, run `firebase emulators:start --only auth --project demo-rakrao`. Set `Firebase__ProjectId=demo-rakrao` and `FIREBASE_AUTH_EMULATOR_HOST=127.0.0.1:9099` in the API process environment, then set `VITE_FIREBASE_PROJECT_ID=demo-rakrao`, `VITE_FIREBASE_AUTH_EMULATOR_URL=http://127.0.0.1:9099` and non-secret demo Firebase Web configuration in `apps/web/.env.local`. Start the API and web app using the commands above. The backend refuses emulator mode outside Development. The emulator avoids live SMS; automated tests use a verifier double for endpoint behavior and an unsigned emulator-mode token to check the official Admin SDK adapter's claim boundary. CI sends no SMS and needs no Firebase secret.

After Google or phone sign-in, the browser sends the Firebase ID token as a bearer token to `POST /api/v1/me`. The API verifies the principal and provisions one application User; `GET /api/v1/me` returns that User with `NEW_MEMBER` and an empty `families` list in Milestone 2. An authenticated `GET` before provisioning returns 404. Neither sign-in method claims a Person or grants Family access. Phone verification uses Firebase's reCAPTCHA flow, and test phone numbers for live development must be configured in Firebase rather than committed to source.

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
