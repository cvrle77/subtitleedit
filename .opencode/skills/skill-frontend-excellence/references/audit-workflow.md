---
title: Multi-Page Audit and Polish Workflow
purpose: Procedural playbook for auditing an existing multi-page site against a reference and polishing every route to one consistent visual bar. The rendered browser is the source of truth.
load-when:
  task-keywords: [audit, route, sweep, screenshot, baseline, capture, polish, drift, widget, component, regression]
  symptoms: [score dropped, viewport overflow, horizontal scroll, broken on Safari, broken on Firefox, dark mode broken]
prereq: SKILL.md
related: [components.md, defects.md, pre-launch.md, design.md]
size: ~444 lines
---

# Multi-Page Audit and Polish Workflow

Use this when the work is not building a new page but bringing an existing surface to a consistent visual bar across every route. The greenfield frame (Frame, Plan, Build, Verify in SKILL.md) is for new work; the 19 phases below are for audit and polish. Run them in order; each phase has a clear input and a clear output.

Vague goal restatement: when a request is a vague quality goal ("best SEO", "make it 100 everywhere"), restate it as the explicit per-category, per-route numeric bars and the measurement method BEFORE acting.

## Core Principle

- Treat the rendered browser as the source of truth. Do not judge visual quality from code alone. Capture, inspect, compare against reference, patch the smallest set of templates/styles/assets, re-capture until clean at every requested viewport.
- Goal is to match the reference site's level of discipline (proportion, density, hierarchy, alignment, typography, spacing, interaction, responsive refinement, absence of defects), not copy another brand pixel-for-pixel unless explicitly requested.
- Repeated widgets (hero, feature card, pricing card, integration tile, CTA band, testimonial, FAQ, footer column, media row, badge, form, nav dropdown) must follow one canonical visual and content contract unless there is a deliberate documented variant. Polishing each instance separately is not enough.

## Operating Rules

Apply to every phase below. Treat any violation as a defect.

- Identify the target URL, project root, build system, source directories, and reference URL or screenshots before making changes.
- Capture a baseline before editing when visual polish is the task.
- Capture at least one desktop and one mobile viewport, using `1440x900` and `375x812` unless the user specifies otherwise. Breakpoints table: see responsive.md.
- Audit route-by-route, including generated pages, legal pages, pricing pages, integration pages, 404 pages, and programmatic content routes if linked or published.
- Audit component-by-component across routes: build a widget inventory and compare every repeated widget type across pages before finalizing. See components.md.
- Extract repeated markup into reusable components/partials/includes/framework primitives when the stack supports it; if extraction is not feasible, normalize the markup and document why duplication remains.
- Compare real screenshots against the reference. "Looks better" is not evidence.
- Fix root component causes when an issue repeats across pages: navigation, footer, cards, buttons, typography, spacing tokens, image rules, grid primitives.
- Re-render and re-screenshot after each meaningful group of fixes.
- Preserve content, brand intent, and information architecture unless the user asks for copy or structure changes.
- Keep changes scoped; do not rewrite the entire design system when a token, component, template, or layout rule fixes the defect.
- Run available validation before final delivery: build, lint, stylelint, tests, visual capture, accessibility checks, Lighthouse, and the geometry sweep in defects.md.
- Never declare the pass complete while known visible issues remain.

### Parallelizing across agents (optional)

- Give each agent a DISJOINT set of files. Two agents editing the same file collide and overwrite each other.
- Do the shared foundation (shared components, tokens, shared data modules) first and serially, before fanning out page-level work that depends on it.
- Verify centrally after all agents finish, with programmatic sweeps and a rebuild. Per-agent self-reports are not the gate.
- Trust but verify each report; re-read a file before acting on a surprising claim (an agent may read a file mid-edit and report it broken when it is not).

## Phase 1: Discover Context

Determine before capturing or editing:

- Project root and package manager.
- Build system and templating mechanism (framework-agnostic; the workflow does not depend on a specific framework).
- Source locations for templates, components, global CSS, component CSS, JavaScript, image assets, fonts, config, generated output.
- Local dev URL and whether a server is already running.
- Reference source: live URL, screenshot folder, design export, previous production site, or written design standard.
- Route scope: entire site, root only, a section, or a list of specific routes.

Read the project's existing scripts before inventing commands. If a dev server is needed and none is running, start it; if the standard port is occupied, use the existing server when it matches the project, otherwise pick another port.

## Phase 2: Inventory Routes

