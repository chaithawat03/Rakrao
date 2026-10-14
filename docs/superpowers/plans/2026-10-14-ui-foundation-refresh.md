# RAKRAO UI Foundation Refresh Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the prototype-like frontend presentation with a responsive, accessible, Family-first UI foundation while preserving all verified authentication and M1–M3 family behavior.

**Architecture:** `AuthHome` remains the sole authentication/state/API coordinator and passes data and callbacks into focused presentation components under `src/ui`. The public authentication screen and authenticated shell use distinct structures; the shell composes authorized Family context, account controls, and the Family Overview without adding routes or backend behavior. A separate Vite HTML entry renders non-production visual fixtures and is never imported by the production application entry point.

**Tech Stack:** React 19, TypeScript 5.9, React Router 7, Testing Library/Vitest, existing Vite 7 tooling, CSS custom properties, existing system Chromium and Playwright runtime (no new package)

**Spec:** `docs/superpowers/specs/2026-10-14-ui-foundation-refresh-design.md`

## Global Constraints

- Baseline is `main` at `6d731858d7112004770e0ae50c8bdd472b124900`; work stays on `ui/foundation-refresh`.
- Preserve Google/Phone authentication, auth restoration, logout/login, `/api/v1/me`, Family selection and URL query behavior, create/edit semantics, capability checks, safe errors, and mutation race guards.
- Keep `AuthHome` as coordinator; presentation components receive props and never call Firebase or the API.
- Add no routes, fake controls, M4+ data, backend/API changes, infrastructure changes, or deployment behavior.
- Add no dependency or package-lock change. Use the installed browser tooling for visual review.
- Apply YAGNI: no generic component library, speculative abstraction, unrelated refactor, or wrapper for trivial markup.
- The approved design, accessibility, responsive quality, security, tests, and explicit requirements override minimization.
- Keep one logical DOM/tab order across responsive layouts; use a labelled native select for multiple Families and static context for one Family.
- Keep commits focused; never deploy the branch, merge the PR, or start Milestone 4.

## Expected File Map

**Create**

- `apps/web/src/ui/BrandMark.tsx` — public and compact authenticated brand variants.
- `apps/web/src/ui/SignedOutHome.tsx` and `.test.tsx` — public identity and Google/Phone presentation.
- `apps/web/src/ui/AppShell.tsx` and `.test.tsx` — authenticated landmarks, skip link, current Overview state, and wide content frame.
- `apps/web/src/ui/FamilySwitcher.tsx` — one-versus-many authorized Family context.
- `apps/web/src/ui/AccountMenu.tsx` — account identity and directly reachable Sign out action.
- `apps/web/src/ui/FamilyOverview.tsx` and `.test.tsx` — Family identity, honest archive state, and capability-gated management entry.
- `apps/web/src/ui/FamilyEditor.tsx` — existing edit fields and save/cancel presentation.
- `apps/web/src/ui/CreateFamilyForm.tsx` — primary no-Family form and secondary disclosure form.
- `apps/web/visual-review.html` and `apps/web/src/visual-review.tsx` — non-production visual state entry; not imported by `main.tsx` or `App.tsx`.
- `apps/web/src/visual-review.test.tsx` — fixture scenario coverage.

**Modify**

- `apps/web/src/AuthHome.tsx` — replace monolithic JSX with the presentation components while retaining all state and handlers.
- `apps/web/src/AuthHome.test.tsx` — update product-language assertions and add integration coverage without weakening mutation/auth tests.
- `apps/web/src/styles.css` — tokenized visual system and four responsive compositions.
- `docs/ui-design.md` — record the implemented shell, Family context, visual system, and signed-out/signed-in split.

**Leave unchanged**

- `apps/web/package.json`, `apps/web/package-lock.json`, API/auth clients, backend, Firebase/Hosting files, workflows, and infrastructure.

## Review Focus

