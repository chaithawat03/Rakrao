# Phase 1 Implementation Milestone Plan

> For future implementation: read [product decisions](product-decisions.md) and the seven design documents before starting. This plan authorizes no application code during Phase 0.

**Goal:** Deliver a secure, private-first RAKRAO (รากเรา) one-family vertical slice with reviewed joining, people and name history, bounded tree views, and staging verification.

**Architecture:** One modular ASP.NET Core API owns Firebase-token verification, Firestore transactions and resource authorization. A React/TypeScript client uses family-scoped REST DTOs, React Flow and ELK for bounded trees, with an accessible text view. Cloud deployment is configured only after current service and residency checks.

**Tech stack:** React, TypeScript, React Flow, ELK, ASP.NET Core, Cloud Firestore, Firebase Authentication/Hosting, Cloud Run and GCS where the shipped slice needs it.

**Sources:** [Architecture](architecture.md), [domain model](domain-model.md), [database design](database-design.md), [permissions](auth-and-permissions.md), [API](api-design.md), [UI](ui-design.md), [roadmap](development-roadmap.md), and [approved decisions](product-decisions.md).

## Global constraints

- Person, User, FamilyPerson and FamilyMembership remain distinct. No automatic identity claim follows membership approval.
- Private-first: no global people/family/relationship directory; all reads and search are backend-filtered by family and resource visibility.
- Family creation is open to authenticated application Users and must atomically create the active creator membership, Creator/FamilyAdmin grants and audit event.
- Person names are historical rows; hidden former names cannot affect unauthorized search.
- Shared Person core edits are denied in Phase 1 until every affected Family's authorization can be verified through a reviewed workflow. A local `FamilyPerson.displayAlias` edit may proceed under that family's policy.
- Cross-family relationships remain unpublished without every affected Family's approval. Phase 1 may reject cross-family creation until that workflow exists.
- No automatic claim for minors and no Phase 1 guardian/delegated ownership model.
- Graph depth, node count and timeout are configuration values; cycle detection and an accessible text view are required.
- `asia-southeast1` is the planning region. Reverify service availability, quota, latency and residency before provisioning; do not put region values in domain code.

## Review focus

1. Forged FamilyId or a role that is valid only in another Family must not authorize a request; test in Milestones 3, 4, 6 and 8.
2. A creator without active membership, or removal of the last Creator, must be impossible; test in Milestone 3.
3. A hidden former name must not produce a search hit, hint or tree label; test in Milestones 6 and 8.
4. Cyclic or very large relationship data must stop within configured graph bounds; test in Milestone 8.
5. A minor or matching name/phone/email must not be linked to an account automatically; verify the absence of such behavior in Milestones 4 and 5.

## Milestone order

### 1. Repository and local development foundation

- **Backend:** Scaffold `RakRao.sln` with `RakRao.Api`, `RakRao.Domain`, `RakRao.Application`, and `RakRao.Infrastructure` boundaries, dependency injection, configuration and health endpoint. Use the `RakRao` namespace prefix.
- **Persistence:** Set up Firestore/Auth emulators and isolated test projects; retain SQL migrations as excluded history, with no production provisioning.
- **Frontend:** Scaffold React/TypeScript shell with package name `rakrao-web`, the `รากเรา`/`RAKRAO` product identity, routing and responsive baseline.
- **Tests:** CI smoke build, TypeScript type check, API startup and Firestore readiness and document mapping smoke test.
- **Completion:** A clean checkout can build, start the app locally and validate transactional persistence in emulator CI using documented commands.

### 2. Verified identity and audit baseline

- **Backend:** Verify Firebase ID tokens, map UID to app User, implement `/api/v1/me`, and define request IDs and audit-writing abstraction.
- **Persistence:** Create `users`, unique UID mappings and create-only `audit_events` through atomic transactions; no raw token storage.
- **Frontend:** Google and phone sign-in entry points, session handling and signed-out/error states.
- **Tests:** Valid/expired/wrong-project tokens, repeat provisioning, SMS test path, token/log redaction.
- **Completion:** A signed-in User is provisioned once; unsigned or invalid requests cannot access protected endpoints; important mutations can emit audit events.

### 3. Families, scoped roles and creator safety

- **Backend:** Create Family for any authenticated User; enforce membership-backed family roles, resource policies and last-Creator protection.
- **Persistence:** Create `families`, `family_memberships`, `role_assignments`, active keys and Family guards; transact Family, active creator membership, Creator/FamilyAdmin grants and audit event.
- **Frontend:** Create-family form, family switcher for authorized families and capabilities-driven controls.
- **Tests:** Rollback on partial failure, forged FamilyId, admin in A/member in B, concurrent last-Creator revocation, creation throttling.
- **Completion:** A new creator immediately has effective family permissions; another User has none, and no active Family loses its last Creator.

### 4. Private invitations and reviewed membership

