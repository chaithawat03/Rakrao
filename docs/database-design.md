# Active persistence design: Cloud Firestore

The approved Phase A architecture replaces PostgreSQL/EF Core and Cloud SQL staging with Firestore Native mode. The modular ASP.NET Core API is the only domain-data access layer. [Historical SQL design](history/postgresql-database-design.md) and excluded EF migration source preserve M1–M3 evidence; no EF migration is generated or executed for Firestore.

## Identity and document conventions

Application IDs remain UUIDs encoded as canonical strings. Firebase UID is neither User ID nor Person ID. Documents use camelCase fields, UTC Firestore timestamps, explicit nullable values, integer versions, and separate Infrastructure mapping code. The API continues to return UTC JSON timestamps, UUIDs and integer ETags. A stable UID mapping preserves application User IDs through repeat/concurrent provisioning and any separately reviewed import.

| Collection/key | Source of truth and lookup |
|---|---|
| `users/{userId}` | Application User, Firebase UID, display name, timestamps, actors, version |
| `firebase_uid_mappings/{SHA256(uid)}` | Unique verified UID → User UUID; point read |
| `families/{familyId}` | Name, description, ACTIVE/ARCHIVED status, actor/timestamps and integer metadata version |
| `family_memberships/{familyId}_{userId}` | One association for a Family/User pair; preserves membership UUID in `id`, status, timestamps and version |
| `role_assignments/{assignmentId}` | Grant history, actors/timestamps, revocation and reason |
| `active_role_keys/{familyId}_{userId}_{role}` | Unique current grant pointer/state for each scoped role; point read |
| `family_guards/{familyId}` | Effective active Creator count and independent guard version |
| `family_creation_limits/{userId}` | Transactionally updated successful creation timestamps in a rolling hour |
| `family_creation_requests/{SHA256(userId + "_" + keyHash)}` | Immutable original request hash and resulting Family ID |
| `audit_events/{eventId}` | Create-only application audit; actor/action/target/family/request IDs, UTC time and redacted diff |

SHA256 hashes use uppercase hexadecimal. Membership/role key components are canonical UUIDs and fixed allowed role strings, so delimiters are unambiguous. Idempotency retains the baseline SHA256 key hash and HMAC-SHA256 request hash over canonical JSON `{ name: trimmedName, description: trimmedDescriptionOrNull }`, using the raw idempotency key only transiently as the HMAC key. No raw retry key or family details are copied into audit diffs.

The M1–M3 implementation has no Person, invitation or membership-review collections. Global SuperAdmin grants are not a new feature of this migration. Future phases must design their scopes explicitly without using Firebase custom claims as the authorization store.

## Transaction and unit of work

`IFirestoreUnitOfWork` owns retryable SDK transactions. Each attempt gets fresh reads and a fresh buffered write set; flushing occurs only after all required reads. Allocate business/grant/audit IDs once outside callbacks. Transaction callbacks perform no external side effects. Use document-create preconditions for unique records and audit, preserving atomic business/audit commits and preventing event duplication on retries.

Provisioning reads the deterministic UID mapping and atomically creates mapping, User and one provisioning audit. Concurrent requests return the winning User. Family creation reads the UID mapping, prior retry record and rolling-limit state before creating Family, membership, Creator/Admin grant history and active keys, guard, limit state, optional request record and audit in one transaction. Retry lookup precedes the throttle, and returns the currently authorized original Family even after metadata edits.

Rolling-hour throttling serializes on the per-User limit document across API instances. Preserve the baseline inclusive cutoff (`createdAt >= operationTime - 1 hour`); do not use fixed wall-clock buckets. Default maximum is three creations; retry records preserve durable idempotency independently of the pruned timestamp list.

Family reads and mutations check active Family, verified UID mapping, active membership and current same-Family grants. Mutation authorization is re-read on every retry. Metadata edits compare the caller's original integer version inside the transaction; SDK retries do not turn a stale edit into success. The metadata version increments only for metadata changes.

Supported role mutations read/write the authoritative Family guard. Creation starts with one effective Creator; Creator grant/revocation adjusts its count transactionally. Inactive memberships cannot authorize actions or provide authority. Concurrent final-Creator removals serialize and cannot both commit. The guard has its own version, independent of metadata `If-Match`. Any future supported membership-state or archive mutation must join this same invariant boundary; this migration adds no such workflow.

## Accepted enforcement boundaries

Firestore has no SQL foreign keys, CHECK/unique indexes, deferred triggers or recursive CTEs. Supported application transactions enforce references, uniqueness, validation and last-Creator safety. Restrict runtime IAM and administrative writes; do not offer generic document editing endpoints.

Audit documents are created atomically with important business writes, and the application exposes no audit update/delete operation. Browser Security Rules deny reads/writes, including authenticated browsers. **Server SDKs use IAM and bypass Security Rules.** A privileged administrator may alter audits, grants, memberships or guards. The previous PostgreSQL UPDATE/DELETE/TRUNCATE and deferred Creator trigger guarantees are not reproduced at the database layer. Stronger tamper resistance needs a separate approved storage/IAM design.

## Query-driven indexes and readiness

`firestore.indexes.json` defines only the two compound membership lookup paths used by M3:

- `family_memberships`: `userId`, `status` for the caller's active associations.
- `family_memberships`: `familyId`, `id` for a specific role-target membership UUID, limited to one result.

UID mapping, active roles, throttling, retry lookup and audit creation use point reads/writes, not collection scans or broad audit queries. Exempt unqueried audit `safeDiff`, rolling-state `createdAt`, and `users.firebaseUid` from single-field indexing. Future query endpoints must add justified indexes before shipping. Emulator tests do not establish production index readiness; Phase B must deploy and verify real indexes.

Readiness is an on-demand point read of `readiness/api`; nonexistence is successful database access. Bound each probe to three seconds and cache its result for thirty seconds. No background polling, writes or database scans are required.

## Provisioning and import boundary

Phase A creates no cloud database. Phase B must inventory the existing Firebase project/database, choose Native mode and location, configure IAM, deploy deny-browser rules and indexes, and verify live access. Plan `asia-southeast1` beside Cloud Run after region/residency review. Prefer one default database per project and isolated staging/production projects.

Do not assume or recreate a Windows database. If authoritative data exists, a separately reviewed import must preserve all IDs, timestamps, versions and audit history, validate UID uniqueness/references and Creator counts, reconstruct active keys/guards and retry state, and reconcile rolling throttling from recent creation audit events. Inventory malformed or conflicting data rather than inventing permissions. Preserve a recoverable export and freeze source writes for cutover; rolling back code after new Firestore writes is not a data rollback.

## Later Phase 1 concepts

The domain model still requires Person/User separation, durable PersonName history, FamilyPerson distinct from membership, typed relationship episodes, private search, cross-family publication approval, and bounded traversal with cycle detection. Their Firestore mappings and bounded query costs must be designed in the relevant later milestone. Do not scaffold those features in the M1–M3 migration or renumber M1–M9.