1. A very long Thai Family or account name must wrap without hiding shell controls or causing horizontal overflow; Task 4 covers it in fixture screenshots and overflow assertions.
2. Zero, one, and multiple authorized Families must each have an honest and accessible context state; Tasks 2–3 test all three.
3. A read-only Family must never reveal management/editor controls; Task 3 tests capability absence and the visual read-only state.
4. Empty descriptions must use neutral fallback copy without implying API facts or fabricated counts; Task 3 tests both the copy and absence of count/activity labels.
5. Session changes and pending create/update work must retain existing generation guards and idempotency behavior; Task 3 reruns the existing integration tests unchanged in substance.

---

### Task 1: Separate the signed-out experience

**Files:**
- Create: `apps/web/src/ui/BrandMark.tsx`
- Create: `apps/web/src/ui/SignedOutHome.tsx`
- Create: `apps/web/src/ui/SignedOutHome.test.tsx`
- Modify: `apps/web/src/AuthHome.tsx`

**Interfaces:**
- `BrandMark({ variant: 'public' | 'compact' })` renders `รากเรา` and `RAKRAO` without owning navigation.
- `SignedOutHome` consumes `loading`, `busy`, `error`, controlled phone/code values, `challengePending`, input callbacks, Google callback, and typed form submit handlers.
- `AuthHome` continues to own every auth state value and handler.

- [ ] **Step 1: Establish the frontend baseline.** From `apps/web`, run `npm ci`, then `npm run typecheck`, `npm run lint`, `npm run format:check`, `npm test`, and `npm run build`; record the existing test count and stop on any unexplained failure.
- [ ] **Step 2: Write failing signed-out tests.** Assert the public `main` contains `รากเรา`, `RAKRAO`, the tagline, “Sign in to your family space,” Google and Phone entry points; assert no `navigation` landmark or Sign out action; retain send/confirm callback assertions.
- [ ] **Step 3: Verify RED.** Run `npm test -- src/ui/SignedOutHome.test.tsx`; expect failure because the component/module and new public heading do not exist.
- [ ] **Step 4: Implement the smallest public components.** Extract only brand and signed-out JSX, keep form prevention/labels in the presentation component, and pass existing coordinator handlers without changing authentication behavior or error text.
- [ ] **Step 5: Verify GREEN and regression behavior.** Run `npm test -- src/ui/SignedOutHome.test.tsx src/AuthHome.test.tsx src/App.test.tsx`, then `npm test`; require all tests to pass with no raw auth error leakage.
- [ ] **Step 6: Commit.** Commit only these files as `feat(web): separate signed-out experience`.

### Task 2: Add the authenticated shell and persistent Family context

**Files:**
- Create: `apps/web/src/ui/AppShell.tsx`
- Create: `apps/web/src/ui/AppShell.test.tsx`
- Create: `apps/web/src/ui/FamilySwitcher.tsx`
- Create: `apps/web/src/ui/AccountMenu.tsx`
- Modify: `apps/web/src/AuthHome.tsx`
- Modify: `apps/web/src/AuthHome.test.tsx`

**Interfaces:**
- `AppShell({ displayName, families, selectedFamilyId, busy, error, onSelectFamily, onSignOut, children })` supplies landmarks and main content.
- `FamilySwitcher({ families, selectedFamilyId, onSelectFamily })` renders nothing for zero, labelled static context for one, and `select#current-family` for multiple.
- `AccountMenu({ displayName, busy, onSignOut })` renders a safe identity fallback and a normal Sign out button.

- [ ] **Step 1: Write failing shell tests.** Assert a focusable skip link targets `#main-content`; `banner`, `navigation`, one current `Overview`, and `main` landmarks exist; no People/Tree/Invitations controls exist; one Family has no combobox; multiple Families expose only supplied options under the “Current family” label; Sign out invokes its callback.
- [ ] **Step 2: Add failing AuthHome integration assertions.** After provisioning, assert the authenticated shell replaces the public tagline, the display name and Family context appear, `/api/v1/me` still receives the same ID token, and provisioning failure retains Sign out plus sanitized recovery copy.
- [ ] **Step 3: Verify RED.** Run `npm test -- src/ui/AppShell.test.tsx src/AuthHome.test.tsx`; expect missing modules/landmarks and old “Account ready” structure.
- [ ] **Step 4: Implement and integrate the shell.** Compose `BrandMark`, `FamilySwitcher`, `AccountMenu`, `nav`, and `main`; remove “Account ready”; move existing signed-in content inside the shell without changing selection or mutations.
- [ ] **Step 5: Verify GREEN.** Run the targeted command, then `npm test`; confirm the existing unavailable-Family URL replacement and auth-transition tests pass.
- [ ] **Step 6: Commit.** Commit the shell, context components, coordinator integration, and tests as `feat(web): add authenticated application shell`.

