# Architecture

## Purpose

RAKRAO (รากเรา) is a family relationship platform where people can appear in trees without accounts, accounts can later claim people, and families can stay separate or connect. This reviewed design baseline guides Phase 1 implementation. The [name-history addendum](../Foundation_02102026_0940.md) extends the original brief.

## Selected architecture

Browser: React + TypeScript + React Flow + ELK → Firebase Hosting → /api/** rewrite → Cloud Run: ASP.NET Core REST API → Cloud Firestore Native mode. Private media storage remains a separately approved later feature. Firebase Authentication supplies Google or phone OTP sign-in; the browser sends a Firebase ID token with API requests.

The API owns permission decisions and database mutations. The frontend renders authorized DTOs, not raw rows. A tree request specifies family, focus person, depth and filters. The API checks access, builds a bounded subgraph, applies visibility rules, then returns nodes and typed edges; client-side ELK lays out that subgraph. Person, family, membership, claim, relationship, media and audit modules live in one backend process with explicit boundaries. Family creation atomically creates an active membership and Creator/FamilyAdmin grants for its creator.

Put the Firebase Hosting `/api/**` rewrite before the SPA catchall `** -> /index.html`. Use relative `/api` URLs and keep the prefix consistently at the API. Use `asia-southeast1` as the planning region for Cloud Run and Firestore, stored in infrastructure configuration; recheck service availability, latency and residency before provisioning. Treat direct Cloud Run requests as untrusted. Use request-based billing, minimum instances zero, bounded concurrency/scaling and bounded Firestore query costs.

## Major decisions

| Selection | Why | Alternatives | Tradeoff |
|---|---|---|---|
| Modular ASP.NET Core monolith | One deployable and transactions across workflows | Microservices; one function per feature | Modules require discipline; shared scaling initially |
| Firestore Native document persistence | Removes always-on database cost and supports atomic M1–M3 workflows | Historical PostgreSQL design | API transactions replace SQL constraints; privileged administrators bypass application invariants; later graph/search queries need explicit bounded document designs |
| Global person plus family associations | One identity can appear in several families | Family-local duplicates with merging | Cross-family changes need approval and visibility policy |
| Separate PersonName history | Repeated changes and former-name search need multiple records | Mutable `persons.full_name`; fixed old-name columns | Adds join, search and privacy filtering |
| Typed relationship rows with history | Parentage and repeated unions stay queryable | Father/mother columns; generic unlabeled edges | More constraints and traversal logic |
| Firebase Auth plus database-backed grants | Provider handles Google/SMS; scoped roles can change immediately | Custom auth; Firebase custom claims for all roles | Protected requests must check app data |
| React Flow with client-side ELK | Interactive bounded views and configurable layout | Image rendering; whole-graph layout | Layout performance and accessibility need attention |
| Private GCS media | Photos inherit person visibility | Public object URLs; database blobs | Signed access and processing add work |

## Name-history change record

The initial design assumed one mutable `persons.full_name`. The addendum supersedes that assumption: `person_names` becomes the source of truth, with exactly one current primary row and any number of former names. Person IDs, family associations and account claims remain unchanged. API `displayName` stays available as a derived compatibility field; writes and searches use name records. If an earlier schema exists, backfill before removing `persons.full_name`. Historical names receive separate visibility checks, and searching a hidden name must not reveal a Person. See [domain model](domain-model.md), [database design](database-design.md), [API design](api-design.md), and [UI design](ui-design.md).
## Proposed repository structure

    /
      RakRao.sln                    planned .NET solution
      docs/                         architecture and product decisions
      apps/web/                      React/TypeScript app; package `rakrao-web`
      src/RakRao.Api/        HTTP endpoints, composition, auth
      src/RakRao.Domain/     rules and typed identifiers
      src/RakRao.Application/ commands, queries, policies, DTOs
      src/RakRao.Infrastructure/ Firestore persistence and Firebase adapters
      tests/RakRao.UnitTests/
      tests/RakRao.IntegrationTests/
      tests/web/                     component and browser tests
      infra/                         deployment configuration and runbooks
      .github/workflows/             CI and controlled deployment

This is a proposal, not a scaffold. .NET namespaces use `RakRao.Api`, `RakRao.Domain`, `RakRao.Application`, `RakRao.Infrastructure` and corresponding child namespaces. API and Infrastructure depend on Application; Application depends on Domain; Domain has no cloud dependency. Use feature folders within projects.

## Data flow and operations

1. Browser signs in through Firebase and sends an ID token in `Authorization: Bearer` to `/api/*`.
2. API verifies the token, maps Firebase UID to an app user, and loads current memberships and grants. Supplied family IDs are selectors, never authorization proof.
3. API authorizes the operation; reads filter each person, field and edge. Mutations write business rows and audit events in one transaction.
4. Authorized media uploads use a short-lived grant to a private staging object. The API validates and processes it before it becomes a current photo. Reads recheck profile visibility.

Use versioned Firestore document mappings/indexes, retry-safe transactions, structured logs and request IDs. Later graph work requires bounded application traversal; SQL recursive CTEs are historical guidance. Apply timeouts, pagination, depth and node caps. Backups and restore drills, retention, export/deletion policy, monitoring and abuse controls are preproduction gates.

## Phase 0 decisions and remaining constraints

[Product decisions](product-decisions.md) is the accepted MVP decision record. The earlier assumptions about family creation eligibility, visibility, minors and region are resolved as follows:

| Topic | Confirmed MVP rule |
|---|---|
| Visibility | Private-first; no global public directory of people, families or relationships; no public person search; living people default to `FAMILY_MEMBER` |
| Family creation | Any authenticated application User may create a Family; creation atomically creates active creator membership, Creator and FamilyAdmin grants and an audit event; never remove the last active Creator |
| Joining and identity | Invitation/controlled entry, reviewed membership request, then membership; Person claiming stays a separate reviewed workflow |
| Shared Person | Core facts are shared; every affected Family's policy applies to edits; a family display alias is local only |
| Cross-family relationship | Every affected Family approves publication; an edge never grants read permission |
| Minors | Records allowed with conservative visibility; no automatic claiming or Phase 1 guardian ownership model |
| Region | Configure `asia-southeast1` for planning; revalidate before provisioning and keep it out of domain logic |
| Scale and accessibility | Configurable traversal depth/node/time limits and cycle detection; maintain a keyboard-accessible text relationship view |

Open policy and measurement questions remain in [product-decisions.md](product-decisions.md), including claim evidence, retention, exact traversal limits, accessibility conformance and any Thailand-only residency requirement. These do not change the approved private-first architecture.
## Platform references

- [Firebase Hosting rewrites](https://firebase.google.com/docs/hosting/full-config)
- [Firebase ID token verification](https://firebase.google.com/docs/auth/admin/verify-id-tokens) and [.NET Admin SDK](https://firebase.google.com/docs/reference/admin/dotnet/class/firebase-admin/auth/abstract-firebase-auth)
- [Firestore transactions](https://firebase.google.com/docs/firestore/manage-data/transactions) and [Firestore IAM](https://firebase.google.com/docs/firestore/security/iam)
- [React Flow layout guidance](https://reactflow.dev/learn/layouting/layouting)




## Approved Phase A architecture change

The reviewed migration supersedes PostgreSQL/EF Core and Cloud SQL staging while preserving the modular monolith, domain privacy rules and public M1–M3 API. Browser Security Rules deny all domain access; server SDKs use IAM and bypass those rules. Infrastructure owns deterministic UID/membership/grant pointers and explicit buffered transactions. Supported Creator changes serialize on a separate Family guard; metadata retains integer ETags. Audit is create-only through application architecture and atomic with important mutations. Privileged administrators can bypass both guarantees; this is explicitly accepted and is not database-trigger parity.

PR CI uses Auth/Firestore emulators with no deployment identity or PostgreSQL. Delivery is Codex Cloud → GitHub → GitHub Actions → WIF → existing Artifact Registry/Cloud Run/Firestore/Hosting resources. Staging deployment is manually gated and not enabled/provisioned in Phase A. See [staging foundation](staging-foundation.md). M4 remains unstarted.
