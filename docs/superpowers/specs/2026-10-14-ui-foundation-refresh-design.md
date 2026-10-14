# RAKRAO UI foundation refresh design

**Status:** Proposed for implementation review  
**Baseline:** `main` at `6d731858d7112004770e0ae50c8bdd472b124900`  
**Scope:** Frontend presentation and frontend tests only; documentation follows the implemented structure

## Intent

RAKRAO should feel like a private family archive and relationship product rather than an authentication demo or CRUD dashboard. The refresh creates a durable visual and interaction foundation for later milestones while preserving every verified M1–M3 behavior.

The signed-out experience introduces the product and makes authentication obvious. The signed-in experience is structurally different: a compact application shell keeps the active Family and account controls in view, while the Family Overview becomes the product home. Family identity carries more visual weight than the RAKRAO brand after authentication.

Success means that the same interface feels deliberately composed at phone, tablet, laptop, and wide-desktop sizes; remains keyboard and screen-reader usable; displays only API-provided facts; and gives future People, Tree, Invitations, and review features a stable shell without exposing inactive navigation.

## Boundaries

This work preserves the existing authentication gateway, `/api/v1/me` provisioning, API request and response contracts, authorized family list, URL `family` query parameter, family creation idempotency, family editing concurrency handling, capability checks, error handling, and authentication restoration. `AuthHome` remains the state and API coordinator.

There are no backend, Firestore, Firebase, Cloud Run, Hosting, WIF, service-account, or other infrastructure changes. There are no new product routes, fake links, invented counts, generated people, tree nodes, invitations, activity entries, or Milestone 4 behavior. The feature branch is not deployed directly.

## Product and visual principles

1. **Family first after sign-in.** The active Family name and description are the page identity. RAKRAO remains visible as a restrained shell mark.
2. **Archive, not admin dashboard.** Use editorial hierarchy, open composition, and a small number of purposeful surfaces. Avoid sidebars, KPI tiles, card grids, and repeated bordered boxes.
3. **Warm and trustworthy.** Cream, deep green, muted earth, and quiet neutral surfaces suggest continuity without memorial-site or genealogy-software clichés.
4. **Connection without illustration.** Restrained line details may suggest relationships or branches, but there is no literal tree clip-art, stock photography, decorative blob system, glass effect, or gradient-heavy SaaS treatment.
5. **Honest content.** Limited M3 data is treated as a constraint for composition, not an invitation to invent information. Future capability areas remain non-interactive and plainly described.
6. **One responsive product.** The DOM, reading order, and interaction order stay consistent while CSS changes composition. Mobile is not a stack of desktop cards, and desktop is not a stretched phone screen.

## Information architecture and component boundaries

`AuthHome` owns authentication observation, session generation guards, loading/busy/error state, API calls, authorized family selection, forms, and mutation outcomes. It chooses one of three presentation states: signed out, signed in with loaded application data, or signed in with account provisioning unavailable. Presentation components receive explicit data and callbacks and do not call Firebase or the API.

The focused component responsibilities are:

- **`BrandMark`** renders the compact `RAKRAO / รากเรา` identity in the application shell and the stronger identity treatment on the public screen through a presentation variant.
- **`SignedOutHome`** renders the public introduction, Google action, Phone flow, loading/authentication feedback, and restrained purpose copy.
- **`AppShell`** supplies the skip link, banner, navigation landmark, family-context region, account region, and main-content container. It renders only the real `Overview` navigation state.
- **`FamilySwitcher`** renders the active Family context from the authorized array. A single Family is stable text; multiple Families use a labelled native select styled as part of the shell. It never manufactures or fetches options.
- **`AccountMenu`** renders the current display name or safe account fallback and a directly keyboard-reachable Sign out button. No hover-only or unnecessarily hidden menu is introduced.
- **`FamilyOverview`** renders the active Family identity, description, honest archive-starting state, and secondary management affordance.
- **`FamilyEditor`** owns presentation for the inline edit form and cancel/save controls. It is mounted only when `EDIT_FAMILY` is present and editing is requested.
- **`CreateFamilyForm`** preserves creation but places it in a secondary section. It is extracted because its form state and action are meaningful, not to create a generic form abstraction.

Shared visual patterns such as `.surface`, `.button`, `.field`, and `.section-heading` stay CSS classes rather than one-line React wrappers. This avoids fragmenting the interface into trivial components.

## Screen structure

### Signed out

