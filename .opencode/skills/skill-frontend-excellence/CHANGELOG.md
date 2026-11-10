# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.3.1] - 2026-06-13

### Changed

- Sharpened the `errors-in-console` third-party-beacon coverage in `lighthouse.md`: the discriminator now names the DOM-inspection confirm step (look for an `integrity` + `crossorigin` pair on an auto-injected third-party script) and the header false-negative trap (a direct `curl -I` of the asset still returns a valid `Access-Control-Allow-Origin`, so a header-only check passes and misses the stale-hash-under-CORS failure), and the fix now states the why (edge auto-injectors skip injection when the tag is already present, so the plain self-injected tag pre-empts the broken pinned one). Mirrored the mechanism into the `anti-patterns.md` beacon row and reconciled the `debug-recipes.md` Hypothesis B fix with this canonical self-inject path.
- Added cross-links the structural index was missing: `build-hygiene.md` See Also now points at the `seo.md` clean-URL contract, and the `seo.md` tabbed-content note names the progressive-enhancement detail (roving tabindex, a deep-linkable active tab) with a pointer to `accessibility.md` Tabs.

## [0.3.0] - 2026-06-03

A corpus-wide density and navigation pass, a machine-enforced structural contract, and the field-tested refinements from a verified 100 / 100 / 100 / 100 build.

### Added

- **Structural validator** (`scripts/validate-structure.mjs`, wired into `npm run validate`, CI, and the pre-commit hook). It enforces what the skill's determinism rests on: complete YAML frontmatter on every reference file, `load-when` keywords and symptoms drawn from the closed vocabulary, no routing orphan (every file reachable from By-Task or By-Symptom, not just the Reference Index), every routing link and `related:` entry resolving, every section-pointer anchor matching a real heading, and an auto-maintained `size:`.
- **Routing section pointers.** The By-Symptom table now jumps to a specific section of a file (for example `performance.md` at "Font Strategy") by heading anchor, so an agent reads the one relevant section, not a whole 600-line file. By-Task and By-Symptom use links throughout and together cover every reference file (the previously index-only `anti-patterns.md` and `data-viz.md` are now task-routed).
- **Field-tested refinements**, folded into their topical files principle-first and framework-agnostic:
  - CSS-delivery changes can reorder the cascade and silently regress contrast, so re-run axe on every route after any delivery change (`lighthouse.md`, `anti-patterns.md`, `quick-reference.md`).
  - Scored audits versus diagnostic Insights: do not chase a clean Insight when the scored metric is already green (`lighthouse.md`).
  - A dev server manufactures phantom failures; score a production build via the framework preview or the live edge, and the live or field number outranks any local run (`lighthouse.md`, `debug-recipes.md`, `quick-reference.md`).
  - `errors-in-console` from a third-party beacon: separate a client-blocker false positive from a real stale-SRI auto-injection bug with a clean headless reproduce (`debug-recipes.md`, `lighthouse.md`, `anti-patterns.md`).
  - Responsive images must cover CSS width times device pixel ratio, with the arithmetic (`responsive.md`, `lighthouse.md`, `quick-reference.md`).
  - Above-the-fold animation ships only past a Lighthouse budget gate, or it is cut (`motion.md`, `quick-reference.md`).
  - Static-host clean URLs are a three-part contract with a flat-file audit gotcha, and a 410 from a submission endpoint means delete the integration (`seo.md`).
  - Inline SVG is `aria-hidden` or `role="img"` with a name, never a role without a name (`accessibility.md`, `anti-patterns.md`, `quick-reference.md`).
  - CI realism for a perfect-score pipeline: keep self-injected third-party tags out of the hermetic gate, gate Performance at a floor while holding the other three at 100, do not oversubscribe runner cores, and place slow gates by feedback cost (`testing.md`).
  - Keep tool scratch out of the dev watcher and shard generated audit artifacts (`build-hygiene.md`); restate a vague quality goal as per-route numeric bars before acting (`audit-workflow.md`); build-time-optimize images inside islands (`performance.md`).

### Changed