- **Backend:** Issue/revoke bounded invitation links, accept own MembershipRequest, handle more-information responses and independent admin review.
- **Persistence:** Design Firestore invitation/request documents, deterministic open-request uniqueness and review concurrency fields.
- **Frontend:** Invitation landing, request form/status and admin review queue.
- **Tests:** Expired/revoked invite, duplicate request, self-approval, review race, approved membership without Person link, nonmember family concealment.
- **Completion:** A User joins only after an authorized review; approval transaction creates active membership, FamilyMember grant and audit event.

### 5. People and durable name history

- **Backend:** Create Person with one primary PersonName and family association; add name change and correction operations with optimistic concurrency. Block shared core edits in Phase 1; keep local alias edits scoped to one Family.
- **Persistence:** Design Firestore `persons`, `person_names` and `family_persons`; enforce one current primary name, name validity and history indexes.
- **Frontend:** Person create form, current/former-name editor and clear shared-edit restriction.
- **Tests:** Repeated name changes, repeated spelling, unknown dates, concurrent primary changes, atomic Person/name creation, minor records with no automatic account link.
- **Completion:** Name changes preserve previous rows and audit events; every Person has exactly one current primary name.

### 6. Private profiles and former-name search

- **Backend:** Return filtered Person profiles and family-scoped search across visible current/former names and local aliases, with one result per Person.
- **Persistence:** Add and inspect the planned name-search/index strategy using representative Thai names; no denormalized global directory.
- **Frontend:** Person profile history, family-scoped search and visible match explanation.
- **Tests:** Current and former spelling resolve to one Person; hidden former name yields no hit or hint; member/admin/private field boundaries; long Thai names.
- **Completion:** Search and profile follow the same visibility policy and do not reveal hidden history.

### 7. Basic relationship episodes

- **Backend:** Create and end biological/adoptive/step parentage and marriage/partnership episodes for authorized people in one Family; reject unsafe cycles and cross-family publication until approval workflow exists.
- **Persistence:** Design typed Firestore relationship documents, endpoint indexes and transactional date/status validation; defer cross-family approval workflow to the later phase while preserving the schema design.
- **Frontend:** Minimal authorized relationship editing in Person profile with type and history labels.
- **Tests:** Cycles, duplicate active edges, remarriage of the same pair, ended unions, nonmember mutation and unauthorized cross-family attempt.
- **Completion:** Relationship history is queryable without father/mother columns or derived marital/sibling fields.

### 8. Bounded and accessible tree

- **Backend:** Build family-authorized bounded tree DTOs with configured depth/node/time limits, cycle detection and lineage-context generation calculations.
- **Persistence:** Add or tune relationship traversal indexes and inspect query plans on realistic families; no stored global generation.
- **Frontend:** React Flow/ELK desktop view, focus-first mobile view, search/focus controls and keyboard/assistive-technology usable text list.
- **Tests:** Cyclic/large family, truncation, timeout, hidden node/name, cross-family boundary, long-name layout, keyboard and touch flows.
- **Completion:** Both graph and text views show only authorized bounded data; large inputs fail or truncate predictably.

### 9. Staging integration and release evidence

- **Backend:** Configure Cloud Run staging with a proposed `rakrao-api-staging` service name, structured logging, rate limits, health checks and operational error handling. Inventory any existing external identifiers before choosing names.
- **Persistence:** Separately bootstrap Firestore Native only after project/region/residency review; deploy rules/indexes and verify live transactions, IAM and a reviewed export/restore path. No Cloud SQL or PostgreSQL staging.
- **Frontend:** Firebase Hosting staging with `/api/**` rewrite ahead of SPA fallback; verify responsive and accessible journeys.
- **Tests:** End-to-end sign-in, create Family, invite, review, create Person, change/search name and tree flows; security regression suite and representative load test for limit selection.
- **Completion:** CI and staging evidence show the Phase 1 slice works with permission boundaries intact. Production release remains gated by the policy, accessibility, retention and operations requirements in the roadmap.

## Handoff

Implement milestones in order, reviewing each completed slice before the next. Do not start Phase 1 application work until this Phase 0 documentation and the remaining blocking policy choices for the first slice have been reviewed.

## M1–M3 migration and acceptance record

Baseline `9aacb465231eba9c392d35992ae184162bb6e9b0` has M1, M2 and M3 VERIFIED under the historical SQL implementation. Phase A re-verifies those capabilities on Firestore; it does not renumber milestones or start M4. Milestone 4 remains private invitations and reviewed membership.

Active acceptance is .NET restore/build/tests and unchanged frontend checks, with real Auth/Firestore emulators and no Docker/PostgreSQL dependency or PR cloud credentials. Family metadata retains integer ETags; idempotency and rolling-hour semantics remain. Creator guard versioning is separate. Application-create-only audit and supported-mutation Creator transactions do not protect against privileged IAM administrators as SQL triggers did.

Phase A prepares manually gated keyless staging delivery. Phase B (separate approval) bootstraps existing-resource identifiers, WIF/IAM, Firestore, Cloud Run and Hosting and verifies live staging. No paid resources or automatic merge are authorized by Phase A.
