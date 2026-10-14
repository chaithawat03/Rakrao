# UI design

## Implemented M1–M3 foundation

The current frontend separates the public authentication experience from the authenticated application structure. Signed-out users see the `รากเรา` / `RAKRAO` identity, the product tagline, a privacy-oriented introduction, and the existing Google and Phone authentication flows. Signed-in users enter a compact application shell where the authorized Family is the primary context. The public hero is not repeated inside the application.

The authenticated shell contains a skip link, banner, primary navigation, Family context, account identity, and main landmark. `Overview` is the only navigation item and is marked as the current page. One authorized Family appears as static context; multiple authorized Families use a labelled native select; zero Families omit the switcher. Sign out remains directly reachable. People, Tree, Invitations, search, and review controls are future functionality and do not render in the M1–M3 foundation.

The active Family name is the page `h1`. The overview displays only the API-provided description, or neutral fallback copy when no description exists. Its Family archive section explains where future people and relationships will appear without showing invented counts, activity, or interactive future features. `Manage family` appears only when the API supplies `EDIT_FAMILY`. Additional Family creation uses a secondary native disclosure; accounts with no Families receive an open first-Family form as the primary task. Create and edit failures remain sanitized and appear beside the relevant form.

`AuthHome` remains the authentication, state, URL, and API coordinator. Presentation is divided among `BrandMark`, `SignedOutHome`, `AppShell`, `FamilySwitcher`, `AccountMenu`, `FamilyOverview`, `FamilyEditor`, and `CreateFamilyForm`. These components receive data and callbacks and do not call Firebase or the API. This preserves authentication restoration, session generation guards, `/api/v1/me`, authorized Family selection, the `family` URL query, creation idempotency, edit version handling, stale-edit guidance, and capability checks.

## Visual system and responsive composition

The implemented CSS uses custom properties for a warm cream canvas, warm-white and quiet surfaces, deep green interaction color, earth accents, semantic feedback, spacing, radii, shallow shadow, focus, and content widths. The local font stack starts with Thai-capable faces and loads no remote font. Controls are at least 44 CSS pixels high, long Thai and Latin names wrap, and the layouts avoid horizontal overflow.

- At `390×844`, the public introduction stays concise above authentication. The application header uses compact rows, and Family Overview, management, and creation follow one readable column.
- At `768×1024`, spacing expands while the logical reading and tab order remain unchanged. The Family context stays prominent and forms use the available width without becoming a narrow phone card.
- At `1366×768`, the public screen uses a balanced split. The application places the archive and Family management in an asymmetric two-column overview with useful content above the fold.
- At `1440×900`, the application reaches its wide content limit and uses the additional height for spacing rather than oversized controls or dashboard cards.

The page has one meaningful `h1` in every rendered state. Form controls retain programmatic labels, the Family switcher is explicitly labelled, the skip link becomes visible on focus, and all interactive elements have a high-contrast `:focus-visible` ring. Errors use `role="alert"`, connecting/loading feedback uses status semantics, decorative connection lines are hidden from assistive technology, and reduced-motion preferences disable nonessential motion.

For local review, `visual-review.html?state=<scenario>` renders presentation-only fixtures for `signed-out`, `phone-verification`, `editable`, `editing`, `readonly`, `multiple`, and `empty`. This HTML entry is for development review only: production `index.html`, `main.tsx`, and `App.tsx` do not import it, and the production build excludes it. Browser review covers the four required viewport sizes, horizontal overflow, touch target size, Family switching, editor opening, and keyboard focus.

## Product identity in the interface

Use `รากเรา` as the Thai user-facing product name and `RAKRAO` where an English/romanized brand is needed. The working tagline is “Discover how we are connected.” Treat it as editable product copy, not a fixed architectural string. Domain component names such as FamilySwitcher, FamilyTree and PersonProfile remain descriptive and do not take a brand prefix.

## Future navigation and key screens (M4+)

Later milestones may extend the authenticated shell with search, tree, person profile, membership status, invitations, and review workflows. There is no global people, family or relationship directory and no public person search. NewMember onboarding is expected to open an existing family through an invitation link, then guide the applicant through a membership request with identifying and relationship information. The planned request status screen shows pending, more information requested, approved or rejected, with a clear next action. Family admins will have a review queue and can approve, reject or ask for more information with an audit reason. A separate planned claim flow links the account to an existing Person after review.

A person card shows permitted photo, current primary display name (or explicit family alias), partial birth information, deceased status and basic relationship summary. The profile page labels the current primary name and lists visible previous names, then adds family context, partner episodes, parentage and edit controls where permitted. Unknown facts say “Unknown” rather than displaying empty or invented values. Privacy filtering occurs in the API; the UI also avoids offering controls the caller cannot use, using capabilities returned by the API. Minor records follow conservative visibility and show no automatic account-claim action.

## Future desktop/notebook tree (M4+)

Show a larger bounded tree canvas with React Flow pan, zoom, fit-to-view, person selection and keyboard access. A persistent search/focus control finds an authorized person, and generation filters request a new bounded subgraph. ELK lays out the returned nodes, with parent-child direction visually distinct from union connections. A selected card opens a details panel; expanding branches requests more authorized nodes. Preserve focus and camera position where possible when relayout happens. Use visual cues for biological, adoptive and step parentage, partnerships, ended unions and deceased people; provide text labels and legend, not color alone.

## Future smartphone tree (M4+)

Default to a focus-person view: selected person, parents, optional grandparents, siblings, partners and children. Fetch only a small neighborhood and offer explicit “show another generation” or branch expansion. Large touch targets, fixed focus/search action and bottom sheet for details keep the graph usable. Pinch zoom and pan are supported, but every key action also has a tap-based alternative. Switching family retains or resets focus explicitly; never silently show a person from another family.

## Future name history presentation and editing (M4+)

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
