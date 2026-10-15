# RAKRAO Fast Development Policy

**Status:** ACTIVE for Milestones 4–9 unless explicitly overridden.

## Purpose

Reduce delivery time, Codex context usage, approval overhead, and unnecessary
ceremony while preserving RAKRAO's security, privacy, correctness, CI, and staging
acceptance standards.

This policy does not relax product invariants, authorization boundaries, privacy
requirements, transactional correctness, accessibility, or security tests.

## 1. Default development mode

RAKRAO uses **Fast Controlled Development** by default.

Normal milestone flow:

```text
PLAN
→ IMPLEMENT + TEST
→ WHOLE-BRANCH REVIEW
→ PR + CI
→ MERGE
→ STAGING DEPLOY
→ LIVE ACCEPTANCE
→ COMPLETE
```

Do not insert additional approval gates unless a stop condition in this policy is
triggered. Do not pause after every internal task or ask for approval between ordinary
implementation steps.

## 2. Existing documentation is authoritative

Before starting a milestone, read only the relevant existing documents.

Authoritative sources include:

- `docs/phase-1-plan.md`
- `docs/product-decisions.md`
- `docs/api-design.md`
- `docs/domain-model.md`
- `docs/database-design.md`
- `docs/auth-and-permissions.md`
- `docs/ui-design.md`
- `docs/development-roadmap.md`
- approved milestone-specific documents already present in the repository

Do not repeat brainstorming for decisions already documented and approved.

Create a new architecture/specification document only when the milestone introduces a
material product or architecture decision that existing documentation does not resolve.

## 3. Planning policy

Use one compact implementation plan per milestone.

A plan should normally identify:

- scope
- affected files/modules
- persistence changes
- API changes
- frontend changes
- security/authorization tests
- integration tests
- staging acceptance criteria

Do not stop for human approval after writing the plan unless it identifies:

- an unresolved architecture decision
- a security-policy change
- a privacy-policy change
- a destructive migration
- new paid infrastructure
- a new external service/provider
- a dependency with significant operational impact
- a change to an approved product invariant

Otherwise proceed directly from plan to implementation.

## 4. Execution mode

Prefer Native sequential execution when tasks share domain types, API contracts,
Firestore transactions, application state, or common frontend interfaces.

Use subagents only when work is genuinely parallel and independent. Do not use them
merely because the workflow supports them.

## 5. YAGNI and token-efficient execution

Apply strict YAGNI:

- read only files needed for the current milestone
- do not rediscover approved architecture
- do not scan unrelated repository areas
- do not refactor unrelated code
- do not introduce abstractions for hypothetical future requirements
- do not add dependencies without a concrete milestone requirement
- prefer existing project patterns
- keep diffs focused
- avoid generic frameworks when a small local implementation is sufficient
- stop implementing once acceptance criteria are met

Approved requirements override minimization. Never reduce security, accessibility,
correctness, or required test coverage to save time or tokens.

## 6. TDD policy

Use test-first development where behavior or security matters.

Mandatory test-first areas:

- authorization
- authentication
- role enforcement
- concealment/privacy
- transaction invariants
- idempotency
- optimistic concurrency
- race-condition prevention
- input validation
- important API behavior
- regression bugs

Ceremonial RED/GREEN is not required for trivial markup, pure CSS, documentation,
mechanical extraction with unchanged behavior, or formatting-only work.

Existing tests must not be weakened to accommodate implementation.

## 7. Security invariants never enter fast mode

The following remain strict regardless of schedule:

- User and Person remain distinct
- no automatic Person claim
- no global Family/Person directory
- Family authorization is membership/resource scoped
- roles from one Family cannot authorize another
- hidden data cannot influence unauthorized search/results
- last-Creator protection remains enforced
- membership joining remains reviewed where required
- cross-family publication remains reviewed where required
- minor handling remains conservative/private
- the backend remains the authorization boundary
- browser clients do not directly access protected Firestore domain collections

Any change affecting these rules requires explicit review.

## 8. Commit policy

Prefer 2–4 meaningful commits per milestone.

Typical boundaries:

1. backend/persistence/domain behavior
2. frontend/integration behavior
3. tests/documentation/final corrections

Do not create many tiny commits solely to demonstrate process. Preserve meaningful
architectural history unless explicitly asked to squash.

## 9. Review policy

Perform one whole-branch review before PR creation.

Review focus:

- security
- authorization boundaries
- transaction correctness
- privacy leaks
- API contract consistency
- frontend regressions
- unnecessary complexity
- milestone scope creep

Do not repeat a full review after every small commit. If the whole-branch review has
no Important/Critical finding, proceed to PR. Minor cosmetic observations block only
when they violate acceptance criteria.

## 10. CI policy

Use focused tests during implementation. At milestone completion run the full required
verification once.

Required final verification normally includes:

Backend:
- restore
- build
- test

Frontend:
- `npm ci`
- typecheck
- lint
- format check
- tests
- production build

Run infrastructure/emulator checks only when relevant to changed areas.

Do not repeatedly run the complete suite after every small edit.

## 11. PR and merge policy

Once whole-branch review is clean, final local verification is green, PR CI is green,
and no unresolved Important/Critical finding exists, the milestone may proceed to
merge without another planning cycle.

Do not merge when required CI failed, security behavior is uncertain, acceptance
criteria are incomplete, or scope unexpectedly changed.

Use merge commits when preserving focused milestone history is useful.

## 12. Staging deployment policy

Deploy staging once per completed milestone or meaningful release candidate.

Normal flow:

```text
PR merged
→ main CI green
→ manually gated staging deployment
→ automated health/security smoke checks
→ consolidated live acceptance
```

Do not recreate infrastructure for ordinary milestone work.

## 13. Live acceptance policy

Use one consolidated live checklist per milestone.

Acceptance should cover:

- main happy path
- authorization boundary
- one representative negative path
- persistence across refresh/re-login where applicable
- important race/idempotency behavior where applicable
- responsive/mobile behavior for user-facing features

Automate live checks where practical. Human testing is required only where credentials,
visual judgment, or real-user interaction makes automation inappropriate.

## 14. Documentation policy

Update documentation only when actual architecture, contracts, operations, or user
flows change.

Prefer updating existing authoritative documents rather than creating parallel
milestone reports that merely repeat commits, tests, CI, or PR descriptions.

## 15. Stop / escalation conditions

Fast mode stops if implementation discovers:

- unclear authorization rules
- ambiguous privacy requirements
- incompatible domain-model assumptions
- destructive data migration
- new paid infrastructure
- a new external provider
- security architecture changes
- inability to preserve an existing invariant
- major divergence from an approved API contract
- a major design change affecting later milestones

When triggered:

```text
STOP
→ explain the decision needed
→ present the smallest set of options
→ wait for approval
```

Do not continue by guessing.

## 16. Milestone completion definition

A milestone is COMPLETE when:

- acceptance criteria are implemented
- required tests pass
- security invariants pass
- whole-branch review passes
- PR CI passes
- code is merged to main
- main CI passes
- staging deployment passes
- milestone live acceptance passes
- documentation is current where necessary

Do not require additional ceremonial documents after these are satisfied.

## 17. Milestones 4–9

For M4–M9, use this policy by default.

Do not repeat the full:

```text
brainstorm
→ design approval
→ specification approval
→ implementation-plan approval
→ implementation approval
→ PR approval
→ deploy approval
```

sequence unless an escalation condition is triggered.

Preferred workflow:

```text
compact plan
→ implementation
→ focused tests
→ whole-branch review
→ PR/CI
→ merge
→ staging
→ consolidated acceptance
```

## 18. Superpowers usage

Superpowers remains active when available, but use only the skills that materially
improve the task.

Use:

- writing-plans when planning is genuinely useful
- TDD for behavioral/security work
- requesting-code-review near completion
- verification-before-completion

Do not invoke brainstorming when requirements are already resolved. If a Superpowers
workflow proposes an unnecessary intermediate approval gate, this project policy
authorizes continuation unless an escalation condition above is present.

## 19. Token/context efficiency

Do not repeat long repository summaries in every task. Assume previously approved
project invariants remain active.

Prefer short references such as:

> Follow RAKRAO Fast Development Policy and the Milestone 4 section of
> `docs/phase-1-plan.md`.

instead of restating the full architecture in every prompt.

Final reports should be concise and evidence-based:

- commits
- tests
- CI
- PR
- deployment
- acceptance
- blockers

Do not produce long process narratives unless requested.

## 20. Priority order

When instructions conflict, use:

1. explicit current user instruction
2. security/privacy/product invariants
3. approved milestone requirements
4. RAKRAO Fast Development Policy
5. Superpowers workflow defaults
6. YAGNI/token minimization

Speed never overrides correctness or privacy.
