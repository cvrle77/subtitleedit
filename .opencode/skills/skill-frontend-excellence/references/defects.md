---
title: Common Visual Defects and Geometry Checks
purpose: Symptom-to-fix lookup for visible defects, plus the canonical programmatic geometry sweep and content-and-markup sweep that gate every audited route.
load-when:
  task-keywords: [defect, bug, regression, geometry, threshold, audit, sweep, screenshot]
  symptoms: [viewport overflow, horizontal scroll, duplicate id, font swap CLS, iOS 100vh, rubber-band scroll, focus not visible, inert leak]
prereq: SKILL.md
related: [audit-workflow.md, components.md, accessibility.md, responsive.md]
size: ~254 lines
---

# Common Visual Defects and Geometry Checks

Symptom-to-fix lookup for visible defects, plus the canonical geometry sweep and content-and-markup sweep that gate every audited route at both capture viewports.

## How to Use

1. Identify the symptom on the rendered page or screenshot.
2. Look it up in the defect table.
3. Apply the fix at the right layer (component, page, or design token).
4. Re-run the geometry sweep on the affected route at both audit viewports.
5. Re-capture before-and-after screenshots; confirm no neighbor regressed.

One big table by design: one search lands the symptom regardless of topic. Topical references point here rather than restate it.

## Defect Lookup Table