- **Corpus-wide density pass.** Every reference file was rewritten to cut prose, hedging, and cross-file repetition and to reformat rules into tables and checklists. The corpus dropped about 1700 lines (roughly 14 percent) while preserving every field-tested check: a 3608-item checks inventory was extracted before the pass and verified after with zero losses. Cross-cutting checks now live once in a canonical home and are echoed as one-liners elsewhere.
- `quick-reference.md` grew to 44 rules (four new high-leverage one-liners) and `anti-patterns.md` gained three rows.
- `CONTRIBUTING.md` now documents the structural validator, the section-pointer syntax, the closed-vocabulary additions, and the rule that a new file must join a routing table.
- `README.md` no longer duplicates the reference list; the Reference Index in `SKILL.md` is the single source.

## [0.2.1] - 2026-05-30

### Changed

- Pin the documented `npx @ctxr/kit` invocations to `npx @ctxr/kit@latest` so install and update resolve reliably (an unpinned scoped npx spec can fail to link its bin on newer npm).

## [0.2.0] - 2026-05-29

### Added

- **Navigation infrastructure.** As the skill grows from 14 to 26 reference files, an agent must be able to pull only the 2 to 3 files matching the current task. New `SKILL.md` "Loading policy" paragraph + three routing tables: By Task (13 rows), By Symptom (15 rows), and an expanded Reference Index with per-file load-when keywords. YAML frontmatter (`title`, `purpose`, `load-when.task-keywords`, `load-when.symptoms`, `prereq`, `related`, `size`) on every reference file so an agent can do a `head -20` to decide whether to load the rest. Closed-vocabulary keyword glossary + frontmatter spec live in `CONTRIBUTING.md`.
- **`SKILL.md` re-architecture** as a navigation hub (about 240 lines). New "When rules conflict" tie-breaker table after Priority Stack (6 canonical conflicts: animation vs INP, aesthetic vs contrast, etc.). "Decision: Render Strategy" promoted from `performance.md` as a compact 5-row decision table on indexability / personalisation / interactivity axes. "Skill freshness" note pointing at `CHANGELOG.md` and naming the rule classes most likely to drift.
- **Eleven new reference files** closing structural gaps:
  - `references/security.md`: beyond CSP. COOP / COEP / CORP and cross-origin isolation, Trusted Types as a CSP-XSS companion, Subresource Integrity, `Permissions-Policy`, `Referrer-Policy`, `frame-ancestors`, third-party-script supply-chain hygiene, secrets discipline, dependency-cost discipline.
  - `references/observability.md`: post-deploy practice. Source-maps-in-prod private upload, `window.onerror` and `unhandledrejection`, error boundaries, sampled RUM with `sendBeacon` + `visibilitychange` flush, Reporting API, INP field attribution via `web-vitals/attribution` and Long Animation Frames API, CrUX / PageSpeed Insights for trend tracking, alert thresholds, session-replay tradeoffs.
  - `references/testing.md`: pre-merge gate discipline. Visual regression (Playwright snapshots, reg-suit, Chromatic), accessibility automation in CI (`@axe-core/playwright`, pa11y-ci), perf budgets (`size-limit`, `bundlesize`, INP CrUX gate), type-only check gates, contract tests, lighthouse-ci GitHub Action wiring at depth. Testing is the pre-merge gate; `audit-workflow.md` is the post-build polish loop.
  - `references/auth.md`: flow-level authentication. Passkeys + WebAuthn conditional UI, OAuth redirect UX (PKCE), magic-link UX, session-expiry handling, account recovery beyond password reset, CAPTCHA / Turnstile / hCaptcha placement and a11y, sign-in form structure, sign-up progressive disclosure, cross-tab session sync via `BroadcastChannel`, Storage Access API for embedded sign-in.
  - `references/embed-patterns.md`: embed-as-host and embed-as-guest. Sandboxing third-party iframes, `sandbox` flags, allow-list via `allow`, viewport coupling, theme inheritance, CHIPS for cross-origin storage. As guest: postMessage handshake, viewport reporting via `ResizeObserver`, theme adoption, account-linked vs anonymous. Origin verification, top-window navigation constraints, COOP / COEP / CORP impact.
  - `references/debug-recipes.md`: eight symptom-driven recipes (Symptom -> Reproduce -> Isolate -> Hypothesise -> Verify) for hydration mismatch, INP regression, CLS root-cause hunt, accessibility false positive, layout overflow, focus trap leak, Lighthouse flake triage, font-swap CLS.
  - `references/anti-patterns.md`: a single index of every pattern the skill names as an anti-pattern, with its standard fix and a deep link. 80 rows. Pure index; no restated content.
  - `references/i18n.md`: translation pipeline UX (extraction, missing-key fallback, 30 percent length-expansion buffer), `Intl.*` at depth (`NumberFormat`, `DateTimeFormat`, `PluralRules`, `ListFormat`, `RelativeTimeFormat`, `Segmenter`), plural rules in depth, bidi correctness beyond `dir="rtl"`, locale-aware navigation, hreflang and canonical interaction, currency display, numbers in forms.
  - `references/pwa-offline.md`: SW lifecycle, offline shell pattern, caching strategies cookbook (cache-first, network-first, stale-while-revalidate, network-only, cache-only), `manifest.json` correctness for installability, install prompt timing, push permission timing, Background Sync, SW kill-switch pattern, Web Share API.
  - `references/build-hygiene.md`: tree-shaking at depth (barrel traps, default-export traps, dynamic property access), `sideEffects` declarations and verification, code-splitting strategy, dependency-cost discipline, lockfile hygiene, dead-code elimination, `import()` analysis with bundler-specific notes, source maps for builds, polyfill discipline (browserslist + `core-js` subsets).
  - `references/print-email.md`: print stylesheets (`@media print`, `@page`, page-break, `color-adjust: exact`, headers / footers / page numbers, orphans / widows); email HTML constraints (table-based layout, inline CSS via build-time inliner, 600px width, mobile cell-stacking, dark-mode media-query support, Outlook conditional comments + VML, image hosting, tracking-pixel ethics, transactional vs marketing envelope).
