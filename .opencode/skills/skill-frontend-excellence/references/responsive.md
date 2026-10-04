---
title: Responsive Layout
purpose: Framework-agnostic rules for layouts that hold across viewports, orientations, themes, zoom, and modern form factors. Covers breakpoints, container queries, fluid units, safe areas, subgrid, responsive images, scrollbar-gutter, and foldables.
load-when:
  task-keywords: [responsive, breakpoint, mobile, container query, viewport, safe area, dvh, srcset, DPR, fluid typography, subgrid, scrollbar-gutter]
  symptoms: [horizontal scroll, viewport overflow, scroll lock side shift, iOS 100vh, broken on Safari, rubber-band scroll, image too small on retina]
prereq: SKILL.md
related: [ui-ux.md, design.md, accessibility.md, performance.md]
size: ~481 lines
---

# Responsive Layout

Layouts that work from 320px phones to 4K displays, in portrait and landscape, light and dark, with system text scaling and browser zoom.

## Mobile-First

| Principle | Check |
| --- | --- |
| Design and code mobile first | Default styles target mobile; min-width media queries layer up. Mobile constraints force the right tradeoffs and most users are mobile. |
| Do not invert the cascade | Avoid max-width queries layered downward; they invert the natural flow. |

```css
/* Default styles target mobile */
.nav { padding: 12px; flex-direction: column; }

/* Min-width media queries layer up */
@media (min-width: 768px) {
  .nav { padding: 16px 24px; flex-direction: row; }
}
```

### Mobile nav must survive without JavaScript

A JS-driven mobile menu has a trap: its links do not exist in static HTML until JS hydrates. With JS disabled, slow, or failed, a phone has a logo and nothing to navigate with.

| Check | Detail |
| --- | --- |
| Ship a `<noscript>` fallback nav | A plain `<nav>` of the same links, shown only on small screens, replaced when the JS menu hydrates. |
| Or render links in static HTML always | Let JavaScript progressively enhance them into a toggle. The LINKS must exist without JS, not the enhanced interaction. |
| Keep the perf-minded hydration directive | Load the menu JS only on small screens. |
| Reachability check | Load any page with JavaScript disabled at a phone width; every primary nav destination must be reachable. |

```html
<noscript>
  <!-- shown only on small screens; the JS menu replaces it when hydrated -->
  <nav aria-label="Primary" class="mobile-only">
    <ul><li><a href="/a">A</a></li><li><a href="/b">B</a></li></ul>
  </nav>
</noscript>
```

## Breakpoints

Pick a small, consistent set. Do not add a breakpoint per device; three or four well-chosen breakpoints handle every reasonable layout.

| Breakpoint | Pixel | Targets |
| --- | --- | --- |
| (default) | < 640px | Phones |
| `sm` | 640px | Large phones, small tablets |
| `md` | 768px | Tablets portrait |
| `lg` | 1024px | Tablets landscape, small laptops |
| `xl` | 1280px | Laptops, desktops |
| `2xl` | 1536px | Large desktops |
| `3xl` (optional) | 1920px | Very large displays |

## Canonical Audit Capture Viewports

Both sizes are required for any cross-page audit. See [audit-workflow.md](audit-workflow.md) Phase 4 for capture and [defects.md](defects.md) for the geometry sweep at both sizes.

| Role | Width x Height | Why |
| --- | --- | --- |
| Desktop audit | `1440x900` | Common laptop/external display proxy; wide enough for desktop nav, narrow enough to catch hero overflow. |
| Mobile audit | `375x812` | iPhone-class; exposes mobile drawer, drift, and touch-target failures. |

## Container Queries

| Feature | Principle + check |
| --- | --- |
| `@container` size queries | Use `container-type: inline-size` when the same component appears in different layouts so it responds to its container, not the viewport. Do NOT use for top-level page layout; viewport queries are simpler and more universally supported. |
| `@container style(--prop)` | Let a descendant respond to a container-scoped custom property (theme, density, brand variant) instead of nested theme-class selectors. |
| `@container scroll-state(stuck: top)` | Style a `position: sticky` element the moment it pins, replacing the JS `IntersectionObserver` sentinel (which remains the fallback). |

