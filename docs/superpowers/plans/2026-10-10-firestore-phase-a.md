# Firestore Phase A implementation plan

**Goal:** Migrate the verified M1–M3 application to Firestore and prepare keyless staging delivery, without M4, provisioning, automatic merging, or destructive data changes.

**Authority:** User-approved migration/CI-CD design and Phase A brief; baseline main `9aacb465231eba9c392d35992ae184162bb6e9b0`.

**Architecture:** Preserve Domain/Application interfaces and REST contracts. Infrastructure owns Firestore document mapping and retry-safe atomic transactions. Browser database access is denied. Creator invariants and create-only audit are application guarantees; privileged administrators are outside that boundary.

## Constraints

- Work on `architecture/firestore-staging`; focused commits, no force push or main merge.
- Preserve UUIDs, UTC timestamps, nulls, integer metadata versions, authorization concealment, rolling-hour throttle and existing request hashing.
- No live infrastructure or credentials; stage workflow is manual and disabled until nonsecret bootstrap configuration exists.
- No Docker/PostgreSQL acceptance dependency. SQL migrations may remain explicitly historical and excluded from active compilation.
- All business/audit mutation writes share one transaction; read before write; stable IDs across retries.

## Task 1: Firestore application and backend acceptance

- [ ] Add transaction/document mapping abstractions and Firestore implementations for me, families and create-only audit.
- [ ] Port tests first; observe expected baseline failures, implement and run the full emulator-backed suite.
- [ ] Cover UID races, atomic rollback, retry execution, roles across families, inactive membership, Creator races, stale metadata, idempotency and rolling-hour limits.
- [ ] Remove SQL runtime wiring and update readiness; keep SDK usage in Infrastructure.
- [ ] Review and commit backend changes.

## Task 2: Cloud configuration and CI/CD

- [ ] Configure Auth/Firestore emulators, deny browser rules and query-driven indexes.
- [ ] Replace PostgreSQL CI with emulator execution, preserving frontend checks.
- [ ] Prepare Cloud Run container and manually triggered WIF staging workflow with explicit nonsecret configuration preflight; no automatic deploy.
- [ ] Configure Hosting API rewrite before SPA fallback; validate rules, config and container where supported.
- [ ] Review and commit delivery changes.

## Task 3: Documentation and final verification

- [ ] Reconcile all requested decision/design/runbook documents with actual implementation; preserve milestone numbering and accepted enforcement boundaries.
- [ ] Update reusable onboarding instructions to Firestore development after testing them.
- [ ] Run backend/frontend acceptance, inspect diffs and tracked credential scans; review whole branch.
- [ ] Commit, push branch, inspect GitHub Actions and correct genuine failures until green, or record an evidenced external blocker.
- [ ] Report Phase A status and Phase B prerequisites; stop without merge, provisioning or M4.

## Review focus

Transactions must not cache role authority before mutations, lose Creator guards, change rolling-hour boundaries, duplicate events on retries, or let SDK exceptions leak credentials. Privileged administrator bypass must be documented honestly. CI must fail rather than silently skip missing emulators. Deploy configuration must not allow untrusted refs to impersonate accounts or provision resources implicitly.
