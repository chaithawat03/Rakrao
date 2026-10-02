# API design

## Contract rules

REST over HTTPS at `/api/v1`. Firebase Hosting routes `/api/**` to Cloud Run. JSON uses camelCase, UTC ISO 8601 timestamps, UUID identifiers and explicit status strings. All protected endpoints require a verified Firebase ID token. API docs describe permissions and visibility, not just DTO shape. Use RFC 7807-style problem responses with stable `code`, `title`, `detail`, `requestId`, and per-field validation errors where applicable. Do not disclose whether hidden people or families exist in 403/404 detail. Use 401 for unauthenticated, 403 for visible but forbidden, 404 for absent or concealed resources, 409 for stale version/conflict, 422 for valid but impossible relationship, 429 for rate limits.

List endpoints use cursor pagination with bounded `limit` and `nextCursor`; search is always family-scoped and permission-filtered. Mutations return `ETag`/version and accept `If-Match` for edits and review actions. Use idempotency keys for creation and approval actions that clients may retry. Server timestamps and actor IDs are authoritative. Never trust request body `userId`, `role`, or `familyId` without binding them to the verified principal and resource policy.

## Endpoint groups

The table describes the target API across phases. [Phase 1](phase-1-plan.md) implements the secure vertical slice; claim approval, cross-family publication and photos follow later. During Phase 1, shared core Person edits are rejected until every affected-family approval can be verified, while family-local display-alias edits remain scoped to that Family.

| Method and route | Purpose | Principal permission |
|---|---|---|
| `GET /api/v1/me` | App user, effective family summaries and onboarding state | Authenticated |
| `POST /api/v1/me` | Idempotently provision app User from verified UID | Authenticated |
| `GET /api/v1/families` | Joined families only; no open directory or public person search | Authenticated; scoped results |
| `GET /api/v1/family-invitations/{token}` | Resolve limited family metadata for joining | Authenticated with valid unexpired token |
| `POST /api/v1/families` | Create family, active creator membership, Creator/Admin grants and audit event transactionally | Any authenticated application User, throttled |
| `POST /api/v1/families/{familyId}/invitations` | Create bounded, revocable invitation link | FamilyAdmin/Creator |
| `GET /api/v1/families/{familyId}` | Family metadata and caller capabilities | Active member/admin; invitation preview uses its own endpoint |
| `PATCH /api/v1/families/{familyId}` | Edit family metadata | Creator/Admin by field |
| `POST /api/v1/families/{familyId}/persons/{personId}/associations` | Associate an existing Person after affected-family review | FamilyAdmin/Creator and shared-person policy |
| `GET /api/v1/families/{familyId}/members` | Paginated memberships | FamilyAdmin/Creator |
| `POST /api/v1/families/{familyId}/membership-requests` | Submit own request and evidence with invitation token | Authenticated, not already active |
| `PATCH /api/v1/families/{familyId}/members/{memberId}/roles` | Grant/revoke scoped roles with last-Creator protection | Creator or limited FamilyAdmin policy |
| `GET /api/v1/families/{familyId}/membership-requests` | Review queue | FamilyAdmin/Creator |
| `PATCH /api/v1/families/{familyId}/membership-requests/{requestId}` | Approve, reject or request more info | FamilyAdmin/Creator, not applicant |
| `POST /api/v1/families/{familyId}/membership-requests/{requestId}/responses` | Supply requested information on own request | Applicant |
| `GET /api/v1/families/{familyId}/persons?cursor=&q=` | Search authorized current, former and family-alias names; return each Person once | FamilyMember or higher |
| `POST /api/v1/families/{familyId}/persons` | Create Person, first current primary name and family association atomically | FamilyAdmin/Creator initially |
| `GET /api/v1/families/{familyId}/persons/{personId}` | Filtered profile with current and visible former names, relationship summary and photo grant | Resource-level read |
| `PATCH /api/v1/families/{familyId}/persons/{personId}` | Edit non-name core fields or family alias | Resource-level edit and shared-person policy |
| `POST /api/v1/families/{familyId}/persons/{personId}/names` | Add a historical/alias name or atomically change current primary name | Resource-level edit and shared-person policy |
| `PATCH /api/v1/families/{familyId}/persons/{personId}/names/{nameId}` | Audited correction or visibility edit; no silent history deletion | Resource-level edit and shared-person policy |
| `POST /api/v1/families/{familyId}/persons/{personId}/claims` | Submit separate reviewed identity claim; never auto-approve from matching identifiers | Authenticated, with claim evidence; no automatic minor claims |
| `GET /api/v1/families/{familyId}/claims` | Review claims | FamilyAdmin/Creator |
| `PATCH /api/v1/families/{familyId}/claims/{claimId}` | Approve, reject or request more info | FamilyAdmin/Creator, not claimant |
| `POST /api/v1/families/{familyId}/claims/{claimId}/responses` | Supply requested claim evidence | Claimant |
| `GET /api/v1/families/{familyId}/tree?focusPersonId=&ancestors=&descendants=&includeSocial=` | Bounded, filtered tree subgraph | Resource-level read |
| `POST /api/v1/families/{familyId}/relationships` | Create parentage/union episode, possibly pending approvals | Resource-level edit |
| `PATCH /api/v1/families/{familyId}/relationships/{relationshipId}` | End or correct an episode with history | Resource-level edit |
| `PATCH /api/v1/families/{familyId}/relationships/{relationshipId}/approvals` | Approve/reject cross-family publication | FamilyAdmin/Creator of affected family |
| `POST /api/v1/families/{familyId}/persons/{personId}/photos/uploads` | Create private upload intent | Resource-level edit |
| `POST /api/v1/families/{familyId}/persons/{personId}/photos/{photoId}/finalize` | Validate and publish staged photo | Uploader plus current edit permission |
| `GET /api/v1/families/{familyId}/persons/{personId}/photos/current` | Authorized short-lived read grant | Resource-level read |
| `GET /api/v1/families/{familyId}/audit-events` | Paginated family audit | FamilyAdmin/Creator, redacted |