```css
.card { container-type: inline-size; container-name: card; }
@container card (min-width: 400px) {
  .card-body { flex-direction: row; }
}
```

```css
.card { container-name: card; --theme: light; }
.card.dark { --theme: dark; }
@container card style(--theme: dark) {
  .card-title { color: var(--color-foreground-on-dark); }
}
```

```css
.sticky-header {
  position: sticky; top: 0;
  container-type: scroll-state;
  background: transparent;
  transition: background 200ms ease-out;
}
@container scroll-state(stuck: top) {
  .sticky-header { background: var(--color-surface); box-shadow: var(--shadow-sm); }
}
```

## :has() Cookbook

`:has()` (Baseline 2023, broad support across Chrome, Safari, Firefox) is the parent selector for parent-state styling.

| Pattern | CSS |
| --- | --- |
| Component variant by child presence | `.card { padding: var(--space-4); } .card:has(img) { padding: 0; }` |
| Form-level invalid state (no JS toggle) | `.form:has(:invalid) { border-color: var(--color-danger); } .form:has(:invalid) .submit-btn { opacity: 0.6; pointer-events: none; }` |
| Layout shift by child count | `.menu:has(> :nth-child(6)) { flex-direction: column; }` (vertical when more than five items) |
| Container-relative hover | `.card:has(button:hover) { transform: translateY(-2px); }` |
| Empty-state styling (no special class) | `.list:not(:has(li)) { display: grid; place-items: center; min-height: 200px; }` |

Rules:

- `:has()` is computed at every style recalculation; do not chain deep `:has(.a:has(.b:has(.c)))` expressions in heavy lists.
- Do not ship the `:has()` polyfill (JavaScript that re-runs on every DOM change); let unsupported browsers degrade to the unstyled state.

## Fluid Typography

| Check | Detail |
| --- | --- |
| Use `clamp(min, preferred, max)` | Scale type smoothly between breakpoints rather than stepping at fixed media queries. The preferred value reaches min at the smallest supported viewport and max at the largest. |
| Keep `line-height` unitless | e.g. `line-height: 1.2`, so fluid scaling happens automatically. |

```css
.h1 { font-size: clamp(2rem, 1.5rem + 2.5vw, 4rem); } /* min 32px, max 64px */
```

## Viewport Units

| Unit | Meaning |
| --- | --- |
| `vw` / `vh` | 1% of viewport width/height (legacy, includes browser chrome on mobile) |
| `dvw` / `dvh` | Dynamic viewport (changes as browser chrome shows/hides); use for full-height mobile layouts |
| `svw` / `svh` | Small viewport (smallest possible, browser chrome visible) |
| `lvw` / `lvh` | Large viewport (largest possible, browser chrome hidden) |
| `cqw` / `cqh` | Container query units |

For full-height mobile heroes use `min-height: 100dvh` (not `100vh`); `100vh` overflows on iOS Safari when the address bar is showing.

## Responsive Images

srcset candidates must cover CSS width times DPR. See [performance.md](performance.md) for image format and `fetchpriority`.

Arithmetic:

- Largest real demand = (max CSS width from the `sizes` attribute) times (max DPR you support, usually 2 or 3).
- Emit candidates up to that width.
- For an LCP/hero image, emit up to about 2x the maximum layout width.

Worked example (brand-free): a card image rendered in a 360px box with `sizes="(min-width: 1024px) 360px, 90vw"` needs a 720w candidate (2x of 360), so emit widths 400 and 720. Below that, `image-size-responsive` fires on retina phones.

```html
<img
  src="card-720.jpg"
  srcset="card-400.jpg 400w, card-720.jpg 720w"
  sizes="(min-width: 1024px) 360px, 90vw"
  alt="" />
```

## Safe Areas

Modern phones have notches, dynamic islands, gesture bars, home indicators. Respect them.

