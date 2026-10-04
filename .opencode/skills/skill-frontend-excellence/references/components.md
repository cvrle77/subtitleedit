---
title: Component Discipline
purpose: Standardize repeated widgets across pages behind one canonical contract, compose with slots over prop explosion, and detect drift programmatically before it ships.
load-when:
  task-keywords: [component, widget, contract, extraction, slots, composition, Storybook, tokens, design]
  symptoms: [duplicate id, score dropped, viewport overflow, dark mode broken]
prereq: SKILL.md
related: [audit-workflow.md, defects.md, design.md, ui-ux.md]
size: ~283 lines
---

# Component Discipline

Polishing each instance separately is not enough: same-purpose widgets must follow one canonical visual and content contract. A repeated widget either has one source of truth or one documented, named variant. There is no third option.

## Terms

- Widget family: a group of repeated UI elements serving the same role across pages (all feature cards, all CTA bands, all hero variants). See the 11-family table.
- Canonical contract: the single authoritative visual + content spec for a family. Names required/optional content fields, allowed variants, the family CSS properties (padding, radius, shadow), and states (hover, focus, active, disabled, loading). A component is not done until the contract is documented and every instance matches it.
- Named variant: an intentional, documented variation exposed through one prop, one class, or one data attribute (`card--feature`, `card--integration`, `card--pricing`). Ad hoc class combinations are not variants, they are drift.
- Drift: a repeated widget that looks or behaves differently across pages without a named variant. Signals a missing abstraction or incomplete variant set. Detected by visual comparison and the drift collector.
- Drift collector: a browser-automation script that reads computed styles and bounding boxes for every family member across every audited route, then flags any difference not explained by a named variant.

## When to Extract

Extract repeated markup into a single source of truth when:

- The same structure appears three or more times across routes.
- The same structure appears twice with clear future reuse.
- Duplicated markup causes visible drift (different padding, radius, typography, or hover behavior on instances that should look identical).

Do not extract when:

- The local stack has no include or component mechanism and adding one adds build complexity out of proportion to the benefit. Normalize markup and class names instead.
- Two instances look similar but serve genuinely different semantic purposes. Build them as named variants, not one flexing component.

Sequencing rules:

- Extract DATA before markup. The first duplication to remove is shared data, not structure: a value used in two or more places (domain/base URL, price, label, disclaimer, magic number, enum) belongs in one module every call site imports. Copied strings/numbers diverge silently.
- Know when to STOP. A duplicate that varies by exactly one value is a clean component with one prop. A near-duplicate needing two or more shape/layout props is a premature abstraction (it pushes styling back to the call site, the over-generic anti-pattern); leave contextually-distinct near-duplicates inline and note them.

## The 11 Widget Families

Every multi-page polish pass starts by inventorying these families. The inventory is mandatory: without it, drift is invisible and standardization is guesswork (see [audit-workflow.md](audit-workflow.md) Phase 3). For each family the inventory records: routes where it appears, source (template/component/duplicated markup), required and optional content fields, allowed variants, and the canonical visual contract (spacing, typography, border, radius, shadow, color, icon placement, image ratio, CTA behavior, focus ring, hover behavior, responsive behavior).

