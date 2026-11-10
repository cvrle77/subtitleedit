---
title: Lighthouse Mastery
purpose: Score-driven audit playbook mapping every common failing Lighthouse audit to root cause and concrete fix, plus CI gate wiring, phantom-failure triage, and field-vs-lab cross-checks.
load-when:
  task-keywords: [lighthouse, audit, lighthouse-ci, performance, errors-in-console, image-size-responsive, diagnostic Insights, phantom failure, CSP, SRI, gate, CrUX]
  symptoms: [score dropped, Lighthouse score drop, LCP regression, CLS regression, phantom dev failure, errors-in-console, image too small on retina, stale SRI beacon]
prereq: SKILL.md
related: [performance.md, accessibility.md, seo.md, debug-recipes.md]
size: ~384 lines
---

# Lighthouse Mastery

Framework-agnostic playbook for hitting and holding the top of the Lighthouse curve. Every audit below is reachable from `lighthouse --output=json` or the DevTools Lighthouse panel.

## Score Targets

| Category | Target |
|----------|--------|
| Performance (mobile) | >= 95 |
| Performance (desktop) | >= 99 |
| Accessibility | 100 |
| Best Practices | 100 |
| SEO | 100 (n/a if `noindex`) |

- A 99 hides one or two regressions; only 100 means "no audits failed". Demand 100 for Accessibility, Best Practices, and SEO unless an audit is genuinely inapplicable.
- Hold Performance at 95 mobile and 99 desktop. A relaxation (a heavy interactive tool that legitimately ships more JS) must be a recorded, justified exception, never a default.

## How Lighthouse Computes Performance

Weighted sum of lab metrics, scaled against the public-web distribution (Lighthouse 11+):

| Metric | Weight | Measures |
|--------|-------:|----------|
| Largest Contentful Paint (LCP) | 25% | When the largest visible element finished painting |
| Total Blocking Time (TBT) | 30% | Time the main thread was blocked by long tasks during load |
| Cumulative Layout Shift (CLS) | 25% | Total visible layout shift during the page view lifetime |
| First Contentful Paint (FCP) | 10% | When the first text or image painted |
| Speed Index (SI) | 10% | How quickly content visually populated |

- TBT (30%) is the single largest lever; reducing main-thread time during load yields the largest gains.
- LCP and CLS together are 50% of the score: the LCP element and any late-arriving content are the two highest-leverage targets.
- FCP and SI together are only 20%; optimizing them without fixing TBT/LCP/CLS plateaus.
- INP is reported but does not affect the lab Performance score. It does affect real-user CrUX data and Search Console.

### Scored audits versus diagnostic Insights

Insights (network dependency tree, LCP breakdown, critical request chains) are leads, not gates. A long chain or non-zero critical path is worth fixing ONLY if it actually delays the LCP element. If LCP already completes before the chain ends, do nothing.

## Run Lighthouse Properly

### Production build only, never the dev server

A dev server actively MANUFACTURES phantom failures that do not exist in production:

| Dev-server artifact | Audit it falsely trips |
|---------------------|------------------------|
| Dev toolbar/overlay injecting a low-quality link or extra DOM | SEO link-text, extra DOM nodes |
| Hot-reload WebSocket holding the connection open | BFCache fail, `errors-in-console` |
| Unminified, unbundled, source-mapped assets | depressed Performance, misleading `unused-javascript` / `unused-css` |

Score a production build via the framework's own preview server or the live edge, never the dev server. See debug-recipes.md Lighthouse flake triage.

```text
<production build command>
<start production server> --port 3001 &
npx lighthouse "http://localhost:3001/<path>" \
  --output=json --output=html \
  --output-path=/tmp/lh-mobile \
  --chrome-flags="--headless=new --no-sandbox --disable-gpu" \
  --only-categories=performance,accessibility,best-practices,seo \
  --quiet
```

### Preview is closer than dev, but is still not the edge

Local preview lacks the host response headers (cache, security, compression) and is not the artifact users hit. Before declaring a live score fixed:

- Confirm the deploy landed: the expected asset hash or a content marker is present in the served HTML.
- Measure the deployed URL or read CrUX.
- The user's live/field number outranks any local run.

### Mobile and desktop, independently

```text
# Mobile (default preset: simulated 4G, Moto G Power, 5.6x CPU throttle; 4x CPU + slow 4G profile)
npx lighthouse "http://localhost:3001/<path>" --output=json --output-path=/tmp/lh-mob.json \
  --chrome-flags="--headless=new --no-sandbox --disable-gpu" --quiet

# Desktop (preset: native viewport, 0x CPU throttle, 40ms RTT, no throttle)
npx lighthouse "http://localhost:3001/<path>" --output=json --output-path=/tmp/lh-desk.json \
  --preset=desktop --chrome-flags="--headless=new --no-sandbox --disable-gpu" --quiet
```

Most regressions show up on mobile first.

### Pin Chrome, take the median

- Local Chrome updates can shift scores by 2-5 points. Pin Chrome for CI (a managed-browsers fetch or a test runner's bundled Chromium) and set `CHROME_PATH` explicitly.

```bash
export CHROME_PATH="$(find ~/Library/Caches/ms-playwright -name 'Google Chrome for Testing' -type f 2>/dev/null | head -1)"
```

- Natural variance is 2-4 points per run. Run at least 3 times and take the median (CI `numberOfRuns: 3` or `5` is standard).
- Pass `--throttling-method=devtools` (or `provided`) only when comparing against real network conditions; default `simulated` is fine for relative regression checks.

## Failing Audit to Fix Map

### Performance audits

| Audit (id) | Symptom and root cause | Fix |
|-----------|------------------------|-----|
| `largest-contentful-paint-element` | LCP element loads late (image) | Add `fetchpriority="high"`, declare `width`/`height`, serve responsive `srcset`, prefer AVIF/WebP, never lazy-load it |
| `largest-contentful-paint-element` | LCP element loads late (text) | Load its font with `font-display: swap` and preload it; pre-render the section server-side |
| `render-blocking-resources` | Synchronous `<link rel="stylesheet">` or head `<script>` blocks first paint | Inline critical CSS (under 14 KB), defer the rest with `<link rel="preload" as="style" onload="this.rel='stylesheet'">` or `media="print" onload="this.media='all'"`; move scripts to `defer` or `async` |
| `unused-css-rules` | > 20 KB unused CSS (global stylesheet, vendor framework CSS) | Run a production CSS purge, split CSS by route, inline only above-the-fold rules |
| `unused-javascript` | > 40 KB unused JS (whole-module imports) | Use named imports; replace heavy libs with native APIs (`Intl.DateTimeFormat` for date formatting, ES built-ins for utility helpers); audit the bundle with a source-map analyzer |
| `total-byte-weight` | Page exceeds ~1.6 MB total | Compress images (AVIF/WebP), code-split, treeshake, drop polyfills for evergreen browsers (`browserslist: ['>0.5%', 'not dead', 'not op_mini all']`) |
| `efficient-animated-content` | Animated unoptimized GIF | Replace with `<video autoplay muted loop playsinline preload="metadata">` serving WebM + MP4, or animated WebP/AVIF |
| `modern-image-formats` | Legacy JPEG/PNG served | Add an AVIF/WebP step; serve via `<picture>` with `<source type="image/avif">`, `<source type="image/webp">`, then `<img>` fallback |
| `uses-optimized-images` | Images larger than necessary, no build-step compression | Run lossy compression (JPEG quality 75-82, lossy PNG, AVIF quality 50-65) and strip metadata |
| `uses-responsive-images` | Image larger than its rendered box, one size for all viewports | Add `srcset` with at least 3 widths (e.g. 480w, 960w, 1440w) and an explicit `sizes`. See the DPR arithmetic below |
| `offscreen-images` | Below-the-fold images load eagerly | Add `loading="lazy"` to all below-the-fold `<img>`; never lazy-load the LCP element |
| `uses-text-compression` | Text assets uncompressed | Enable Brotli at the edge with gzip fallback; verify with `curl -H "Accept-Encoding: br,gzip" -I` |
| `uses-rel-preload` | Critical request discovered late (font, hero, critical script) | Add `<link rel="preload" as="font" type="font/woff2" crossorigin>` or `as="image"` for the LCP image; limit preloads to genuinely critical resources |
| `font-display` | Invisible text during font load (`font-display` defaults to `block`) | Set `font-display: swap` on body fonts and `font-display: optional` on display fonts when LCP risk exists |
| `uses-long-cache-ttl` | Static assets cached < 1 year | Static immutable assets get `Cache-Control: public, max-age=31536000, immutable`; HTML gets `s-maxage=300, stale-while-revalidate=86400` or similar |
| `legacy-javascript` | Polyfills shipped to evergreen browsers | Set `target: 'es2020'` or higher; use module/nomodule only if actively supporting legacy |
| `bootup-time` | JS execution > 2s on mobile | Code-split, defer non-critical hydration (islands, lazy hydration), move heavy work to web workers |
| `mainthread-work-breakdown` | Long tasks > 50ms | Split bundles, defer hydration, batch reads/writes, move to `requestIdleCallback` |
| `dom-size` | DOM > 1500 nodes | Virtualize lists with 50+ items, lazy-render off-screen sections, strip dead containers |
| `third-party-summary` | Third parties take > 250ms main thread | Audit and remove; lazy-load chat via `requestIdleCallback`; first-party proxy for analytics; move third-party JS to a worker |
| `cumulative-layout-shift` | CLS > 0.1 (undimensioned media, late fonts, late-injected banners/ads) | Declare width/height on every `<img>`, `<iframe>`, `<video>`; reserve space with `aspect-ratio`; use `font-display: optional` + `size-adjust` to prevent FOUT shifts; never inject above-the-fold content after first paint |
| `non-composited-animations` | Animating `width`, `height`, `top`, `left`, `margin` | Refactor to `transform` (`translate3d`, `scale`) and `opacity` |
| `uses-passive-event-listeners` | `touchstart`/`wheel` listeners block scroll | Add `{ passive: true }` to scroll-side listeners |
| `no-document-write` | `document.write` detected (legacy third party) | Remove; replace with async DOM manipulation |

#### Responsive-image arithmetic (image-size-responsive and uses-responsive-images)

Both fail when the largest `srcset` candidate is smaller than rendered CSS width times device pixel ratio (DPR). The arithmetic IS the fix:

- largest demand = (max CSS width from `sizes`) times (max DPR, usually 2 or 3).
- A 360px box at 2x needs a 720w candidate.
- For an LCP/hero image, emit up to about 2x the maximum layout width.

### Accessibility audits

Lighthouse runs the axe-core ruleset; failing any rule drops the score below 100 (binary on most rules).

| Audit | Root cause | Fix |
|-------|-----------|-----|
| `color-contrast` | Foreground/background below 4.5:1 (or 3:1 for large text) | Darken/lighten one side; verify the `getComputedStyle` ratio in DevTools; test light AND dark modes independently |
| `image-alt` | `<img>` without `alt` | Add `alt="..."`; decorative images get `alt=""` (empty string, not missing) |
| `label` | Form control without label | Wrap with `<label>` or use `for`/`id`; `aria-label` only when a visible label is genuinely impossible |
| `link-name` | Link with no discernible name | Provide visible text or `aria-label`; icon-only links must have `aria-label` |
| `button-name` | Button with no discernible name | Same as link-name |
| `aria-required-attr` / `aria-valid-attr-value` | Invalid ARIA usage | Validate against the ARIA 1.2 spec; prefer native semantics over ARIA |
| `heading-order` | Skipped heading level | Sequential h1 -> h2 -> h3; never use a heading for styling |
| `html-has-lang` | Missing `<html lang="...">` | Set the document language |
| `html-lang-valid` | Invalid lang code | Use a valid BCP 47 code (`en`, `en-US`, `de-DE`) |
| `meta-viewport` | Missing or zoom-disabled viewport | `<meta name="viewport" content="width=device-width, initial-scale=1">`; never `user-scalable=no` or `maximum-scale=1` |
| `tabindex` | Positive tabindex (`tabindex="3"`) | Natural document order; `tabindex="0"` and `tabindex="-1"` are fine, positive values break tab flow |
| `duplicate-id` | Two elements share the same `id` | Derive a per-instance unique id (a unique-id generator) and namespace child ids under it. See components.md |
| `bypass` | No skip link | Add `<a href="#main" class="sr-only-focusable">Skip to content</a>` as the first focusable element |
| `aria-hidden-focus` | Focusable element inside `aria-hidden` | Do not focus into hidden subtrees; move focus or remove `aria-hidden` |
| `frame-title` | `<iframe>` without `title` | Provide a `title` describing the frame's purpose |
| `list` | `<ul>`/`<ol>` with non-`<li>` children | Restructure markup |
| `td-headers-attr` / `th-has-data-cells` | Data table without header association | Use `<th scope="col">` and `<th scope="row">`; for complex tables use `headers="..."` |

#### CSS delivery can silently flip a contrast pass

A change to HOW CSS is delivered (inlining, bundling, chunk/concat order, a build flag) can reorder the cascade and silently flip a computed color or link style for rules of equal specificity, with zero authored-rule change.

- Check: after ANY CSS-delivery change, re-run axe / the Accessibility audit on the FULL set of routes (not just the changed page) and eyeball both audit viewports.

### Best Practices audits

| Audit | Cause | Fix |
|-------|-------|-----|
| `is-on-https` | Page or sub-resource over HTTP | Force HTTPS at the edge with HSTS; audit mixed content (`https://` page loading `http://` sub-resources) |
| `errors-in-console` | JS errors at load | Fix every error; Lighthouse fails on any console error |
| `image-aspect-ratio` | Distorted aspect ratio | Honor the natural ratio or use `object-fit: cover/contain` |
| `image-size-responsive` | Natural size much smaller than rendered | Serve a higher-resolution source (see DPR arithmetic above) |
| `notification-on-start` | Notification permission requested without a user gesture | Move to a user-initiated trigger |
| `geolocation-on-start` | Geolocation requested without a user gesture | Move to a user-initiated trigger |
| `paste-preventing-inputs` | `onpaste="return false"` | Remove it; users must be able to paste |
| `inspector-issues` | DevTools-flagged issues | Open the Issues panel; fix each |
| `csp-xss` | Missing or weak CSP | Add a Content-Security-Policy: per-request nonces for inline scripts on a dynamic server, build-time script hashes on static hosting; treat `'unsafe-inline'` for `script-src` as an explicit fallback only; keep `object-src`, `base-uri`, `frame-ancestors` strict (see below) |
| `csp-trusted-types` | CSP set but Trusted Types not enforced | Add `require-trusted-types-for 'script'` and `trusted-types <policy-name>`; this blocks DOM-XSS sinks (`innerHTML`, `outerHTML`, `document.write`, script `src`) unless the value is a `TrustedHTML`/`TrustedScript`/`TrustedScriptURL` from a named policy. Route raw sink calls through one sanitizing policy. See security.md |
| `sri` | Third-party script without integrity | For any third-party `<script src>` or `<link rel="stylesheet" href>` you cannot self-host, add `integrity="sha384-<hash>" crossorigin="anonymous"` and pin to a specific versioned URL (SRI does not work against `latest` tags). See the beacon note below |
| `coop-coep-corp` | Cross-origin isolation headers missing | Set `Cross-Origin-Opener-Policy: same-origin`, `Cross-Origin-Embedder-Policy: require-corp`, `Cross-Origin-Resource-Policy: same-origin` on first-party responses (required for `SharedArrayBuffer`, high-precision `performance.now()`, `OffscreenCanvas` in a worker); every cross-origin sub-resource must opt in via `Cross-Origin-Resource-Policy: cross-origin` or CORP-equivalent CORS |
| `permissions-policy` | Powerful APIs available to every origin | Gate `geolocation`, `camera`, `microphone`, `payment`, `usb`, `accelerometer`, `gyroscope`, `magnetometer` via the `Permissions-Policy` header; default to deny (`Permissions-Policy: geolocation=(), camera=(), microphone=(), payment=(), usb=()`) and opt in only origins that need it (`geolocation=(self "https://maps.your-domain")`); same rule for `<iframe allow="...">` |
| `valid-source-maps` | Source maps not served or invalid | Ship production source maps to your error tracker via its private upload step; never publish `.map` files alongside the JS on the public CDN. Either upload at build time (no public reference) or restrict the `SourceMap:` header / `//# sourceMappingURL` comment to error-tracker IP ranges via an edge rule. The public response stays unsourced |
| `no-unload-listeners` | `unload` event listener | Replace with `pagehide` or `visibilitychange` |
| `deprecations` | Deprecated API used | Replace per the deprecation message |

#### errors-in-console from a third-party beacon: two opposite causes

| Cause | Mechanism | Reality |
|-------|-----------|---------|
| Client false positive | An ad/privacy/DNS blocker in the audit profile blocks it: `net::ERR_BLOCKED_BY_CLIENT` | Production is fine |
| Real ships-to-everyone bug | An edge platform auto-injects the third-party tag with a pinned SRI `integrity` hash + `crossorigin`; SRI on a cross-origin script forces CORS-mode fetch. When the vendor rolls the asset forward, the pinned hash/version goes stale and the load fails with `net::ERR_FAILED` / no `Access-Control-Allow-Origin`, tripping `errors-in-console` for every visitor | Breaks for all |

- Discriminator: reproduce in a clean headless run. If it vanishes, it was the client. If it reproduces every run, inspect the rendered DOM for an `integrity` + `crossorigin` pair on an auto-injected third-party script. A direct fetch of the asset (`curl -I`) still returns a valid `Access-Control-Allow-Origin`, so a header-only check passes and misses it: the failure is the stale pinned hash under CORS-mode page load, not a missing CDN header.
- Fix the real one by self-injecting the plain vendor tag (no `integrity`, no `crossorigin`, no pinned version) so it loads no-cors. Most edge auto-injectors skip injection when the tag is already present, so the plain self-injected tag pre-empts the broken pinned one.

#### CSP and framework hydration

A strict `script-src 'self'` (no nonce, no inline) is the single most common way to silently break a modern site. Island, partial, and resumable hydration emit small inline bootstrap scripts; counters, facades, and disclosure widgets do too. Block them and two things fail at once: the components never hydrate (the page looks fine but nothing is interactive), and `errors-in-console` fails on the CSP violation logs, so you cannot reach 100.

- Dynamic server: mint a per-request nonce; add it to every inline script tag and to `script-src 'nonce-...'`.
- Static hosting (no per-request server): prefer build-time script HASHES. Compute a `sha256` (or `sha384`/`sha512`) hash of each enumerable inline script and list them in `script-src 'sha256-...'`; hashes are strictly safer than `'unsafe-inline'` and work on a static host. Fall back to `'unsafe-inline'` only when the hash set is impractical to enumerate. Either way keep everything else strict: `object-src 'none'`, `base-uri 'self'`, `frame-ancestors 'none'`, and scope `frame-src` and `img-src` to the embeds you actually use.
- Verify in the SHIPPED headers, not the source config. A headers file or rule can be transformed or dropped by the build; check the built output and the live response (`curl -I`).

### SEO audits

| Audit | Cause | Fix |
|-------|-------|-----|
| `document-title` | Missing `<title>` | Add a unique, intent-matching title of 50-60 chars |
| `meta-description` | Missing `<meta name="description">` | Add a unique 140-160 char description with the primary intent term and a value proposition |
| `http-status-code` | Non-200 status | Fix the route |
| `link-text` | Generic link text ("click here", "read more") | Replace with descriptive anchor text |
| `crawlable-anchors` | Anchors with `javascript:void(0)` or no `href` | Use real `href` values; SPA link components should still emit href |
| `is-crawlable` | `noindex` or robots block | Remove the block on indexable pages |
| `robots-txt` | robots.txt malformed or unreachable | Validate; ensure it returns 200 and references the sitemap |
| `image-alt` | Missing alt | See Accessibility |
| `hreflang` | Invalid hreflang | Use valid codes; ensure mutual hreflang on all locale variants |
| `canonical` | Missing or incorrect canonical | Self-referencing canonical on the indexable URL |
| `structured-data` | (manual) | Validate via a rich-results test; add JSON-LD per the schema.org type that matches the page |

## Lighthouse User Flow Audits

The default `npx lighthouse <url>` scores only a cold page load (a "navigation" audit) and cannot meaningfully score INP, which needs a real interaction during the page lifetime. Two extra modes via the Flow API:

- Timespan: records every Web Vitals metric during an arbitrary window while the user (or script) interacts. The only lab way to get a meaningful INP number. Use it on the slowest interaction (search submit, filter apply, modal open) and read INP from the timespan report.
- Snapshot: scores the page in its current rendered state. Use it inside a multi-step flow to score a modal, sheet, or post-interaction state the navigation audit cannot reach.

Run from a headless browser of your choice (Puppeteer, Playwright, or equivalent):

```js
import { startFlow } from 'lighthouse';
import { launch } from '<headless browser of your choice>';

const browser = await launch({ headless: 'new' });
const page = await browser.newPage();
const flow = await startFlow(page, { name: 'Search flow' });

await flow.navigate('https://your-domain/');
await flow.startTimespan({ name: 'Search interaction' });
await page.click('[data-search-input]');
await page.type('[data-search-input]', 'frontend excellence');
await page.keyboard.press('Enter');
await page.waitForSelector('[data-search-result]');
await flow.endTimespan();
await flow.snapshot({ name: 'Search results visible' });

const report = await flow.generateReport();
await browser.close();
```

Capture during the real interaction against the real DOM, not a synthetic mouse event in `<head>`; the long-task budget only shows up if the handler runs against the real DOM.

## Setting Up CI Gates

A minimal config asserts category scores and metric ceilings, runs each URL `numberOfRuns: 3`, desktop preset:

```json
{
  "ci": {
    "collect": {
      "startServerCommand": "npm run start -- -p 3001",
      "url": [
        "http://localhost:3001/",
        "http://localhost:3001/<key-route-1>",
        "http://localhost:3001/<key-route-2>"
      ],
      "numberOfRuns": 3,
      "settings": { "preset": "desktop" }
    },
    "assert": {
      "assertions": {
        "categories:performance": ["error", { "minScore": 0.99 }],
        "categories:accessibility": ["error", { "minScore": 1.00 }],
        "categories:best-practices": ["error", { "minScore": 1.00 }],
        "categories:seo": ["error", { "minScore": 1.00 }],
        "largest-contentful-paint": ["error", { "maxNumericValue": 2000 }],
        "cumulative-layout-shift": ["error", { "maxNumericValue": 0.05 }],
        "total-blocking-time": ["error", { "maxNumericValue": 100 }]
      }
    },
    "upload": { "target": "temporary-public-storage" }
  }
}
```

Two URL-list rules:

- Cover every indexable route CLASS, not a subset. One representative per template (home, each hub, a detail page of each kind, each standalone page). A route class never scored can regress and still pass CI; the common miss is a new content-page template never added to the list.
- Never put a `noindex` page in a config asserting `"categories:seo": ["error", { "minScore": 1 }]`. The `is-crawlable` audit fails on `noindex`, dropping SEO below 1 and failing the gate even though the page is intentionally non-indexable. Score noindex pages (a 404, a duplicate, an auth wall) in a separate config without the SEO assertion, or leave them out.

Run a separate mobile config with `preset: 'mobile'` (default) and a lower 0.95 perf threshold. Run both mobile and desktop as required CI gates.

### CI wiring

A pre-built CI action runs on every PR, uploads HTML reports as artifacts, and posts score deltas back to the PR:

```yaml
name: Lighthouse CI
on: [pull_request]
jobs:
  lhci:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with: { node-version: '20' }
      - run: npm ci && npm run build
      - uses: <lighthouse-ci-action>@v12
        with:
          configPath: ./lighthouserc.json
          uploadArtifacts: true
          temporaryPublicStorage: true
```

- `uploadArtifacts: true` attaches every HTML report so reviewers can open the full waterfall on a failure.
- `temporaryPublicStorage: true` pushes the report to public storage and prints the URL in the log; the action also adds a status check per assertion, so the failing audit is visible without opening the report.
- For a self-hosted history server (retention, trend graphs, alerts), point `upload.target` at `lhci-server` and set `LHCI_TOKEN` / `LHCI_SERVER_BASE_URL` as secrets.

Local equivalent (reproduce CI failures before pushing):

```bash
lhci collect   # runs Lighthouse N times against the URL list
lhci assert    # exits non-zero if any assertion fails
lhci upload    # pushes the reports to your chosen target
```

## Diagnosing Score Drops

When Lighthouse drops 5+ points between runs (a 2-3 point swing is noise; 5+ is a regression):

1. Open the new and old reports side by side; compare each metric numerically; identify the metric that moved.
2. LCP: did the LCP element id change, or the asset get bigger?
3. TBT: in the Performance trace long-tasks panel, identify the new long task by its source URL.
4. CLS: in the Layout Instability filmstrip, identify the shifting element.
5. FCP: check whether new render-blocking resources were added.

Fix scored-audit failures and ignore an Insight when the scored metric is already green.

## Field vs Lab

| Source | What it is | Use |
|--------|-----------|-----|
| Lighthouse (lab) | Controlled lab run | Catch regressions before deploy |
| CrUX (field) | Real-user 28-day p75 | What Search Console uses for ranking signals |
| `web-vitals` (field) | Real-time field telemetry to your own pipeline | Production instrumentation, especially INP |

Pass the lab AND the field. INP shows up in the field but not the lab; instrument it in production.

INP-in-CI gate via CrUX: lab Lighthouse cannot score INP meaningfully. Query the CrUX API (or PageSpeed Insights API) for the origin's 28-day p75 INP and fail the build if it exceeds 200ms. Run on a daily schedule, not per PR (CrUX updates lag deploys):

```bash
curl -s "https://chromeuxreport.googleapis.com/v1/records:queryRecord?key=$CRUX_KEY" \
  -H 'Content-Type: application/json' \
  -d '{"origin":"https://your-domain","metrics":["interaction_to_next_paint"]}' \
  | jq -e '.record.metrics.interaction_to_next_paint.percentiles.p75 <= 200'
```

Pair with the Flow API timespan audit (above) for interaction-level lab gates on PRs.

## Common Misconceptions

- "Desktop is always 100." False. Heavy hydration, oversized images, and broken meta still tank desktop.
- "We can fix it later." Performance debt compounds; add a budget guard now.
- "It is only a public-facing surface." Public surfaces are the entry point for organic search and directly affect CrUX and rankings.
- "We minified, we are fine." Minification reduces bytes by ~30%; the bigger lever is what you ship at all (treeshake, code-split, defer hydration).
- "We have a CDN, that is enough." A CDN moves bytes faster; it does not parse, hydrate, or render faster. The client work is the bottleneck.

## See Also

- [performance.md](performance.md) for the deep dive on each performance lever
- [accessibility.md](accessibility.md) for the deep dive on accessibility audits
- [seo.md](seo.md) for the deep dive on SEO audits
- [debug-recipes.md](debug-recipes.md) for Lighthouse flake triage and CLS/INP diagnosis
