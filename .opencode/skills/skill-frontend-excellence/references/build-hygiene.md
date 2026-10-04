---
title: Build Hygiene
purpose: Bundle, tree-shake, code-split, and audit dependencies so every shipped kilobyte has a line item and the JS budget is gated in CI, not aspirational.
load-when:
  task-keywords: [build, tree-shaking, dependency, sideEffects, lockfile, dead code, code splitting, bundle, dead code, audit]
  symptoms: [bundle size grew, slow page, score dropped, LCP regression]
prereq: SKILL.md
related: [performance.md, security.md, observability.md, testing.md]
size: ~362 lines
---

# Build Hygiene

Every shipped kilobyte is paid for: by the user's network, by parse-and-compile CPU, by main-thread execution. Bundle hygiene makes that cost an auditable line item.

## Why Bundle Hygiene Matters

| Fact | Value |
|---|---|
| Parse-and-compile cost (mid-range Android) | ~50 ms per 30 KB gzipped JS |
| Execution cost under cold start | +100 to 200 ms per 30 KB gzipped JS |
| Initial-route JS budget | 90 KB compressed (decompresses to ~270 KB of code) |

Two refusals:

- "Budgets are aspirational." A budget not gated in CI is a wish. Gate budgets with `size-limit` and `bundlesize` (see testing.md).
- "It is only one more dependency." A real audit found an unused export pulled in by a barrel re-export accounted for 14 KB of the initial bundle: a dependency-of-a-dependency nobody read, kept in by a side-effects misdeclaration despite tree-shaking that should have removed it.

Discipline: every dependency has a line item, every line item has an owner, every owner can defend the bytes.

## Tree-Shaking at Depth

Tree-shaking is dead-code elimination on ESM imports. Three preconditions, all required:

- ES modules end-to-end
- accurate `sideEffects` declarations
- static (analyzable) imports

### ESM-only imports

CommonJS resists shaking: module shape is not statically known until execution, so `require('lodash')` pulls every export. `import { debounce } from 'lodash-es'` pulls only `debounce` plus its transitive imports.

| Check | Threshold/target |
|---|---|
| Authored packages declare module type | `"type": "module"` in `package.json`, or every authored file uses `.mjs` |
| Dependencies expose ESM | each has an `"exports"` map with an `"import"` condition, not just `"require"` |
| Shipped code is CJS-free | search the production bundle: no `require(` calls remain |

### `sideEffects` in `package.json`

`"sideEffects": false` tells the bundler that importing for named-export retrieval has no observable side effect, so unused imports drop:

```json
{
  "name": "your-pkg",
  "type": "module",
  "sideEffects": false
}
```

Declare side-effectful files explicitly (a CSS import, a polyfill registration, a global mutation):

```json
{
  "sideEffects": ["**/*.css", "./src/polyfills.js"]
}
```

Omitting the `sideEffects` field is the worst case: the bundler must assume every import is load-bearing and ship everything reachable.

### The three traps

- Re-export barrels: `export * from './a'; export * from './b';` forces the bundler to walk every transitive module; if any module has a side effect or resists analysis, the whole barrel ships. Prefer deep imports (`import { foo } from 'pkg/foo'`) over barrel imports (`import { foo } from 'pkg'`) for any non-trivial library.
- Default exports: a default object resists property-level shaking (the bundler cannot prove only one property is used). Convert default exports to named exports for any module with more than one logical export.
- Dynamic property access: `lib[methodName]()` or `import(`./${variable}.js`)` prevents static export determination; shaking falls back to including everything. Replace with switch statements or named lookups.

Check: build a production bundle, open the analyzer, search for any package supposedly imported for one function. If the rest of the package is present, one of the three traps is in play.

## `sideEffects` Declarations and How to Verify

Two declaration levels: package (`package.json`) and file (a build-tool comment).

### Package-level

| Pattern | `sideEffects` value |
|---|---|
| Pure utility library, no globals | `false` |
| Library with one polyfill entry, otherwise pure | `["./src/polyfills.js"]` |
| Library that registers global event listeners on import | omit (forces pessimistic inclusion) |

### File-level

Use the `/*#__PURE__*/` annotation on factory calls so the bundler drops them if the result is unused:

```javascript
export const validator = /*#__PURE__*/ buildValidator(SCHEMA);
```

Without it, `buildValidator(SCHEMA)` is presumed side-effecting and stays even if `validator` is never imported.

### How to verify

| Tool | Use | Cadence |
|---|---|---|
| Bundle analyzer (`webpack-bundle-analyzer`, `rollup-plugin-visualizer`, `esbuild --analyze`, `vite-bundle-visualizer`) | eyeball the largest squares | every production build |
| Source map explorer | trace each kilobyte back to its source file; unrelated packages = side-effect leaks | per investigation |
| Diff between releases | flag any dependency that grew by more than 5 KB or any new dependency over 10 KB | every release |

## Code-Splitting Strategy

Ship only the code the current view needs. Three granularities.

