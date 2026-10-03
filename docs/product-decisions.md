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

Recommended future environment identifiers are `rakrao-dev`, `rakrao-staging`, and `rakrao-prod`; proposed service and database names are `rakrao-api-dev`, `rakrao-api-staging`, `rakrao-api-prod`, `rakrao-db-dev`, `rakrao-db-staging`, and `rakrao-db-prod`. The frontend package is `rakrao-web`; any future bucket name must also satisfy global uniqueness. These are naming recommendations only. Cloud project IDs, Firebase IDs, buckets, URLs, OAuth configuration and service names may have external dependencies and must be inventoried before any separate resource migration. No cloud resource was changed by this documentation update.
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
| Plan initial Cloud Run, Cloud SQL and GCS configuration in `asia-southeast1` (Singapore), close to Thailand. | The current official service lists confirm this common region and Firebase Hosting supports a Cloud Run rewrite there. | Store region in infrastructure configuration, colocate services, and verify current service availability, latency, quotas and data-residency requirements before production provisioning. No domain rule depends on the region. | Bangkok supports all required services in the chosen product editions and its latency/residency benefit is measured, or a residency requirement dictates another location. |
| Tree traversal is bounded. | Whole-family graphs can be large or cyclic. | Enforce configured depth and node limits, timeout and cycle detection; never return an entire graph by default. Select exact values after representative performance tests. | Measured scale supports a different limit or precomputed views. |
| Keep a keyboard/assistive-technology usable text relationship view. | The visual graph alone is not accessible to every user. | Tree features must have equivalent list navigation and authorized content. | A new interface proves equivalent access without that view. |
| Keep the modular monolith and selected stack. | One backend and PostgreSQL constraints fit the first MVP and preserve transactional workflows. | React, TypeScript, React Flow, ELK, ASP.NET Core, EF Core, PostgreSQL, Firebase Auth, Firebase Hosting, Cloud Run, Cloud SQL and GCS remain the baseline. | Measured requirements show a specific bottleneck that a service split or graph database solves. |

## Region evidence and provisioning gate

As checked for this Phase 0 package, [Cloud Run locations](https://cloud.google.com/run/docs/locations), [Cloud SQL for PostgreSQL locations](https://cloud.google.com/sql/docs/postgres/locations), [Cloud Storage locations](https://cloud.google.com/storage/docs/locations), and [Firebase Hosting Cloud Run rewrites](https://firebase.google.com/docs/hosting/cloud-run) list Singapore as a compatible choice. Cloud Run and Hosting also list Bangkok, while the Cloud SQL and Storage availability lists checked here do not consistently confirm it. This is a planning selection, not a provisioned resource. Recheck the relevant product edition, project quota, current availability and data-residency requirements before creating production resources.

## Still open for implementation planning

- Evidence standard and review policy for UserPersonClaims, especially disputed identities; the MVP never auto-approves.
- Exact minor classification threshold, handling of unknown/partial birth dates, and future guardian/delegated ownership. The initial conservative rule above applies meanwhile.
- Retention, export and erasure periods for living-person data, former names, evidence and audit events.
- Exact graph limits and performance target, to be set after realistic benchmarks.
- Target accessibility conformance level, languages and Thai search behavior beyond exact/prefix matching.
- Whether Thailand-only data residency is required; if so, reassess the Singapore planning region before provisioning.
