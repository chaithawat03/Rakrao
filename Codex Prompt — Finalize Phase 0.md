We have reviewed the architecture package.

Proceed with Phase 0 finalization.

Do NOT implement the application yet.

Use the existing documents in `/docs` as the current source of truth.

## Product decisions for the first MVP

Record the following decisions.

### Product visibility

The application is PRIVATE-FIRST.

There is no global public directory of:
- people
- families
- relationships

Public person search is disabled in the initial release.

Living-person information defaults to FAMILY_MEMBER visibility unless explicitly changed through an approved policy.

### Family creation

An authenticated application user may create a Family.

When a Family is created:

- the creator receives the family-scoped Creator role
- the creator also receives FamilyAdmin permissions
- the operation must be transactional
- an audit event must be recorded

A Family must never lose its last active Creator.

### Family joining

Users do not automatically become FamilyMembers.

The initial workflow is:

Firebase Sign-In
→ application User
→ invitation or Family entry point
→ Membership Request
→ FamilyAdmin / Creator review
→ approval
→ FamilyMembership

Membership approval does NOT automatically prove which Person represents that User.

### User and Person identity

User and Person remain separate entities.

Joining a Family and claiming an existing Person are separate operations.

A name match, former-name match, phone number or email address alone must never automatically approve identity.

UserPersonClaim remains a reviewed workflow.

### Name history

Keep the current PersonName design.

A Person may have:

- one current primary name
- multiple previous names
- aliases
- birth name
- preferred name
- legal name

Previous names must remain searchable when the caller has permission to see that name.

Changing a person's name must preserve history rather than overwrite the previous value.

### Shared people

A Person may be associated with more than one Family.

Core Person facts are shared information.

For the first implementation, changes to shared core Person information must respect every affected Family's authorization policy.

Family-specific presentation may use FamilyPerson.displayAlias without modifying global PersonName history.

### Cross-family relationships

A relationship connecting people belonging to different Family scopes must not become visible automatically.

Each affected Family must authorize publication before the relationship becomes visible in ordinary tree queries.

A relationship must never implicitly grant permission to view another Family.

### Minors

For the initial implementation:

- minors may exist as Person records
- their data should receive conservative visibility defaults
- do not implement automatic Person claiming for minors
- leave guardian/delegated account ownership as a future product decision

Do not invent a legal guardian model during Phase 1.

### Deployment region

The primary expected users are initially in Thailand.

Before provisioning production resources, verify the currently supported Google Cloud/Firebase deployment options and select a region appropriate for users in Thailand.

Cloud Run and Cloud SQL should be colocated where practical.

Record the selected region as infrastructure configuration rather than embedding it into domain logic.

### Scale

Design for bounded tree traversal.

Never return an entire Family graph by default.

Tree APIs must enforce:

- depth limits
- node limits
- execution timeout
- cycle detection

The exact limits should remain configuration values and should be selected after realistic performance tests.

### Accessibility

The visual tree must not be the only representation of relationships.

Maintain the existing requirement for a textual/list representation usable through keyboard and assistive technology.

### Architecture constraints

Keep:

- React
- TypeScript
- React Flow
- ELK
- ASP.NET Core
- PostgreSQL
- EF Core
- Firebase Authentication
- Cloud Run
- Cloud SQL
- Google Cloud Storage
- Firebase Hosting

Continue using a modular monolith.

Do not introduce microservices or a graph database unless a future measured requirement justifies them.

## Tasks

1. Review all existing `/docs/*.md` files against these decisions.

2. Update documents where these decisions resolve an existing assumption.

3. Create:

`docs/product-decisions.md`

Document each decision with:

- decision
- rationale
- consequences
- future reconsideration conditions

4. Create:

`AGENTS.md`

This file must contain permanent engineering rules for Codex working in this repository.

At minimum include:

- Person and User are separate concepts.
- Never authorize using frontend-supplied roles.
- Never use FamilyId from the client as proof of authorization.
- Person name changes preserve history.
- Do not add `old_name` style one-off fields.
- Do not store global generation numbers.
- Do not store derived sibling or marital-status fields.
- All tree traversal is bounded.
- Hidden names must not influence unauthorized search results.
- Shared Person edits require shared-person policy checks.
- Database schema changes require migrations.
- Important mutations require audit events.
- Security enforcement belongs in the API.
- Do not silently change architectural decisions.
- New requirements require impact analysis before implementation.

5. Review the Phase 1 implementation checklist.

6. Produce:

`docs/phase-1-plan.md`

Break Phase 1 into small implementation milestones.

Each milestone should identify:

- backend work
- database work
- frontend work
- tests
- completion criteria

Do not generate application code yet.

At the end show:

- documents changed
- decisions resolved
- decisions still unresolved
- proposed Phase 1 milestone order

Stop after documentation and planning.