- **`references/quick-reference.md`** (extracted from `SKILL.md`): the 41 highest-leverage rules now live in their own reference file. SKILL.md links out instead of inlining the rules block.
- **2024 to 2026 web-platform currency** folded across the existing reference files:
  - Performance / Lighthouse: BFCache hygiene, Speculation Rules API, Early Hints (HTTP 103), INP field-attribution recipe via `PerformanceObserver` event + Long Animation Frames, `fetchpriority` beyond images, `<iframe loading="lazy">`, streaming SSR / RSC envelope, ESM-only + import maps + module workers, Compression Dictionaries. New Best Practices rows for Trusted Types, SRI, COOP / COEP / CORP, `Permissions-Policy`; rewritten `valid-source-maps` for private upload; Lighthouse User Flow audits for lab INP; lighthouse-ci CI gate at depth; INP-in-CI via the CrUX / PageSpeed Insights API.
  - Motion / UI-UX: native HTML Popover API, `<dialog>` + `inert` proper treatment, CSS Anchor Positioning with progressive-enhancement framing, tooltip dismissal + `aria-describedby` (WCAG 1.4.13), pointer / gesture conflicts on scroll containers (`touch-action`, `overscroll-behavior`), undo / redo systems beyond toast, notification permission timing, `@starting-style` + `transition-behavior: allow-discrete` + `display: none` transitions, cross-document View Transitions, `animation-timeline: scroll()`, Web Animations API, CSS `linear()` easing, motion budget.
  - Design / Responsive: OKLCH / OKLab + `color-mix()` for perceptual palettes, P3 wide-gamut, CSS `@layer` for design-system precedence, variable-font axes beyond `wght`, `text-wrap: pretty` perf note, `prefers-reduced-transparency`, brand-tinted dark-mode shadows, `scrollbar-gutter: stable`, `subgrid` (Baseline 2024), `:has()` cookbook, container queries depth (style queries, `@container scroll-state`), `interpolate-size` + `calc-size()` with `height: auto`, foldable / `device-posture`.
  - Accessibility: forced-colors mode, WCAG 2.2 new SCs (2.4.11, 2.5.7, 3.2.6, 3.3.7, 3.3.8), live-region timing rules, accessible drag-and-drop pattern, combobox / listbox ARIA APG 1.2 patterns at depth.
  - SEO / Forms / Data-viz / Workflow: third-party-cookie deprecation impact, Storage Access API + CHIPS, pagination signals post-`rel=prev/next`, international SEO depth (ccTLD vs subdir vs subdomain, `Content-Language` vs `hreflang`), image and video sitemap extensions, Search Console CrUX mapping, HTML `constraintValidation` API, form analytics, address autocomplete depth, Canvas vs SVG vs WebGL rubric, timezone / DST in time-series, chart annotations, streamed-chart performance, geographic projection rubric.
  - Pre-launch / Audit / Components / Defects: security headers gate, privacy / consent gate, error-tracking sanity check, i18n smoke test, SW kill-switch gate, evidence manifest section. Diff-driven re-audit scope, authenticated route capture, side-by-side diff tooling named, audit cadence and ownership. Versioning / breaking-change discipline, slots and composition vs prop explosion, server vs client boundary on the contract, Storybook discipline, token transformation pipeline. New defect rows for iOS Safari `100vh` / rubber-band, z-index stacking-context inside transformed ancestors, `-webkit-autofill` dark-mode mismatch, font-swap CLS metrics-override, `inert` / `aria-hidden` background leaks.