| Family | Members | Standardization criteria |
|--------|---------|--------------------------|
| Global shell | Header, primary nav, dropdowns, mobile drawer, footer, legal/social row | One header, one footer, one drawer; consistent across every route |
| Hero systems | Centered, asymmetric, product, landing, legal/simple | Each hero variant is a named component; no ad-hoc combinations |
| Section headers | Eyebrow/label, heading, subheading | One spacing rule, one max-width rule, one alignment rule per variant |
| CTA systems | Primary button, secondary button, text link, final CTA band, pricing CTA, card CTA | One size scale, one radius scale, one focus ring, one hover transform |
| Card systems | Feature, benefit, outcome, pain, directory, integration, pricing, FAQ, legal-content | Equal heights in rows, consistent padding, consistent CTA placement |
| Media systems | Image rows, logo tiles, screenshots, illustrations, icon boxes, avatars | Fixed image box dimensions per family, one alt-text policy, one loading strategy |
| Data/list systems | Pricing tables, comparison tables, FAQ lists, pain lists, checklists, icon lists | Tabular figures for numbers, one bullet/icon style per list type |
| Interactive systems | Accordions, tabs, menus, dropdowns, drawers, modals, hover cards, forms | One open/close behavior, one focus management policy, one Esc/outside-click rule |
| Empty states | Empty list, empty search, zero data | Same polish as marketing surfaces; specific message and primary action |
| Legal pages | Terms, privacy, acceptable use, security | Shared layout shell; consistent typography and spacing |
| 404 / error pages | 404, 5xx, offline | Branded and useful (search box, home link), not just an apology |

## Component Contract Checklist

A widget is ready for extraction when every item is true:

- [ ] Required content fields documented by usage: title, body, eyebrow, image, CTA, secondary CTA, metadata, icon, badge.
- [ ] Optional fields have graceful empty states and leave no blank gaps.
- [ ] Markup is semantic and stable: headings use the right level, landmarks are correct, lists are real lists.
- [ ] Layout is resilient to long text, very short text, and missing optional fields. Test the longest realistic title and the shortest.
- [ ] Desktop and mobile behavior is defined explicitly, not by accident.
- [ ] Hover, focus, active, disabled, and loading states are defined when relevant.
- [ ] Accessible names come from visible content unless a different name is necessary; visible text and `aria-label` agree.
- [ ] Images have consistent intrinsic dimensions, aspect ratio, loading strategy, and alt behavior.
- [ ] No page-specific magic numbers inside the component unless exposed as a named variant.
- [ ] The component owns its CSS. Page-local overrides on shared widgets are forbidden.
- [ ] Variants are explicit (one prop, one class, one data attribute), not arbitrary class combinations.
- [ ] No hardcoded DOM `id` inside the component. Any `id` (and any `aria-controls`, `aria-labelledby`, `for`, or `href="#..."` that targets one) is derived from a per-instance unique id (`useId()` or the framework equivalent) and child ids are namespaced under it. A static id breaks the moment the component renders twice on a page.

## Server vs Client Boundary

State the boundary at the top of the contract. A component that tries to be both fails both.

| Boundary | Contract |
|----------|----------|
| Server | No event handlers, no client state, no browser-only APIs (`window`, `document`, `localStorage`, `IntersectionObserver`). Renders on the server (or build time), ships zero client JS for itself: data fetch, layout, semantic markup, the static skeleton. |
| Client | Interactive, hydrated, owns its event handlers and local state, ships the JS to drive its behaviour: a client island (modal, popover, tab strip, autocomplete). |
| Hybrid | A defect. A "server" component that calls a hook, attaches a listener, or reads `window` explodes in a server runtime or skips hydration silently. A "client" component doing no interactive work pays for hydration without earning it. |

- Declare the boundary in the contract header (a JSDoc tag, a top-of-file comment, or a framework-recognised directive).
- Lint or type-check rejects components that import a server-only module from a client boundary, or a browser-only API from a server boundary.

## Extraction Sequence

Follow in order; skipping a step turns extraction into another duplicated markup variant.

1. Choose the canonical contract from the best existing implementation, the reference target, and product needs. It is one design, not a union of all current usages.
2. Name the widget and variants clearly (`hero-asymmetric`, `section-heading`, `feature-card`, `integration-card`, `pricing-card`, `final-cta`, `alternating-row`, `faq-list`). Avoid generic names: `card`, `section`, `block`.
3. Move repeated markup into the framework's native component or partial mechanism (server-side partials, single-file components, JSX components, or build-time includes). Use the mechanism the project already uses.
4. Pass content as explicit fields. Embedded page-specific text inside shared markup is the most common cause of a component that still drifts.
5. Make variants explicit through one prop, one class, or one data attribute. If a variant needs three coordinated changes, encode them in one named variant, not three knobs.
6. Centralize the CSS for the family and remove/reduce page-local overrides. Page CSS adjusts only what the component exposes.
7. Re-render every route that uses the widget. A single broken instance proves the component is incomplete.
8. Compare every instance side-by-side at both audit viewports. If two instances do not look like members of the same system, the contract or variant set is wrong.

