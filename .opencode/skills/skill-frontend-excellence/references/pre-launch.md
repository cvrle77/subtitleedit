---
title: Pre-Launch Checklist
purpose: The final verification gate before any public-visible page or interface ships. One checklist, every item runs on every change, every failure is a blocking defect.
load-when:
  task-keywords: [pre-launch, checklist, ship, release, deployment, gate, verification, evidence, audit, lighthouse, screenshot]
  symptoms: [score dropped, Lighthouse score drop, consent banner CLS, font swap CLS, noindex with sitemap]
prereq: SKILL.md
related: [lighthouse.md, audit-workflow.md, security.md, defects.md]
size: ~411 lines
---

# Pre-Launch Checklist

The final gate before a public-visible page or interface is declared complete.

## How to Use

- Walk top to bottom on every page that changed.
- For shared components, walk every page that consumes them.
- Never skip an item because it passed last time; regressions hide in unchanged areas.
- Mark "n/a" explicitly when a section does not apply (e.g., no forms); never silently skip.
- Treat any failure as a blocking defect.

## 1. Lighthouse (Production Build)

Run against the production build (not the dev server), mobile and desktop separately, at least 3 times, take the median.

```
<framework-build>
<framework-start-prod-server> -p 3001 &
export CHROME_PATH="$(find ~/Library/Caches/ms-playwright -name 'Google Chrome for Testing' -type f 2>/dev/null | head -1)"

# Mobile
npx lighthouse "http://localhost:3001/<path>" \
  --output=json --output-path=/tmp/lh-mob.json \
  --chrome-flags="--headless=new --no-sandbox --disable-gpu" \
  --only-categories=performance,accessibility,best-practices,seo --quiet

# Desktop: same plus --preset=desktop, output to /tmp/lh-desk.json
npx lighthouse "http://localhost:3001/<path>" \
  --output=json --output-path=/tmp/lh-desk.json --preset=desktop \
  --chrome-flags="--headless=new --no-sandbox --disable-gpu" \
  --only-categories=performance,accessibility,best-practices,seo --quiet
```

| Score | Mobile | Desktop |
|-------|--------|---------|
| Performance | >= 95 | >= 99 |
| Accessibility | 100 | 100 |
| Best Practices | 100 | 100 |
| SEO | 100 (n/a for noindex pages) | 100 (n/a for noindex pages) |

- [ ] If any category drops below the bar, identify the failing audit and fix per [lighthouse.md](lighthouse.md).
- [ ] Run BOTH programmatic sweeps and require both clean: geometry sweep (visual, headless browser) and content-and-markup sweep (Node over built HTML). See [defects.md](defects.md). A route is not signed off until both return clean.
- [ ] Verify the SHIPPED artifact, not the source: check static files and headers (`_headers`/redirects, `robots.txt`, `sitemap.xml`, security headers, manifest) in the built output directory and in the live response (`curl -I https://...`), not just the source tree. A correct source header that did not make it into the build is the classic "it works locally" header bug.

## 2. Core Web Vitals (Lab)

From the same Lighthouse run:

| Metric | Mobile | Desktop |
|--------|--------|---------|
| LCP | < 2.5s | < 2.0s |
| CLS | < 0.1 | < 0.05 |
| TBT | < 200ms | < 100ms |
| FCP | < 1.8s | < 1.0s |
| TTFB | < 800ms | < 600ms |

- [ ] INP is not in lab Lighthouse; verify in the field via `web-vitals` JS instrumentation.

## 3. Asset Budgets

- [ ] Initial JS payload (gzipped) < budget (default 90 KB mobile / 130 KB desktop)
- [ ] Initial CSS payload (gzipped) < budget (default 25 KB mobile / 35 KB desktop)
- [ ] Above-the-fold image budget met
- [ ] <= 2 font families, <= 4 weights total
- [ ] Third-party scripts within count and main-thread time budget
- [ ] If tooling supports `bundlesize`, `size-limit`, or a custom CI check, run it; treat budget violations as blocking

## 4. Accessibility

Automated:

- [ ] Lighthouse Accessibility = 100
- [ ] axe DevTools (or `@axe-core/playwright`) shows zero violations
- [ ] HTML validates (no broken markup)

Manual:

- [ ] Tab through the entire page in document order
- [ ] Every interactive element shows a visible focus ring with 3:1 contrast against surface and resting state
- [ ] Esc closes any open modal, popover, dropdown
- [ ] Forms: labels visible, required marked, error inline + announced, focus moves to first invalid (see [forms.md](forms.md))
- [ ] Modals: focus moves in on open, trapped while open, restored to trigger on close
- [ ] Screen reader pass on the primary user flow (VoiceOver or NVDA)
- [ ] Reading order matches visual order
- [ ] Headings: one H1, sequential, no skipped levels
- [ ] Images: every meaningful image has descriptive alt; decorative images have alt=""
- [ ] Links: descriptive text (not "click here")
- [ ] Skip-to-content link present and working

Visual accessibility:

- [ ] Body text contrast >= 4.5:1 in both light and dark mode (verified independently, not by inversion)
- [ ] Large text and meaningful UI graphics >= 3:1
- [ ] Color is never the only signal (status, error, success paired with icon or text)
- [ ] Page usable at 200% browser zoom with no horizontal scroll
- [ ] Page usable at largest system text size

## 5. Responsive

Test at every breakpoint and orientation:

- [ ] 320px (smallest supported phone)
- [ ] 375px (typical phone)
- [ ] 768px (tablet portrait)
- [ ] 1024px (tablet landscape, small laptop)
- [ ] 1280px (desktop)
- [ ] 1920px (large desktop)
- [ ] Phone landscape (short height)
- [ ] Tablet landscape

Per breakpoint:

- [ ] No horizontal scroll
- [ ] No clipped content
- [ ] Layout is intentional, not just fluid
- [ ] Standalone touch targets >= 44x44 with 8px gaps (mobile/tablet); inline text links are NOT inflated (WCAG 2.5.8 exception)
- [ ] Body text >= 16px on mobile (avoids iOS auto-zoom)
- [ ] Tables either fit, scroll horizontally, or transform to cards
- [ ] Sidebar / nav transforms appropriately
- [ ] Modals fit smallest viewport
- [ ] Safe areas respected (notch, dynamic island, gesture bar)
- [ ] `100dvh` (not `100vh`) for full-height mobile sections
- [ ] Mobile nav works with JavaScript disabled (a `<noscript>` fallback or static links that enhance)
- [ ] Modals and drawers lock body scroll AND compensate for scrollbar width (no sideways jump on open or close)

## 6. Theme

- [ ] Light mode designed and tested independently
- [ ] Dark mode designed and tested independently (not just inverted)
- [ ] All semantic tokens work in both modes
- [ ] Borders and dividers visible in both modes
- [ ] Focus rings visible in both modes
- [ ] Imagery looks correct in both modes (may need separate dark variants)
- [ ] Theme toggle (if present) works without flicker
- [ ] Default theme respects `prefers-color-scheme`
- [ ] User-selected theme persists across navigation

## 7. Motion

- [ ] All animations use `transform` and/or `opacity` (no `width`, `height`, `top`, `left`, `margin`)
- [ ] Durations 100-500ms (no animations longer than 500ms in routine UI)
- [ ] Exit faster than entrance (60-70%)
- [ ] `prefers-reduced-motion: reduce` removes or shortens non-essential animation
- [ ] Loading: skeleton at 300ms+, spinner before, progress for determinate
- [ ] Spinner / skeleton has accessible name or `aria-busy`
- [ ] Animations don't block input
- [ ] Animations don't cause layout shift (CLS)
- [ ] Continuous animations pause when off-screen and tab is inactive

## 8. Visual Quality

Typography:

- [ ] Maximum 2 font families, 4 weights total
- [ ] Distinctive choices (not just Inter on white)
- [ ] Type scale is consistent (multiplicative, not random)
- [ ] Body text 16px+ on mobile
- [ ] Line height 1.5-1.75 for body
- [ ] Line length 60-75 characters for prose
- [ ] Tabular figures for numeric columns

Color:

- [ ] Semantic tokens used (no raw hex in components)
- [ ] Dominant + 1-2 accents (not evenly distributed)
- [ ] Brand color contrasts properly in both modes
- [ ] Neutral scale used consistently for surfaces and text

Spacing:

- [ ] Spacing scale chosen (4-pt or 8-pt)
- [ ] No random spacing values
- [ ] Visual rhythm consistent