| Check | Detail |
| --- | --- |
| Pad fixed bars with safe-area insets | `padding-bottom: max(16px, env(safe-area-inset-bottom))` and `padding-top: max(16px, env(safe-area-inset-top))`. |
| Viewport meta needs `viewport-fit=cover` | Required for `env(safe-area-inset-*)` to work. |

```css
.fixed-bottom-bar { padding-bottom: max(16px, env(safe-area-inset-bottom)); }
.fixed-top-bar    { padding-top: max(16px, env(safe-area-inset-top)); }
```

```html
<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover" />
```

## Logical Properties

Use logical properties for RTL support; when `<html dir="rtl">` they automatically flip while physical properties do not. See [i18n.md](i18n.md) for RTL mirroring.

| Physical | Logical |
| --- | --- |
| `margin-left` | `margin-inline-start` |
| `margin-right` | `margin-inline-end` |
| `padding-top` | `padding-block-start` |
| `padding-bottom` | `padding-block-end` |
| `text-align: left` | `text-align: start` |
| `text-align: right` | `text-align: end` |
| `border-left` | `border-inline-start` |
| `width` | `inline-size` |
| `height` | `block-size` |

## Layout Primitives

Build layouts from a small set of primitives.

| Primitive | Purpose + CSS |
| --- | --- |
| Stack | Vertical layout, consistent gap: `.stack { display: flex; flex-direction: column; gap: var(--space-4); }` |
| Cluster | Horizontal wrapping layout: `.cluster { display: flex; flex-wrap: wrap; gap: var(--space-3); align-items: center; }` |
| Center | `.center { box-sizing: content-box; max-inline-size: 65ch; margin-inline: auto; padding-inline: var(--space-4); }` |
| Frame | Fixed aspect ratio: `.frame { aspect-ratio: 16 / 9; overflow: hidden; }` with child `img`/`video` at `inline-size: 100%; block-size: 100%; object-fit: cover`. |

### Sidebar

Two columns, one fixed-width, the other flexes.

```css
.with-sidebar { display: flex; flex-wrap: wrap; gap: var(--space-6); }
.with-sidebar > .sidebar { flex-basis: 280px; flex-grow: 1; }
.with-sidebar > .main { flex-basis: 0; flex-grow: 999; min-inline-size: 50%; }
```

### Switcher

Multi-column that becomes a stack below a threshold; items stack when the container drops below 600px.

```css
.switcher { display: flex; flex-wrap: wrap; gap: var(--space-4); }
.switcher > * { flex-grow: 1; flex-basis: calc((600px - 100%) * 999); }
```

### Cover

Fill available space with header, centered content, footer.

```css
.cover { display: flex; flex-direction: column; min-block-size: 100dvh; padding: var(--space-4); }
.cover > main { margin-block: auto; }
```

### Reel

Horizontal scrolling cluster (carousel without arrows).

```css
.reel {
  display: flex; gap: var(--space-4);
  overflow-x: auto; scroll-snap-type: x mandatory; scrollbar-width: thin;
}
.reel > * { flex: 0 0 auto; scroll-snap-align: start; }
```

### Grid (auto-fit)

Wraps 1 to 2/3/4 columns as the container grows.

```css
.grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  gap: var(--space-4);
}
```

### Subgrid

Subgrid (Baseline 2024, Chrome, Safari, Firefox) aligns a nested element to the parent grid's rows or columns. Titles, bodies, and footers align across siblings automatically because they share tracks.

```css
.card-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  grid-template-rows: auto 1fr auto; /* title, body, footer */
  gap: var(--space-4);
}
.card { display: grid; grid-template-rows: subgrid; grid-row: span 3; }
.card-title  { grid-row: 1; }
.card-body   { grid-row: 2; }
.card-footer { grid-row: 3; }
```

| Use subgrid for | Anti-pattern |
| --- | --- |
| Card grids where titles, prices, CTAs align across rows with varying body lengths | Using subgrid on a grid that does not need named-area alignment. If children need not align to the parent's tracks, regular grid is simpler. |
| Forms where labels, inputs, helper text form three clean columns across nested fieldsets | |
| Editorial pages where a sidebar caption column aligns to the body grid | |

