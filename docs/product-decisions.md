# Phase 0 product decisions

Status: approved MVP direction from [Finalize Phase 0 prompt](<../Codex Prompt — Finalize Phase 0.md>). These decisions supersede earlier working assumptions in the architecture package. Phase 1 implementation now follows this record.

## Product identity and naming

| Use | Official value |
|---|---|
| User-facing Thai product name | รากเรา |
| Romanized brand | RAKRAO |
| English meaning | Our Roots |
| Technical identifier and .NET solution/project/namespace prefix | `RakRao` |
| Lowercase repository, package and URL slug | `rakrao` |
| Frontend package name when lowercase ASCII is required | `rakrao-web` |
| Working tagline | Discover how we are connected. |

The tagline is editorial copy, not an architectural requirement; do not hard-code it throughout the application. Planned .NET names are `RakRao.sln`, `RakRao.Api`, `RakRao.Domain`, `RakRao.Application`, `RakRao.Infrastructure`, and corresponding test projects. Domain terms such as Family, Person, FamilyMembership and their API/database identifiers stay unchanged.

Recommended future environment identifiers are `rakrao-dev`, `rakrao-staging`, and `rakrao-prod`; proposed API service names are `rakrao-api-dev`, `rakrao-api-staging`, and `rakrao-api-prod`. Firestore uses the explicitly selected database ID, initially `(default)`; the old `rakrao-db-*` SQL identifiers are historical. The frontend package is `rakrao-web`; any future bucket name must also satisfy global uniqueness. These are naming recommendations only. Cloud project IDs, Firebase IDs, buckets, URLs, OAuth configuration and service names may have external dependencies and must be inventoried before any separate resource migration. No cloud resource was changed by this documentation update.
## Decisions

| Decision | Rationale | Consequences for the first MVP | Reconsider when |
|---|---|---|---|
| Private-first visibility: no global public directory for people, families or relationships; no public person search. Living-person data defaults to `FAMILY_MEMBER`. | Family records contain sensitive information and connections can reveal others. | Family discovery uses an invitation or another controlled family entry point; all search is authorized and family-scoped. Public exposure needs an explicit approved policy. | A reviewed public-sharing policy includes consent, minors, indexing and revocation. |
| Any authenticated application User may create a Family. | Allows family groups to start without a platform operator. | One transaction creates Family, active creator membership, family-scoped Creator and FamilyAdmin grants, and an audit event. Protect the last active Creator. Throttle creation. | Abuse, billing or governance data justifies an eligibility gate. |
| Joining requires a MembershipRequest and FamilyAdmin/Creator review. | An invitation is an entry point, not proof of family membership. | Sign-in creates a User only; approval creates an active FamilyMembership and FamilyMember grant transactionally. | A separately reviewed invitation policy permits lower-friction admission. |
| User and Person stay separate; claiming a Person is a separate reviewed workflow. | A record can predate an account, and matching identifiers do not prove identity. | A current/former name, phone number or email match never auto-approves a claim. Membership approval does not link a Person. | A verified identity-proof process is designed and approved. |
| Keep `PersonName` history with one current primary name and multiple former/other names. | Repeated changes must remain visible without fixed old-name fields. | Name changes append history; visible former names remain searchable and resolve to the same Person. Hidden names do not influence unauthorized search. | Evidence demands more detailed legal-name or cultural naming rules. |
| A Person may belong to several Families; core facts are shared. | One human should not be copied into conflicting family identities. | Shared core edits, including names, must satisfy every affected Family's authorization policy. Phase 1 blocks shared core edits until an all-family approval workflow exists; `FamilyPerson.displayAlias` changes only local presentation. | A product-approved ownership or conflict-resolution model changes. |
| Cross-family relationships require publication approval from each affected Family. | An edge can expose another family and its members. | Pending edges are absent from ordinary tree queries; no relationship grants read access by itself. | Explicit consent and visibility rules justify a different publication policy. |
| Minors may be Person records but receive conservative visibility. | Children need representation without weak account-ownership assumptions. | No public exposure for minors in the initial policy; no automatic minor claim; no guardian/delegated ownership model in Phase 1. Apply the safer rule when age is uncertain. | The guardian/consent and minor-age policy is defined for target jurisdictions. |
| Plan initial Cloud Run and Firestore configuration in `asia-southeast1` (Singapore), close to Thailand. | Colocating the API and database reduces cross-region latency; verify current Firestore Native and Hosting/Cloud Run compatibility before provisioning. | Store region in infrastructure configuration, colocate services, and verify current service availability, latency, quotas and data-residency requirements before production provisioning. No domain rule depends on the region. | Bangkok supports all required services in the chosen product editions and its latency/residency benefit is measured, or a residency requirement dictates another location. |
| Tree traversal is bounded. | Whole-family graphs can be large or cyclic. | Enforce configured depth and node limits, timeout and cycle detection; never return an entire graph by default. Select exact values after representative performance tests. | Measured scale supports a different limit or precomputed views. |
| Keep a keyboard/assistive-technology usable text relationship view. | The visual graph alone is not accessible to every user. | Tree features must have equivalent list navigation and authorized content. | A new interface proves equivalent access without that view. |
| Keep the modular monolith and selected stack. | One backend preserves authorization boundaries; Firestore transactions support M1–M3 while removing the always-on database cost. | React, TypeScript, React Flow, ELK, ASP.NET Core, Firestore, Firebase Auth, Firebase Hosting and Cloud Run are the active baseline. EF Core/PostgreSQL/Cloud SQL staging are superseded; media provisioning remains later scope. | Measured requirements show a specific bottleneck that a service split or graph database solves. |