## Versioning and Breaking Changes

The canonical contract is an API; carry a semver, tell consumers their version, ship breaking changes with a migration path.

| Bump | Meaning |
|------|---------|
| Patch (`x.y.Z`) | Bug fix that does not change the contract (focus ring 2px to 3px for contrast; margin 16px to 24px to match the token). Consumers pick up the fix on next install, no code change. |
| Minor (`x.Y.0`) | Backward-compatible addition: a new optional prop, new named variant, new slot, additional state. Existing call sites keep working unchanged. |
| Major (`X.0.0`) | Removed prop, removed variant, renamed slot, changed default behaviour, changed required-vs-optional, or changed semantic markup affecting accessibility wiring. Existing call sites need an update. |

Deprecation discipline for any planned removal:

- Mark the prop, variant, or slot with `@deprecated` JSDoc and a short reason. Type checkers and editors surface the warning at every call site.
- Emit a one-line `console.warn` in development builds gated by `process.env.NODE_ENV !== "production"` (or the framework equivalent). Production stays silent; the warning names the deprecated thing and points at the migration.
- Link the migration codemod from the deprecation message itself, not from a separate changelog entry:

```text
[component] prop `size="huge"` is deprecated; use the `<header slot>` instead. Run `npx your-codemod component-size-huge` to migrate. Removed in v3.0.0.
```

- The codemod ships with the deprecation, not after the removal. A deprecation without a migration path is a future breakage.

## What To Avoid

These four anti-patterns produce most cross-page drift:

- Copy-pasting a polished widget and tweaking classes page by page. The first divergence starts the drift; every later page makes the contract harder to recover.
- Creating near-duplicate components with different names but the same semantic purpose. `feature-card`, `feature-tile`, and `benefit-card` that differ only in padding and CTA copy are one component with one variant axis.
- Fixing drift by adding more one-off CSS selectors. Page-local overrides reduce the symptom and reproduce the disease at the next site of drift.
- Making one component so generic that every call site needs overrides to look right. Generic components without strong defaults push the styling burden back to the page, where the drift lives.

Plus the `id` trap: hardcoding a DOM `id` (or an `aria-controls`/`aria-labelledby` target) inside a reusable component rendered more than once produces duplicate ids: invalid HTML, a Lighthouse `duplicate-id` (or axe `duplicate-id-aria`) failure, and broken ARIA wiring (a label or control points at the wrong instance). Generate a unique id per instance and namespace children under it.

## Slots and Composition Over Prop Explosion

The over-generic-component anti-pattern resolves to slots, not more props. Rule: if you would need three or more shape props (props that change WHICH elements render, not WHAT they contain), use a slot instead.

- A slot is `children` (the default slot), named slots like `<header>` and `<footer>` (web components, single-file frameworks), or render props (component-as-function patterns). Expose composition explicitly per framework.
- Reach for slots when the variation is structural: optional sections, optional decorations, optional adjacent content.
- Stay on props when the variation is data: title text, image URL, CTA label, variant name (one named variant prop, not a knob per CSS property).
- Composed components stay testable: each slot has a known contract; the parent need not know what fills it. Slots break the prop-explosion to drift cycle.

## Drift Detection

Programmatic detection complements visual review. For each repeated widget selector, collect:

- Width, height, padding, border-radius, border color, border width, border style, background, box-shadow.
- Heading font-size, weight, line-height.
- Body font-size, line-height, color.
- CTA position and size.
- Image and logo rendered size and aspect ratio.
- Grid row height variance across the row.