### Task 3: Build the Family-first Overview and preserve mutations

**Files:**
- Create: `apps/web/src/ui/FamilyOverview.tsx`
- Create: `apps/web/src/ui/FamilyOverview.test.tsx`
- Create: `apps/web/src/ui/FamilyEditor.tsx`
- Create: `apps/web/src/ui/CreateFamilyForm.tsx`
- Modify: `apps/web/src/AuthHome.tsx`
- Modify: `apps/web/src/AuthHome.test.tsx`

**Interfaces:**
- `FamilyOverview` consumes one `FamilySummary`, edit state/values, busy state, and start/cancel/change/submit callbacks.
- `FamilyEditor` consumes controlled name/description values and the existing typed submit/cancel callbacks.
- `CreateFamilyForm` consumes controlled values and submit callbacks plus `priority: 'primary' | 'secondary'`; secondary uses native disclosure semantics, primary is open for zero Families.

- [ ] **Step 1: Write failing presentation tests.** Assert the Family name is the only page `h1`, API description is shown, a missing description gets approved neutral fallback copy, the archive state is non-interactive and contains no counts/activity, `Manage family` appears only with `EDIT_FAMILY`, editor labels/limits and save/cancel are preserved, and secondary creation starts collapsed while zero-Family creation is immediately available.
- [ ] **Step 2: Update integration tests before implementation.** Replace implementation-language assertions with shell/overview outcomes; retain exact API arguments, idempotency-key reuse, 409 copy, selected-Family version/ID, session-generation protection, and capability-driven absence. Add the zero-Family primary creation and one-Family static context cases.
- [ ] **Step 3: Verify RED.** Run `npm test -- src/ui/FamilyOverview.test.tsx src/AuthHome.test.tsx`; expect missing components, `Manage family`, and disclosure behavior.
- [ ] **Step 4: Implement the overview and forms.** Replace the remaining signed-in monolith with these components; keep all `useState`, refs, API calls, and mutation handlers in `AuthHome`; add no data derivation beyond selected authorized Family and capability membership.
- [ ] **Step 5: Verify GREEN and full behavioral regression.** Run the targeted command and then `npm test`; require Google, Phone, sign-out, restoration, family creation/switch/edit, permission absence, stale edit, URL, and pending-session tests to pass.
- [ ] **Step 6: Commit.** Commit as `feat(web): build family-first overview`.

### Task 4: Establish tokens, responsive layouts, and browser fixtures

**Files:**
- Modify: `apps/web/src/styles.css`
- Create: `apps/web/visual-review.html`
- Create: `apps/web/src/visual-review.tsx`
- Create: `apps/web/src/visual-review.test.tsx`

**Interfaces:**
- `VisualReview({ scenario })` accepts `signed-out | editable | editing | readonly | multiple | empty` and renders presentation components directly with clearly fake local fixture values.
- `visual-review.html` imports only `src/visual-review.tsx`; production `index.html`, `main.tsx`, and `App.tsx` never import the fixture.