Build a route list from: linked anchors on homepage/primary nav; static-build output directories; sitemap files; route manifests/content data/collection templates/programmatic page data; known required pages from the user.

Exclude only routes that are intentionally external, authenticated admin flows, logout/delete actions, file/mail/tel links, or fragments of pages already covered.

If route generation is broken, fix missing published pages before polishing the rest of the site. A polished 404 for a page that should exist is still a failure.

### Diff-driven re-audit scope

On a re-audit (not a first audit), scope the route list to what the diff touched. Re-running every route every PR wastes time; missing a route the diff DID touch ships a regression.

1. Get the diff: `git diff --name-only main...HEAD` (or the appropriate base).
2. Filter to source-of-truth files: templates, components, global CSS, design tokens, layout primitives, route data, content collections. Exclude tests, fixtures, generated output, lockfiles, docs.
3. Apply blast-radius rules:
   - Page-level template change scopes to that page only.
   - Shared widget edit scopes to EVERY route that imports the widget transitively. Re-capture every importing route; do not trust "this page does not use it" without re-checking the import graph.
   - Global token or layout primitive (shared spacing scale, color, layout shell) scopes to every route.
   - Route-data change (sitemap, route manifest, collection schema) scopes to every generated page.
4. Resolve the import graph from the build tool's module graph (or a project script that emits one), not from grep; grep against component names misses re-exports and aliases.
5. Re-audit set is the intersection of (routes in scope) with (routes the diff touched transitively). Capture and sweep every route in the set at both viewports.

## Phase 3: Inventory Repeated Widgets

Before editing styles, build a cross-page widget inventory. Mandatory for any multi-page polish. The 11 widget families and the inventory record format live in components.md. Per family record: routes where it appears, source that renders it, required/optional content fields, allowed variants, canonical visual contract, intentional-vs-accidental deviations.

- Extraction rule (same as components.md): extract at three or more duplicates, or at two with clear future reuse, or any visible drift.
- Do not keep two visually different implementations of the same widget type unless they have distinct semantic purposes and named variants.

## Phase 4: Capture Baseline

Capture every in-scope route at both audit viewports before editing. Store in a timestamped audit directory. Capture both viewport and full-page screenshots when useful.

Filename convention (load-bearing for mechanical before/after comparison):

- `<route>_desktop.png`
- `<route>_desktop_full.png`
- `<route>_mobile.png`
- `<route>_mobile_full.png`
- `reference_desktop.png`
- `reference_mobile.png`
- `report.json`

Run from a headless browser of your choice (Puppeteer, Playwright, or equivalent). If the project uses another driver, port the loop body; the file naming convention is load-bearing, not the driver. Install the automation library (for the example below: `npm install --save-dev puppeteer`), save as `capture.cjs`, invoke with `node capture.cjs <output-dir>` (defaults to `visual-audit/`).

```js
const fs = require("fs");
const path = require("path");
const puppeteer = require("puppeteer");

const baseUrl = process.env.TARGET_URL || "http://localhost:3000";
const outDir = process.argv[2] || "visual-audit";
const routes = ["/"];
const viewports = [
  { name: "desktop", width: 1440, height: 900 },
  { name: "mobile", width: 375, height: 812 },
];

const safeName = (route) =>
  (route === "/"
    ? "home"
    : route.replace(/^\/|\/$/g, "").replace(/[^a-z0-9]+/gi, "_").toLowerCase()
  ) || "home";

(async () => {
  fs.mkdirSync(outDir, { recursive: true });
  const browser = await puppeteer.launch({ headless: "new" });
  const failures = [];
  for (const route of routes) {
    for (const vp of viewports) {
      const page = await browser.newPage();
      await page.setViewport({ width: vp.width, height: vp.height, deviceScaleFactor: 1 });
      try {
        const response = await page.goto(`${baseUrl}${route}?qa=${Date.now()}`, {
          waitUntil: "networkidle0", timeout: 30000,
        });
        if (response && response.status() >= 400) {
          failures.push({ route, viewport: vp.name, status: response.status() });
        }
        const file = path.join(outDir, `${safeName(route)}_${vp.name}.png`);
        await page.screenshot({ path: file });
        await page.screenshot({ path: file.replace(".png", "_full.png"), fullPage: true });
      } catch (error) {
        failures.push({ route, viewport: vp.name, error: String(error.message || error) });
      } finally {
        await page.close();
      }
    }
  }
  await browser.close();
  fs.writeFileSync(path.join(outDir, "report.json"), JSON.stringify({ baseUrl, routes, failures }, null, 2));
})();
```