Run from a headless browser of your choice (Puppeteer, Playwright, or equivalent). The snippet uses only standard DOM and CSSOM APIs:

```js
const componentMetrics = await page.evaluate(() => {
  const read = (el) => {
    const cs = getComputedStyle(el);
    const r = el.getBoundingClientRect();
    const heading = el.querySelector("h2, h3, .card-title");
    const body = el.querySelector("p");
    const cta = el.querySelector("a, button, .btn, .btn-text");
    const img = el.querySelector("img");
    return {
      selector: el.className,
      width: Math.round(r.width),
      height: Math.round(r.height),
      padding: cs.padding,
      radius: cs.borderRadius,
      background: cs.backgroundColor,
      borderColor: cs.borderColor,
      borderWidth: cs.borderWidth,
      borderStyle: cs.borderStyle,
      shadow: cs.boxShadow,
      headingSize: heading ? getComputedStyle(heading).fontSize : null,
      headingWeight: heading ? getComputedStyle(heading).fontWeight : null,
      bodySize: body ? getComputedStyle(body).fontSize : null,
      bodyLine: body ? getComputedStyle(body).lineHeight : null,
      cta: cta ? cta.getBoundingClientRect().toJSON() : null,
      image: img ? img.getBoundingClientRect().toJSON() : null,
    };
  };
  const families = {
    cards: [".feature-card", ".card-link", ".grid-item"],
    integrations: [".integration-card", ".integration-tile", ".integration-logo-tile"],
    pricing: [".pricing-card", ".pricing-table"],
  };
  const out = {};
  for (const [name, selectors] of Object.entries(families)) {
    out[name] = selectors.flatMap((s) => [...document.querySelectorAll(s)].map(read));
  }
  return out;
});
```

Run the collector on every route in scope and compare each family across pages. Any difference in padding, radius, shadow, heading size, or CTA position not explained by a named variant is drift: fix the component (not the page) until the next run is clean. A family is drift-free when every same-variant instance produces the same metrics within rounding tolerance.

## CSS Patterns That Prevent Drift

Use these on every shared widget; they are the multi-instance subset. Layout primitives live in [responsive.md](responsive.md); color, typography, and shadow tokens in [design.md](design.md).

- Tokens before primitives: normalize spacing, color, border, radius, shadow, and type-scale tokens before building. A component referencing tokens stays in step; one that hardcodes values drifts.
- Stable dimensions for repeated UI: `min-height` on cards prevents short-content rows from collapsing; `aspect-ratio` on media boxes prevents image jitter; `align-items: stretch` on grid/flex rows keeps siblings the same height; fixed icon boxes (square wrapper around the SVG) prevent optical mis-centering.

Card grid pattern:

```css
.card-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 24px;
  align-items: stretch;
}

.card-grid > * {
  min-width: 0;
  height: 100%;
}

.card-link {
  display: flex;
  flex-direction: column;
  min-height: 100%;
  text-decoration: none;
}

.card-link:focus-visible,
.button:focus-visible {
  outline: 3px solid var(--focus-ring-fallback, #4f8cff);
  outline: 3px solid color-mix(in srgb, var(--accent) 45%, white);
  outline-offset: 3px;
}

@media (max-width: 767px) {
  .card-grid {
    grid-template-columns: 1fr;
    gap: 16px;
  }
}
```