- [ ] **Step 1: Write failing fixture tests.** Assert all six scenarios render; `multiple` has the labelled Family combobox, `readonly` lacks Manage family, `editing` shows the editor, and `empty` shows primary family creation.
- [ ] **Step 2: Verify RED.** Run `npm test -- src/visual-review.test.tsx`; expect failure because the fixture module does not exist.
- [ ] **Step 3: Implement the fixture without dependencies.** Use the existing presentation interfaces and query `?state=` only in the separate visual entry. Do not add a route, API call, environment variable, package script, or production import.
- [ ] **Step 4: Implement the tokenized CSS.** Replace current styling with root tokens for Thai-capable typography, type scale, spacing, radii, surfaces, borders, shallow shadows, semantic colors, focus, content widths, and breakpoints. Implement the approved public split, compact shell, Family-first asymmetric grid, native-control styling, 44px controls, wrap/overflow rules, reduced motion, and phone/tablet/laptop/wide layouts.
- [ ] **Step 5: Verify fixture and static checks.** Run `npm test -- src/visual-review.test.tsx`, `npm run typecheck`, `npm run lint`, `npm run format:check`, and `npm run build`; confirm `dist` contains no visual-review HTML entry.
- [ ] **Step 6: Run the browser matrix.** Start `npm run dev -- --host 127.0.0.1 --port 4173`. Using the installed Playwright Python runtime (`$(dirname "$(command -v playwright)")/python`) with `/usr/bin/chromium`, visit all six scenarios at `390×844`, `768×1024`, `1366×768`, and `1440×900`; assert `scrollWidth <= clientWidth` and save full-page PNGs under `/tmp/rakrao-ui-review`.
- [ ] **Step 7: Inspect interaction and accessibility visuals.** At `390×844` and `1366×768`, use Playwright keyboard Tab input to capture visible focus on Family/account/form controls, switch the multiple-Family select, open Manage family, and capture the editor. Inspect every PNG with the image viewer for fold placement, Thai/Latin type, whitespace, hierarchy, long names, touch sizing, overflow, and absence of dashboard/fake-feature visuals.
- [ ] **Step 8: Fix visible defects and repeat affected gates.** For a behavior defect, first add a failing test; for a CSS-only defect, adjust the smallest token/rule and repeat its viewport plus static checks. Stop the Vite server after review.
- [ ] **Step 9: Commit.** Commit the reviewed visual system and fixture as `style(web): establish responsive UI foundation`; do not commit screenshots or temporary scripts.

### Task 5: Document the implemented foundation and run final verification

**Files:**
- Modify: `docs/ui-design.md`

**Interfaces:**
- Documentation describes actual components and behavior delivered by Tasks 1–4; planned M4+ sections remain clearly future scope.

- [ ] **Step 1: Update UI documentation.** Record signed-out versus authenticated structure, compact shell landmarks, persistent Family context, component responsibilities, token principles, four viewport compositions, accessibility guarantees, and visual-review fixture usage. Do not rewrite backend design.
- [ ] **Step 2: Commit documentation.** Commit only `docs/ui-design.md` as `docs: record refreshed UI foundation`.
- [ ] **Step 3: Run fresh required frontend verification.** From `apps/web`, run in order: `npm ci`, `npm run typecheck`, `npm run lint`, `npm run format:check`, `npm test`, `npm run build`. Record the final test count and require every command to exit zero.
- [ ] **Step 4: Verify repository scope.** From repository root run `git diff --check`, confirm a clean tree, confirm `package.json`/lockfile, `.github`, backend, Firebase, and infrastructure are unchanged from `origin/main`, and review `git diff --stat origin/main...HEAD` plus every changed file.
- [ ] **Step 5: Apply Superpowers completion gates.** Use `superpowers:requesting-code-review` for the complete branch and `superpowers:verification-before-completion` before any completion claim. Resolve genuine findings with test-first fixes and rerun affected plus full checks.

### Task 6: Push and prepare the pull request without deployment or merge

**Files:**
- No repository files unless review finds a genuine scoped defect.

**Interfaces:**
- GitHub receives the reviewed branch and PR only; no workflow dispatch, environment change, deployment, or merge occurs.

- [ ] **Step 1: Push the branch.** Push `ui/foundation-refresh` to origin and verify the remote SHA equals local HEAD.
- [ ] **Step 2: Wait for branch CI.** Identify the push-triggered `CI` run for exact HEAD, wait for completion, and require backend, frontend, and container jobs to pass.
- [ ] **Step 3: Create the PR.** Target `main` with a title such as `Refresh RAKRAO UI foundation`; summarize signed-out structure, shell/Family context, overview, design tokens/responsiveness, accessibility, visual review, unchanged APIs/infrastructure, and M4 not started.
- [ ] **Step 4: Verify PR state.** Confirm base/head SHAs, inspect PR-triggered CI to green, confirm no deployment workflow ran, and report the PR URL, commits, checks, browser-review sizes, and any remaining concerns. Do not merge.
