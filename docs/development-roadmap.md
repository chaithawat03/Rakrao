# Development roadmap

## Sequence and exit criteria

| Phase | Deliverable | Exit criteria |
|---|---|---|
| 0: design review | Record approved MVP decisions and Phase 1 milestones | [Product decisions](product-decisions.md), permanent repository rules and [Phase 1 plan](phase-1-plan.md) agree; remaining policy questions are tracked |
| 1: secure foundation and one-family vertical slice | Repository scaffold, CI, local PostgreSQL, migrations, Firebase sign-in, verified API, family creation, reviewed membership, people with name history and former-name search, bounded tree and audit | A new account signs in, joins a family through review, views permitted people on phone/notebook, searches by a current or former name, and cannot read or mutate another family; automated tests pass |
| 2: identity and cross-family connection | Claims, person links, expanded relationship history, private photos and cross-family approval | Remarriage/history and shared people work without duplicate identities or unauthorized cross-family disclosure |
| 3: richer traversal and controls | Ancestor/descendant filters, relationship path explanations, advanced privacy, admin tools | Bounded queries meet latency targets on realistic datasets; path explanation never exposes hidden nodes |
| 4: production hardening | Accessibility conformance audit, localization, abuse controls, backup/restore, retention/erasure, observability and load testing | Security review, restore drill, accessibility tests and operational runbooks pass release gate |

Phase boundaries are sequencing proposals. If photos or full relationship types are essential to the first public release, bring them into Phase 1 with their security and test work, not as a UI-only shortcut. The first release should not launch without Phase 4 gates that apply to its shipped capabilities.

## Phase 1 implementation checklist

- [ ] Confirm open policy details before the affected feature ships: claim evidence, exact minor-age handling, retention/erasure periods, accessibility conformance and any Thailand-only residency requirement. Recheck the configured `asia-southeast1` service availability before provisioning.
- [ ] Create the proposed repository structure and a minimal React/TypeScript app plus one ASP.NET Core Web API; pin supported dependency versions after a current compatibility check.
- [ ] Add local PostgreSQL configuration, CI, lint/type checks and separate unit, integration and browser test commands.
- [ ] Configure Firebase Google and phone sign-in, authorized domains/SMS regions and a development emulator/test-account strategy.
- [ ] Implement API Firebase ID token verification and a current-user endpoint; test invalid, expired and wrong-project tokens.
- [ ] Create migrations for users, persons, person_names, families, family-person associations, memberships, role assignments, family invitations, membership requests, relationships and audit events needed by the slice.
- [ ] Implement family creation with transactional Creator/Admin grants and at least-one-Creator protection.
- [ ] Implement family-scoped authorization and resource visibility, including an admin in one family who is only a member of another.
- [ ] Implement private family invitation links, then membership request submission, review, more-information loop and transactional approval with audit entries.
- [ ] Implement authorized Person create/read/search and explicit family association; create the first current primary name transactionally, support repeated historical names and authorized former-name search, and keep Person separate from User.
- [ ] Implement name-change, former-name and correction API operations with optimistic concurrency and audit events; block shared core edits in Phase 1 until all affected-family approvals can be verified; keep tree nodes compact and profile history readable.
- [ ] Implement minimal biological/adoptive/step parentage and marriage/partnership representation needed for the tree; enforce endpoint constraints and prevent parentage cycles.
- [ ] Implement a bounded family tree endpoint with authorization filtering, configurable depth/node/time caps, cycle handling and no global generation field; choose limit values from representative performance tests.
- [ ] Build desktop React Flow view with ELK, search/focus, pan/zoom and node selection; build focus-first mobile view with tap expansion and a keyboard/assistive-technology usable text alternative.
- [ ] Configure Firebase Hosting `/api/**` rewrite ahead of SPA fallback and deploy a staging Cloud Run API connected to staging Cloud SQL using managed secrets/identity; use the selected `asia-southeast1` planning region as infrastructure configuration after a current availability check.
- [ ] Add tests for permission boundaries, review races, repeated name changes, current/former-name search resolving to one Person, hidden-name search non-disclosure, duplicate relationships, graph truncation, phone/Google sign-in paths, keyboard use and narrow-screen behavior.
- [ ] Add structured logs, request IDs, health checks, database backup plan and a staging smoke test; verify no raw evidence, names hidden by privacy or tokens reach logs.
- [ ] Review the working Phase 1 slice before expanding to claims, cross-family publication, photos or path labeling.

## Implementation approach and tradeoffs

Build a vertical slice early so schema, API and UI authorization assumptions are tested together. Alternative: implement every backend entity before any UI, which delays discovery of usability and contract issues. Phase 1 deliberately limits graph depth and workflow breadth; this reduces delivery risk but means cross-family links, photo uploads and identity claims remain unavailable until Phase 2. Keep schema support for these features in the design while migrating only the tables required by shipped behavior. Use meaningful integration tests for security and database constraints, with unit tests for kinship and policy rules; avoid tests that merely restate DTO mappings.

## Remaining review questions

The MVP decisions are recorded in [product-decisions.md](product-decisions.md). Before the relevant implementation or production gate, resolve:

1. What evidence and independent review standard is required for a disputed UserPersonClaim?
2. What exact minor-age rule applies when a birth date is partial or unknown, and what future consent/delegation model is appropriate?
3. What retention, export and erasure periods apply to living-person data, historical names, request evidence and audit events?
4. What graph latency target and configured depth/node/time caps pass realistic family data tests?
5. What accessibility conformance target and language/search behavior are required for launch?
6. Is Thailand-only residency required, and does that change the `asia-southeast1` planning region?

The ordered implementation milestones are in [phase-1-plan.md](phase-1-plan.md). No production application code is authorized by this Phase 0 package.