| Defect | Likely cause | Fix |
|--------|--------------|-----|
| Button shifts oddly on hover | Parent and child both transform | Disable the child transform inside the animated card, or move the transform to one layer |
| Dark line after sticky header | Border/shadow/background mismatch between header and section top | Normalize header border, shadow, and section top background so the seam is invisible |
| Dropdown not centered | Absolute position tied to item edge | `left: 50%`, `transform: translateX(-50%)`, clamp width on small screens |
| Mobile dropdown off-canvas | Desktop hover rule overrides mobile rule | Add a media-specific override; constrain the dropdown to the viewport |
| Card row uneven heights | Content-dependent heights, no stretch | Grid stretch, flex column, `height: 100%`, stable `min-height` |
| Only nested CTA clickable | Card behaves as a link but the anchor wraps only the CTA | Make the full card an anchor, or use a button-card pattern with a stretched pseudo-element on the title link; never nest anchors (invalid HTML per the `<a>` content model) |
| Duplicate arrows | CSS pseudo-element arrow plus a text or icon arrow | Choose one arrow source; suppress the other by component class |
| Pricing implies wrong plan | Ambiguous trial or setup wording on shared cards | State paid plan terms and fees plainly; remove repeated misleading phrases |
| Social icons too far apart or too small | Independent margins, small hit boxes | Fixed 44px boxes plus `gap` |
| Cross or check icon off-center | Pseudo-element dimensions mismatch the icon box | Square icon box with absolutely centered pseudo-elements, or inline SVG |
| Mobile horizontal scroll | Hidden off-canvas element, wide image, long unbreakable word, or `100vw` misuse | Inspect `scrollWidth`; constrain media; `min-width: 0` on grid children; avoid `width: 100vw` inside padded containers |
| Low-quality page versus reference | Thin sections, repeated template copy, generic stock images | Add useful sections, vary copy, use relevant images, tighten hierarchy |
| Same widget looks different across pages | Duplicated markup, page-local CSS, no component contract | Extract a shared component or normalize markup and classes to one contract; see [components.md](components.md) |
| Many card variants without purpose | Ad hoc class combinations applied page by page | Define named variants in the component; map old usages into them |
| Integration or logo tiles inconsistent in size | Each page hand-codes image and card rules | One logo tile component: fixed image box, label treatment, grid behavior |
| CTA bands vary randomly | Page-specific final sections | One final-CTA component with explicit variants: centered, split, compact, gradient |
| Focus ring clipped | Parent `overflow: hidden` with insufficient padding | Move the ring to an `outline` (paints outside the box), or add internal padding so the ring fits |
| Focus ring invisible on dark surfaces | Single-color ring chosen for light mode only | Ring color with 3:1 contrast against both surface and resting state, in both light and dark mode |
| Hero image causes CLS | Missing intrinsic `width` and `height` | Declare `width` and `height` attributes; reserve space with `aspect-ratio` |
| Late-loading hero image hurts LCP | Hero image is `loading="lazy"` or behind hydration | `loading="eager"` plus `fetchpriority="high"` on the hero LCP image only |
| Webfont flash on first paint | `font-display: block` or none set | `font-display: swap` for body, `optional` for display; preload at most one critical weight |
| Unstyled flash on hydration | Critical CSS not inlined | Inline above-the-fold CSS under 14 KB so it fits the first round trip |
| Button label truncates on mobile | Fixed width or `white-space: nowrap` with insufficient room | Constrain by `max-width`, allow wrap, or shorten the label |
| Form validation appears late | Validation runs on every keystroke, then debounces | Validate on blur for new errors; clear errors live as the user types |
| Modal scrolls with the page | Body scroll not locked while modal open | Lock body scroll on open, restore on close. Compensate scrollbar width or the page shifts sideways: measure `gap = window.innerWidth - document.documentElement.clientWidth`, set `body { overflow: hidden }` plus `padding-right: gap` while open, restore both on close. Overlay-scrollbar systems give `gap` 0 (no shift). Restore scroll position too. |
| Modal traps keyboard but not screen reader | Background interactive in the accessibility tree | Mark background `inert` (or `aria-hidden="true"` plus `pointer-events: none`) while open |
| Skip link overflows the viewport while hidden | `position: absolute; left: -9999px` extends the layout box, or focus inherits the off-screen position | Hidden: `clip-path: inset(100%)` plus `position: absolute; width: 1px; height: 1px; overflow: hidden`. On `:focus`: `clip-path: inset(0); position: fixed; top: 1rem; left: 1rem` |
| Tables overflow on mobile | Fixed-width table, no scroll wrapper | Wrap in a scroll container with `overflow-x: auto`, or transform to cards below the breakpoint |
| Empty state is blank | No specific message, no primary action | Add a specific message, the condition that fills it, and a primary action that resolves it |
| iOS Safari `100vh` clips behind home indicator; rubber-band scroll exposes page background | `100vh` resolves to the largest viewport; inner-container momentum scroll bleeds to the document | `100dvh` for currently-visible height, `100svh` for the smallest stable height (no shift on URL-bar collapse), `100lvh` for the largest. On momentum-scroll containers add `-webkit-overflow-scrolling: touch` and `overscroll-behavior: contain` |
| Modal `z-index: 9999` sits behind an ancestor | Ancestor establishes a stacking context (`transform`, `filter`, `position: fixed`, `will-change`, `opacity < 1`, or `isolation: isolate`); the child `z-index` is bounded by that context | Portal the modal to `<body>` so it escapes the context, or hoist the stacking-context owner. Note: `position: fixed` does NOT escape a `transform` ancestor's context |
| `-webkit-autofill` paints inputs yellow, ignores dark-mode surface color | Browser applies a non-styleable background; CSS `background-color` does not win | Box-shadow trick: `input:-webkit-autofill { -webkit-box-shadow: 0 0 0 30px var(--surface) inset; -webkit-text-fill-color: var(--text); transition: background-color 9999s ease-out 0s; }`. Set light and dark variants; verify both |
| Font-swap CLS persists even with `size-adjust` | `size-adjust` scales glyph size only; line metrics (ascent, descent, line-gap) still differ and the swap shifts layout | Full metrics-override set on the fallback `@font-face`: `ascent-override`, `descent-override`, `line-gap-override`, and `size-adjust` tuned to the web font. Tools like `fontaine` or `next/font` compute the values; or sample the web font's metrics manually |
| Modal opens but background still announced | `inert` applied to the wrong ancestor, or `aria-hidden="true"` on the body that contains the modal (hiding the modal too) | Apply `inert` to every sibling of the dialog at its level (not the body, not a parent). A top-layer `<dialog>` does this automatically; a portalled modal in body marks all OTHER body children `inert` while open, restores on close. Never put `aria-hidden` on an ancestor that contains the modal |