The public page uses a split composition on larger screens. The identity area carries `รากเรา`, `RAKRAO`, “Discover how we are connected.”, and one short privacy-oriented product sentence. The authentication surface sits alongside it with an explicit “Sign in to your family space” heading, Google as the primary action, and Phone as a clearly separated alternative. The Firebase implementation is never named in user-facing copy.

On small screens, the identity becomes a concise introduction at the top and authentication follows immediately. The title remains expressive but does not push sign-in below the initial viewport. Loading and safe error messages occupy a stable feedback area so the layout does not jump unnecessarily.

### Signed-in application shell

The shell header is compact rather than heroic. Its desktop reading order is brand, Overview navigation, Family context, then account controls. Family context occupies more space and contrast than the brand. The content container is approximately `76rem`–`80rem` wide with responsive side padding, allowing a deliberate two-column desktop layout while preserving comfortable line length.

The navigation includes only `Overview`, marked as the current page. The structure can later accept implemented destinations without changing the header contract, but People, Tree, Invitations, and review controls do not render now.

On phone widths the header becomes two intentional rows: brand plus account controls first, then the full-width Family context and Overview state. This preserves logical tab order and keeps family switching and Sign out reachable without horizontal scrolling.

### Family Overview

The active Family is the visual hero. Its name is the page `h1`; its existing description follows as editorial body copy. When there is no description, the interface uses neutral product copy such as “A private space for this family’s shared history” rather than pretending the family supplied facts.

The desktop overview uses an asymmetric main/secondary composition rather than a card dashboard:

- The larger primary column contains the Family identity and an open “Family archive” area. This area explains that people and relationships will appear as the family record grows, without showing counts, nodes, activity, or clickable future features. A restrained connection-line motif may support the composition but carries no state.
- The smaller secondary column contains a quiet “About this family” treatment using only the existing description and a secondary `Manage family` text/outline action when authorized. Opening it reveals `FamilyEditor` in place without changing routes.
- Family creation remains available in a secondary “Your families” area below the overview or in the secondary column where space allows. The form is not open in a dominant position by default; its entry point uses clear disclosure semantics and remains usable by keyboard.

When the account has no Families, the signed-in shell remains present and the main content becomes an honest first-family state. Family creation is then the primary task. There is no empty Family switcher.

The provisioning-unavailable state also keeps a compact authenticated frame with the safe existing recovery guidance and a reachable Sign out control. It does not expose partial family content.

## Family context and mutations

The first authorized Family remains the fallback selection. A valid `?family=` selection is honored; an unavailable ID is replaced with an authorized selection exactly as today. Switching updates the existing query parameter, closes editing, and changes only content already returned by `/api/v1/me`.

The switcher has an explicit “Current family” accessible label. With one Family it shows stable text without implying that it is interactive. With multiple Families the native select retains browser keyboard, touch, and assistive-technology behavior while custom styling gives it the weight of persistent context rather than a generic form field.

`Manage family` appears only with `EDIT_FAMILY`. Editing uses the existing name and description limits, save/cancel behavior, version, error mapping, and selected Family ID. Create Family retains the existing idempotency-key reuse after a recoverable failure. Busy states disable the affected actions and never erase the user's inputs.

## Visual system

All implementation values derive from CSS custom properties rather than local arbitrary numbers.

### Typography

The font stack starts with locally available Thai-capable faces (`Noto Sans Thai`, `Leelawadee UI`, Tahoma) and falls back to the platform UI sans-serif stack. No remote font is loaded. The scale includes caption/metadata, label, body, lead, section heading, family title, and public display roles. Signed-in family titles use a responsive size around `2.25rem`–`3.75rem`; the compact shell brand remains below body-heading scale. Thai and Latin line heights are tested with real strings and avoid clipping marks.

### Color and surfaces

Tokens cover canvas cream, raised warm-white surface, quiet neutral surface, primary/deep green, green interaction state, earth accent, primary and muted text, border, error background/text, and focus ring. The palette avoids pure-black text and low-contrast beige-on-cream combinations. Contrast is verified rather than assumed.

Surfaces rely primarily on tone and spacing. Borders are used for form boundaries and essential separation; shadows are shallow and limited to the authentication surface or a small number of raised elements. Family Overview does not become a grid of matching cards.

### Spacing, shape, and layout

Spacing tokens follow a compact `0.25rem`, `0.5rem`, `0.75rem`, `1rem`, `1.5rem`, `2rem`, `3rem`, and `4rem` rhythm. Radius tokens cover controls, surfaces, and pills without rounding every container. Control height is at least `2.75rem` for touch use. Content-width tokens distinguish readable prose from the wide application canvas.

