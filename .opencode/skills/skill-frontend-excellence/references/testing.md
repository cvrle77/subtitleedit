---
title: Frontend Testing Discipline
purpose: The pre-merge gate. Frontend test pyramid, visual regression, accessibility automation, performance budgets, type and lint gates, contract tests, and lighthouse-ci wiring kept hermetic. The pre-merge counterpart to the post-build polish loop.
load-when:
  task-keywords: [testing, visual regression, axe-core, pa11y, size-limit, bundlesize, lighthouse-ci, contract test, type check, hermetic gate, verification, gate]
  symptoms: [score dropped, bundle size grew, contrast fail, focus not visible, hydration mismatch]
prereq: SKILL.md
related: [lighthouse.md, observability.md, pre-launch.md, audit-workflow.md]
size: ~302 lines
---

# Frontend Testing Discipline

Tests are the pre-merge gate: what to test, how to budget the categories, how to wire CI so a red gate blocks the merge instead of the deploy. Audit-workflow is the post-build polish loop. Both run; neither replaces the other.

Patterns are stated at the standard level (snapshot diffing, accessibility-tree validation, bundle-size assertion). Tool names appear only as concrete implementations of a pattern.

## The Frontend Test Pyramid

Five categories. Each catches a different regression class. Budget the count per category; spend it on the highest-leverage tests. Add a test at the lowest level that catches the regression class: unit is cheap, end-to-end is slow, flaky, and expensive to maintain.

| Category | Scope | Typical count | Catches |
|----------|-------|---------------|---------|
| Unit | Pure functions, hooks, utilities | Hundreds | Logic regressions, edge cases, refactor breakage |
| Integration | A component plus its immediate collaborators | Dozens to low hundreds | Wiring bugs, prop contracts, state flow |
| Visual | Rendered component or page at fixed viewport | Dozens per surface | Style drift, layout regression, theme breakage |
| End-to-end | Full app, real browser, scripted user flow | Single digits to low dozens per surface | Cross-surface flows, real-browser quirks, integration with auth/payment |
| Contract | API request and response shape | One per consumed endpoint | Backend drift, schema breakage, type-only safety |

- Unit catches what integration mocks pass over: pure-function correctness, sub-second feedback on save.
- Integration catches "the form submits but validation passes when it should fail" wiring bugs unit tests miss.
- Visual catches a shared-token shift that moves a button or drifts a dark-mode background: what unit tests cannot see.
- End-to-end catches surface integration (a flow ending at a 500, a button disabled by an unexpected API shape), not component logic.
- Contract catches a backend field rename deterministically, where mocks still pass and end-to-end caught it only flakily.

Check: every PR touches at least one test, and the test category matches the change category (component edit -> visual test; logic edit -> unit or integration test). PR review questions any change that adds production code without a test.

## Visual Regression Testing

The category most teams skip and most regressions slip through. Pick the tool by tradeoff; the checks below hold across all of them.

| Approach | How it stores baselines | Strength | Weakness |
|----------|-------------------------|----------|----------|
| Headless-browser screenshot diff | PNG baseline committed in git | Zero extra infrastructure, fast feedback | Binary diffs in PRs; OS font rendering drift |
| Git-aware history service | Cloud object storage keyed by git ref | No git bloat, always diffs the actual base, posts side-by-side PR comment | Needs a storage bucket and CI credentials |
| Hosted story-snapshot service (Storybook stories) | Hosted, per story per variant | Auto fan-out across viewport/theme/locale, purpose-built diff review, one-click accept | Hosting cost, story-maintenance coupling |
| Raw pixel matcher | Caller-managed (two PNGs in, diff PNG + count out) | Models one-off pipelines (generated chart, server-rendered email) others cannot | Lowest level, you build the harness |

Worked example. Run from a headless browser of your choice (Puppeteer, Playwright, or equivalent):

```js
import { test, expect } from '<headless-browser-runner>';

test('homepage hero', async ({ page }) => {
  await page.goto('/');
  await expect(page).toHaveScreenshot('home-hero.png', {
    fullPage: false,
    mask: [page.locator('[data-volatile="true"]')],
    threshold: 0.2,
    maxDiffPixelRatio: 0.001,
  });
});
```