## Programmatic Geometry Sweep

Catches what a screenshot pass misses: sub-44 hit targets buried in long pages, hidden text overflow on edge cases, viewport bleed from a single rogue element, duplicate arrows that read fine alone but stack across siblings.

Run from a headless browser of your choice (Puppeteer, Playwright, or equivalent). Standard DOM and CSSOM only:

```js
const issues = await page.evaluate(() => {
  const out = [];
  const visible = (el) => {
    const r = el.getBoundingClientRect();
    const cs = getComputedStyle(el);
    return r.width > 0 && r.height > 0 && cs.display !== "none"
      && cs.visibility !== "hidden" && parseFloat(cs.opacity) > 0;
  };
  const nameFor = (el) =>
    el.tagName.toLowerCase()
    + (el.className
        ? "." + String(el.className).trim().split(/\s+/).slice(0, 3).join(".")
        : "");

  if (document.scrollingElement.scrollWidth > window.innerWidth + 1) {
    out.push({ type: "viewport-bleed", value: document.scrollingElement.scrollWidth, viewport: window.innerWidth });
  }

  for (const el of document.querySelectorAll("body *")) {
    if (!visible(el)) continue;
    const cs = getComputedStyle(el);
    if (el.clientWidth > 0 && el.scrollWidth > el.clientWidth + 2 && cs.overflowX === "visible") {
      const text = (el.textContent || "").trim().replace(/\s+/g, " ").slice(0, 80);
      if (text && !["HTML", "BODY", "MAIN", "SECTION"].includes(el.tagName)) {
        out.push({ type: "text-overflow", selector: nameFor(el), text });
      }
    }
  }

  for (const el of document.querySelectorAll("a, button, input, select, textarea, [role='button'], .btn")) {
    if (!visible(el)) continue;
    const r = el.getBoundingClientRect();
    if (r.width < 44 || r.height < 44) {
      out.push({ type: "small-target", selector: nameFor(el), width: Math.round(r.width), height: Math.round(r.height) });
    }
  }

  for (const el of document.querySelectorAll("a, .btn-text")) {
    const text = (el.textContent || "").replace(/\s+/g, " ").trim();
    if (/→\s*→|›\s*›|»\s*»/.test(text)) out.push({ type: "duplicate-arrow", text });
  }

  return out;
});
```

The sweep returns nine check categories. Run it on every audited route at both capture viewports.

| # | Check | Triggers when |
|---|-------|---------------|
| 1 | Viewport bleed | `document.scrollingElement.scrollWidth` exceeds `window.innerWidth` by more than 1 px |
| 2 | Visible text overflow | An element's `scrollWidth` exceeds its `clientWidth` by more than 2 px while `overflow-x` is `visible` |
| 3 | Small interactive target | A visible interactive element measures less than 44 by 44 CSS pixels |
| 4 | Duplicate arrow | A link or text-button label contains stacked arrow glyphs (`→ →`, `› ›`, `» »`) |
| 5 | Pricing or card clickable mismatch | A card behaves as a link but only an inner CTA is the anchor (extend with a per-project selector list) |
| 6 | Header or dropdown centering | The dropdown's horizontal center is more than 4 px from its trigger center (extend with the per-project selector) |
| 7 | Mobile drawer scroll lock | Body scroll is not locked while the drawer is open (extend with a per-project trigger sequence) |
| 8 | Focus state presence | A keyboard-focusable element produces no visible outline change on focus (extend with a per-project focus drive) |
| 9 | Project-specific extension | Slot for negative letter-spacing, banned colors, or any rule the project standardizes on |

The base snippet covers checks 1 through 4 directly. Checks 5 through 8 need project-specific selectors and triggers added to the same `evaluate` call. Check 9 is the project-rule slot.

Filter false positives only when you can explain them: hidden off-canvas content, intentionally overflowing dropdown internals that do not affect the page viewport, or a tap target intentionally part of a larger ancestor hit area. Document the filter so the next run does not re-discover it.