- **Real-project gaps** folded in: third-party script discipline at depth (taming playbook in `performance.md`), list virtualization + infinite scroll (`performance.md`), embed-as-host / embed-as-guest patterns (new `references/embed-patterns.md`), print stylesheets + email HTML (new `references/print-email.md`).
- **`CONTRIBUTING.md`** updated with the YAML frontmatter contract and the corpus-wide keyword glossary (closed task-keyword + symptom vocabulary, synonym clusters). Editing-section pointers refreshed.

### Changed

- **Quick Reference rules moved out of SKILL.md.** The block that lived inline in SKILL.md (41 rules after the prior round) is now `references/quick-reference.md`. SKILL.md links to it instead of inlining; the rest of the corpus cross-references `quick-reference.md` rather than SKILL.md rule numbers.
- **SKILL.md is now a navigation hub.** Loading policy, By Task / By Symptom / Reference-Index-with-keywords routing tables, conflict-resolution table, render-strategy decision tree, freshness note. North Star Targets, Priority Stack, Workflow, Multi-Page Polish Loop index, and Self-Improvement gates are kept; Quick Reference is linked out.
- **Reference Index expanded** from 14 rows to 26 with one-line purposes and comma-separated load-when keywords per file. New rows for security, observability, testing, auth, debug-recipes, anti-patterns, i18n, pwa-offline, build-hygiene, embed-patterns, print-email, quick-reference.

### Migration notes

- **`SKILL.md` Quick Reference rules moved to `references/quick-reference.md`.** Cross-references previously of the form `Rule N in SKILL.md` are rewritten to `Rule N in [quick-reference.md](quick-reference.md)`. If an external consumer linked to a SKILL.md rule anchor, the anchor moved; update links.
- **Frontmatter is now required** on every file under `references/`. Any new reference file added without the YAML block at line 1 will fail `npm run validate` (markdownlint via document-structure rules) and the no-dashes guardrail does not save you. See `CONTRIBUTING.md` "Reference file frontmatter".
- **Keyword glossary is a closed vocabulary.** Adding a new task-keyword or symptom requires updating `CONTRIBUTING.md` "Keyword glossary" in the same change so the corpus stays consistent.

## [0.1.2] - 2026-05-26

### Added