## Approved Phase A decision and provisioning gate

The user-approved migration keeps Firebase Hosting and Authentication, runs the modular ASP.NET Core API on Cloud Run, and makes Firestore Native the active M1–M3 persistence. PostgreSQL/EF Core and Cloud SQL staging are superseded; Neon and local Docker/PostgreSQL are not acceptance dependencies. Near-zero fixed monthly infrastructure cost is the objective; usage charges and a billing-enabled project still apply.

Accept the changed enforcement boundary explicitly: all supported effective Creator changes and atomic business/audit writes use Firestore transactions. The application permits audit creation only. Privileged Google Cloud administrators may bypass these invariants, and Firestore Security Rules do not apply to server SDKs. This is not parity with SQL deferred/append-only triggers. Browser domain access remains denied, and ordinary users must use the verified API.

Plan `asia-southeast1` for Cloud Run/Firestore after checking current edition availability, quota, existing database location and residency. The older Cloud SQL regional assessment no longer dictates this design. Phase A provisions no resources; Phase B requires separate bootstrap authorization and live verification. Do not rename existing Firebase project/app/OAuth resources based on proposed names alone.

CI validates against Auth/Firestore emulators. Codex Cloud → GitHub → GitHub Actions → Google Cloud/Firebase deployment uses GitHub OIDC Workload Identity Federation and dedicated runtime ADC, without service-account JSON keys. The prepared staging workflow is manual, protected and disabled until explicit nonsecret bootstrap configuration exists. M4 remains private invitations and reviewed membership, not part of the migration.

## Still open for implementation planning

- Evidence standard and review policy for UserPersonClaims, especially disputed identities; the MVP never auto-approves.
- Exact minor classification threshold, handling of unknown/partial birth dates, and future guardian/delegated ownership. The initial conservative rule above applies meanwhile.
- Retention, export and erasure periods for living-person data, former names, evidence and audit events.
- Exact graph limits and performance target, to be set after realistic benchmarks.
- Target accessibility conformance level, languages and Thai search behavior beyond exact/prefix matching.
- Whether Thailand-only data residency is required; if so, reassess the Singapore planning region before provisioning.
