---
title: Quick Reference
purpose: The condensed view of the highest-leverage rules across performance, accessibility, SEO, UI/UX, design, and multi-page consistency. Scan against any change.
load-when:
  task-keywords: [rule, quick reference, highest leverage, checklist, axe, srcset, inline SVG, motion, phantom failure, lighthouse]
  symptoms: [score dropped, contrast fail, duplicate id, image too small on retina, phantom dev failure]
prereq: SKILL.md
related: [pre-launch.md, lighthouse.md, performance.md, accessibility.md]
size: ~90 lines
---

# Quick Reference

A condensed view. Each rule is one dense line: principle, concrete check, threshold. Every rule has a longer treatment in the references.

## Performance (the levers that actually move the score)

- One LCP image, optimized end-to-end: serve AVIF first with WebP fallback, responsive srcset with at least 3 widths, declared width/height, fetchpriority="high", no lazy-load, preload only if the image is rendered by client JS.
- srcset must cover CSS width times device pixel ratio (a 360px box at 2x needs a 720w candidate); for an LCP or hero image emit up to about 2x the maximum layout width. Audit: image-size-responsive, uses-responsive-images.
- Inline critical CSS for above-the-fold and defer the rest: aim for under 14 KB of inline CSS so it fits in the first TCP round trip.
- Self-host fonts, subset to used glyphs, font-display: swap for body and optional for display, preload at most one critical font weight (never 4 weights).
- Cap initial JS at the budget: code-split by route and by interaction, defer hydration of below-the-fold islands, prefer server components or static HTML where possible.
- No layout-shifting late content: reserve space with aspect-ratio, min-height, or skeletons. The CLS budget is < 0.1 mobile / < 0.05 desktop.
- Animate only transform and opacity: anything that triggers layout (width/height/top/left/margin) is forbidden in animations.
- Pin third-party scripts to async/defer with fetchpriority="low", treat each as a Lighthouse hostage, and audit count and total CPU monthly.
- Cache aggressively at the edge: static assets get immutable + 1 year, HTML gets stale-while-revalidate, API responses get the longest TTL the data allows.
- Unique ids in reused components: a hardcoded DOM id produces duplicate ids when rendered more than once (invalid HTML, broken ARIA), so derive a per-instance id (useId() or equivalent) and namespace child ids.
- After ANY change to how CSS is delivered (inlining, bundling, chunk or concat order, a build flag), re-run axe on ALL routes, not just the changed page: an equal-specificity cascade reorder can flip a computed color with zero authored-rule change.
- Verify the shipped artifact: check copied static files and headers (a _headers file, robots.txt, security headers) in the BUILT output and the live response, not just source. Local preview is not the edge (it lacks the host response headers and is not the artifact users hit), so the live or field number outranks any local run, and you score a production build served by the framework preview or the live edge, never the dev server (a dev server manufactures phantom failures: an injected dev-toolbar link, a hot-reload socket that fails BFCache, unminified assets).

## Accessibility (the levers that actually move the score AND help real users)

- Semantic HTML first, ARIA only where semantics fall short: a <button> beats <div role="button"> every time.
- Contrast >= 4.5:1 for body, >= 3:1 for large text and meaningful UI graphics, verified in both light and dark mode separately.
- Visible focus on every interactive element with a 2-4px outline and 3:1 contrast against the surface and against the element's resting state.
- One H1, sequential headings, no skipped levels, no headings used as styling hooks.
- Every input has a programmatic label: a placeholder is not a label.
- Every meaningful image has descriptive alt; every decorative image has alt="".
- Keyboard parity: everything reachable by mouse must be reachable, operable, and visible-when-focused by keyboard.
- Respect prefers-reduced-motion by removing or shortening non-essential animation, never by leaving full motion in place.
- Inline svg is either aria-hidden="true" (decorative) or role="img" with a title or aria-label (meaningful). Never role="img" without a name (it trips image-alt).

## SEO (the levers that actually move the score AND drive traffic)