Iconography:

- [ ] One icon set throughout
- [ ] Consistent stroke width
- [ ] Consistent sizes per hierarchy level
- [ ] No emoji as structural icons (only as accents where intentional)
- [ ] SVG only (no PNG icons)

Composition:

- [ ] At least one section breaks the safe centered three-card layout (where appropriate to the brand)
- [ ] Visual hierarchy: scanning the page, the most important element is the largest/boldest
- [ ] Whitespace intentional, not just residual
- [ ] One primary CTA per screen

## 9. SEO (public-visible pages)

- [ ] Unique `<title>` 50-60 chars
- [ ] Unique `<meta name="description">` 140-160 chars
- [ ] One `<h1>` matching primary intent
- [ ] Sequential headings, no skipped levels
- [ ] Self-referencing `<link rel="canonical">`
- [ ] `<meta name="robots" content="index, follow">` (or `noindex` if not indexable)
- [ ] Open Graph tags (og:title, og:description, og:image 1200x630, og:url, og:type)
- [ ] Twitter card tags
- [ ] `<html lang="...">`
- [ ] Structured data validated via Rich Results Test (where applicable)
- [ ] Image alt text on every meaningful image
- [ ] Internal links use descriptive anchor text (not "click here")
- [ ] Page reachable from home in <= 3 clicks
- [ ] Listed in sitemap.xml (if indexable)
- [ ] HTTPS, no mixed content
- [ ] No render-blocking content critical to indexing
- [ ] Content-and-markup sweep over built HTML returns clean: one H1 per page, title and description in range (rendered length), self-referencing canonical, OG tags, every JSON-LD parseable, every image has alt, no duplicate ids, no orphan pages (except an error page)
- [ ] If AI discovery matters: `llms.txt` exists and is complete, robots.txt does not block major AI crawlers (and any dedicated user-agent group repeats all disallows), load-bearing facts are server-rendered

## 10. State Coverage

Every screen and component has intentional designs for:

- [ ] Empty (message and action, not blank)
- [ ] Loading (skeleton matching layout, or spinner)
- [ ] Success (brief feedback)
- [ ] Error (cause + fix + recovery action)
- [ ] Disabled (visually distinct, programmatically disabled)
- [ ] Initial / first-run (where applicable)
- [ ] Offline (network-dependent actions disabled, message shown)
- [ ] Limit reached (where applicable)

## 11. Forms (if forms changed)

- [ ] Every input has a visible, programmatic label
- [ ] Right `type` and `inputmode` for the data
- [ ] `autocomplete` per WHATWG spec (especially username/password/address/payment)
- [ ] Required fields marked visually + `aria-required`
- [ ] Validation on blur (not on every keystroke)
- [ ] Errors inline, near the field, with cause + fix
- [ ] Errors announced via `role="alert"` or `aria-live`
- [ ] On submit error, focus moves to first invalid field
- [ ] Submit button has loading state, disabled during submission, specific verb-noun label
- [ ] Helper text for complex fields
- [ ] Long forms autosave; "Saved" indicator visible
- [ ] Multi-step forms show progress and allow back navigation
- [ ] Destructive actions confirm or provide undo
- [ ] Tested with password manager / autofill

## 12. Charts and Tables (if data viz changed)

- [ ] Right chart for the data
- [ ] Axes labeled with units
- [ ] Colorblind-safe palette
- [ ] Color paired with shape/pattern/label
- [ ] Tooltip works on hover AND tap
- [ ] Text alternative (summary or accessible data table)
- [ ] Interactive elements keyboard-accessible
- [ ] Touch targets >= 44x44
- [ ] Responsive: simplifies on small screens
- [ ] Loading skeleton matches layout
- [ ] Empty state with message and action
- [ ] Error state with retry
- [ ] Numbers locale-formatted
- [ ] Tables: sorting, sticky header, mobile-adaptive

## 13. Cross-Browser

Test on at least:

- [ ] Latest Chrome (Chromium engine, what most users have)
- [ ] Latest Safari (WebKit, all iOS users + many macOS users)
- [ ] Latest Firefox (Gecko, smaller share but valuable diversity)
- [ ] Supported older browsers (per browserslist): spot-check key flows

Safari watch-list:

- [ ] `:has()` (now widely supported, but verify on older iOS)
- [ ] `<dialog>` (added in Safari 15.4)
- [ ] `view-transition` API (Safari 18+)
- [ ] Container queries (Safari 16+)
- [ ] Backdrop filter (use prefixed `-webkit-backdrop-filter`)

## 14. Real-User Monitoring

If RUM is set up:

- [ ] `web-vitals` library ships LCP, INP, CLS, FCP, TTFB to your pipeline
- [ ] Monitoring view shows p75 over rolling 28 days
- [ ] Alerts fire when p75 crosses thresholds
- [ ] Source maps uploaded for production error symbolication

## 15. Security Headers

Launch gate only; full policy authoring lives in [security.md](security.md). Verify the production response (`curl -I https://<host>`) carries every header below; source headers that did not make it into the build are the classic "it works locally" header bug.

- [ ] `Strict-Transport-Security` set with a long `max-age` (>= 1 year), `includeSubDomains`, and `preload` once the domain is HSTS-preload-listed
- [ ] `Content-Security-Policy` present, restrictive, no `unsafe-inline` for scripts; `frame-ancestors` set; report endpoint configured
- [ ] `Permissions-Policy` denies camera, microphone, geolocation, payment, USB, and other powerful features the page does not use
- [ ] `Referrer-Policy: strict-origin-when-cross-origin` (or stricter)
- [ ] `Cross-Origin-Opener-Policy: same-origin` (required for cross-origin isolation)
- [ ] `Cross-Origin-Embedder-Policy: require-corp` or `credentialless` (required for cross-origin isolation when high-resolution timers or SharedArrayBuffer are used)
- [ ] `Cross-Origin-Resource-Policy: same-origin` (or `same-site` for shared subdomain assets)
- [ ] `X-Content-Type-Options: nosniff` on every response

## 16. Privacy and Consent

- [ ] Cookie banner exposes a clear "Reject all" path with the same prominence as "Accept all" (no dark patterns, WCAG-readable contrast, no pre-checked non-essential categories)
- [ ] Reject parity verified: the reject button is equally clickable, equally large, and reaches the same dismiss endpoint
- [ ] Banner reserves its space (CLS-safe overlay): use `position: fixed` over reserved layout, or render server-side at known dimensions; verify CLS < 0.1 with the banner visible
- [ ] `Sec-GPC: 1` request header is honoured: treat it as an opt-out signal for non-essential cookies, analytics, and ad personalisation
- [ ] Analytics and non-essential third-party scripts load only AFTER explicit consent (or never when `Sec-GPC: 1` arrives); verify by loading in a fresh profile and watching the network panel before clicking "Accept"

## 17. Error-Tracking Sanity Check

- [ ] Source-map upload pipeline runs in the production build and posts maps to the error tracker's private endpoint (maps NOT served from the public origin)
- [ ] Throw a known error in production (e.g., a hidden test button that calls `throw new Error("prod-sanity-<commit-sha>")`) and confirm it lands in the error tracker within the expected window
- [ ] Confirm the symbolicated stack trace shows original file names, line numbers, and function names (not minified `t.aB.x`)
- [ ] Release tag (commit SHA or version) is attached to the captured event so it groups against the right deploy

## 18. i18n Smoke Test

Launch-time subset; full pipeline lives in [i18n.md](i18n.md).

- [ ] At least one non-Latin locale (Japanese, Arabic, Hindi, Greek) renders without falling back to a system font: inspect `font-family` resolution in DevTools on a glyph in the target script
- [ ] One RTL locale (Arabic or Hebrew) loads with mirrored navigation (logical properties for borders, padding, icons), no layout breaks, no clipped text, no LTR-only icons that lose meaning when flipped
- [ ] Numbers and dates use `Intl.NumberFormat` and `Intl.DateTimeFormat` against the active locale
- [ ] `<html lang>` and `dir` are set per route, not hardcoded to one value

## 19. Service Worker Kill-Switch Gate

If the site ships a service worker; full lifecycle lives in [pwa-offline.md](pwa-offline.md).