The small-target check must EXEMPT inline text links (per the WCAG 2.5.8 inline exception) or it floods with false positives. The base snippet does NOT do this yet; extend its small-target loop to skip anchors whose computed `display` is `inline` or `inline-block` and that sit in running text, a breadcrumb, or a footer text-link list. Constrain the exemption to anchors on purpose: buttons and menu triggers default to `inline-block` and often live inside an `<li>`, so a broader rule would wrongly exempt genuinely too-small standalone controls. After the extension, every standalone control (button, toggle, CTA, icon button, menu trigger, form control) is still flagged. Minimal conditional at the top of the small-target `for...of` loop (use `continue`, not `return`, or you exit the whole `page.evaluate` callback):

```js
const cs = getComputedStyle(el);
if (el.tagName === "A" && (cs.display === "inline" || cs.display === "inline-block") && el.closest("p, nav[aria-label*='breadcrumb' i], footer")) continue;
```

## Programmatic Content and Markup Sweep

Catches content, markup, and SEO defects a screenshot cannot show: missing canonical, second H1, over-long title, unparseable JSON-LD, image with no alt, orphan page, duplicate id. Run over the BUILT HTML (shipped output, not the dev server) on every route. It complements, does not replace, the Rich Results Test (which validates JSON-LD content against Google's rules); this sweep only checks markup is present and parseable.

```js
import { readFileSync, readdirSync, statSync } from "node:fs";
import { join } from "node:path";

const DIST = process.argv[2] || "dist";
const files = [];
(function walk(d){ for (const e of readdirSync(d)) {
  const p = join(d, e);
  statSync(p).isDirectory() ? walk(p) : p.endsWith(".html") && files.push(p);
}})(DIST);

const decode = (s) => s
  .replace(/&amp;/g, "&").replace(/&lt;/g, "<").replace(/&gt;/g, ">")
  .replace(/&#39;|&apos;/g, "'").replace(/&quot;/g, '"');

const problems = [];
const linkedTo = new Set();
const pages = [];

for (const f of files) {
  const rel = f.replace(DIST, "");
  const s = readFileSync(f, "utf8");
  const robotsTag = (s.match(/<meta\b[^>]*\bname=["']robots["'][^>]*>/i) || [])[0] || "";
  const noindex = /noindex/i.test(robotsTag);
  pages.push(rel.replace(/index\.html$/, "").replace(/\.html$/, "") || "/");

  const h1 = (s.match(/<h1[\s>]/g) || []).length;
  if (h1 !== 1) problems.push(`H1=${h1} ${rel}`);

  const title = decode((s.match(/<title>([^<]*)<\/title>/) || [])[1] || "");
  if (!noindex && (title.length < 50 || title.length > 60)) problems.push(`TITLE ${title.length} ${rel}`);

  const descTag = (s.match(/<meta\b[^>]*\bname=["']description["'][^>]*>/i) || [])[0] || "";
  const desc = decode((descTag.match(/\bcontent=["']([^"']*)/i) || [])[1] || "");
  if (!noindex && (desc.length < 140 || desc.length > 160)) problems.push(`DESC ${desc.length} ${rel}`);

  if (!noindex && !/rel=["']canonical["']/.test(s)) problems.push(`NO-CANONICAL ${rel}`);
  if (!noindex && !/property=["']og:title["']/.test(s)) problems.push(`NO-OG ${rel}`);

  for (const m of s.matchAll(/<img\b[^>]*>/g)) if (!/\salt=/.test(m[0])) problems.push(`IMG-NO-ALT ${rel}`);
  if (/href=["']#["']/.test(s)) problems.push(`DEAD-ANCHOR ${rel}`);

  const ids = [...s.matchAll(/\sid=["']([^"']+)["']/g)].map((m) => m[1]);
  const seen = new Set(), dup = new Set();
  for (const id of ids) { if (seen.has(id)) dup.add(id); seen.add(id); }
  if (dup.size) problems.push(`DUP-ID ${rel}: ${[...dup].join(", ")}`);

  for (const m of s.matchAll(/<script type=["']application\/ld\+json["'][^>]*>([\s\S]*?)<\/script>/g)) {
    try { JSON.parse(m[1]); } catch (e) { problems.push(`BAD-JSONLD ${rel}: ${e.message}`); }
  }

  for (const m of s.matchAll(/href=["'](\/[^"'#?]*)/g)) linkedTo.add(m[1].replace(/\/$/, "") || "/");
}

for (const p of pages) {
  const norm = p.replace(/\/$/, "") || "/";
  if (norm !== "/404" && !linkedTo.has(norm)) problems.push(`ORPHAN ${norm}`);
}

console.log(problems.length ? problems.join("\n") : "NO PROBLEMS");
```

| Flag | Triggers when |
|------|---------------|
| `H1=count` | The count of `<h1` matches is not exactly 1 |
| `TITLE` | Non-noindex page `<title>` length is < 50 or > 60 characters |
| `DESC` | Non-noindex page meta description length is < 140 or > 160 characters |
| `NO-CANONICAL` | Non-noindex page missing `rel="canonical"` |
| `NO-OG` | Non-noindex page missing `property="og:title"` |
| `IMG-NO-ALT` | Any `<img>` without an `alt=` attribute |
| `DEAD-ANCHOR` | Page contains `href="#"` |
| `DUP-ID` | Any `id=` value appears more than once on a page |
| `BAD-JSONLD` | A `<script type="application/ld+json">` block fails `JSON.parse` (reports the error message) |
| `ORPHAN` | Any page (other than `/404`) not in the set of internally linked-to normalized paths |

A run is clean when it prints `NO PROBLEMS`. Tune the length thresholds to the project's title and description bars. The `decode()` helper covers the five most common entities only (`&amp;`, `&lt;`, `&gt;`, `&#39;`/`&apos;`, `&quot;`); a title carrying `&nbsp;`, `&#x27;`, `&#8217;`, or any other entity over-counts against the 50 to 60 length check, so swap in a complete entity decoder (a small library, or read `document.title` in a DOM context) before trusting the length numbers. Node and string parsing only, no browser; pair it with the geometry sweep (which needs a headless browser) for full coverage. Filter a false positive only when explainable (a deliberately noindex utility page, an intentional single-instance id) and document it.

## Per-Check Thresholds

These make the bars measurable; they do not add bars. They align with SKILL.md North Star Targets and the touch-target rule in [ui-ux.md](ui-ux.md).

| Check | Threshold | Source |
|-------|-----------|--------|
| Capture viewports | `1440x900` desktop, `375x812` mobile | [responsive.md](responsive.md) breakpoints table |
| Touch target minimum | 44 by 44 CSS pixels | Rule 29 in [quick-reference.md](quick-reference.md), [ui-ux.md](ui-ux.md) hit targets table |
| Horizontal overflow tolerance | 0 px on mobile, 0 px on desktop | [responsive.md](responsive.md) |
| Duplicate arrow count | 0 across all visible labels | This file, defect table |
| Dropdown centering tolerance | 4 px from trigger center | This file, geometry sweep |
| Focus ring contrast | 3:1 against surface and resting state | [accessibility.md](accessibility.md) contrast targets |
| LCP image declared dimensions | Both `width` and `height` present | Rule 1 in [quick-reference.md](quick-reference.md), [performance.md](performance.md) |

## Acceptance

A polish pass is not complete until:

- The geometry sweep returns zero issues on every audited route at `1440x900` and `375x812`.
- Every defect found in the lookup table is fixed at the right layer (component, page, or design token).
- Re-captured screenshots show the fix and no neighbor regression.

Until then, the work is not done. Do not present an incomplete visual pass as complete.

## See Also

- [audit-workflow.md](audit-workflow.md) for the full multi-page audit procedure
- [components.md](components.md) for component contracts and drift detection
- [accessibility.md](accessibility.md) for focus, contrast, and skip-link patterns
- [responsive.md](responsive.md) for capture viewports and breakpoints
- [performance.md](performance.md) for LCP, font, and image strategy