## Containers and Max Widths

Do not go full-width on huge displays; lines longer than ~75 characters become hard to read. For prose, set `max-inline-size: 65ch`.

```css
.container {
  max-inline-size: var(--container-width, 1280px);
  margin-inline: auto;
  padding-inline: clamp(16px, 4vw, 48px);
}
```

| Max-width | Use |
| --- | --- |
| 640px | Long-form reading content |
| 768px | Reading with a sidebar |
| 1024px | Routine application UI |
| 1280px | Hero or feature-rich layouts |
| 1440px | Big imagery, generous whitespace |
| 1600px | Wide screens, data-heavy surfaces |

## Animating Intrinsic Sizes

`interpolate-size: allow-keywords` (Baseline 2026, Chrome shipping) lets the browser interpolate intrinsic-size keywords (`auto`, `min-content`, `max-content`), replacing the JS pre-measure pattern for `height: auto` animations. `calc-size()` extends it for arithmetic: `height: calc-size(auto, size + 16px)` animates to natural height plus padding. Older browsers without `interpolate-size` snap open instantly (clean degradation).

```css
:root { interpolate-size: allow-keywords; }
.accordion-body { height: 0; overflow: hidden; transition: height 250ms ease-out; }
.accordion-body[data-open] { height: auto; }

@media (prefers-reduced-motion: reduce) {
  .accordion-body { transition: none; }
}
```

## Scrollbars

`scrollbar-gutter: stable` (Baseline 2024) reserves scrollbar space so removing the scrollbar (scroll lock, content shrink, fading overlay scrollbar) no longer reflows the page. This fixes the page-jump-sideways modal-shift bug without JS scrollbar-width padding. On overlay-scrollbar systems (macOS default, mobile) the gutter is 0px and nothing changes. Use `stable both-edges` to reserve the gutter on both sides for symmetry on a centered layout.

```css
:root { scrollbar-gutter: stable; }
```

## Horizontal Scroll: Forbidden

The single most common responsive bug. Causes: fixed-pixel widths exceeding viewport, long unbreakable text (URLs, code, CJK), images without `max-width: 100%`, tables without overflow, sidebars that do not collapse.

| Fix | Detail |
| --- | --- |
| `max-inline-size: 100%` on images/embeds | Or `max-width: 100%` for non-RTL contexts. |
| `overflow-wrap: break-word` + `word-break: break-word` | For long unbreakable strings. |
| Wrap wide tables in `<div style="overflow-x: auto">` | Handles table overflow. |
| Collapse sidebars at small breakpoints | |
| Test at 320px width | Scroll horizontally; if you can, fix it. |

## Accessibility at Different Viewports

| Standard | Requirement |
| --- | --- |
| WCAG 1.4.10 (Reflow) | Content usable at 320 CSS pixels wide without horizontal scroll. |
| WCAG 1.4.4 (Resize Text) | Content usable at 200% browser zoom without loss of functionality. |

Test by resizing the browser to 320px wide, setting browser zoom to 200%, and setting OS text size to largest; fix if the layout breaks. See [accessibility.md](accessibility.md) for accessible scaling.

## Orientation

| Check | Detail |
| --- | --- |
| Do not lock layout to portrait | Some users hold phones in landscape. |
| Landscape phones are short | Do not assume they have a tablet-class screen height. |
| Support both orientations for full-screen forms/inputs | Test landscape mode on an actual phone. |

### Foldables and device-posture

`viewport-fit=cover` on the viewport meta is required so layout can reach the seam and safe-area insets work. The `device-posture` media query and the `screen.fold` API (where supported) let layout adapt to the hinge. For most surfaces, foldable support is one breakpoint test (does the layout look right at 600 to 800px unfolded?) plus a viewport meta; treat `device-posture` as progressive enhancement (partial support).

```css
@media (device-posture: folded) {
  .reader { columns: 1; padding-inline: var(--space-4); }
}
@media (device-posture: continuous) {
  .reader { columns: 2; column-gap: var(--space-8); }
}
```

