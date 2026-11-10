---
title: Performance Deep Dive
purpose: Core Web Vitals discipline with exact thresholds, plus the levers for asset loading, render strategy, hydration, network, caching, fonts, images, INP field attribution, BFCache, and list virtualization. Framework agnostic.
load-when:
  task-keywords: [LCP, INP, CLS, TTFB, performance, bundle, hydration, render strategy, BFCache, preload, fetchpriority, Speculation Rules]
  symptoms: [LCP regression, INP regression, CLS regression, slow page, score dropped, bundle size grew, third-party script slow]
prereq: SKILL.md
related: [lighthouse.md, audit-workflow.md, defects.md, motion.md]
size: ~556 lines
---

# Performance Deep Dive

Lighthouse measures symptoms; this file covers causes and the levers you pull. Every optimization moves work between phases or eliminates it. The cheapest work is the work you do not do.

## The Loading Pipeline

- Network: DNS, TCP, TLS, HTTP (request to first byte). Lever: edge config, resource hints.
- HTML parse: browser discovers sub-resources. Lever: HTML structure, `<link>` order.
- CSS parse plus render-tree build: blocked by stylesheets in `<head>`. Lever: critical CSS.
- JS download plus parse plus execute: blocked by sync scripts. Lever: `defer` / `async` / module strategy.
- First paint, then LCP paint (largest in-viewport element finishes painting).
- Hydration / interactivity: JS attaches handlers, page becomes responsive (TBT/INP).
- Lazy phase: below-the-fold images, non-critical scripts, deferred islands.

## Core Web Vitals

### LCP

LCP is the render time of the largest text block, image, video poster, or background image in the initial viewport. Target: < 2.5s mobile, < 2.0s desktop.

Four phases and their levers:

| Phase | Typical share | Lever |
|-------|---------------|-------|
| TTFB (server response) | 25-40% | Edge caching, server location, route compute |
| Resource load delay | 0-30% | Preconnect, preload, critical-CSS inlining, font preload |
| Resource load duration | 30-60% | Compression, format (AVIF/WebP), responsive sizing |
| Render delay | 0-20% | Render-blocking JS/CSS, font swap, hydration cost |

Identify the LCP element: open DevTools, Performance, capture a load, find the LCP marker (the element is named).

If LCP is an image:

- Serve AVIF as the 2026 default (Baseline since 2024, supported in every evergreen engine). Keep a WebP source for older releases; keep JPEG only as the universal `<img>` fallback. Compression: AVIF q=50-65, WebP q=75-82.
- Use `<picture>` with type-keyed sources.
- Set `width` and `height` (or `aspect-ratio`) to prevent CLS.
- Add `fetchpriority="high"`.
- Never `loading="lazy"` on the LCP image.
- Use `srcset` with at least 3 widths plus `sizes` matching the rendered box.
- Preload only when the image is rendered by client JS (CSR/hydration); a server-rendered `<img>` does not need preload.

If LCP is text:

- Preload the font that renders it: `<link rel="preload" as="font" type="font/woff2" crossorigin>`.
- Set `font-display: swap` so the fallback shows immediately.
- Use `size-adjust`, `ascent-override`, `descent-override`, `line-gap-override` on `@font-face` to match metrics to the fallback and prevent CLS on swap.
- Avoid web fonts entirely for the largest hero text if possible (use a system stack).

If LCP is a video poster: treat as image, add the `poster` attribute, declare dimensions.

Reduce TTFB (see TTFB section): cache HTML at the edge with `s-maxage` plus `stale-while-revalidate`; move compute close to users (edge functions, regional deploy); stream HTML rather than block origin compute on slow data fetches.

### INP

INP measures the slowest interaction during the page lifetime. Target: < 200ms (good), < 500ms (needs improvement). It is dominated by long tasks during the interaction (event handler > 50ms), tasks blocking the next paint, sync layout reads, and large framework re-renders.

Levers:

- Break long tasks with `scheduler.yield()` (fallback `setTimeout(fn, 0)` / `requestIdleCallback`) to chunk work.
- Move expensive work off the main thread (Web Workers, OffscreenCanvas).
- Avoid sync layout thrash: batch reads then writes (read all `getBoundingClientRect()` once, then write).
- Memoize expensive renders (component-level memo/compute primitives per framework, e.g. memo + derived/computed values).
- Use CSS for state when possible (`:hover`, `:active`, `:focus-visible`, `:has()`, `:checked`, popover API) instead of JS state updates.
- Virtualize large lists (see List Virtualization).
- Debounce expensive input computations (search, validation) with a 150-300ms debounce.

INP attribution recipe (field side). Lab Lighthouse cannot meaningfully score INP; attribute from real users with two sampled observers shipped via `sendBeacon`:

- `PerformanceObserver` on entry type `event` gives every interaction's duration, target, and processing time. Filter `duration > 40ms` to keep volume manageable.
- `PerformanceObserver` on `long-animation-frame` (LoAF) gives the frame that paid for the next paint, including the scripts that ran, their source URLs, and forced-style/forced-layout work.
- A web-vitals attribution build packages both: `onINP` returns `attribution` with `targetElement`, `eventTarget` (a CSS selector), `eventType`, `loadState`, and the longest LoAF script `sourceURL`. Ship that, not just the metric value.

```html
<script type="module">
  import { onINP } from 'https://unpkg.com/web-vitals@4/attribution?module';
  onINP((metric) => {
    const a = metric.attribution;
    navigator.sendBeacon('/rum/inp', JSON.stringify({
      value: metric.value,
      rating: metric.rating,
      eventTarget: a.eventTarget,
      eventType: a.eventType,
      loadState: a.loadState,
      longAnimationFrame: a.longAnimationFrameEntries?.[0]?.scripts?.[0]?.sourceURL,
    }));
  }, { reportAllChanges: false });
</script>
```

The two highest-signal columns: `eventTarget` (which control is slow) and `longAnimationFrame.scripts[0].sourceURL` (which script paid the cost). Together they point at the fix.

### CLS

CLS sums layout-shift scores during the page session. Target: < 0.1 (mobile), < 0.05 (desktop). Common sources: images/iframes/embeds without dimensions, late-loading fonts (FOUT), late-injected banners and ads, layout-affecting animations, scrollbar appearance changes.

Fixes:

- Declare `width` and `height` on every `<img>`, `<iframe>`, `<video>`; use `aspect-ratio` for fluid layouts.
- Reserve space for late content with `min-height` or skeletons.
- Cookie/consent banners go below the fold or as a fixed-position overlay, not in document flow.
- For font swap, use `size-adjust` and the `*-override` properties on `@font-face` to size-match the fallback so the swap is invisible.
- Animate only `transform` and `opacity`; never animate layout-affecting properties.
- For dynamic content (search results), set `min-height` on the container so the page does not grow as results arrive.

### TTFB

TTFB is request to first byte received. Target: < 800ms mobile, < 600ms desktop.

- Edge cache HTML where possible (SSG, ISR, edge functions).
- Use a CDN with global PoPs.
- For dynamic pages, stream the response so the first byte arrives before all data is fetched.
- Move compute to the edge.
- Profile the server route for serial DB calls, slow third-party APIs, and cold starts.

## JavaScript Strategy

### Render strategies

The render-strategy decision tree (SSG, ISR, SSR, streaming SSR, CSR, islands, resumability) lives in SKILL.md under `Decision: Render Strategy`. Decide there first, return here for levers.

### Streaming SSR shell-plus-stream envelope

Streaming SSR beats classic SSR (slow TTFB) and CSR (slow LCP) when the page depends on slow data.

