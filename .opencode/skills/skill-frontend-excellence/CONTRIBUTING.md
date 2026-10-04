# Contributing to skill-frontend-excellence

This is a content-only skill: every change is to a Markdown file under `SKILL.md` or `references/`. There is no runtime to wire up, no FSM to author, no corpus to rebuild. The discipline lives in the prose.

## Local Setup

```bash
git clone https://github.com/ctxr-dev/skill-frontend-excellence.git
cd skill-frontend-excellence
npm install
```

`npm install` pulls in `markdownlint-cli2` and sets up the Husky pre-commit hook, which runs the validators (markdown lint plus the em/en dash guard) before every commit.

## Editing

The two files you will touch:

- `SKILL.md` (entry point, North Star Targets, priority stack, routing tables, conflict-resolution table, render-strategy decision tree, freshness check)
- `references/<topic>.md` (deep dives; the canonical Quick Reference rules live in `references/quick-reference.md`)

After every edit, run:

```bash
npm run validate
```

This runs:

- `node scripts/check-no-dashes.mjs` to reject em (U+2014) and en (U+2013) dashes
- `node scripts/validate-structure.mjs` to enforce the frontmatter and routing contract (complete frontmatter, closed-vocabulary keywords, no routing orphan, resolving links and section pointers, accurate `size:`)
- `markdownlint-cli2` over `SKILL.md` and `references/**/*.md`

All three must pass before commit. The pre-commit hook enforces this. Run `npm run validate:structure:fix` to auto-correct drifted `size:` fields.

## Style Rules

These are strict. Failing any of them is a defect, like a failing test.

- **Framework-agnostic.** Express every rule as a principle plus a concrete check. Never name a library API (no `useEffect`, no `getServerSideProps`, no `<Image />`). When a rule is naturally tied to a platform feature (Service Workers, `<picture>`, `font-display`), name the standard, not the framework that wraps it.
- **Generic placeholders only.** No project-specific brands, products, or domain names (no Acme, no Stripe, no example.com plot devices that imply a real product). Use `your-app`, `your-domain`, `Product Name`, or omit the example entirely.
- **No em or en dashes.** Use commas, colons, parentheses, or line breaks. The repo enforces this via `scripts/check-no-dashes.mjs`. (Yes, every contributor hits this once.)
- **Consistent voice.** Match the existing references: short declarative sentences, lists over prose where possible, every rule actionable. Avoid hedge words ("perhaps", "might be", "tends to") when a concrete threshold exists.
- **Concrete thresholds win.** Prefer "contrast >= 4.5:1" over "good contrast", "<= 90 KB gzipped" over "small bundle", "44x44pt" over "comfortable touch targets".
- **One H1 per file, sequential headings.** No skipped levels. The skill itself follows the rules it teaches.
- **JS code samples in references are illustrations, not deliverables.** Keep each in the 30 to 50 line range; longer snippets are smelly and fragment into smaller pieces. Use only standard DOM and CSSOM APIs (`document.querySelectorAll`, `getComputedStyle`, `getBoundingClientRect`, etc.) plus standard Node modules where the snippet runs the browser (e.g., `fs`, `path`, `puppeteer`). Introduce every browser-automation snippet with the framework-neutral phrasing "Run from a headless browser of your choice (Puppeteer, Playwright, or equivalent)". Do not name a test runner, build tool, or component library inside the snippet itself beyond the launcher line. Even when a pattern is naturally associated with one framework, the reference must phrase it as a principle plus check, not a library API.

## Reference file frontmatter (required)

Every file under `references/*.md` MUST start with a YAML frontmatter block. The block is part of the navigation infrastructure: a runtime agent reads it (a cheap `head -20`) to decide whether to load the rest of the file. Missing or malformed frontmatter is a defect, like a failing test.

Schema:

```yaml
---
title: Performance Deep Dive
purpose: One or two sentences naming the file's scope. Keep it short, dense, scannable.
load-when:
  task-keywords: [LCP, INP, CLS, hydration, bundle, preload, Speculation Rules, BFCache, render strategy]
  symptoms: [LCP regression, INP regression, CLS regression, score dropped, slow page]
prereq: SKILL.md
related: [lighthouse.md, observability.md, debug-recipes.md, build-hygiene.md]
size: ~620 lines
---
```

Field rules:

- **`title`**: short title, capitalised, no trailing punctuation. Matches the file's H1.
- **`purpose`**: one or two sentences. Plain prose. No em / en dashes. This is the snippet a routing agent reads first.
- **`load-when.task-keywords`**: the canonical task vocabulary that should pull this file. PICK FROM the keyword glossary below; do not invent new keys without adding them to the glossary. Six to twelve keywords per file is the working range. Use the same key for the same concept across files (e.g., `LCP`, not `Largest Contentful Paint` in some files and `LCP` in others).
- **`load-when.symptoms`**: the canonical regression vocabulary that should pull this file. Same picking-from-glossary rule. Three to eight symptoms per file.
- **`prereq`**: always `SKILL.md` for now. Reserved for future tiered prereqs.
- **`related`**: two to four sibling files most likely to be loaded with this one. Builds the connected graph; no file is a leaf. Cross-link from each file's "See also" footer to a subset of `related:`.
- **`size`**: approximate line count, `~N lines`. The agent uses this to budget context.

