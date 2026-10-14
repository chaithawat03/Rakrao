# รากเรา / RAKRAO

RAKRAO (Our Roots) is a private-first family relationship platform. The repository implements Phase 1 Milestones 1–3: application foundation, verified identity/audit, and families with scoped roles. Milestone 4 (private invitations and reviewed membership) is **not started**. Person records, claims and trees remain later work.

The active architecture is **Firebase Hosting + Firebase Authentication + Cloud Run ASP.NET Core + Cloud Firestore**. The modular Domain/Application/Infrastructure boundaries and React/TypeScript frontend remain. PostgreSQL/EF Core and the earlier Cloud SQL staging proposal are superseded; no PostgreSQL, Neon or Docker database is required for acceptance.

## Development prerequisites

- .NET SDK 10 and compatible ASP.NET Core runtime
- Node.js >=22.12 and npm
- Java 21 for the Firestore emulator

Use the existing checkout in an isolated Codex Cloud task; do not create another worktree unless explicitly requested. Do not assume local Windows files or credentials exist.

```bash
npm ci --prefix apps/web
npm ci --prefix infra/firebase
bash infra/firebase/test-emulators.sh
```

The test script restores/builds the solution and runs backend and browser Security Rules tests under Firebase Auth/Firestore emulators. Emulator integration tests fail if the emulator is absent; they never silently skip or contact live Firestore. Tests use isolated `demo-` project IDs and need no Google Cloud credentials or SMS. The pinned CLI checks downloaded emulator integrity. Its download requires `storage.googleapis.com`; npm/NuGet use their normal package hosts. CI uses these same emulators, with no PostgreSQL service and no deployment identity.

Frontend checks:

```bash
cd apps/web
npm run typecheck
npm run lint
npm run format:check
npm test
npm run build
```

## Run the application with emulators

Start emulators from the repository root:

```bash
infra/firebase/node_modules/.bin/firebase emulators:start --only auth,firestore --project demo-rakrao
```

In a second terminal at the repository root:

```bash
ASPNETCORE_ENVIRONMENT=Development \
Firebase__ProjectId=demo-rakrao \
Firestore__ProjectId=demo-rakrao \
FIREBASE_AUTH_EMULATOR_HOST=127.0.0.1:9099 \
FIRESTORE_EMULATOR_HOST=127.0.0.1:8080 \
dotnet run --project src/RakRao.Api --no-launch-profile --urls http://127.0.0.1:5080
```

In a third terminal:

```bash
cd apps/web
VITE_FIREBASE_API_KEY=demo-api-key \
VITE_FIREBASE_AUTH_DOMAIN=demo-rakrao.firebaseapp.com \
VITE_FIREBASE_PROJECT_ID=demo-rakrao \
VITE_FIREBASE_APP_ID=demo-app-id \
VITE_FIREBASE_AUTH_EMULATOR_URL=http://127.0.0.1:9099 \
npm run dev -- --port 5173 --strictPort
```

These are public demo values, not live credentials. Vite forwards relative `/api` requests to port 5080. Both emulators are refused outside Development. Keep private local configuration ignored; public `VITE_` configuration must never contain secrets.

Validate `/health` for liveness and `/health/ready` for a bounded Firestore read. Readiness uses an on-demand cached probe, not background polling. Protected `/api/v1/me` must return 401 without a valid token. An authenticated GET before provisioning returns 404; POST provisions one User, without a Person claim or Family membership.

## Persistence and safety

Firestore Infrastructure implementations preserve UUID User/Family/membership/grant identifiers, UTC timestamps, nulls, integer metadata ETags and API DTOs. Firebase UID is distinct from User ID and Person ID. See [database design](docs/database-design.md) for collections, keys, transactions and indexes.

Family creation commits Family, active creator membership, Creator/Admin grants, invariant/uniqueness records, rolling-hour limit state, optional retry record and audit together. Requests above `FamilyCreation:MaxPerHour` (default three in a rolling hour) return 429. An idempotent retry is checked before throttling and survives later Family edits; reusing its key for different canonical details returns 409.

The API resolves current active membership and same-Family grants. Creator role changes serialize on a Family guard whose version is independent of metadata ETags. Supported mutations cannot remove the last effective Creator. Important business writes and create-only audit events share one retry-safe transaction.

**Enforcement boundary:** these are application transaction guarantees. Privileged administrators using Firestore IAM/server SDKs can bypass them. Firestore browser Security Rules deny all domain access but do not constrain server SDKs. This is not parity with the historical PostgreSQL audit/Creator triggers. There are no normal application audit update/delete operations.

## Cloud-first delivery

Codex Cloud → GitHub → GitHub Actions → Google Cloud/Firebase. PR CI has no deployment credentials. Staging delivery uses GitHub OIDC/Workload Identity Federation and runtime Application Default Credentials, never service-account JSON keys or Firebase CI tokens.

[Staging foundation](docs/staging-foundation.md) describes the manually gated prepared workflow, nonsecret configuration, IAM, existing-resource preflight, rollback, and Phase B bootstrap/live-verification requirements. Phase A provisions no infrastructure and does not merge automatically. The Cloud Run container listens on `0.0.0.0:$PORT`; staging uses `rakrao-api-staging` in `asia-southeast1`, request-based billing and minimum instances zero. Hosting routes `/api/**` before the SPA fallback.

Near-zero fixed infrastructure cost is the goal, not a promise of zero usage charges. Cloud Run/Hosting integration needs billing; Firestore operations, image storage, traffic, logs and real Phone Authentication can incur charges. No Cloud SQL, VPC connector or always-on database is part of this architecture.

## Historical evidence and data migration

Existing EF migrations remain under `src/RakRao.Infrastructure/Persistence/Migrations` as excluded historical source. [Historical SQL design](docs/history/postgresql-database-design.md), Git history and optional `compose.yaml` preserve the prior implementation evidence. They are not active runtime/setup requirements. Do not run EF migrations for Firestore.

Inventory authoritative persisted data before a separate import. If data exists, preserve UUIDs, timestamps, versions, grants and audit rows; verify UID uniqueness, references and effective Creator counts; freeze writes for cutover and retain a recoverable export. No Windows database or uncommitted workstation state is assumed or imported by Phase A.
