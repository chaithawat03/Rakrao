# UI design

## Product identity in the interface

Use `รากเรา` as the Thai user-facing product name and `RAKRAO` where an English/romanized brand is needed. The working tagline is “Discover how we are connected.” Treat it as editable product copy, not a fixed architectural string. Domain component names such as FamilySwitcher, FamilyTree and PersonProfile remain descriptive and do not take a brand prefix.
## Navigation and key screens

Authenticated shell: family switcher, search, tree, person profile, membership status and account menu. There is no global people, family or relationship directory and no public person search. An account can switch among authorized families; the current family is explicit in the URL and heading. Any authenticated application User can create a Family from the account shell; creation immediately opens that Family with Creator and FamilyAdmin capabilities. NewMember onboarding opens an existing family through an invitation link, then guides the applicant through a membership request with identifying and relationship information. The request status screen shows pending, more information requested, approved or rejected, with a clear next action. Family admins have a review queue and can approve, reject or ask for more information with an audit reason. A separate claim flow links the account to an existing Person after review.

A person card shows permitted photo, current primary display name (or explicit family alias), partial birth information, deceased status and basic relationship summary. The profile page labels the current primary name and lists visible previous names, then adds family context, partner episodes, parentage and edit controls where permitted. Unknown facts say “Unknown” rather than displaying empty or invented values. Privacy filtering occurs in the API; the UI also avoids offering controls the caller cannot use, using capabilities returned by the API. Minor records follow conservative visibility and show no automatic account-claim action.

## Desktop/notebook tree

Show a larger bounded tree canvas with React Flow pan, zoom, fit-to-view, person selection and keyboard access. A persistent search/focus control finds an authorized person, and generation filters request a new bounded subgraph. ELK lays out the returned nodes, with parent-child direction visually distinct from union connections. A selected card opens a details panel; expanding branches requests more authorized nodes. Preserve focus and camera position where possible when relayout happens. Use visual cues for biological, adoptive and step parentage, partnerships, ended unions and deceased people; provide text labels and legend, not color alone.

## Smartphone tree

Default to a focus-person view: selected person, parents, optional grandparents, siblings, partners and children. Fetch only a small neighborhood and offer explicit “show another generation” or branch expansion. Large touch targets, fixed focus/search action and bottom sheet for details keep the graph usable. Pinch zoom and pan are supported, but every key action also has a tap-based alternative. Switching family retains or resets focus explicitly; never silently show a person from another family.

## Name history presentation and editing

Affected frontend components: PersonCard, PersonProfile, NameHistoryList, PersonNameEditor, FamilyPersonSearch, TreePersonNode and the accessible relationship list. These are proposed component responsibilities, not a required file structure.

The profile leads with “Current name” and shows “Formerly known as” entries in a separate list. Show name type and effective dates only when known and useful; unknown dates are not implied. Preserve Thai and other entered display order from `fullDisplayName`, rather than recomposing first/middle/last. A family alias, when used, is visibly labeled as an alias so the global current name remains understandable. Ordinary viewers never see reviewer notes or hidden names.

Person create asks for one current primary full display name; optional components can help later editing/search. A permitted editor can choose “Change current name” (adds a new primary, preserves the former row) or “Add another/previous name” (does not replace the primary). A correction action is separate and requires a reason. The UI shows concurrency conflicts and reloads the latest history before retrying. Shared-person changes show that core edits are unavailable in Phase 1 until affected-family approval exists; local display-alias edits remain possible under that Family's policy.

Family-scoped search matches authorized current names, former names and family aliases. Results always lead with the current permitted display name. When the match was on a visible former name, a small “Formerly: …” explanation helps users recognize the result; hidden names never affect results or hints. Search by two spellings of the same Person returns the same profile, without duplicate cards. Test representative Thai names, spacing and repeated name changes.

Family-tree nodes show the current primary name by default. A compact former-name hint is optional only when the viewer may read it and node density allows it; omit it on small mobile nodes. The detail panel/profile carries the full list. Text alternatives and screen-reader labels use the same permitted primary name and must not announce hidden historical names.
## Accessibility and responsive behavior

Offer an ordered textual relationship list alongside the canvas because a graph alone is hard to use with screen readers and keyboards. Cards have accessible names that include relationship type and vital status. Ensure visible focus, sufficient contrast, semantic form errors, live review-state announcements and reduced-motion support. Layout must work at narrow phone widths and notebook screens; test portrait/landscape, high zoom and long names. Use responsive node/card detail density rather than trying to shrink a whole tree onto a phone.

## States and feedback

Provide loading skeletons for tree/profile, empty family and no-results states, explicit truncation when a graph cap is reached, recoverable layout error with list view, permission-denied state without leaking hidden names, and optimistic edit conflict resolution. Admin review shows evidence only to authorized reviewers and makes the effect of approving membership versus approving an identity claim explicit. Upload UI shows staged, processing, rejected and ready states. Do not put raw technical implementation details in end-user copy.

## UI decision record

| Selection | Why | Alternatives | Tradeoff |
|---|---|---|---|
| Focus-first mobile tree | Relationships near one person remain legible | Miniaturized full tree | More navigation to distant people |
| API-provided subgraph, client layout | Authorization remains central while interaction stays fluid | Server image/layout | Client CPU use needs caps |
| Person profile separate from card | Keeps tree readable | All details in nodes | Extra navigation |
| Text list paired with visual graph | Accessible and useful when layout fails | Graph-only view | Two presentations to maintain |
| Separate membership and claim flows | Joining a family and proving person identity are different | Single combined approval | More steps for applicants and admins |