Adapt the script rather than rewriting it. Add route discovery, contact sheets, interactive captures, and the geometry sweep from defects.md as the task requires.

### Authenticated and stateful route capture

Logged-in routes, multi-step flows, and screens that depend on stored state (theme, locale, feature flags, cart contents) need seeded state before screenshotting. Public-only capture misses the bulk of any real product.

- Seed cookies, `localStorage`, and `sessionStorage` at browser launch, not via a UI flow per capture. The flow runs once to record state; every subsequent capture reuses it.
- Drive the multi-step flow in one capture session: navigate to login, submit credentials, wait for the post-login redirect, set required state (select org, accept terms, dismiss onboarding), THEN screenshot the target route.
- Snapshot post-login storage with the driver's facility (Playwright `storageState`, Puppeteer `page.cookies()` plus `localStorage` dump) and reuse it across every authenticated capture: one login per audit, not one login per route.
- Refresh the snapshot when the auth schema changes or the session expires; detect via an HTTP 401 on the first authenticated request, then re-record and retry.
- Use a dedicated audit account (not a real user) with deterministic data; seed the database with known fixtures so screenshots compare cleanly across runs.
- Tag each authenticated capture so the diff tool knows it depends on seeded state; a pixel diff against a fresh login on a different day drifts even when the UI did not change.

## Phase 5: Capture Reference

Capture the reference at the same viewport sizes as Phase 4. If the reference is a live site, use cache-busted URLs and the same browser engine as the baseline. If it is a screenshot or design export, place assets next to local screenshots for one-command side-by-side comparison.

When comparing to a reference:

- Match polish level, not necessarily exact color or content.
- First viewport first: navigation, hero, primary CTA, image treatment, first hint of next section.
- Then section rhythm: density, vertical spacing, card grids, media-to-copy balance, CTA placement.
- Finally details: shadows, borders, radii, icon alignment, label treatment, focus states, hover states, mobile drawer behavior, footer density.

## Phase 6: Audit Each Route

For every route and viewport, record findings as: route + viewport + component + what is wrong + why it matters + likely source + screenshot + fix status.

| Category | What to inspect |
|----------|-----------------|
| Header and navigation | Logo alignment, nav item spacing, dropdown centering, active state, hover/focus/pressed, sticky behavior, z-index, seam lines, mobile drawer, scroll lock |
| Hero | First-viewport composition, heading scale, line length, CTA alignment, media size, image sharpness, next-section reveal, mobile fold |
| Sections | Vertical rhythm, background bands, max-widths, gutters, content density, section transitions |
| Cards and tiles | Equal heights in rows, equal widths, consistent padding, aligned baselines, clickable area, hover, focus ring, icon and image alignment, aspect ratio |
| Buttons and links | Size, label clarity, icon centering, hover drift, duplicate arrows, disabled states, touch target, focus visibility |
| Typography | Family, weight, scale, line-height, letter-spacing, wrapping, measure, hierarchy, alignment |
| Color | Background, foreground, accent consistency, contrast, border, hover, active, muted text, light and dark bands |
| Images and media | Crop, aspect ratio, resolution, `srcset`, lazy and eager loading, alt text, visual relevance, repeated or placeholder assets |
| Forms | Labels, placeholders, validation states, focus, disabled, field heights, mobile keyboard fit |
| Footer | Layout balance, link columns, legal row, social icon spacing, hit area, mobile stacking |
| Responsive behavior | No horizontal scroll, no clipping, no desktop layout on mobile, no text overflow, no oversized buttons or cards, no tiny tap targets |

Compare each widget against other instances of its family: same padding/radius/border/hover/min-height/CTA position/typography; same CTA label style; one image/logo/fallback treatment for tiles; same eyebrow/H2/subhead/width/gap for section headers; named hero variants with shared internals; explicit (not accidental) component variants.

## Phase 7: Prioritize Fixes

Fix in tiered order; higher tiers always block lower tiers.

1. Broken routes, 404s, missing generated pages, hard layout failures.
2. Global shell defects: header, navigation, footer, body overflow, typography tokens, color tokens.
3. Cross-page widget drift: same widget type implemented differently across pages without a named variant.
4. Cross-page component defects: cards, buttons, pricing, integration tiles, form controls, media blocks.
5. Page-specific hierarchy/density; interaction and accessibility; asset/performance polish; minor copy clarity.