When you add a new reference file: place it under `references/`, give it the frontmatter, add a row to the `SKILL.md` Reference Index, **add it to at least one of the By-Task or By-Symptom routing tables** (no file may be reachable only via the Reference Index), and add it to the `related:` list of at least one existing file. Run `npm run validate` to confirm structural integrity. The structural validator fails on a missing frontmatter field, an off-vocabulary keyword, a routing orphan, a broken link, a bad section pointer, or a stale `size:`.

### Routing section pointers

A routing-table row may point at one section of a large file instead of the whole file, using the heading anchor:

```text
[performance.md: Font Strategy](references/performance.md#font-strategy)
```

The anchor is the GitHub-style slug of a real `##` heading in that file (lowercase, spaces to hyphens, punctuation dropped). The validator resolves every anchor against the target file's headings, so a renamed or mistyped heading fails CI. Use section pointers by heading name, never line numbers.

## Keyword glossary (the corpus vocabulary)

The skill builds its agent-routing index from a closed vocabulary. Using the SAME word for the same concept across files is the whole point: it keeps the semantic tree consistent so an agent's substring match resolves cleanly. When you need a concept not listed, ADD it here AND to the `TASK_KEYWORDS` / `SYMPTOMS` sets in `scripts/validate-structure.mjs` (the machine-authoritative copy that CI enforces), in the same PR.

### Task-keyword vocabulary

Grouped by domain (the group itself is not the keyword; the items are):

- **Core Web Vitals and perf**: `LCP`, `INP`, `CLS`, `TTFB`, `FCP`, `TBT`, `performance`, `hydration`, `bundle`, `preload`, `prefetch`, `Speculation Rules`, `Early Hints`, `fetchpriority`, `BFCache`, `render strategy`, `SSR`, `SSG`, `CSR`, `ISR`, `streaming`, `islands`, `partial hydration`, `resumable`
- **Accessibility**: `accessibility`, `a11y`, `WCAG`, `screen reader`, `keyboard`, `focus`, `contrast`, `semantic HTML`, `ARIA`, `dynamic type`, `reduced motion`, `forced colors`, `axe`, `inline SVG`
- **SEO**: `SEO`, `indexing`, `canonical`, `sitemap`, `robots`, `structured data`, `JSON-LD`, `AEO`, `GEO`, `llms.txt`, `hreflang`, `Open Graph`, `meta description`, `title tag`, `clean URLs`
- **Lighthouse**: `lighthouse`, `diagnostic Insights`, `phantom failure`, `errors-in-console`, `image-size-responsive`
- **UI / UX**: `UI`, `UX`, `interaction`, `hover`, `press`, `loading state`, `error state`, `empty state`, `success state`, `modal`, `popover`, `dialog`, `drawer`, `sheet`, `menu`, `tooltip`, `snackbar`, `toast`, `breadcrumb`, `navigation`, `touch target`, `hit target`, `popover API`, `inert`
- **Design**: `design`, `typography`, `color`, `palette`, `OKLCH`, `P3`, `wide gamut`, `spacing`, `composition`, `atmosphere`, `dark mode`, `light mode`, `brand`, `font`, `variable font`
- **Responsive**: `responsive`, `breakpoint`, `mobile`, `tablet`, `desktop`, `container query`, `viewport`, `safe area`, `dvh`, `srcset`, `DPR`, `fluid typography`, `subgrid`, `scrollbar-gutter`
- **Motion**: `motion`, `animation`, `transition`, `easing`, `View Transitions`, `scroll-driven`, `WAAPI`, `will-change`, `@starting-style`
- **Forms**: `form`, `validation`, `autofill`, `autocomplete`, `label`, `input`, `select`, `checkbox`, `radio`, `file upload`, `constraintValidation`
- **Data viz**: `chart`, `data viz`, `axis`, `legend`, `colorblind`, `Canvas`, `SVG`, `WebGL`, `timezone`, `DST`
- **Pre-launch**: `pre-launch`, `checklist`, `ship`, `release`, `deployment`, `gate`, `verification`, `evidence`
- **Audit / polish**: `audit`, `route`, `sweep`, `screenshot`, `baseline`, `capture`, `polish`, `drift`, `measurable bars`
- **Components**: `component`, `widget`, `contract`, `extraction`, `slots`, `composition`, `Storybook`, `tokens`
- **Defects**: `defect`, `bug`, `regression`, `geometry`, `threshold`
- **Security**: `security`, `CSP`, `COOP`, `COEP`, `CORP`, `cross-origin isolation`, `SRI`, `Trusted Types`, `Permissions-Policy`, `Referrer-Policy`, `frame-ancestors`
- **Observability**: `observability`, `RUM`, `monitoring`, `error capture`, `source maps`, `INP attribution`, `Reporting API`, `error boundary`, `CrUX`, `Long Animation Frames`, `LoAF`
- **Testing**: `testing`, `visual regression`, `axe-core`, `pa11y`, `size-limit`, `bundlesize`, `lighthouse-ci`, `contract test`, `type check`, `hermetic gate`
- **Auth**: `auth`, `authentication`, `login`, `signup`, `passkey`, `WebAuthn`, `OAuth`, `magic link`, `session`, `account recovery`, `CAPTCHA`, `Turnstile`, `Storage Access API`
- **Debug**: `debug`, `recipe`, `hydration mismatch`, `layout overflow`, `focus trap`, `font-swap CLS`
- **Anti-patterns**: `anti-pattern`, `what to avoid`, `mistake`
- **i18n**: `i18n`, `l10n`, `internationalization`, `localization`, `locale`, `translation`, `Intl`, `plural rules`, `bidi`, `RTL`, `mirroring`
- **PWA / offline**: `PWA`, `offline`, `service worker`, `SW`, `install prompt`, `push`, `background sync`, `manifest`
- **Build hygiene**: `build`, `tree-shaking`, `dependency`, `sideEffects`, `lockfile`, `dead code`, `code splitting`
- **Embed patterns**: `embed`, `iframe`, `sandbox`, `postMessage`, `host`, `guest`, `widget`, `third-party widget`
- **Print / email**: `print`, `email`, `@page`, `page-break`, `transactional email`, `Outlook`
- **Quick reference**: `rule`, `quick reference`, `highest leverage`