Breakpoints are content-driven around phone, tablet, application-grid, and wide-canvas transitions, initially near `40rem`, `56rem`, and `75rem`. Focus uses a high-contrast earth/green ring with offset on both light and green surfaces. Motion is limited to short disclosure/state transitions and removed under `prefers-reduced-motion`.

## Responsive compositions

### 390 × 844

The public screen presents a compact brand introduction followed by authentication within the initial scroll. The application header uses brand/account and Family-context rows. The Family name, description, archive state, management disclosure, and create-family section follow one readable column with `1rem`–`1.25rem` side padding. Actions meet touch target size, long names wrap, and no region requires horizontal scrolling.

### 768 × 1024

The public screen may retain two balanced regions when content fits or use a generous vertical composition without a narrow centered phone card. The application header keeps Family context prominent. Overview content uses a wider primary region and a subordinate management region where line lengths remain comfortable; otherwise it transitions cleanly to one column. Form actions may share a row without changing DOM order.

### 1366 × 768

The compact header leaves the Family title and useful overview content above the fold. A roughly two-thirds/one-third layout uses most of a `72rem`–`76rem` content canvas. The primary archive area has intentional depth while the management/create regions balance the right side. Empty whitespace supports hierarchy rather than forming a large unused vertical gap.

### 1440 × 900

The canvas reaches its approximately `76rem`–`80rem` maximum with comfortable outer margins. Family identity spans the visual opening; primary and secondary regions align on a shared grid. Additional height increases breathing room between editorial sections rather than enlarging the title or cards.

## Accessibility

The page uses a single `h1`, ordered section headings, `header`, `nav`, and `main` landmarks, plus a skip link visible on focus. Responsive CSS never changes the semantic/tab order. Every input retains a programmatic label; the Family switcher label is explicit. Sign out and family-management actions are normal buttons and do not depend on hover.

All interactive elements have visible `:focus-visible` treatment on cream, white, and green backgrounds. Errors retain `role="alert"`; loading/status text uses suitable live semantics without repeatedly announcing decorative content. Disabled states use more than color. Touch targets are at least 44 CSS pixels where practical, and text remains usable at browser zoom. Decorative connection lines are hidden from assistive technology.

## State and error presentation

The signed-out, connecting, authenticated, empty-family, provisioning-unavailable, editing, busy, and error states keep their current behavioral transitions. User-facing copy removes “Account ready” and other implementation language. Errors remain sanitized: provider/internal messages and raw status codes are never displayed. The stale edit keeps its existing reload guidance.

The visual hierarchy reserves room for status feedback near the action it affects while retaining the existing shared error source. No optimistic content or invented family data appears during loading.

## Testing and visual verification

Behavioral tests are written first and must prove their failure before production changes. They cover:

- signed-out identity and both authentication entry points;
- authenticated shell landmarks and absence of the public marketing hero;
- Family name/description as the overview identity;
- one-Family static context and multiple-Family switching;
- unavailable URL Family replacement;
- capability-gated `Manage family` and editor entry;
- absence of management controls without `EDIT_FAMILY`;
- first-Family creation, mutation error behavior, and authentication transitions;
- logical structural order shared by responsive layouts.

Existing tests for API calls, idempotency, stale edits, session changes, Google/Phone flows, and sign-out remain active. The required frontend commands are `npm ci`, `npm run typecheck`, `npm run lint`, `npm run format:check`, `npm test`, and `npm run build` from `apps/web`.

Visual review is a separate completion gate. Implementation provides a local, non-production visual fixture that supplies authorized Family/account props directly to presentation components, allowing authenticated states to be inspected without changing routes, authentication, or API behavior. The fixture does not enter the production application entry point or production build. Actual rendered signed-out, signed-in editable, signed-in read-only, multiple-Family, and no-Family states are inspected at `390×844`, `768×1024`, `1366×768`, and `1440×900`. Review covers fold placement, typography including Thai, whitespace, long Family names, switcher, header, action hierarchy, editor, focus states, zoom, and horizontal overflow. Visible defects found there are fixed and the affected checks repeated.

## Documentation and delivery

After implementation, `docs/ui-design.md` is updated to distinguish signed-out presentation from the authenticated shell, describe persistent Family context, record the responsive compositions and tokens, and list the implemented component responsibilities. Unrelated backend design remains unchanged.

Work occurs on `ui/foundation-refresh` from the authoritative main SHA. Focused commits separate the tested component structure, visual system/responsive treatment, and documentation where practical. After all frontend verification and visual review pass, the branch is pushed and a PR targets `main`. The PR is not merged and no staging workflow is dispatched.