### Route-level

One chunk per route, loaded on navigation. The default in modern frameworks and the highest-leverage split.

Check: on the landing route's network tab, the number of JS requests matches framework runtime plus the route entry, not the whole app.

### Component-level

Lazy-load a heavy widget behind a Suspense or loading boundary using the platform `import()`:

```javascript
async function openEditor() {
  const { Editor } = await import('./editor.js');
  mount(Editor);
}
```

Earns its cost when the widget is over 30 KB compressed and used by fewer than half of route visitors. Below that, the split adds latency without saving bytes for the typical user.

### Interaction-level

Load on intent (hover, focus, intersection), guarded by a `loaded` flag:

```javascript
const trigger = document.querySelector('#open-modal');
let loaded = false;
trigger.addEventListener('pointerenter', async () => {
  if (loaded) return;
  loaded = true;
  await import('./modal.js');
});
```

Below-the-fold widgets: load when the viewport approaches via IntersectionObserver with `{ rootMargin: '200px' }`:

```javascript
const observer = new IntersectionObserver((entries) => {
  for (const entry of entries) {
    if (entry.isIntersecting) {
      import(entry.target.dataset.module);
      observer.unobserve(entry.target);
    }
  }
}, { rootMargin: '200px' });
```

### Decision matrix

| Scenario | Strategy |
|---|---|
| Per-route view | Route-level split (default) |
| Heavy widget, used by some visitors | Component-level lazy |
| Below-the-fold widget, used by many | Interaction-level (intersection) |
| Modal opened by a button | Interaction-level (hover or focus) |
| Hot path used by everyone | Bundle eagerly (no split) |
| Auth-gated module | Route-level split plus auth check |

## Dependency-Cost Discipline

Every install is a budget decision. Three checkpoints.

| Checkpoint | Rule |
|---|---|
| Size gate (bundlephobia or equivalent) | Reject any production dependency over 30 KB minified-and-gzipped without a clear defended reason; reject any over 100 KB without a one-paragraph PR justification. |
| Transitive tree | Before merge, run `npm install --dry-run <pkg>` and inspect the tree; a direct dependency pulling 40 transitive deps differs from one pulling two. Count transitives as part of the cost. |
| Platform-first justification | Every dependency added in the last sprint has a PR comment explaining why the platform was not enough. |

The "one dep doubled the bundle" failure mode: a date library added to format two timestamps eagerly loads its own locale data (200 KB), doubling the bundle. Responses in order of preference:

- Use the platform: `Intl.DateTimeFormat`, `Intl.NumberFormat`, `Intl.RelativeTimeFormat` (see i18n.md).
- Pick a slimmer alternative with tree-shaken, scoped imports over one that imports every locale at module load.
- Write the utility yourself: five lines of `String.prototype.padStart` beat 30 KB of dependency in many cases.

## Lockfile Hygiene

The lockfile is the source of truth for reproducible builds.

| Check | Rule |
|---|---|
| Lockfile committed | Always commit `package-lock.json`, `pnpm-lock.yaml`, or `yarn.lock`; without it CI produces different bundles each run and regressions become unattributable. |
| CI install command | Use `npm ci` (or `pnpm install --frozen-lockfile`, `yarn install --immutable`): installs exactly what the lockfile pins and fails on `package.json`/lockfile disagreement. Never plain `npm install` in CI. |
| Scan CI config | Search for `npm install` without `ci`; every occurrence is a defect. |
| Dirty-lockfile gate | CI fails if the lockfile is dirty after install: `git diff --exit-code package-lock.json`. |
| Review lockfile diff | The lockfile is reviewed in every PR that touches `package.json`. |

CI install step:

```yaml
- run: npm ci
```

### The `npm audit` budget

```text
npm audit --audit-level=high --omit=dev
```

Reject high and critical vulnerabilities in production dependencies. Track and triage moderate vulnerabilities on a weekly or sprint-aligned cadence. Dev dependencies are filtered out because they do not ship to users. See security.md for Subresource Integrity and the broader supply-chain story.

## Dead-Code Elimination

Dead code (provably unreachable) and unreferenced-but-imported code look similar but have different fixes.

### Minifier configuration

Modern minifiers (`terser`, `swc`, `esbuild` in minify mode) do dead-branch elimination as part of compression. Verify in CI:

- The production build sets `NODE_ENV` to `production` so dev-only branches drop out.
- The minifier enables `dead_code`, `unused`, `pure_funcs`, and `passes: 2` (terser) or the framework equivalent.
- Production builds contain no string literals from dev-only code paths (search the bundle for known dev-only error messages).

### Fix table

| Pattern | Fix |
|---|---|
| Dead branch (`if (false) { ... }`) | Trust the minifier; verify the branch is gone in the prod bundle. |
| Unreferenced import with side effects | Remove the import, or accurately scope `sideEffects` so it can be dropped when unused. |
| Imported export, never read | Verify the package has `sideEffects: false`; if not, file an upstream issue or patch. |