### Symptom vocabulary

Use these EXACT strings (the agent matches by substring):

`LCP > 2.5s`, `LCP regression`, `INP > 200ms`, `INP regression`, `CLS > 0.1`, `CLS regression`, `slow page`, `slow interaction`, `score dropped`, `Lighthouse score drop`, `bundle size grew`, `hydration mismatch`, `focus trap leak`, `duplicate id`, `horizontal scroll`, `viewport overflow`, `broken on Firefox`, `broken on Safari`, `font swap CLS`, `iOS 100vh`, `rubber-band scroll`, `third-party script slow`, `focus not visible`, `contrast fail`, `aria-hidden leak`, `inert leak`, `noindex with sitemap`, `canonical mismatch`, `consent banner CLS`, `popover not dismissing`, `scroll lock side shift`, `auth redirect loop`, `passkey not offered`, `RTL broken`, `dark mode broken`, `phantom dev failure`, `errors-in-console`, `image too small on retina`, `stale SRI beacon`

### Synonym clusters

The agent gets `<concept> ↔ <abbrev>` for free via these clusters. Subagents writing prose should prefer the abbreviation in `task-keywords:` (saves frontmatter space) and either form in prose.

- `LCP` / `Largest Contentful Paint`
- `INP` / `Interaction to Next Paint`
- `CLS` / `Cumulative Layout Shift`
- `TTFB` / `Time to First Byte`
- `FCP` / `First Contentful Paint`
- `TBT` / `Total Blocking Time`
- `SSR` / `Server-Side Rendering`
- `SSG` / `Static Site Generation`
- `CSR` / `Client-Side Rendering`
- `a11y` / `accessibility`
- `i18n` / `internationalization`
- `l10n` / `localization`
- `RUM` / `Real-User Monitoring`
- `CWV` / `Core Web Vitals`
- `BFCache` / `back/forward cache`
- `SW` / `service worker`
- `LoAF` / `Long Animation Frames`

## Commits

Conventional commit messages are documented convention; commitlint is not enforced. Examples:

```text
feat(performance): add resource hints decision matrix
fix(accessibility): correct contrast threshold for large text
docs(readme): clarify submodule install path
chore(lint): bump markdownlint-cli2
```

Types in use: `feat`, `fix`, `docs`, `chore`, `refactor`, `style`, `test`. Scope is the affected file or topic (e.g., `performance`, `seo`, `readme`).

## Pull Requests

Open PRs against `main`. CI runs `npm run validate` and must pass. Keep PRs focused: one topical reference, or one cross-cutting principle, per PR. Larger restructures get their own PR with a brief rationale in the description.

## Releasing (Maintainers Only)

The release flow is PR-gated; the bot does not push to `main` directly.

1. **Actions, Release, Run workflow.** Branch: `main` (any other ref is rejected). Version bump: `patch`, `minor`, or `major`.
2. The workflow bumps `package.json` on a `release/v<version>` branch and opens a release PR.
3. Review and merge the PR.
4. `tag-on-main.yml` detects the version change on the merge commit, creates the annotated `v<version>` tag, and pushes it.
5. The tag push triggers `publish.yml`, which validates and publishes to npm via OIDC trusted publishing.

Full operator walkthrough (including troubleshooting for stale tags, non-main dispatches, and the "Allow GitHub Actions to create and approve pull requests" org-level policy) lives in the [Releasing section of the README](README.md#releasing).