## Dynamic Type / Font Scaling

Use `rem` for font-size (body `1rem` defaults to 16px) so user agent text scaling and browser zoom work. Avoid `px` for font sizes except logos and single-line UI labels.

```css
body { font-size: 1rem; } /* defaults to 16px, scales with user prefs */
h1 { font-size: 2.5rem; }
```

## Media Preferences

| Preference | CSS |
| --- | --- |
| Reduced motion | Respect `prefers-reduced-motion`. See [motion.md](motion.md). |
| Reduced data | `@media (prefers-reduced-data: reduce) {}` to skip non-essential animations, replace videos with posters, lower image quality. |
| High contrast | `@media (prefers-contrast: more) { :root { --color-border: var(--color-foreground); } }` to bump border weights and increase contrast. |

### Color scheme

```css
@media (prefers-color-scheme: dark) {
  :root { /* dark tokens */ }
}
/* or a [data-theme="dark"] selector for a user-controlled toggle */

/* Opt in to native UA dark mode controls (scrollbars, form controls) */
:root { color-scheme: light dark; }
[data-theme="light"] { color-scheme: light; }
[data-theme="dark"]  { color-scheme: dark; }
```

## Print

Content-rich surfaces may be printed. Add a minimal print stylesheet. See [print-email.md](print-email.md) for full print/email treatment.

```css
@media print {
  nav, footer, .no-print { display: none; }
  body { font-size: 12pt; color: black; background: white; }
  a { color: black; text-decoration: underline; }
  a[href]::after { content: " (" attr(href) ")"; }
}
```

## Touch and Pointer

| Pointer type | Meaning |
| --- | --- |
| `fine` | mouse, trackpad, stylus |
| `coarse` | touch |
| `hover` capability | device can hover (hover-on-hold for touch is not real hover) |

```css
/* Hover-capable pointer */
@media (hover: hover) and (pointer: fine) {
  .card:hover { transform: translateY(-2px); }
}

/* Touch-only: larger hit targets, no hover-only affordances */
@media (hover: none) and (pointer: coarse) {
  .icon-btn { padding: 12px; }
}
```

## Common Responsive Mistakes

- One desktop-first stylesheet with `max-width` overrides that pile up.
- Fixed-pixel widths (`width: 1200px`) on containers.
- Images without `max-width: 100%`.
- Tables without horizontal scroll.
- Sidebars that do not collapse.
- Hero text readable on desktop but a wall of words on mobile.
- Buttons that are tiny on mobile.
- Inputs that auto-zoom on iOS because text size is below 16px.
- A `100vh` hero that overflows on iOS Safari.
- A surface that scrolls 12 screens on mobile while only 2 screens on desktop.
- Padding too tight (cramping) or too generous (wasting space) on mobile.
- Font too big (5 words per line) or too small (10pt body) on mobile.

## Self-Healing for Responsive

Before declaring work complete:

- [ ] Tested at 320px, 375px, 768px, 1024px, 1280px, 1920px
- [ ] Tested in portrait and landscape on a phone
- [ ] No horizontal scroll at any width
- [ ] Body text >= 16px on mobile
- [ ] Touch targets >= 44x44 with 8px gaps
- [ ] All images, videos, iframes have max-width: 100%
- [ ] Long tables have horizontal scroll
- [ ] Sidebar collapses on small screens
- [ ] Modals fit smallest viewport
- [ ] 200% browser zoom: layout intact
- [ ] System text size at largest: layout intact
- [ ] Safe area respected on notched/island devices
- [ ] `100dvh` instead of `100vh` for full-height mobile
- [ ] Works at `prefers-reduced-motion: reduce`
- [ ] Works at `prefers-color-scheme: dark`
- [ ] Tested with keyboard only

## See Also

- [ui-ux.md](ui-ux.md) for the functional spacing/layout rules
- [design.md](design.md) for visual rhythm and grids
- [accessibility.md](accessibility.md) for accessible scaling
- [performance.md](performance.md) for responsive image loading and `fetchpriority`