- Unique, intent-matching <title> and <meta name="description"> per page: 50-60 char title, 140-160 char description.
- Self-referencing canonical on every indexable page, with no conflicting canonicals between hreflang variants.
- One H1 with the primary intent term used naturally, plus sequential H2/H3.
- Descriptive, kebab-case, lowercase URLs without tracking params in canonical.
- JSON-LD structured data where applicable (Organization, WebSite, BreadcrumbList, Article, FAQPage, Product, SoftwareApplication, HowTo).
- Internal linking with descriptive anchor text: no "click here", no orphan pages.
- robots: index, follow on indexable pages, noindex on private, search-result, or duplicate pages.
- XML sitemap lists only canonical, indexable, 200-status URLs, referenced from robots.txt.
- AI answer-engine readiness: ship an llms.txt page index (and optionally an llms-full.txt of quotable facts), ensure AI crawler user-agents are not blocked in robots.txt (a dedicated named group is needed only when it repeats your disallows, since it replaces the * group), and keep load-bearing facts in server-rendered text because generative search reads and cites the rendered HTML and valid structured data, not images or client-only JS.
- Never fabricate structured data: emit aggregateRating, review, sameAs, and SearchAction only when each is backed by something real (real reviews, real profiles, a working search endpoint), and every JSON-LD value must be derivable from visible content.

## UI/UX (the levers that move perceived quality)

- Touch targets >= 44x44 CSS px for standalone controls (the iOS 44pt / Android 48dp guideline) with 8px+ spacing.
- Inline text links (breadcrumbs, in-prose links, footer text lists) are exempt under WCAG 2.5.8: do not inflate them to 44px (it reads as broken), and reserve the large target for buttons, toggles, icon buttons, and CTAs.
- Every async action has loading -> success/error feedback within 100ms of the trigger; skeleton screens beat spinners after 300ms.
- One primary CTA per screen, with secondary actions visually subordinate and destructive actions visually separated.
- State the empty state: empty lists, empty searches, and zero-data charts get a specific message, not a blank canvas.
- The mobile nav must work without JavaScript: if the small-screen nav is JS-driven, ship a <noscript> fallback nav or render the links in static HTML and enhance, so the links exist before and without hydration.
- Scroll-lock without a sideways jump: when locking body scroll for a modal, reserve the scrollbar width as padding-right (window.innerWidth - document.documentElement.clientWidth) and restore it on close, or the page shifts when the scrollbar disappears.

## Motion (the levers that keep motion from costing the score)

- Above-the-fold or hero motion ships only past a Lighthouse budget gate on the LCP page (LCP, TBT, CLS still in budget), otherwise make it static or cut it. Visual sign-off is not performance sign-off.

## Design (the levers that move "this looks designed")

- Pick a distinctive type pairing: refuse Inter + system-ui defaults, pair a characterful display face with a refined body face, cap to 2 families and 4 weights.
- Commit to a dominant color with one or two sharp accents (timid, evenly-distributed palettes read as generic), use semantic tokens, never raw hex in components.

## Multi-page consistency (the levers that prevent cross-page drift)

- Extract repeated markup at the third instance (or at the second with clear future reuse, or at any visible drift): one source of truth or one named variant, no third option.
- No page-local CSS overrides on shared widgets: components own their visual contract, pages adjust only what the component exposes.
- Capture before-and-after screenshots at 1440x900 and 375x812 for every visible change: vague claims of improvement without screenshot evidence are not evidence.
- Re-render every route after every fix group on shared widgets: a global change verified on one page is not verified.
- Geometry sweep returns zero issues at both audit viewports (viewport bleed, text overflow, sub-44 targets, duplicate arrows, drawer scroll-lock, focus presence). See [defects.md](defects.md).

## See also

- [pre-launch.md](pre-launch.md) for the verification gate to score these rules against
- [lighthouse.md](lighthouse.md) for the Lighthouse audit map underpinning these rules
- [performance.md](performance.md), [accessibility.md](accessibility.md) for the depth behind each rule