- `min-width: 0` on grid children is the most-skipped guard against text overflow. Without it, long words break out of the cell and drag the page wide on mobile.
- `color-mix(in srgb, ...)` is Baseline 2024. Always provide the static-color `outline` line first as the fallback. If the project supports pre-2024 browsers, drop the `color-mix` line and use a precomputed token (`var(--color-focus-ring)`).
- Spacing via `gap`, not margin stacks: `gap` is owned by the container and stays constant; margin stacks accumulate across instances and produce per-page drift.
- Line length via `max-inline-size: 65ch` (or similar). Cards without measure constraints produce 90-character lines on desktop and 30-character lines on mobile.
- Full-card anchor, not nested anchors: when a card visually behaves as a link, the entire card is the anchor. Nested anchors are invalid HTML. For an inner CTA distinct from the outer link, switch to the button-card pattern: a stretched `::before` pseudo-element on the title link makes the whole card clickable while keeping nested controls separate.
- Hover transforms do not stack: if the card animates on hover and an inner CTA also animates, the two transforms compose into a buggy shift. Pick one layer for the hover transform and suppress the other inside the animated container.
- `:focus-visible`, not `:focus`: mouse users get no ring on click, keyboard users always do. The ring is 2 to 4 px with 3:1 contrast against both the surface and the resting state. See [accessibility.md](accessibility.md).
- Image loading strategy per role: hero LCP image uses `loading="eager"`, `fetchpriority="high"`, declared `width` and `height`, plus `srcset` and `sizes`; below-the-fold imagery uses `loading="lazy"`, `decoding="async"`; logo tiles and avatars use fixed intrinsic dimensions with consistent ratio across the family. See [performance.md](performance.md).
- Visible text is the accessible name: do not hide visible text behind a mismatched `aria-label`. When a control is icon-only, the `aria-label` matches the meaning the icon conveys.

## Component Playground Discipline

A component playground (Storybook or any component workshop) exercises the contract in isolation. If a state is not in a story, it is not in the contract.

- One story per variant: every named variant in the contract has its own story; reviewers compare variants side-by-side without the host app.
- One matrix story per family: a single story renders every state-by-prop combination on one page (resting, hover, focus, disabled, loading, error; primary, secondary, ghost; small, medium, large). The matrix catches unrendered combinations.
- Visual regression on the story tree, not on integration screens: snapshot each story and the matrix story; gate PRs on the story-tree diff. Integration-level visual tests are flaky (data shifts, animation timeouts) and miss component-level drift. Story-level tests are deterministic.
- Interaction tests live next to stories: a story exercising an interaction (click, focus, submit) carries its own play function and assertions.
- Stories ship with the component, in the same package and the same review. A component without stories is not done.

## Token Transformation Pipeline

Design tokens live in one place, not in CSS, JS, iOS, Android, and the design tool independently. One transformation pipeline takes the single source of truth and emits every target. Choose ONE pipeline; mixing them produces drift between targets.

- Source format: a single JSON or YAML file (or a small directory) defining color, spacing, typography, radius, shadow, motion, and z-index tokens with semantic names. Aim for the W3C Design Tokens Community Group (DTCG) format: platform-neutral and tool-agnostic, so the source survives a future tool change.
- Pipeline options that read the source:
  - A mature scriptable build that emits CSS variables, JS exports (ESM and CJS), iOS Swift, Android XML, and arbitrary custom formats via JS templates. Use when the team controls the pipeline and ships to multiple platforms.
  - A design-tool plugin that round-trips tokens between design and code and integrates with the scriptable build as its build step. Use when designers own the tokens and need a design-first edit surface.
  - A smaller, format-faithful DTCG-compatible CLI that emits whichever targets the project needs without the broader surface. Use when the project commits to the W3C format and wants the lowest pipeline complexity.
- Outputs by default: CSS custom properties (a `:root` block per theme), a JS or TS export module, and optionally platform-specific outputs (iOS, Android) when relevant.
- Pipeline runs in CI: it emits outputs from the source on every commit; outputs are committed (or generated and consumed inline). Drift between source and output ships unnoticed.
- No raw hex, raw px, or raw ms in component CSS: every value comes from a token, and lint enforces it.

## See Also

- [audit-workflow.md](audit-workflow.md): the multi-page audit procedure that builds the widget inventory.
- [defects.md](defects.md): symptom-to-fix lookup and the canonical geometry sweep.
- [ui-ux.md](ui-ux.md): state coverage and interaction patterns.
- [design.md](design.md): tokens, typography, and shadow language.
