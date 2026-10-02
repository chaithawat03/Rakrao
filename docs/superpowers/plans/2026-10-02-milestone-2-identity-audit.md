# Milestone 2 Identity and Audit Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task by task.

**Goal:** Ship the authenticated application User foundation described in `RAKRAO — Phase 1 Milestone 2 Implementation Prompt.md` without Milestone 3 features.

**Architecture:** The API verifies Firebase ID tokens through the official Admin SDK behind a testable verifier. PostgreSQL owns the unique Firebase UID mapping and stores User and audit rows atomically. The React client signs in through the Firebase Web SDK and bootstraps `/api/v1/me` with an ID token.

**Tech Stack:** ASP.NET Core 10, EF Core 10, PostgreSQL, Firebase Admin .NET, React 19, TypeScript, Firebase Web SDK, Vitest, xUnit.

**Spec:** `docs/phase-1-plan.md` (Milestone 2), its linked design documents, and the user supplied implementation brief for this work.

## Global Constraints

- `User != Person`; no Family or Person tables or workflows.
- Verify protected requests on the server. Never authorize from client fields.
- Preserve `/api/v1`, camelCase JSON, RFC 7807 errors, and request IDs.
- Store no ID tokens, OTPs, OAuth secrets, or proxy credentials.
- Cloud changes are limited to Firebase Authentication in `rakrao-dev` after project inspection.

## Review Focus

- Concurrent provisioning of one Firebase UID produces one User and one provision audit event.
- Missing, malformed, expired, and wrong-project credentials produce 401 without logging token text.
- `GET /me` before provisioning has a documented response and does not silently mutate data.
- Frontend session changes and sign-out cannot display stale application User data.
- Development configuration cannot accidentally disable token verification in production.

---

### Task 1: User, audit persistence, and migration

- Add `User` and `AuditEvent` entities and EF mappings in Domain/Infrastructure.
- Test PostgreSQL migration, unique UID, and audit persistence; observe test failure before implementation.
- Generate and review migration; no backfill is required because Milestone 1 has no business rows.

### Task 2: Backend authentication and `/api/v1/me`

- Add an application verifier interface and official Firebase Admin adapter.
- Add authenticated endpoint tests first, covering missing/invalid tokens, request IDs, repeat provisioning, audit, and concurrent UID behavior.
- Implement safe authentication middleware and `GET`/`POST /api/v1/me`.

### Task 3: Web authentication shell

- Add Firebase Web SDK configuration, Google popup, phone/reCAPTCHA, session observer, API bootstrap, and sign out.
- Test signed-out, transitions, bootstrap, errors, and sign out with an external-auth test double.
- Keep Family and Person UI out of scope.

### Task 4: Docs, CI, cloud inspection, and verification

- Document environment configuration and emulator/test strategy.
- Confirm active cloud project before any Firebase configuration action; report console actions if tooling cannot configure them safely.
- Run backend/frontend checks, PostgreSQL integration tests, credential and log review, then commit, push, and check CI.
