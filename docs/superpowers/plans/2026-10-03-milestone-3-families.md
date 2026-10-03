# Milestone 3 families implementation plan

**Goal:** Complete Phase 1 Milestone 3 in [phase-1-plan.md](../../phase-1-plan.md): transactional Family creation, scoped roles, last-Creator safety, creation throttling, and an authorized family UI. Milestone 4 invitation and membership review are outside this work.

**Architecture:** Keep the modular ASP.NET Core and React application. The API resolves current membership and grants from PostgreSQL. Family creation writes the Family, active creator membership, Creator and FamilyAdmin grants, and audit event in one transaction. A family row lock serializes role changes; deferred database triggers enforce the active-Creator invariant even for direct SQL.

**References:** [product decisions](../../product-decisions.md), [domain model](../../domain-model.md), [database design](../../database-design.md), [auth and permissions](../../auth-and-permissions.md), [API design](../../api-design.md), and [UI design](../../ui-design.md).

## Work

- [x] Recover the `283fb5b` Milestone 2 baseline and check `main` against `origin/main` before editing.
- [x] Add Family, FamilyMembership, RoleAssignment, EF mappings, and additive migrations with a reviewed no-backfill path.
- [x] Add `IFamilyService.CreateAsync(string firebaseUid, CreateFamilyRequest request, string? idempotencyKey, string requestId, CancellationToken)` and Family summaries.
- [x] Enforce transactional creation, audit logging, durable per-user hourly throttling, and immutable request matching for idempotent retries.
- [x] Resolve family reads and capability decisions from active memberships and current scoped grants.
- [x] Protect role changes with a family row lock and a database invariant covering INSERT, UPDATE, DELETE, and TRUNCATE.
- [x] Add authenticated family list/detail/create/edit and role-change API routes with validation and version checking.
- [x] Add create-family and authorized family selection/edit controls in the React shell.
- [x] Add integration and UI tests for creation rollback, concealment, cross-family scope, active membership, concurrent Creator revocation, throttling, retries, input validation, and auth changes.
- [x] Update README and affected architecture, database, API, and decision documents.
- [x] Run final .NET and web checks, inspect the diff and migration state, then commit only Milestone 3 files on `milestone-3-families`.