- [ ] A "unregister all SWs" page exists at a known stable path (e.g., `/_/unregister-sw`), deployable independently of the SW build, running `navigator.serviceWorker.getRegistrations().then(rs => rs.forEach(r => r.unregister()))` plus `caches.keys().then(ks => ks.forEach(k => caches.delete(k)))`
- [ ] The SW respects a `?nosw=1` query parameter on the entry navigation: when present, the SW does not handle the fetch (or unregisters itself) so a fresh request can bypass a broken cached shell
- [ ] The SW does NOT cache its own script with a long TTL: the SW file is served with `Cache-Control: max-age=0` (or `no-cache`) so the browser picks up a corrected SW on next revisit
- [ ] The SW versioning strategy is documented (cache name carries a build id; old caches deleted in `activate`)

## 20. Documentation

- [ ] Component docs updated (if applicable)
- [ ] Storybook / playground stories cover new states
- [ ] Design system tokens updated (if new tokens introduced)
- [ ] README / CHANGELOG updated (if significant change)

## 21. Git and Review

- [ ] Branch is up to date with main
- [ ] All changes are intentional (review the diff)
- [ ] No leftover console.log, debugger, TODO, FIXME without owner
- [ ] No leftover commented-out code
- [ ] Tests pass (unit, integration, e2e if applicable)
- [ ] Lint and format pass
- [ ] Type check passes
- [ ] Build succeeds for production target

## 22. Multi-Page Polish Gate

Launch-time subset; full Phase 19 acceptance list in [audit-workflow.md](audit-workflow.md) is authoritative when both gates apply. Any failure is blocking.

- [ ] Widget inventory exists for the affected widget families
- [ ] Same-purpose widgets extracted to one source of truth or normalized to one contract
- [ ] Drift collector returns no unexplained differences across pages for shared widgets
- [ ] Geometry sweep returns zero issues on every affected route at `1440x900` and `375x812`
- [ ] Before-and-after screenshots captured at both viewports for every affected route
- [ ] Reference comparison heuristics in [design.md](design.md) pass
- [ ] No same-purpose widget looks different on two routes without a named, intentional variant

## 23. Final Smell Test

Open in a fresh browser (incognito, no cached assets). Walk through as a new user:

- [ ] Is the primary action obvious within 2 seconds?
- [ ] Does the surface feel like a single product, or a collection of unrelated pieces?
- [ ] Does it look distinctive, or could it be any other product's equivalent screen?
- [ ] Would I be proud to share this URL?

If "no" to any, address it before declaring complete.

## 24. Evidence Manifest

The single list of artefacts the PR must produce. Link every item from the PR description; a reviewer must be able to re-run every check from the links alone. A PR missing any item is incomplete and review can be declined.

- [ ] Lighthouse JSON for mobile: file path (e.g., `audit/lh-mob.json`), median of >= 3 runs
- [ ] Lighthouse JSON for desktop: file path (e.g., `audit/lh-desk.json`), median of >= 3 runs
- [ ] axe JSON for every audited route: file path (e.g., `audit/axe-<route>.json`), zero violations
- [ ] Geometry sweep stdout: file path (e.g., `audit/geometry-<viewport>.txt`), zero issues at `1440x900` and `375x812`
- [ ] Content-and-markup sweep stdout: file path (e.g., `audit/content-markup.txt`), prints `NO PROBLEMS`
- [ ] Baseline screenshot directory at `1440x900` and `375x812`: file path (e.g., `audit/baseline/`)
- [ ] After screenshot directory at `1440x900` and `375x812`: file path (e.g., `audit/after/`)
- [ ] Per-route audit table (one row per route): issues found, fixes applied, before screenshot link, after screenshot link, format matches the table in [audit-workflow.md](audit-workflow.md) Phase 18
- [ ] Re-run instructions: the exact commands a reviewer runs to reproduce every artefact (build command, capture script, sweep invocations)

## When the Checklist Fails

Every item here has, at some point, been the regression that shipped. For each failure:

1. Identify the specific cause.
2. Fix at the right layer (component, page, system, or design token).
3. Re-run the relevant section (not the whole thing).
4. Once green, run a final full pass to verify nothing else regressed.

## See Also

- [lighthouse.md](lighthouse.md) for score-driven audit fixes
- [audit-workflow.md](audit-workflow.md) for multi-page polish work and the evidence-capture procedure
- [security.md](security.md) for header policy authoring
- [defects.md](defects.md) for symptom-to-fix lookup and the geometry sweep