## `import()` Analysis

The bundler determines lazy chunks by walking every `import()` at build time. Shape matters:

```javascript
import('./modal.js');                 // Static path: produces one chunk.
import(`./views/${viewName}.js`);     // Template with literal head: one chunk per matching file.
import(routeModule);                  // Pure variable: not analyzable; falls back to one big chunk or fails.
```

Prefer the first two; avoid the pure-variable form in production code (it defeats code-splitting).

### Magic comments and bundler equivalents

```javascript
import(/* webpackChunkName: "editor" */ './editor.js');
import(/* webpackPrefetch: true */ './settings.js');
import(/* webpackPreload: true */ './critical-modal.js');
```

`webpackChunkName` produces stable file names (better cache hit rate); `webpackPrefetch` adds `<link rel="prefetch">` (fetched when idle); `webpackPreload` adds `<link rel="preload">` (immediately needed).

| Bundler | Chunk naming | Preload signal |
|---|---|---|
| Webpack | `/* webpackChunkName */` | `/* webpackPreload */` |
| Vite (Rollup) | `chunkFileNames` config | Manual `<link>` tag |
| Rollup | `output.chunkFileNames` | Manual `<link>` tag |
| esbuild | `chunkNames` config | Manual `<link>` tag |
| Parcel | Automatic | Manual `<link>` tag |

For bundlers without inline preload directives, add the hint to the document head:

```html
<link rel="prefetch" href="/assets/editor.abc123.js" as="script">
```

## Source Maps for Builds

Source maps make production stack traces readable without shipping unminified code.

| Pattern | What it does | When |
|---|---|---|
| Inline (`//# sourceMappingURL=data:`) | Embeds the map in the bundle | Dev only; never production |
| Public (`//# sourceMappingURL=file.js.map`) | Map sits next to the bundle, browser fetches it | OK for open-source; leaks source for proprietary |
| Hidden (no comment, build emits the map) | Map built but not referenced; upload to error tracker | Production for proprietary code |

Production pattern: build hidden source maps, upload them to the error tracker on deploy, never serve them from origin. Users see a minified bundle; the team sees real frames.

```text
# Webpack
devtool: 'hidden-source-map'

# Vite
build.sourcemap: 'hidden'

# Rollup
sourcemap: 'hidden'

# esbuild
sourcemap: 'external'
```

Upload step in CI (error trackers support similar commands):

```text
sentry-cli sourcemaps upload --release "$VERSION" ./dist
```

See observability.md for the full error-capture pipeline that depends on the source-map upload.

## Polyfill Discipline

Ship the minimum polyfills the supported browsers need, never more.

### `browserslist` as the source of truth

One list in `package.json`, read by PostCSS, Babel, Autoprefixer, and the ESLint browser-compat plugin. Do not duplicate per tool:

```json
{
  "browserslist": [
    ">0.2%",
    "not dead",
    "not op_mini all"
  ]
}
```

Audit the resolved list at least once per quarter: run `npx browserslist`, drop browsers below 0.2 percent usage and vendor-unsupported browsers.

### `core-js` minimum-target subsets

Configure `core-js` with the same `browserslist` so only used polyfills are added:

```json
{
  "presets": [["@babel/preset-env", {
    "useBuiltIns": "usage",
    "corejs": { "version": "3.36", "proposals": false }
  }]]
}
```

`useBuiltIns: "usage"` adds only the polyfills the code uses; `"entry"` and the global import are bigger and rarely justified.

### Modern and legacy bundles

Differential serving ships a modern bundle to modern browsers and a transpiled legacy bundle to older ones:

```html
<script type="module" src="/assets/modern.js"></script>
<script nomodule src="/assets/legacy.js"></script>
```

Pay the two-bundle build complexity only when the legacy share is over 10 percent of traffic and the modern bundle saves over 20 KB compressed. Below those thresholds, ship one transpiled bundle.

Check: the modern-browser production bundle does not include `Array.prototype.flat` polyfill, generator runtime, or async-await transforms. If it does, `browserslist` is wider than the audience.

## Build Plumbing (Footnote)

Not bundle hygiene proper, but it breaks builds:

- Exclude tool output directories (audit scratch dir, screenshot dumps, generated reports) from the dev/preview file watcher; otherwise the watcher loops on its own output and the preview server thrashes.
- Date-shard or namespace generated audit artifacts so runs do not overwrite each other.

## See Also

- [performance.md](performance.md): budget framing, render strategies, and the asset-loading rules bundle hygiene feeds.
- [security.md](security.md): Subresource Integrity, supply-chain, and dependency vulnerability gates.
- [observability.md](observability.md): hidden source-map upload and the error-tracking pipeline that depends on it.
- [testing.md](testing.md): `size-limit`, `bundlesize`, lighthouse-ci, and the CI checks that make budgets auditable.
- [seo.md](seo.md): the clean-URL contract (flat-file build output and the static-host audit gotcha).