- The server flushes a complete HTML shell first (`<head>`, critical CSS, navigation, footer, layout containers); nothing in the shell awaits data.
- Wrap slow regions in Suspense (or the framework's equivalent boundary) as placeholders in the shell.
- As each async data fetch resolves, the server streams an HTML chunk plus a tiny inline script that replaces the placeholder in place, no client round trip.

Rules that make the envelope work:

- The `<head>` must not wait: no data-dependent `<title>`, `<meta>`, or canonical in the shell stream; resolve statically per route or stream as out-of-order chunks.
- Name Suspense boundaries per UX intent (around "Reviews", "Related products"), not per technical boundary.
- The shell is the LCP candidate, not the streamed regions: put the hero image in the shell; everything below the fold can stream.
- Verify with a slow-network capture (DevTools, Slow 3G): the shell paints fast and regions fill in; a blank page until everything resolves means a boundary is misplaced.

### Hydration cost

Hydration cost is roughly bundle parse plus module init plus component constructor plus diff against existing DOM.

- Server components ship no JS at all; use them for static structure.
- Client-component boundaries (the framework's "use client" or equivalent) should be small leaves, not big trees.
- Lazy hydrate below-the-fold islands with a Suspense boundary plus dynamic import or framework helpers.
- Island-by-default frameworks: mark client components per criticality with idle / visible / load directives.
- Resumable frameworks: resumability instead of hydration, components do not re-execute on the client unless the user interacts.
- SSR-plus-selective-client frameworks: head injection helpers plus route-level server data files.
- Partial-hydration island wrappers exist in most SSR frameworks; SSR-plus-reactive-primitive frameworks keep hydration cost low.

### Bundle splitting

Default splits: per route, per dynamic import (lazy-loaded modal, chart, editor), per third party (vendor chunk). Anti-patterns:

- One giant vendor bundle: split heavy libs (chart, editor, video player) so they load only on pages that use them.
- Importing default exports of barrel files (prevents tree-shaking): import named exports directly, `import { foo } from 'lib/foo'`, not `import { foo } from 'lib'`.
- Dev-only code shipped to production: wrap with `process.env.NODE_ENV !== 'production'` (or framework equivalent) so the bundler tree-shakes it out.

### Tree-shaking

- Use ES modules end to end (`"type": "module"` or `.mjs`).
- Mark library `package.json` with `"sideEffects": false` (or list specific files with side effects).
- Avoid importing entire libraries: `import _ from 'lodash'` blocks tree-shaking; use `import debounce from 'lodash/debounce'` or a native replacement.

## Image Strategy

Format priority:

| Pri | Format | When | Note |
|-----|--------|------|------|
| 1 | AVIF | Photographic content | ~50% smaller than JPEG at equal quality |
| 2 | WebP | Fallback for older browsers (Safari < 16) | ~30% smaller than JPEG |
| 3 | JPEG | Final fallback | mozjpeg quality 75-82, progressive=true |
| 4 | PNG | Transparency AVIF/WebP cannot handle (rare) | |
| 5 | SVG | Icons, logos, illustrations | inline when small (< 1 KB), reference when large |

Images inside a hydrated island still must be optimized at the build-time image step, not shipped raw because they happen to live in a component.

Every non-trivial image needs `srcset` and `sizes`:

```html
<picture>
  <source
    type="image/avif"
    srcset="/img/hero-480.avif 480w, /img/hero-960.avif 960w, /img/hero-1440.avif 1440w"
    sizes="(max-width: 640px) 100vw, (max-width: 1024px) 80vw, 1280px"
  />
  <source
    type="image/webp"
    srcset="/img/hero-480.webp 480w, /img/hero-960.webp 960w, /img/hero-1440.webp 1440w"
    sizes="(max-width: 640px) 100vw, (max-width: 1024px) 80vw, 1280px"
  />
  <img
    src="/img/hero-960.jpg"
    width="1440"
    height="900"
    alt="..."
    fetchpriority="high"
    decoding="async"
  />
</picture>
```

Do not ship a high-resolution master inline for a thumbnail. Serve small responsive `srcset` widths (for example 640, 960, 1280) for the inline thumbnail; load the full-resolution master only when the user opens the fullscreen view. The inline image and the fullscreen image are two sources for the same asset.

Lazy loading:

- `loading="lazy"` on every `<img>` and `<iframe>` below the fold.
- `<iframe loading="lazy">` is the cheapest single win for embeds: a typical video embed costs 500 KB to 1 MB plus JS execution; deferring it past first paint can recover 10 to 20 Lighthouse points on a content page.
- Never `loading="lazy"` on the LCP image.
- For video, use `preload="metadata"` and lazy-load the actual video on user interaction.

Aspect-ratio reservation for fluid layouts (reserves space, prevents CLS, no hardcoded pixels):

```css
.hero-image {
  aspect-ratio: 16 / 9;
  width: 100%;
  height: auto;
}
```

## Font Strategy

Web fonts are one of the largest regressions any surface can absorb. Budget them.

- Maximum 2 font families per page (1 display plus 1 body, or 1 family with multiple weights).
- Maximum 4 weights total.
- Subset to the languages you actually serve (Latin, Latin-extended, Cyrillic, etc.).
- Use variable fonts when available; one variable file replaces 3-5 static weights.
- Self-host; a hosted font CDN adds an extra DNS lookup, self-hosted is faster.

Preload the critical body font, woff2 only:

```html
<link rel="preload" as="font" type="font/woff2" href="/fonts/body-regular.woff2" crossorigin />
```

Preload the asset the build actually emits: resolve the fingerprinted href from the build manifest, do not hardcode the filename, or the preload silently does nothing and LCP regresses with no error. Same rule for any preloaded image or script.

```css
@font-face {
  font-family: 'BrandBody';
  src: url('/fonts/body-regular.woff2') format('woff2');
  font-weight: 400;
  font-style: normal;
  font-display: swap;
  /* CLS-prevention: match metrics to a fallback */
  size-adjust: 100%;
  ascent-override: 90%;
  descent-override: 22%;
  line-gap-override: 0%;
}
```

- `font-display: swap` for body fonts: fallback then swap (slight FOUT, no FOIT).
- `font-display: optional` for display fonts on poor networks: shows fallback only if the font is not ready in 100ms. Best for LCP.
- `font-display: block` is forbidden in production (causes invisible text / FOIT).

## CSS Strategy

Critical CSS: the first 14 KB of HTML fits in one TCP round trip; inline the CSS needed to render the first viewport there and defer the rest. Two approaches:

- Build-time critical-CSS extraction (works for static pages).
- Component-scoped CSS that ships only what is used (CSS Modules, zero-runtime CSS-in-JS, JIT utility CSS); works for any framework.

Defer non-critical CSS:

```html
<link
  rel="preload"
  href="/css/below-fold.css"
  as="style"
  onload="this.rel='stylesheet'"
/>
<noscript><link rel="stylesheet" href="/css/below-fold.css" /></noscript>
```

Alternative:

```html
<link rel="stylesheet" href="/css/below-fold.css" media="print" onload="this.media='all'" />
```

Avoid CSS bloat:

- Run a production purge step.
- Avoid loading entire UI-library themes; import only what is used.
- Use logical properties (`margin-inline`, `padding-block`) so RTL works without duplicating rules.

## Network Strategy

### Resource hints

Use sparingly; every hint costs a connection slot.

| Hint | Cost / use |
|------|------------|
| `dns-prefetch` | Lowest cost, resolves DNS only; for low-priority third parties |
| `preconnect` | DNS + TCP + TLS; for high-priority third parties (your CDN, your API origin) |
| `preload` | Forces an early fetch; only for critical resources (LCP image, critical font) |
| `prefetch` | Low-priority hint for next-page resources; for likely next navigation |
| `modulepreload` | For ES module dependencies |

Anti-patterns: preloading every font weight (pick one or two); preconnecting to 10+ origins (pick three); preloading images already discovered in the HTML (wasteful).

### fetchpriority beyond images

`fetchpriority` hints the browser's resource scheduler and applies to `<link>`, `<script>`, `<img>`, and `fetch()`.

- `fetchpriority="high"` on the critical CSS preload (`<link rel="preload" as="style" fetchpriority="high">`) so it outranks images and fonts.
- `fetchpriority="high"` on the hero font preload and on the critical JS module the LCP depends on.
- `fetchpriority="low"` on every third-party script tag, every below-the-fold image preload, and any analytics/ads `fetch()`.
- Pair `<script async fetchpriority="low">` for third-party tags and audit the Network panel "Priority" column to confirm the hint took effect.

### Speculation Rules API

The page tells the browser which next-navigations to prefetch or prerender; the browser runs a hidden background page and swaps it in on click.

```html
<script type="speculationrules">
{
  "prerender": [
    { "source": "list", "urls": ["/pricing", "/docs"], "eagerness": "moderate" }
  ],
  "prefetch": [
    { "source": "document", "where": { "href_matches": "/blog/*" }, "eagerness": "conservative" }
  ]
}
</script>
```

- `prerender` runs the next page (HTML, CSS, JS) in the background; instant navigation but costly, use for the two or three most likely next pages.
- `prefetch` downloads bytes but does not execute; cheaper, use where prerender would be wasteful.
- `eagerness` `eager`: fire as soon as the rule is parsed (one or two near-certain pages only).
- `eagerness` `moderate` (default for hover/focus): fire on hover or focus of a matching link.
- `eagerness` `conservative`: fire on `pointerdown`; cheapest hint with the most user-intent signal.

Anti-patterns: prerender every link (burns CPU/bandwidth, blocks BFCache); prerender pages with auth side effects in the loader; prerender pages that mutate analytics on load (double-count).

### Early Hints (HTTP 103)

HTTP 103 Early Hints let the CDN send `Link: <...>; rel=preload` headers before the origin returns the HTML, so the browser preloads critical CSS, hero image, and key font during otherwise dead TTFB.

```http
HTTP/1.1 103 Early Hints
Link: </css/critical.css>; rel=preload; as=style
Link: </fonts/body.woff2>; rel=preload; as=font; type=font/woff2; crossorigin
Link: </img/hero.avif>; rel=preload; as=image; fetchpriority=high
```

The `200` follows with the full HTML. The win is 200 to 400ms of LCP recovered on slow-origin pages and zero on already-fast pages; prioritise where TTFB dominates LCP.

### BFCache hygiene

A BFCache-qualifying page returns in under 100ms; a non-qualifying page pays the full reload cost. Rules to qualify:

- Replace `unload` listeners with `pagehide` and `visibilitychange`; any `unload` listener disqualifies the page. The Lighthouse `no-unload-listeners` audit catches it; verify third-party scripts too.
- Audit `Cache-Control: no-store` on HTML responses; `no-store` disqualifies BFCache. Reserve it for genuinely sensitive routes.
- Close open `WebSocket` and `IndexedDB` transactions on `pagehide`; an open connection disqualifies the page in some engines.
- Avoid `Cache-Control: no-cache` plus a redirect; the round trip defeats the purpose.

Verify with the `NotRestoredReasons` API: in the `pageshow` handler, `event.notRestoredReasons` returns the structured list of blockers (with `src`, `reason`, `url`). Ship it through RUM.

```html
<script type="module">
  window.addEventListener('pageshow', (event) => {
    if (event.persisted) return; // BFCache hit; nothing to log
    const reasons = (event.notRestoredReasons || {}).reasons || [];
    if (reasons.length) {
      navigator.sendBeacon('/rum/bfcache', JSON.stringify({
        url: location.pathname,
        reasons,
      }));
    }
  });
</script>
```

### Compression Dictionaries

The `Use-As-Dictionary` and `Available-Dictionary` headers (shared Brotli) let a repeat visitor reuse a previous response's bytes as the dictionary for the next compression.

- Serve the first response with `Use-As-Dictionary: "<id>"`; the browser stores it.
- On the next response for the same resource family, the browser sends `Available-Dictionary: <id>` and the origin (or CDN) emits a shared-Brotli body keyed to that dictionary.
- Wire bytes drop by 70 to 95 percent on small updates.

Use for high-traffic, slowly-changing assets (framework runtime bundles, design-system CSS, large JSON config blobs); verify CDN support and fall back to standard Brotli where unsupported. Pure win for repeat visitors, zero cost for first-time visitors.

### ESM-only upgrade path

On an evergreen-only target:

- `<script type="module">` everywhere; drop the nomodule pair.
- import maps (`<script type="importmap">`) resolve bare specifiers in the browser without a bundler; pair with a CDN serving stable, versioned modules.
- Module workers (`new Worker(url, { type: 'module' })`) share code with the main thread via `import` rather than `importScripts`.
- Set `browserslist` to evergreen-only (`['>0.5%', 'not dead', 'not op_mini all', 'not ie 11']`) so the bundler stops emitting legacy transforms; typical savings 15 to 30 KB gzipped on the initial bundle.

### Transport and compression

- HTTP/2 multiplexing means many small requests are fine; do not over-bundle.
- HTTP/3 (QUIC) reduces handshake latency; most modern CDNs support it, verify with `curl --http3`.
- Brotli for static assets (~20% better than gzip on text); gzip as fallback; no compression for already-compressed assets (images, video, woff2).

### Caching

| Asset | Cache-Control |
|-------|---------------|
| Versioned static (`/static/abc123.js`) | `public, max-age=31536000, immutable` |
| Versioned image | `public, max-age=31536000, immutable` |
| HTML | `public, max-age=0, s-maxage=300, stale-while-revalidate=86400` |
| API JSON (read-mostly) | `public, max-age=30, s-maxage=60, stale-while-revalidate=300` |
| API JSON (private / per-user) | `private, max-age=0, must-revalidate` |
| Service worker | `no-cache` |

## Third-Party Strategy

Third-party scripts are the silent assassin of Lighthouse scores. Treat them as a budget item.

Audit each third party: what it does (analytics, ads, chat, embed, A/B test, error tracking), bytes (compressed), main-thread time (Lighthouse `third-party-summary`), whether it blocks rendering, whether removal is acceptable.

Mitigations:

- Remove first: most pages have at least one third party that contributes nothing measurable.
- Lazy-load on idle: wrap with `requestIdleCallback` or load after the `load` event.
- Lazy-load on interaction: chat widgets load on hover/click of the trigger, not on page load.
- Move to a worker: run analytics/ads on a Web Worker (worker proxy tooling) so they do not compete for the main thread.
- Self-host: a tag manager can be proxied through your own origin to remove the extra connection.
- Use a server-side equivalent: page-view analytics via a measurement protocol; ad attribution server-side; A/B tests edge-side.

Budget, enforced in CI via Lighthouse `third-party-summary`:

- At most 5 distinct third-party origins per page.
- Total main-thread time at most 250ms on the mobile profile, ideally under 150ms. A page outside the budget is a defect, not a tradeoff.

Run the monthly audit on the first Monday: fresh Lighthouse capture of the top three traffic routes, open `third-party-summary`, list every script with its main-thread cost, and decide keep / defer / sandbox / remove for each.

Load mode by intent:

| Script intent | Loads on | How |
|---------------|----------|-----|
| Analytics (page-view ping) | `requestIdleCallback` after `load` | `<script defer fetchpriority="low">`, tag manager or first-party proxy |
| Error tracking | Page boot | `<script async fetchpriority="low">`, must capture early errors, cannot fully defer, keep and budget it |
| A/B test flicker-prevention | Synchronous in `<head>` | `<script>` with a strict timeout; render-blocking flicker-prevention is a known LCP killer, prefer server/edge-side experimentation |
| Chat / support widget | User intent (hover or click of trigger) | Dynamic `import()` in the trigger handler; deferring saves 200 to 500 KB |
| Display ads | After main content paints | `requestIdleCallback` plus IntersectionObserver, lazy per slot within one viewport |
| Heavy analytics / marketing tags | Web Worker (worker proxy) | Move the tag off the main thread via a worker-proxy runtime; verify it tolerates worker context (no DOM access) |

Sandbox the embed. Third-party embeds (video players, social timelines, maps, share buttons) ship inside an `<iframe sandbox>` with the minimum `allow-*` flags:

```html
<iframe
  src="https://video-host.invalid/embed/<id>"
  title="<descriptive title>"
  loading="lazy"
  sandbox="allow-scripts allow-same-origin allow-presentation"
  allow="autoplay; fullscreen; picture-in-picture"
  referrerpolicy="strict-origin-when-cross-origin"
></iframe>
```

The `sandbox` attribute denies storage, navigation, and pop-ups by default and re-grants only what the embed needs; the `allow` attribute (Permissions Policy on the frame) gates powerful APIs. Full embed-as-host playbook in embed-patterns.md; cross-origin isolation and CSP for embedded scripts in security.md.

Isolate analytics on a first-party subdomain with a separate cookie scope.

## List Virtualization and Infinite Scroll

A list that ever exceeds 100 to 200 items, or any list with rich children (cards with images, charts), is a virtualization candidate. Cost model: 1000 DOM nodes is heavy, 10000 is broken; Lighthouse `dom-size` fails at 1500. Virtualize when the list can grow past 200 items, each row has more than 3 DOM nodes, or the list is the LCP or near it.

Two framework-agnostic strategies:

- Windowing libraries (framework-native or standalone) render only viewport rows plus a small overscan buffer and recycle DOM nodes; best for very long lists, complex rows, or precise row-height control.
- CSS `content-visibility: auto` with `contain-intrinsic-size`: the browser skips render and layout for off-screen blocks, no JS; best for medium-long lists of similar-sized blocks.

```css
.card {
  content-visibility: auto;
  contain-intrinsic-size: 0 320px; /* width auto, expected height 320px */
}
```

`contain-intrinsic-size` must be close to the real rendered height (use the median measured height) or the scrollbar jumps as cards render.

Infinite scroll:

- IntersectionObserver on a sentinel at the list's end triggers the next-page fetch (cheap, debounced, native).
- API uses cursor pagination (`?cursor=<opaque_id>&limit=20`), not offset (`?offset=400`); cursors are stable across inserts, offsets are not.
- Cache loaded pages in memory keyed by cursor; back-navigation should not refetch.
- Persist scroll position and loaded-page set in `history.state` on every page load; on back navigation (`navigation.type === 'back_forward'` or BFCache miss) restore both.
- Set `<html style="scroll-behavior: auto">` for the restore (smooth scroll defeats it).

Accessibility of virtualized rows (full treatment in accessibility.md): set `role="grid"` or `role="list"` on the container; set `aria-rowcount="<total>"` (the full total, not the rendered count) on the container and `aria-rowindex="<n>"` (1-indexed position in the full list) on each rendered row; keyboard up/down at a window edge must trigger a scroll that loads the next row before focus moves to it, or focus disappears into a recycled node.

## Performance Budgets in CI

Lighthouse CI assertions are covered in lighthouse.md. Add a bundle-size guard.

Framework budgets:

```json
"budgets": [
  { "type": "initial", "maximumWarning": "120kb", "maximumError": "160kb" },
  { "type": "anyComponentStyle", "maximumWarning": "20kb" }
]
```

Granular file-size checks (CI fails the PR if budgets are exceeded):

```json
{
  "size-limit": [
    { "path": "dist/main.js", "limit": "90 KB" },
    { "path": "dist/main.css", "limit": "25 KB" }
  ]
}
```

## Real-User Monitoring

Lab numbers are necessary but not sufficient. Instrument production:

```html
<script type="module">
  import { onLCP, onINP, onCLS, onFCP, onTTFB } from 'https://unpkg.com/web-vitals@4?module';
  const send = (m) => navigator.sendBeacon('/rum', JSON.stringify(m));
  onLCP(send); onINP(send); onCLS(send); onFCP(send); onTTFB(send);
</script>
```

Track p75 (the metric used for ranking signals) over a 28-day window; alert when p75 crosses the threshold.

## Common Performance Mistakes

- Hero carousel: each slide is a hero candidate so LCP is unpredictable; replace with one strong hero or use IntersectionObserver to load slides only when active.
- Animated gradient backgrounds: continuous repaint; use a static gradient or a CSS-only animation that runs once.
- Background videos as decoration: heavy bytes, heavy CPU, distracting; replace with a still image or a short looping low-bitrate WebM.
- Massive sprite sheets: decoding cost; use individual SVGs or a font-icon system.
- Auto-playing audio: the browser blocks it and the script keeps trying.
- Overusing `requestAnimationFrame`: each rAF runs every frame; cancel when off-screen.
- Long-running `setInterval`: eats battery; replace with on-demand updates.
- Synchronous storage reads in the render path: `localStorage.getItem` is sync and blocking; cache to a variable.

## See Also

- [lighthouse.md](lighthouse.md) for score-driven audit fixes
- [audit-workflow.md](audit-workflow.md) for the capture-and-evidence loop
- [responsive.md](responsive.md) for layout integrity
- [motion.md](motion.md) for animation costs