- Three new reference files: `references/audit-workflow.md` (19-phase multi-page audit and polish workflow with capture script and final acceptance gate), `references/components.md` (when to extract, the 11 widget families, component contract checklist, drift detection with collector snippet, CSS patterns that prevent drift), and `references/defects.md` (29-row symptom-to-fix lookup, canonical 9-check programmatic geometry sweep, per-check thresholds).
- Five new "Multi-page consistency" rules in SKILL.md (rules 31 through 35), covering extraction at three instances, no page-local CSS overrides on shared widgets, before-and-after screenshots at canonical viewports, re-render after every fix group on shared widgets, and a clean geometry sweep at `1440x900` and `375x812`.
- New "Multi-Page Polish Loop" section in SKILL.md indexing the 19 phases with deep links into `audit-workflow.md`.
- Six new gates in SKILL.md "Self-Improvement" for multi-page audit work (widget inventory built, baseline and after screenshots at both viewports, geometry sweep clean, drift collector clean, per-route audit table delivered).
- New "Canonical Audit Capture Viewports" subsection in `references/responsive.md` defining `1440x900` (desktop) and `375x812` (mobile) as the standard capture sizes for multi-page polish.
- New "Cross-page consistency" and "Mobile drawer scroll lock" subsections in `references/ui-ux.md`.
- New "Lang, Main, and Skip Link" mini-checklist in `references/accessibility.md`.
- New "Reference Comparison Heuristics" section in `references/design.md` (the 16 visual quality tests).
- New section 17 "Multi-Page Polish Gate" in `references/pre-launch.md` (renumbers the prior 17 to 18).
- New JS-sample policy in `CONTRIBUTING.md` (~30 line cap, standard DOM and CSSOM APIs only, framework-neutral introduction sentence).
- 27 field-tested additions from the frontend-perfection proposal, folded across SKILL.md and 11 reference files (collected from real landing-page optimization work, kept framework-agnostic in the principle-plus-check house voice). Grouped by domain:
  - SEO (`references/seo.md`): AI answer-engine readiness (AEO/GEO, an `llms.txt` page index, and a robots.txt note for AI crawler user-agents), render primary content on the server, the FAQ/HowTo SERP-rollback note, templated-title brand-suffix overflow, measuring title length as rendered text (decode entities), BreadcrumbList parity with the visible trail, `og:type` discipline, never fabricate structured data, pairing `noindex` with sitemap exclusion, and build-time per-template Open Graph image generation.
  - Lighthouse (`references/lighthouse.md`): CI route-class coverage and the `noindex` SEO-gate trap, CSP versus framework hydration on static hosts (build-time script hashes preferred over `'unsafe-inline'`), and a `duplicate-id` cause pointer.
  - Performance (`references/performance.md`): preload by the build-emitted asset URL rather than a hardcoded hash, and serve a high-resolution master only for zoom or lightbox views.
  - Components (`references/components.md`): per-instance unique ids in reused components (`useId()`), and DRY applied to data before markup.
  - Accessibility, UI/UX, design, responsive: the 44px standalone-control minimum with the WCAG 2.5.8 inline-link exception, modal scroll-lock scrollbar-width compensation, dark-theme accent-text contrast, outside-click plus Escape dismissal, tap-highlight suppression, and a no-JS mobile-nav fallback.
  - Defects and workflow (`references/defects.md`, `references/audit-workflow.md`, `references/pre-launch.md`): a programmatic content-and-markup sweep companion to the geometry sweep, a verify-the-shipped-artifact gate, optional parallel-agent audit discipline, and consolidated pre-launch gates.

### Changed

- Quick Reference in SKILL.md grew from 30 to 41 highest-leverage rules. The first batch added 5 "Multi-page consistency" rules; the frontend-perfection batch then added 6 more (AI answer-engine readiness, never fabricate structured data, no-JS mobile nav, unique ids in reused components, scroll-lock scrollbar compensation, verify the shipped artifact) slotted under their domain headings, and refined the touch-target rule to lead with CSS pixels and carry the WCAG 2.5.8 inline-link exception. Section heading and rule numbers updated to match.
- "When to Use" in SKILL.md adds the multi-page audit and polish trigger.
- "Workflow" in SKILL.md frames the rendered browser as the source of truth and points readers to the Multi-Page Polish Loop when the work is auditing or polishing an existing site.
- Subsumed `visual-polish-websites` skill: every practice from that skill (operating rules, 19-phase workflow, widget inventory, screenshot-driven evidence, geometry sweep, defect lookup, reference-level heuristics, final acceptance gate) is now covered in the merged skill, with framework-named extraction examples replaced by a single framework-neutral component contract.
- Repositioned as an Agent Skills package supporting both Claude Code and OpenAI Codex CLI (plus any other harness that follows the open Agent Skills standard). README headline, package.json description, and keywords now lead with cross-harness framing rather than Claude-only branding. No content changes: the SKILL.md prose was already provider-neutral.
- Reordered package.json keywords to lead with `agent-skills`, `agents-md`, `codex`, `claude-code` so registry searches for open-standard terms surface this package first.
- Updated git-submodule install path in README from `.claude/skills/` to `.agents/skills/` to match the canonical install topology used by `@ctxr/kit`.
- Declared `publishConfig.access: "public"` so scoped npm publish does not require a trailing `--access public`.

## [0.1.0] - 2026-05-08

### Added

- Initial release. SKILL.md plus 11 reference docs covering Lighthouse mastery, performance optimisation, accessibility (WCAG 2.2 AA), SEO, UI/UX patterns, design aesthetics, responsive layout, motion and animation, forms and feedback, data visualisation, and a pre-launch verification checklist.
