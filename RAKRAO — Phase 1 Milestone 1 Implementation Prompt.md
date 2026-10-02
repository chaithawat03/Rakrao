You are continuing development of the RAKRAO (รากเรา) project.

The project has completed Phase 0 architecture and product planning.

The current repository is the source of truth.

Before changing anything, read:

- `AGENTS.md`
- `README.md` if present
- `docs/product-decisions.md`
- `docs/architecture.md`
- `docs/domain-model.md`
- `docs/database-design.md`
- `docs/auth-and-permissions.md`
- `docs/api-design.md`
- `docs/ui-design.md`
- `docs/development-roadmap.md`
- `docs/phase-1-plan.md`

Do not redesign the product or architecture.

We are now starting:

# Phase 1 — Milestone 1
Repository and Local Development Foundation

STOP after Milestone 1.

Do not automatically continue to Milestone 2.

---

# Product identity

Official product name:

Thai:
`รากเรา`

Brand:
`RAKRAO`

Technical identifier:
`RakRao`

Frontend package:
`rakrao-web`

Expected solution/project naming:

```text
RakRao.sln

RakRao.Api
RakRao.Domain
RakRao.Application
RakRao.Infrastructure

RakRao.UnitTests
RakRao.IntegrationTests
```

Do not rename domain terms such as:

```text
Family
Person
FamilyMembership
FamilyPerson
Relationship
PersonName
```

Product naming and domain naming are separate concerns.

---

# Current Google Cloud status

A Google Cloud project already exists:

```text
Project ID: rakrao-dev
Environment: Development
```

The local machine has Google Cloud CLI installed.

The developer may have an authenticated Google account and may be able to access `rakrao-dev`.

However:

THIS MILESTONE DOES NOT AUTHORIZE GOOGLE CLOUD PROVISIONING OR DEPLOYMENT.

Do NOT create, modify, delete, or deploy:

- Cloud Run services
- Cloud SQL instances
- Cloud Storage buckets
- Artifact Registry repositories
- Firebase projects/resources
- IAM bindings
- Service accounts
- Secrets
- VPC/network resources
- APIs/service enablement
- billing configuration

Do NOT run mutating `gcloud` commands.

If useful, you may perform non-mutating verification such as:

```powershell
gcloud config get-value project
gcloud projects describe rakrao-dev
```

But cloud access is NOT required to complete Milestone 1.

If gcloud authentication or proxy access fails, report it and continue with local development.

Do not run:

```powershell
gcloud auth login
gcloud auth print-access-token
```

as part of this milestone.

Never print or inspect access tokens, proxy passwords, OAuth secrets or other credentials.

---

# Corporate network / proxy awareness

The development machine may be behind a corporate HTTP proxy.

Do not modify proxy settings unless explicitly required for package restore/install.

Do not write proxy usernames/passwords into:

- repository files
- `.env`
- committed scripts
- README
- CI
- application settings

Do not echo secrets into logs.

If npm, NuGet, Docker, Git or other network tooling fails because of proxy/network restrictions:

1. identify the failing tool,
2. report the exact non-secret error,
3. use existing environment configuration when possible,
4. do not disable TLS validation globally,
5. do not commit insecure workarounds.

---

# First task: inspect repository state

Before creating or changing files:

1. Inspect the repository structure.
2. Inspect Git status.
3. Identify any existing partial Milestone 1 implementation.
4. Identify duplicate or obsolete project names such as:
   - FamilyPlatform
   - Family Relationship Platform
   - Family_Relationship_Platform
5. Distinguish:
   - product/project identifiers that should now use RakRao,
   - domain terminology that must remain Family/Person/etc.,
   - historical documentation that should not be blindly modified.
6. Do not overwrite existing work without reviewing it.

Then provide a short implementation plan and proceed with Milestone 1.

Do not wait for approval unless you encounter an architectural conflict or destructive operation.

---

# Milestone 1 objective

At completion, a clean checkout must be able to:

- restore backend dependencies
- build the backend
- start the backend locally
- start PostgreSQL locally
- apply EF Core migrations
- run backend tests
- install frontend dependencies
- type-check the frontend
- build the frontend
- start the frontend locally
- run baseline automated tests

No production or cloud deployment is required.

---

# 1. Repository structure

Normalize the repository toward:

```text
/
  AGENTS.md
  README.md
  RakRao.sln

  docs/

  apps/
    web/

  src/
    RakRao.Api/
    RakRao.Domain/
    RakRao.Application/
    RakRao.Infrastructure/

  tests/
    RakRao.UnitTests/
    RakRao.IntegrationTests/

  tests/
    web/

  infra/

  .github/
    workflows/
```

If an equivalent existing structure is already present and technically sound, do not reorganize merely for cosmetic reasons.

Keep documentation intact.

---

# 2. .NET foundation

Use a currently supported .NET version that is compatible with the selected ASP.NET Core, EF Core and PostgreSQL provider versions.

Check the locally installed SDK first:

```powershell
dotnet --info
dotnet --list-sdks
```

Do not blindly select a version from an old tutorial.

Create or normalize:

```text
RakRao.Api
RakRao.Domain
RakRao.Application
RakRao.Infrastructure
```

Architectural dependency direction:

```text
RakRao.Domain
        ↑
RakRao.Application
        ↑
RakRao.Infrastructure
        ↑
RakRao.Api
```

Interpret this conceptually:

- Domain has no infrastructure/cloud dependency.
- Application depends on Domain.
- Infrastructure implements Application abstractions and may depend on Domain/Application.
- API composes the application and infrastructure.

Avoid circular project references.

Configure:

- dependency injection
- options/configuration
- environment-specific settings
- structured logging baseline
- request/correlation ID support
- health checks

Provide a simple health endpoint such as:

```text
GET /health
```

Do not create Family/Person/Auth business endpoints yet.

---

# 3. Local PostgreSQL

Use PostgreSQL for local development.

Prefer Docker Compose unless a suitable repository approach already exists.

Example intent:

```text
PostgreSQL container
        │
        ├── development database
        └── separate integration-test database
```

Do not require PostgreSQL to be installed directly on the Windows host.

Do not commit real passwords.

Provide safe local-development defaults where appropriate.

Create the EF Core DbContext infrastructure required for future milestones.

Provide a migration mechanism.

Milestone 1 does NOT require the complete production domain schema.

Do not prematurely implement all tables from `database-design.md`.

Document commands for:

- start PostgreSQL
- stop PostgreSQL
- apply migrations
- create migration
- reset local development database

---

# 4. Frontend foundation

Under:

```text
apps/web
```

create or normalize a React + TypeScript application.

Package name:

```text
rakrao-web
```

Use the existing selected frontend stack from the architecture documents.

Set up:

- React
- TypeScript
- routing
- strict TypeScript where practical
- linting
- formatting
- environment configuration
- API client/config foundation
- responsive base layout

Use relative API URLs:

```text
/api
```

Do not hard-code:

- localhost ports throughout components
- Cloud Run URLs
- Firebase URLs
- production endpoints

Create only a minimal application shell.

Display:

```text
รากเรา
RAKRAO
```

The working tagline may appear as editable product copy:

```text
Discover how we are connected.
```

Do not hard-code the tagline throughout the application.

Do NOT implement React Flow family tree yet.

---

# 5. Local developer configuration

Create a safe configuration strategy for development.

Do not commit:

- Google credentials
- Firebase Admin credentials
- service account JSON files
- access tokens
- refresh tokens
- proxy credentials
- production database passwords
- API keys
- secrets

Update `.gitignore` appropriately.

If `.env.example` or equivalent examples are useful, include placeholders only.

For .NET development secrets, prefer normal secure development mechanisms rather than checked-in credentials.

---

# 6. Tests

Create baseline testing infrastructure.

Backend:

```text
RakRao.UnitTests
RakRao.IntegrationTests
```

At minimum test:

- solution builds
- API can start
- `/health` responds successfully
- database connection works in integration environment
- initial migrations can be applied

Frontend:

At minimum:

- TypeScript type check
- production build
- minimal render/smoke test if the chosen testing setup adds reasonable value

Avoid meaningless tests written only to increase test count.

---

# 7. CI

Create an initial GitHub Actions CI workflow.

At minimum verify:

Backend:

```text
dotnet restore
dotnet build
dotnet test
```

Frontend:

```text
dependency install
typecheck
build
tests if configured
```

Use locked dependency installation where supported.

If integration tests require PostgreSQL, use an isolated CI service/container.

Do NOT deploy to Google Cloud from CI.

Do NOT add production credentials to GitHub Actions.

---

# 8. README

Create or update `README.md`.

Include:

- project name: รากเรา / RAKRAO
- short project description
- architecture summary
- repository structure
- prerequisites
- backend setup
- frontend setup
- local PostgreSQL setup
- migration commands
- test commands
- development startup commands

Keep it developer-focused.

Do not duplicate the full architecture documentation into README.

Link to `/docs` instead.

---

# 9. Do not implement yet

Milestone 1 must NOT implement:

- Firebase Authentication
- Google Sign-In
- Phone OTP
- application User provisioning
- Family creation
- FamilyMembership
- roles and authorization
- Family invitations
- MembershipRequest
- Person
- PersonName
- name-history search
- UserPersonClaim
- Relationships
- React Flow family tree
- photo uploads
- Cloud Storage
- Cloud SQL
- Cloud Run
- Firebase Hosting deployment

These belong to later milestones.

Infrastructure should be ready for them without implementing their business behavior.

---

# 10. Do not redesign existing decisions

Preserve the Phase 0 decisions.

Especially preserve:

- User != Person
- PersonName history
- private-first behavior
- family-scoped authorization
- bounded graph traversal
- no stored global generation
- no stored derived marital status
- no stored sibling field
- shared Person policy
- cross-family publication policy
- modular monolith architecture
- PostgreSQL database choice

If implementation reveals a contradiction:

STOP that specific change.

Document:

- existing decision
- implementation conflict
- possible solutions
- recommendation

Do not silently rewrite architecture documentation.

---

# 11. Verification before completion

Run all practical verification locally.

Backend:

```text
dotnet restore
dotnet build
dotnet test
```

Frontend:

```text
install dependencies
typecheck
build
tests if configured
```

Database:

```text
start PostgreSQL
connect successfully
apply migrations
run integration database smoke test
```

Application:

```text
start API
GET /health
start frontend
verify shell loads
```

Inspect Git status at the end.

Ensure no secret files or credentials were accidentally added.

---

# 12. Completion report

When Milestone 1 is complete, provide:

## Summary

What was implemented.

## Repository changes

Important files and projects created or changed.

## Architecture

Any implementation-level architecture choices made.

## Versions

Report selected versions for:

- .NET
- ASP.NET Core
- EF Core
- PostgreSQL provider
- PostgreSQL
- Node.js requirements
- package manager
- React
- TypeScript
- build tooling

## Local commands

Exact commands required to run:

- database
- API
- frontend
- tests

## Verification results

Show results for:

- backend restore
- backend build
- backend tests
- frontend typecheck
- frontend build
- database migration
- health endpoint
- integration tests

Do not expose credentials or tokens in the report.

## Deviations

List any deviation from `docs/phase-1-plan.md`.

If none, say so.

## Problems / blockers

List any remaining local environment issues, including proxy/network problems.

## Deferred to Milestone 2

Clearly identify what was intentionally not implemented.

---

# Final boundary

Complete ONLY Phase 1 Milestone 1.

Do not start Firebase Authentication or any Milestone 2 implementation.

Do not create or deploy Google Cloud resources.

Stop after the Milestone 1 completion report.