Future route: `GET /api/v1/families/{familyId}/relationships/path?fromPersonId=&toPersonId=&policy=` returns authorized path steps, edge directions, confidence/ambiguity and optional kinship label. It is deliberately outside Phase 1, but uses the same typed relationships and visibility rules.

## Name DTOs and search contract

Person creation requires `primaryName: { fullDisplayName, firstName?, middleName?, lastName?, nameType?, effectiveFrom? }`. `POST .../names` accepts the same name fields plus `makePrimary`, optional `effectiveTo` for a historical entry, optional `visibilityOverride`, and reviewer-only `notes`. When `makePrimary = true`, the service closes the previous current primary and creates the new row in one transaction. It increments the Person aggregate version and checks `If-Match`; a stale concurrent change returns 409. Historical corrections use the name-row ID and are audited separately. The API never mutates a former row into a new life-event record.

The profile DTO retains `displayName` for existing clients and adds `currentName` and `formerNames[]` with name ID, full display name, optional components, type, known effective dates and visibility-filtered status. `displayName` normally equals the current primary `fullDisplayName`; an explicit family alias may override it in that family's tree, while the profile still labels the current primary name. Only authorized former names appear, sorted by known effective date and then recorded order. Reviewer notes are absent from ordinary DTOs. Tree nodes keep one `displayName` and may include an optional, authorized compact former-name hint; clients must not require it.

`GET .../persons?q=` searches all visible current and former names and the visible family alias within the selected family. It returns one result per Person with the current permitted `displayName`, `matchedOn: CURRENT_NAME | FORMER_NAME | FAMILY_ALIAS`, and an optional `matchedName` only when the matching name is visible. A hidden former name cannot cause a result or reveal a match reason to an unauthorized caller. Search ranking favors current primary matches; pagination and caps still apply. Both current and former spellings must resolve to the same Person ID in authorized searches. The API does not search globally or use a name match as proof for a UserPersonClaim.
## Example tree response

    {
      "familyId": "uuid",
      "focusPersonId": "uuid",
      "lineagePolicy": "BIOLOGICAL_AND_ADOPTIVE",
      "nodes": [{ "personId": "uuid", "displayName": "Example", "generationOffset": 0,
                  "vitalStatus": "UNKNOWN", "photoUrl": null, "visibleFields": ["displayName"] }],
      "edges": [{ "relationshipId": "uuid", "sourcePersonId": "uuid",
                  "targetPersonId": "uuid", "type": "PARENT_CHILD", "kind": "ADOPTIVE" }],
      "truncated": false,
      "ambiguousGenerationPersonIds": []
    }

The server sets configured maximum ancestors, descendants, node count and execution time even if the client asks for more. Select exact values after representative performance tests, not as domain constants. The API never returns hidden IDs as placeholders by default. `photoUrl` is a short-lived authorized URL or an API media URL resolved after authorization; it must not be cached publicly. Node placement is a frontend concern, so the API does not return screen coordinates.

## Review and mutation behavior

Family creation creates active creator membership, Creator and FamilyAdmin grants and an audit event in one transaction. Membership approval creates membership and FamilyMember grant in one transaction and records an audit event. Claim approval creates a unique active UserPersonLink but is independent of membership approval. Cross-family relationship creation makes a PENDING edge and approval rows for affected families; only after every required approval does it become ACTIVE and visible. Photo finalization checks the actual object, not just client-declared metadata. Optimistic concurrency and idempotency keep retrying clients from double-applying a decision.