Prefer global fixes when a defect repeats; prefer page-level fixes when the defect is truly contextual.

## Phase 8: Patch With Component Discipline

When repeated widgets drift, fix the component system before tuning page-specific CSS. Full extraction sequence, component contract checklist, and anti-patterns (copy-paste tweaking, near-duplicate components, one-off CSS for drift, overly generic components needing per-call-site overrides) live in components.md. Summary: choose a canonical contract, name widget and variants, move markup into the component mechanism, pass content as explicit fields, make variants explicit, centralize CSS, re-render every route, compare instances at both viewports.

## Phase 9: Patch With Design Discipline

Use existing design primitives before adding new ones. Patch CSS, templates, and components conservatively. Token detail: design.md. Layout primitives: responsive.md. Contrast: accessibility.md. Image strategy: performance.md.

- Normalize tokens first: spacing, color, border, radius, shadow, type scale.
- Use stable dimensions for repeated UI: `min-height`, `aspect-ratio`, grid tracks, `align-items: stretch`, fixed icon boxes.
- Avoid arbitrary one-off margins; use local precedent or none.
- Use `gap` for internal spacing instead of stacked margins where possible.
- Use `max-width` and responsive constraints for readable line lengths.
- Use full-card anchors when a card visually behaves as a link; do not nest anchors.
- Keep CTA hover transforms from stacking with card hover transforms.
- Use `:focus-visible` rings that are clearly visible and consistent.
- Avoid hiding visible text behind a mismatched `aria-label`; visible text is the accessible name.
- Make hover and focus states visually related so a keyboard user gets the same clarity as a pointer user.
- Use `srcset` and `sizes` for recurring large images.
- Use `loading="eager"` and `fetchpriority="high"` only on the hero LCP image; use `loading="lazy"` and `decoding="async"` for below-the-fold imagery.

## Phase 10: Verify After Every Fix Group

After a meaningful fix group:

1. Rebuild or let the dev server hot reload.
2. Re-capture the affected routes at desktop and mobile.
3. Compare before and after.
4. Inspect at least the first viewport and the edited section.
5. Confirm no regression on shared components.
6. If a reusable widget changed, capture every route where the widget appears, not only the route where the defect was first noticed.

Do not wait until the end to discover a global CSS fix broke mobile.

### Side-by-side diff tooling

Eyeballing two tabs misses small pixel shifts that compound. Pick the tool by cadence:

| Tool | Strength | Use when |
|------|----------|----------|
| pixelmatch | Raw pixel diff, single dependency, scriptable | One-off baseline vs after; quick local sanity check; custom CI gate where the diff threshold is the only signal |
| reg-suit | Git-aware visual regression, stores baselines per branch, reports diff to PR | Team wants a history of baselines tied to commits; self-hosted alternative to a hosted service |
| Playwright `toMatchSnapshot` | Built into the test runner, lives next to the suite | CI gate runs as part of an existing Playwright job; one toolchain; tolerable for small surfaces |
| Chromatic | Hosted, integrates with a component playground, design-review UI for non-engineers | Design system with many stakeholders needing per-change comments; budget for a paid hosted service |

Decision rule: one-off diff -> pixelmatch; CI gate alongside other tests -> Playwright `toMatchSnapshot`; history of baselines with PR-level diff comments and no hosted service -> reg-suit; design-system review surface for stakeholders -> Chromatic.

Never approve a baseline on green CI alone. A human reviews the rendered comparison images (not just the percentages) before the baseline is promoted.

## Phase 11: Programmatic Geometry Checks

Use browser automation to catch what screenshots miss: viewport bleed, hidden text overflow, sub-44 hit targets, duplicate arrows, dropdown miscentering, mobile drawer scroll-lock failures, focus invisibility.

The canonical 9-check geometry sweep, its JS snippet, and per-check thresholds live in defects.md. Run on every audited route at both capture viewports; a run is clean when every check returns zero issues.

Filter false positives only when you can explain them (hidden off-canvas content, intentionally overflowing dropdown internals that do not affect the page viewport) and document the filter so the next run does not re-discover it.

Run BOTH sweeps: the geometry sweep (visual, headless browser) and the content-and-markup sweep (SEO and HTML validity, Node over built HTML). See defects.md. A route is not signed off until both return clean.

## Phase 12: Component Drift Checks

