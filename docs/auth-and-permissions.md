# Authentication and permissions

## Authentication boundary

The React client uses Firebase Authentication for Google Sign-In and phone/SMS OTP. The browser sends a Firebase ID token in `Authorization: Bearer` to every protected `/api/*` request. ASP.NET Core verifies signature, issuer, audience/project ID, issued/expiry times and subject using the Firebase Admin .NET SDK, then maps UID to `users.firebase_uid`. Registration creates an app User with no family access. An invalid token yields 401; a valid token without required permission yields 403. For high-risk operations such as claim approval, creator transfer and SuperAdmin grants, check revocation/recent sign-in as appropriate. Never accept a client-supplied role, UID or FamilyId as proof of authority.

Phone OTP is handled by Firebase's web flow and anti-abuse challenge, not by an API-owned SMS code. Configure allowed SMS regions and authorized domains. Phone possession can change, so it is insufficient proof for an existing Person claim. Do not log phone numbers or OTPs unnecessarily. Account linking across Google and phone providers requires an explicit, reviewed UX to avoid duplicate application users. [Firebase phone authentication guidance](https://firebase.google.com/docs/auth/web/phone-auth) documents the flow and security limits.

## Roles and scopes

`NewMember` is an inferred onboarding state for an authenticated user without an active membership in the selected family; no stored grant is needed. `FamilyMember`, `FamilyAdmin` and `Creator` are family-scoped assignments. `SuperAdmin` is a global grant assigned through a controlled operational procedure; it is never self-service. Family membership must be active for family grants to be effective. `Creator` is a family owner grant with admin permissions plus ownership transfer and family archive. A family can have more than one Creator, but cannot lose its last active Creator while active. FamilyAdmin may manage FamilyMember grants; Creator manages FamilyAdmin and Creator grants. No one may grant themselves a higher role. Role changes are effective from database state, not stale Firebase custom claims.

| Action | NewMember | FamilyMember | FamilyAdmin | Creator | SuperAdmin |
|---|---|---|---|---|---|
| Request family membership | Own request | Own other-family request | Own other-family request | Own other-family request | Operational only |
| Read member-visible family tree/profile | No | Yes, within family and field policy | Yes | Yes | Policy-mediated, audited |
| Edit ordinary family person data | No | Proposed edit or own permitted fields | Yes, subject to shared-person rule | Yes | Exceptional audited use |
| Review requests and claims | No | No | Yes, for own family | Yes | Exceptional audited use |
| Manage family roles | No | No | May grant/revoke FamilyMember | May grant/revoke FamilyAdmin or Creator; creator transfer | Exceptional audited use |
| Archive family / transfer creator | No | No | No | Yes | Exceptional audited use |

`SuperAdmin` is a platform support role, not unrestricted routine access to PRIVATE data. Exceptional access needs purpose, short duration and audit. Creator and admin can see `FAMILY_ADMIN` fields in their family. `PRIVATE` fields require the linked person's consent or an explicit grant; unclaimed-person PRIVATE access requires a reviewed policy. Minors receive the conservative living-person default, cannot be made public in the initial policy, and cannot be automatically claimed; guardian/delegated ownership is deferred.

## Resource-level rules

Authorization checks operation, active user status, family membership, effective role, Person association, relationship publication status and field visibility. For shared Person core edits, require approval from each affected family or use a family alias; no single-family admin can silently change shared global facts. In Phase 1, reject a shared core edit until an all-family approval workflow can verify the required decisions. Every tree and path query filters traversed nodes and returned fields by the requested family; an edge never conveys read access. No global public directory of people, families or relationships exists in the MVP, and public person search is disabled. An authenticated application User may create a Family; the API creates the active creator membership, Creator and FamilyAdmin grants, and audit event in the same transaction. A profile's effective visibility is the more restrictive of its global setting and the family override; a field-specific policy can be added later without changing endpoint shape. Default living-person visibility is `FAMILY_MEMBER`; any broader exposure requires an approved policy.

Membership review may create an active membership and FamilyMember grant. Linking to Person requires separate claim evidence and approval; a current/former name, phone number or email address alone cannot approve identity; one active user/person link in either direction. Applicant cannot approve their own request or claim. Reviewer must have active FamilyAdmin/Creator scope for the target family and must be independent of the applicant; sensitive approvals are audited. Cross-family relationship publication requires an authorized decision from all affected family scopes.

## Name-history privacy and audit

Authorize current and former name rows separately after checking the Person and family association. The current primary name inherits Person visibility. Former and other nonprimary names default to `FAMILY_MEMBER` unless their own override, Person setting or family override is stricter. The effective rule is the most restrictive applicable rule. `PRIVATE` former names require the existing explicit access/consent policy. A name hidden from a caller cannot influence search results, match hints, tree nodes, accessible labels or audit responses shown to that caller. A public Person profile therefore does not make every former name public by default.

Changing a shared Person's name follows the affected-family review rule for core Person edits. Record name creation, primary switch, correction and privacy change with actor, affected name IDs, time, family context and reason. Do not copy sensitive name spellings or reviewer notes into general logs or broad audit diffs; the historical rows retain the values under tighter access control. Test that prior-name search works for authorized members and produces no information leak for nonmembers or callers lacking access to a private name.
## Media and input security

A private GCS bucket holds media. API authorizes a photo upload intent, creates an unpredictable staging object key and short-lived write grant, and records a PENDING photo. The client cannot pick arbitrary bucket paths. On finalization, the API checks object existence, actual size and MIME/file signature, decodes and re-encodes allowed image formats, strips metadata, scans if the operating policy requires it, and marks READY. Reject oversize, unsupported or malformed files; apply quotas and rate limits. The API creates short-lived read grants only after profile authorization; signed URLs are bearer capabilities and must not appear in logs or public caches. [GCS signed URL documentation](https://cloud.google.com/storage/docs/access-control/signed-urls) describes their time-limited access semantics.

Validate all DTOs, sizes, Unicode/name lengths, date consistency and relationship endpoints server-side. Use parameterized SQL/EF Core, concurrency tokens, explicit CORS policy for direct Cloud Run access, HTTPS, request/body limits, CSRF-aware design if cookies are introduced, and rate limits for search, membership requests and uploads. CORS is not authorization. Secrets use managed identity/secret storage, never repository files. Audit successful and denied sensitive operations with minimal redacted metadata; define retention and access controls for audit data.

## Security tests before release

Verify invalid/expired/wrong-project tokens; role changes during a session; a user who is admin in Family A but member in B; forged family IDs; unauthorized tree/path traversal through cross-family edges; PRIVATE fields and photos; duplicate approvals; self-approval; claim collisions; upload content spoofing; and lost-last-Creator concurrency.


