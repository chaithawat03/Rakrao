# Domain model

## Bounded concepts

| Concept | Meaning and invariant |
|---|---|
| User | Application principal keyed to one Firebase UID; may have no person link or family membership |
| Person | Human in a genealogy, with or without an account; may appear in several families |
| PersonName | One recorded current or former name for a Person; names are historical facts, not account identities |
| Family | Administrative and viewing scope, not sole owner of all connected people |
| FamilyPerson | Approved association of a person with a family; distinct from user membership |
| FamilyMembership | Account participation in a family; does not prove which person the user is |
| RoleAssignment | Global or family-scoped permission grant |
| MembershipRequest | Applicant request with identifying and relationship evidence and review history |
| UserPersonClaim | Separate request to link an account to an existing person |
| Relationship | Typed person-to-person parentage or union episode |
| RelationshipApproval | Family admin approval to publish a cross-family connection |
| PersonPhoto | Private object metadata and processing state |
| AuditEvent | Append-only record of important actions, actors, targets, time and reason |

## Name history

A Person owns an ordered set of `PersonName` records. Exactly one is the current primary name used by profiles, search results and tree cards. Name changes add a new row and mark the former primary row as former in one transaction; they do not overwrite the old spelling. Multiple former rows are allowed, including repeated use of the same spelling at different times. Date information may be unknown; do not invent effective dates. Corrections to a mistaken record are separate audited edits, not another life event.

Each name stores a required `full_display_name` as entered, with optional `first_name`, `middle_name` and `last_name`. The display value is authoritative because automatic Western-style concatenation is wrong for some languages. `name_type` is optional classification (`UNSPECIFIED`, `LEGAL`, `BIRTH`, `PREFERRED`, `ALIAS`); `FORMER` is a temporal status, not a type, so a former legal name remains identifiable as legal. A current alias may coexist with the current primary name. `is_current` and `is_primary` make status explicit when dates are unknown; `is_primary` implies `is_current`. A family-specific `display_alias` remains an intentional presentation override and is not a substitute for global name history.

The current primary name inherits Person visibility. Former and nonprimary names have their own optional visibility override, defaulting to `FAMILY_MEMBER` if no stricter Person or family rule applies. Name history and search matches are exposed only where the viewer may read that name. Shared Person name changes use the existing affected-family approval rule. Account claims must not be granted merely because a current or former name matches an applicant.
## Relationship semantics

`PARENT_CHILD` has `person_a_id = parent`, `person_b_id = child`, and kind `BIOLOGICAL`, `ADOPTIVE`, or `STEP`. An optional validity interval supports historical step relationships. Parentage is never inferred from marriage. Multiple parents are allowed; unusual combinations are flagged for review rather than enforcing a universal two-parent limit.

`UNION` has canonically ordered endpoints and kind `MARRIAGE` or `PARTNERSHIP`. Each row is an episode with optional start, end and end reason `DIVORCE`, `SEPARATION`, `ANNULMENT`, `DEATH`, or `OTHER`. Remarriage of the same pair creates a new episode. The system does not impose monogamy. Current marital or partner status derives from union history and is never stored on Person. A deceased person retains all links. End reason `DEATH` needs a reviewed operation; it is not inferred blindly from a death date.

Both types require distinct endpoints and reject an active duplicate of the same kind. Adding a biological or adoptive parent edge must reject cycles; all traversal still has cycle detection. `STEP` is an explicit social relationship. Siblings derive from shared parentage under a selected kinship policy. Cousin and in-law labels derive from paths and relationship types, with cultural naming rules deferred.

## Family context, visibility and generation

FamilyPerson is the admission list for a family's tree. A relationship can connect different families, but it appears in a family view only when endpoints are authorized for that view and cross-family publication is approved. An edge never grants automatic access to the other family's people. The default response omits unauthorized connections entirely. The MVP has no global directory of people, families or relationships; joining starts from an invitation or another controlled family entry point.

Generation is a query result keyed by `(familyId, rootPersonId, lineagePolicy, personId)`, not a Person attribute. Traverse accepted biological/adoptive parentage with parent `-1` and child `+1`. Partners occupy a visual band but do not establish lineage. Step parentage is included only when a social-lineage policy is selected. Multiple paths may yield conflicting offsets, so the view chooses a shortest interpretable path and reports ambiguity. Limit recursive queries by authorized nodes, depth, result count and visited path.

For “How is this person related to me?”, find bounded shortest interpretable paths over authorized nodes, retaining edge kind and direction. The result explains the path first; a kinship label is a separate rule layer. When multiple paths exist, show alternatives or mark the label uncertain. Never reveal an unauthorized intermediate person.

## Workflows

    Firebase sign-in -> User -> NewMember (inferred onboarding state)
      -> MembershipRequest: PENDING -> MORE_INFO_REQUESTED -> PENDING
                            -> APPROVED -> active membership and FamilyMember grant
                            -> REJECTED or WITHDRAWN

    Optional person link:
      UserPersonClaim: PENDING -> MORE_INFO_REQUESTED -> PENDING
                       -> APPROVED -> active UserPersonLink
                       -> REJECTED or WITHDRAWN

Membership approval and identity claim approval are separate decisions. A new person can be created during onboarding only through a reviewed creation operation and associated with the family. Any authenticated application User may create a Family. Creation atomically establishes the Family, an active FamilyMembership for its creator, family-scoped `Creator` and `FamilyAdmin` grants, and an audit event. Creator transfer must leave at least one active creator per active family. Each approval is transactional and stores reviewer, timestamp and audit event.

## Consistency boundaries

- Shared core Person edits require permission for each affected family context, or a reviewed multi-family change. Phase 1 rejects such edits when multiple Families are affected until an all-family approval workflow exists. A family-specific display alias can change without altering the global person.
- Cross-family relationship publication requires approval from each affected family. Pending edges are absent from ordinary tree reads.
- A matching current or former name, phone number or email address is insufficient evidence for a claim: these identifiers can be shared or reassigned. Minors may be Person records, but the initial implementation does not automatically link their records to accounts or model guardian/delegated ownership.
- Node, field, relationship and photo permissions are checked separately. A visible edge does not expose private fields.