Use the drift collector to compare same-family widgets across pages. Catches drift early; does not replace visual judgment. The snippet and property list (width, height, padding, radius, border, background, shadow, typography, CTA position, image metrics) live in components.md. Run on every route in scope; compare each family across pages.

Flag any difference not explained by a named variant. A family is drift-free when every same-variant instance produces the same metrics within rounding tolerance.

## Phase 13: Interaction QA

Capture explicit interaction screenshots: desktop dropdown open, mobile menu open, hovered card or button, focus-visible state on major controls, pricing card hover/focus if cards animate, and modal/drawer/accordion/tabs/carousel/form-validation states if present.

Behavioral checks:

- Dropdowns are centered or intentionally aligned.
- Dropdowns stay within the viewport.
- Esc closes popovers.
- Outside click closes popovers.
- Mobile drawer locks page scroll while open and restores scroll after close.
- Buttons do not drift on hover.
- Active and current nav states are visible.
- Focus ring is not clipped.
- Interactive element accessible names match visible labels.

State coverage (empty, loading, success, error, partial, disabled, read-only, stale, offline, unauthorized, limit-reached, initial, done): see ui-ux.md.

## Phase 14: Accessibility Validation

Use Lighthouse, axe, or equivalent as baseline checks, not replacements for human visual inspection. Full WCAG 2.2 AA framework, ARIA, contrast targets, keyboard patterns, screen reader checks, focus management: see accessibility.md.

Minimum basics for multi-page polish:

- `<html lang>` exists and is valid on every route.
- Exactly one primary `<main>` per route.
- Logical heading order: one H1 per route, sequential H2 to H3.
- Links and buttons have accessible names.
- Form controls have labels.
- Visible labels match accessible names.
- Images have useful alt text or empty decorative alt.
- Contrast passes for text, UI controls, and focus indicators in both light and dark mode.
- Focus order follows visual order.
- No keyboard traps.
- Skip link works and does not create viewport overflow while hidden.

If a visual fix creates an accessibility problem, the visual fix is incomplete.

## Phase 15: Lighthouse and Performance Polish

Run Lighthouse when the project has it set up or the user asks for production polish. Fix failing assertions; treat warnings as triage without over-optimizing at the cost of design unless asked. Setup, scoring weights, audit-by-audit fixes: lighthouse.md. Performance mechanics and asset strategy: performance.md.

Common visual-performance fixes that surface in audit work:

- Add responsive `srcset` variants for hero and recurring large images.
- Use correct intrinsic `width` and `height` on every image to prevent layout shift.
- Preload or prioritize only the actual LCP image.
- Avoid loading offscreen imagery eagerly.
- Remove giant decorative images visually indistinguishable from smaller assets.
- Keep webfont usage intentional: cap to two families and four weights.
- Avoid expensive shadows and filters on large scrolling surfaces if they cause jank.

## Phase 16: Reference-Level Design Heuristics

Yes-or-no judgments supported by the captured screenshots when comparing the audited site to the reference. The same 16 live in design.md "Reference Comparison Heuristics"; the two copies are intentionally identical so each file is self-contained for its reader's mode.

1. First viewport has a clear hierarchy and does not feel accidental.
2. The brand or product is visible immediately when relevant.
3. The next section peeks intentionally rather than crowding the hero.
4. Repeated cards in a row align on top edges, bottom edges, and CTA baselines.
5. Text blocks have readable line lengths and consistent rhythm.
6. Labels, eyebrows, and badges use one visual language.
7. Buttons use one size system and one radius system.
8. Icons sit optically centered, not just mathematically centered.
9. Borders are subtle and consistent.
10. Shadows support hierarchy but do not muddy the palette.
11. Alternate background bands feel deliberate.
12. Mobile layout feels designed, not merely stacked.
13. Footer spacing is calmer than the body, with no stray gaps or cramped links.
14. Empty states, legal pages, and 404 pages share the same polish as marketing pages.
15. Same-family widgets look like members of one system across pages.
16. Variants are recognizably intentional, not accidental drift.

A site at reference level passes every test; a site at "fine" level fails three or more.

## Phase 17: Common Defects and Fixes

The 29-row symptom-to-fix lookup table lives in defects.md. Use it whenever a defect surfaces during Phase 6 or Phase 13. Apply the standard fix at the right layer (component, page, or design token), then re-run the geometry sweep on the affected route.

## Phase 18: Deliverables

Provide a concise final package:

- Summary of main changes.
- Per-page checklist: issues found, fixes applied, screenshot references.
- Before-and-after screenshot paths for desktop and mobile.
- Reference screenshot paths.
- Interactive screenshot paths if menus, drawers, or cards were tested.
- Validation commands and pass-or-fail results.
- Remaining risks only if real issues remain.
- Widget inventory and standardization notes: which repeated widgets were found, which were extracted or normalized, which named variants remain.

Per-page checklist format:

```markdown
| Route | Issues found | Fixes applied | Before | After |
| --- | --- | --- | --- | --- |
| `/pricing/` | Pricing CTA drift, partial card click area | Full-card anchors, stable hover state | [D](before/pricing_desktop.png) [M](before/pricing_mobile.png) | [D](after/pricing_desktop.png) [M](after/pricing_mobile.png) |
```

Widget inventory format:

```markdown
| Widget family | Routes | Source | Standardization action | Variants |
| --- | --- | --- | --- | --- |
| Integration tile | `/integrations/`, `/whatsapp/`, `/for/.../` | duplicated markup plus partial | Extracted to `integration-card` | `link`, `static-logo` |
```

Validation summary format:

```markdown
- Build: passed
- Lint: passed
- Capture: 24 routes, `failures: []`
- Geometry sweep: `[]` at `1440x900` and `375x812`
- Drift collector: zero unexplained differences across families
- Lighthouse: passed required assertions
```

## Phase 19: Final Acceptance Gate

Authoritative gate for multi-page audit and polish work (the Self-Improvement section in SKILL.md is the short version). Confirm every item:

- Every in-scope route was captured at desktop (`1440x900`) and mobile (`375x812`).
- Every route returns the expected HTTP status.
- Repeated widget families have been inventoried and compared across pages.
- Same-purpose widgets have been extracted into reusable components/partials, or normalized to one contract where extraction is not feasible.
- Remaining variants are named, intentional, and visually consistent within their variant.
- Every edited shared component was re-captured on every route where it appears (not just the route where the defect was noticed) and the geometry sweep passed.
- The latest screenshots reflect the latest code.
- Build and lint pass, or failures are clearly unrelated and reported.
- No horizontal scroll exists on mobile.
- No known text overflow, clipped controls, duplicate arrows, tiny touch targets, or partial clickable-card defects remain.
- The geometry sweep returns zero issues on every audited route at both capture viewports.
- The drift collector returns no unexplained differences for any widget family.
- Navigation, footer, pricing, cards, forms, and interactive states pass Phase 13 interaction QA and Phase 14 accessibility validation.
- Accessibility basics from Phase 14 are clean on every route.
- Reference-Level Design Heuristics from Phase 16 pass.
- The final checklist is written and linked.

If any item fails, continue fixing or clearly state the blocker. Do not present an incomplete visual pass as complete.

## Audit Cadence and Ownership

A one-shot audit ages. Without a cadence and a clear owner per surface, the polish bar drifts back to "fine" within a quarter. Lock in both before declaring complete.

### Cadence

- Every release-candidate build: full geometry sweep, content-and-markup sweep, Lighthouse on critical routes, axe on every route. Block the release on any new regression.
- Every shared-widget edit: re-audit every route that imports the widget (per the diff-driven re-audit scope in Phase 2). Treat shared-widget PRs as cross-page changes, not local edits.
- Weekly drift sweep: run the geometry sweep, content-and-markup sweep, drift collector, and a Lighthouse pass against a fixed set of canonical routes; compare against the previous week's baseline; file issues for any regression.
- Quarterly reference comparison: re-run Phase 16 heuristics against current production. Match the reference's level of discipline today, not last year.

### Ownership

Pick ONE model and write it down. Mixed ownership produces dropped polish work.

- Route-owning team: the team that ships features on a route also owns its geometry sweep, Lighthouse score, and drift findings. Works when teams are vertically aligned to surfaces.
- Polish rotation: a dedicated rotating engineer (or pair) for one to two weeks runs the weekly drift sweep, files issues against owning teams, and drives the highest-impact fixes. Works when teams are horizontally aligned and no single team owns the visual bar.

Either way, polish work is named work on the roadmap, not an "if there is time" residual. The bar drops the moment polish is the first thing to slip.

## See Also

- components.md for extraction discipline, contracts, and drift detection
- defects.md for the symptom-to-fix lookup table and the canonical geometry sweep
- pre-launch.md for the launch gate that runs alongside this workflow
- design.md for tokens, typography, color, and shadow language