- Mask volatile regions: timestamps, animated avatars, ad slots. Masked regions are filled with a solid color before diffing.
- Pin the viewport per project, defaulting to 1280x720 desktop and 375x667 mobile; add others when a surface has a breakpoint at risk.
- Set both `threshold` (per-pixel color tolerance) and `maxDiffPixelRatio` (total-pixel tolerance), tuned per surface, never one global value.
- Run visual tests in a containerised CI environment with a pinned OS image; OS font rendering differs, so CI and local otherwise disagree.
- Review baselines in PRs by inspecting the diff PNG written alongside the failure.

Check: every customer-facing surface has at least one visual test; new surfaces get one in the same PR that ships them; a human reviews the diff before the baseline updates.

## Accessibility Automation in CI

Lint catches the mechanical violations (about 30 percent of WCAG); it raises the floor, it does not replace a manual screen-reader pass.

Worked example (axe-core in a headless browser). Run from a headless browser of your choice (Puppeteer, Playwright, or equivalent):

```js
import { test, expect } from '<headless-browser-runner>';
import AxeBuilder from '<axe-core-binding>';

test('homepage a11y', async ({ page }) => {
  await page.goto('/');
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'best-practice'])
    .disableRules(['color-contrast'])  // only with a documented design-system override
    .analyze();
  expect(results.violations).toEqual([]);
});
```

- Run axe on every route in CI; fail the build on any violation. Disable a rule only with a documented reason and a tracked issue.
- axe catches: missing alt text, missing form labels, duplicate ids, ARIA mismatches, contrast under 4.5:1 (when not disabled), `<html>` without `lang`, headings out of order, missing landmark roles.
- axe does NOT catch: meaningful alt text (it accepts `alt=""` as valid), correct heading hierarchy (ordering only, not semantic structure), focus order, screen-reader announcement quality, dynamic content updates.

Second-opinion gates:

- pa11y-ci: `pa11y-ci --sitemap https://<host>/sitemap.xml --threshold 0` runs HTML_CodeSniffer against a sitemap (different rule set than axe, high but partial overlap).
- Lighthouse a11y: `lhci autorun --collect.url=https://<staging-host>` catches contrast in computed styles after JS runs. Require `categories:accessibility:score >= 0.95` (target 1.0).

Check: every route has an axe test asserting zero violations; disabled rules are enumerated in a config file with a comment per rule; manual axe in DevTools is in the pre-launch checklist (see pre-launch.md). At least two a11y tools run in CI (axe plus pa11y or Lighthouse), all asserting zero violations or a documented baseline that only decreases over time, never increases.

## Performance Budgets in CI

A performance budget is a number asserted on every PR that fails the merge when exceeded. Without it, every PR is one regression closer to a slow site.

JS/CSS gzipped budget (size-limit). Block merge when exceeded; PR comment shows current vs limit vs delta; use `--why` to name the dependency that pushed it over.

```json
{
  "size-limit": [
    { "path": "dist/assets/index-*.js", "limit": "90 KB", "gzip": true },
    { "path": "dist/assets/index-*.css", "limit": "20 KB", "gzip": true },
    { "path": "dist/assets/vendor-*.js", "limit": "120 KB", "gzip": true }
  ]
}
```

Per-route budget (bundlesize). Stricter than total-app budgets; catches a heavy library imported on a route that did not need it:

```json
{
  "bundlesize": [
    { "path": "dist/route-home-*.js", "maxSize": "60 KB", "compression": "gzip" },
    { "path": "dist/route-product-*.js", "maxSize": "80 KB", "compression": "gzip" },
    { "path": "dist/route-checkout-*.js", "maxSize": "100 KB", "compression": "gzip" }
  ]
}
```

| Budget | Limit (gzipped) | Tool |
|--------|-----------------|------|
| Main index JS | 90 KB | size-limit |
| Main index CSS | 20 KB | size-limit |
| Vendor JS | 120 KB | size-limit |
| Per-route home chunk | 60 KB | bundlesize |
| Per-route product chunk | 80 KB | bundlesize |
| Per-route checkout chunk | 100 KB | bundlesize |

Field-INP gate (lab budgets miss field INP). Query CrUX p75 INP for the production origin, compare to baseline, and fail the deploy on a regression of more than 50ms. Gates on the rolling 28-day CrUX p75: the signal lags 28 days but is the only field-INP gate that needs no private RUM pipeline:

```bash
curl -s "https://<crux-api-host>/runPagespeed?url=https://<origin>&strategy=mobile&key=$PSI_KEY" \
  | jq -e '.loadingExperience.metrics.INTERACTION_TO_NEXT_PAINT_MS.percentile <= 200'
```

Check: CI has at least one bundle-size gate (size-limit or bundlesize) and at least one field-perf gate (CrUX query); budgets are defined per route, not just per app; budget changes are reviewed in PRs.

## Type-Only and Lint Gates

Static analysis catches regressions runtime tests miss. Run each as a separate CI job for faster feedback than the bundler.

- Type check: `npx tsc --noEmit --project tsconfig.json` type-checks every file without output; assert zero errors. tsconfig has `strict: true`.
- Strict flags worth enabling, each catching a real bug class: `strict`, `noUncheckedIndexedAccess`, `exactOptionalPropertyTypes`, `noImplicitOverride`, `noFallthroughCasesInSwitch`. Turn on new strict flags one at a time with a clean-up PR per flag.
- Lint beyond types: `npx biome check src/` or `npx eslint --max-warnings 0 src/` treats warnings as errors, catching dead code, hooks-rules violations, accessibility lint, framework-specific traps, import-cycle warnings.

Check: type check and lint run in CI; warnings count is monitored and trends down; ignored rules are enumerated in the config with a comment per rule.

## Contract Tests

The frontend-backend contract is a recurring regression source. Two patterns.

Consumer-driven contract: the consumer writes the requests it makes and responses it expects; the provider verifies that contract against its own implementation in its own CI; both publish to a shared broker. When the backend renames a field without updating the contract, its CI fails, so the frontend learns at PR time not deploy time. Requires the backend to run verification in their CI.

```js
provider.addInteraction({
  state: 'a user exists',
  uponReceiving: 'a request for the user profile',
  withRequest: { method: 'GET', path: '/api/users/1' },
  willRespondWith: {
    status: 200,
    body: { id: 1, name: like('Alice'), email: like('alice@example.com') },
  },
});
```

Runtime schema validation (lower coordination): declare the response schema (Zod, Valibot, JSON Schema, OpenAPI), validate every response in development, log failures to the error tracker. Catches schema drift in production with attribution:

```js
const UserSchema = z.object({
  id: z.number(),
  name: z.string(),
  email: z.string().email(),
});

const response = await fetch('/api/users/1');
const parsed = UserSchema.safeParse(await response.json());
if (!parsed.success) reportError({ type: 'schema-mismatch', issues: parsed.error.issues });
```

Check: every consumed API endpoint has either a consumer-driven contract or a runtime schema validation; schema mismatches in production trip an alert (see observability.md).

## lighthouse-ci Wiring

The highest-leverage perf gate. Wire it into the PR loop so a regression blocks the merge.

CI job (run on pull_request after dependency install and build):

```yaml
name: lighthouse-ci
on: pull_request
jobs:
  lhci:
    runs-on: ubuntu-latest
    steps:
      - uses: <checkout-step>
      - uses: <setup-node-step>
        with:
          node-version: '20'
      - run: npm ci
      - run: npm run build
      - name: run lhci
        uses: <lighthouse-ci-action>
        with:
          urls: |
            https://<staging-host>/
            https://<staging-host>/product
            https://<staging-host>/checkout
          uploadArtifacts: true
          temporaryPublicStorage: true
          configPath: ./lighthouserc.json
```

- `urls`: one per representative surface, covering home, a content route, and a flow-critical route (checkout, sign-up).
- `uploadArtifacts: true` saves the raw Lighthouse JSON as a build artefact (failures reproducible in review).
- `temporaryPublicStorage: true` posts a public-viewer URL in the PR comment.
- `configPath` points to the assertion config below.

Assertion config. `numberOfRuns: 5` reduces flake; the action reports the median:

```json
{
  "ci": {
    "collect": { "numberOfRuns": 5, "settings": { "preset": "desktop" } },
    "assert": {
      "preset": "lighthouse:recommended",
      "assertions": {
        "categories:performance": ["error", { "minScore": 0.9 }],
        "categories:accessibility": ["error", { "minScore": 0.95 }],
        "categories:best-practices": ["error", { "minScore": 0.95 }],
        "categories:seo": ["error", { "minScore": 0.95 }],
        "first-contentful-paint": ["error", { "maxNumericValue": 1800 }],
        "largest-contentful-paint": ["error", { "maxNumericValue": 2500 }],
        "cumulative-layout-shift": ["error", { "maxNumericValue": 0.1 }],
        "total-blocking-time": ["error", { "maxNumericValue": 200 }],
        "interactive": ["warn", { "maxNumericValue": 3500 }]
      }
    }
  }
}
```

The action posts a per-surface score-delta comment so the reviewer sees the trend and fixes regressions in the same PR:

```text
Lighthouse scores

| Category      | / | /product | /checkout |
|---------------|---|----------|-----------|
| Performance   | 96 (-1) | 94 (-2) | 91 (=) |
| Accessibility | 100 (=) | 100 (=) | 100 (=) |
| Best Practices| 100 (=) | 100 (=) | 100 (=) |
| SEO           | 100 (=) | 100 (=) | 100 (=) |
```

Check: every PR shows the Lighthouse summary as a comment within 5 minutes of the build completing; score regressions block the merge; the public-viewer URL is opened during PR review.

## CI realism for a perfect-score pipeline

Keeping the gate green in a real runner needs three discipline rules. The gate must stay hermetic, the variable metric must be floored not pinned, and gates split by feedback cost.

- Hermetic gate (T1-D): when a Lighthouse CI gate runs against a local preview server, do NOT let a self-injected third-party tag into that build, or the gate starts depending on the external network and fails in a locked-down runner. Emit such tags only in the real deploy build (gate the tag on a deploy-only env var). The audit gate stays hermetic; production still gets the tag.
- Performance is the variable category (T3-J): on a shared CI runner (often 2 vCPU) the mobile preset 4x CPU throttle competes for the same cores, depressing and destabilizing Performance, while A11y, Best Practices, and SEO are deterministic. Gate Performance at a floor (e.g. `minScore` 0.9) while holding the other three at 1.0. Do not run more Lighthouse workers than the runner has cores; get wall-clock parallelism from a sliced-URL matrix across separate runners, not many workers on one box.
- Split gates by feedback cost (T3-K): fast deterministic gates (format, lint, type, unit) run per PR; slow gates (full LHCI, visual regression, end-to-end) run on pre-push and on-merge, not on every PR push.

## Testing vs Audit Workflow

Two complementary disciplines, often conflated. Run both: tests are the gate, the audit loop is the polish.

| Property | Testing (pre-merge gate) | Audit (post-build polish loop) |
|----------|--------------------------|--------------------------------|
| When | Every PR | Per deploy or per cadence |
| Output | Pass / fail | Findings list |
| Feedback | Minutes | Hours |
| Scope | Changed code | Entire surface |
| Decision | Block merge | Schedule polish work |
| Coverage | Mechanical | Includes judgement |

- Testing catches: logic regression (unit, integration), style drift (visual), surface-flow break (end-to-end), accessibility violation (axe, Lighthouse a11y), performance-budget breach (size-limit, Lighthouse perf assertion), schema drift (contract).
- Audit catches what fixed-viewport snapshots and lab Lighthouse cannot: real-device rendering and OS font stacks, real-network/real-CPU performance, human screen-reader findings (meaningful alt text, heading semantics), SEO drift (canonical drift, broken structured data, sitemap freshness), cross-browser quirks (Firefox-only, Safari-only), brand polish (motion timing, copy tone, illustration consistency).

A site with tests and no audits ships mechanically correct code that looks shabby; a site with audits and no tests ships polished code that regresses every release.

Check: the release process documents both gates (tests mandatory, audits scheduled); audit findings feed the next sprint as issues, not emergency hotfixes (see audit-workflow.md).

## See Also

- [lighthouse.md](lighthouse.md): the audit-row mapping the lighthouse-ci assertions reference
- [observability.md](observability.md): post-deploy field signals that complement pre-merge tests
- [pre-launch.md](pre-launch.md): the evidence manifest that consumes the CI artefacts
- [audit-workflow.md](audit-workflow.md): the post-build polish loop these gates complement
