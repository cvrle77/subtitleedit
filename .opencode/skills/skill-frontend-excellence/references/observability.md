---
title: Frontend Observability
purpose: Post-deploy production practice covering source maps in production, error capture surfaces, sampled RUM, INP field attribution, the Reporting API, CrUX trend tracking, session replay tradeoffs, alert thresholds, and synthetic monitoring. Lighthouse measures a deploy; observability measures users, and one does not substitute for the other.
load-when:
  task-keywords: [observability, RUM, monitoring, error capture, source maps, INP attribution, Reporting API, error boundary, CrUX, Long Animation Frames, LoAF, performance]
  symptoms: [score dropped, INP regression, LCP regression, slow page, slow interaction]
prereq: SKILL.md
related: [performance.md, lighthouse.md, debug-recipes.md, pre-launch.md]
size: ~335 lines
---

# Frontend Observability

Lighthouse measures a deploy; observability measures users. Patterns are named at the standard level (`PerformanceObserver`, `Reporting-Endpoints` header, `sendBeacon`). Vendor tools are referred to generically (error tracker, paging system) so the standard, not a product, is the contract.

## Source Maps in Production

Minified production code turns every error into an opaque stack. Public maps disclose source. The answer is private upload.

Principle: build hidden source maps, upload them to the error tracker out of band, strip the public source-map URL from the bundle.

Config by toolchain:

- Webpack / Rspack: `devtool: 'hidden-source-map'`
- Vite / Rollup: `build.sourcemap: 'hidden'`
- esbuild: `sourcemap: 'external'` plus a script step to strip the `//# sourceMappingURL=` comment

The `hidden` variant emits `.map` files alongside the bundle but appends no `//# sourceMappingURL=` comment, so the browser never fetches them; the error tracker fetches them through its own upload pipeline (a CLI map-upload step).

Checks:

- `curl -sI https://your-cdn.example/static/main.<hash>.js.map` returns 403 or 404
- The bundle contains no `//# sourceMappingURL=` comment
- The error-tracker dashboard shows symbolicated stacks
- Production HTML does not link to `.map` files

### Source-map response header (cross-origin alternative)

When the error tracker self-hosts maps on a different origin, the `SourceMap:` HTTP response header on the JS resource points at the map URL, restricted to the error-tracker IP range at the edge (a WAF IP allow-list).

Example:

```text
GET /static/main.abc123.js
SourceMap: https://maps.internal.your-domain.example/main.abc123.js.map
```

Check: the map URL returns 200 only for the error-tracker egress IP range and 403 for everything else; the IP range is documented and refreshed on every vendor update.

### Never ship maps on the public CDN

Principle: a `main.js.map` on the public CDN is source code disclosure (original file paths, comments, inline-bundled secrets). The mistake is building with `devtool: 'source-map'` (the default in many configs) and deploying it to the same path as the bundle.

Check: a pre-deploy script asserts no `*.map` files in the public output directory (or asserts they are excluded from the CDN upload manifest); CI fails when the assertion fails.

## Error Capture Surfaces

A JS error reaches the tracker through one of four hooks. Wire all four; each catches a different surface.

| Hook | Catches | Note |
|------|---------|------|
| `window.onerror` | synchronous uncaught errors from any script, including inline | mandatory baseline |
| `unhandledrejection` | Promise rejections that never had a `.catch` | more than half of all reported errors in modern apps |
| Error boundary (component-tree) | render/commit throws in a subtree | renders fallback UI, forwards error |
| `ReportingObserver` | browser deprecations and interventions | never throw, never log to console outside DevTools |

`window.onerror`:

```js
window.addEventListener('error', (event) => {
  reportError({
    message: event.message,
    source: event.filename,
    line: event.lineno,
    column: event.colno,
    stack: event.error?.stack,
    type: 'uncaught',
  });
});
```

`unhandledrejection`:

```js
window.addEventListener('unhandledrejection', (event) => {
  reportError({
    message: event.reason?.message ?? String(event.reason),
    stack: event.reason?.stack,
    type: 'unhandled-rejection',
  });
});
```

Check: a deliberate `Promise.reject(new Error('test'))` from the DevTools console arrives in the tracker within the expected sampling rate.

Error boundary: wire a component-tree boundary at every route and at every dangerous subtree (a chart, a third-party widget, a markdown renderer). It renders a fallback UI and forwards the error. Checks: every route has a boundary that renders a recovery UI without a full-page crash; boundary errors arrive with the route name and subtree identifier; a deliberate throw in a subtree does not blank the page.

`ReportingObserver`:

```js
const observer = new ReportingObserver((reports) => {
  for (const report of reports) {
    reportError({
      type: report.type,           // 'deprecation' or 'intervention'
      message: report.body.message,
      source: report.body.sourceFile,
      line: report.body.lineNumber,
    });
  }
}, { buffered: true });
observer.observe();
```

It catches features removed from the platform, slow scripts the browser stopped, and autoplay blocked. Checks: registered before any third-party script runs; `deprecation` and `intervention` reports reach the tracker; the dashboard groups reports by source file.

## Sampled RUM

Lab numbers do not predict field numbers. Real-user monitoring is the only way to know what users experience. Use a field-metrics library exposing per-metric callbacks (`onLCP`, `onINP`, `onCLS`, `onFCP`, `onTTFB`) with an attribution entry point that adds per-metric debugging context (LCP element, INP event target, CLS source).

```js
import { onLCP, onINP, onCLS, onFCP, onTTFB } from 'web-vitals/attribution';

const queue = [];
const flush = () => {
  if (queue.length === 0) return;
  navigator.sendBeacon('/rum', JSON.stringify(queue.splice(0)));
};

const send = (metric) => {
  queue.push({
    name: metric.name,
    value: metric.value,
    rating: metric.rating,
    id: metric.id,
    attribution: metric.attribution,
    nav: performance.getEntriesByType('navigation')[0]?.type,
    cls: metric.name === 'CLS' ? metric.entries.length : undefined,
  });
};

onLCP(send); onINP(send); onCLS(send); onFCP(send); onTTFB(send);

addEventListener('visibilitychange', () => {
  if (document.visibilityState === 'hidden') flush();
});
addEventListener('pagehide', flush);
```

Three things this gets right:

- `sendBeacon` plus `visibilitychange` is the only reliable flush for the BFCache and tab-close case; `fetch` with `keepalive: true` is a backup but loses entries when the browser kills the request.
- Queue and batch: one beacon per page session, not one per metric, to reduce beacon overhead.
- Attribution payload: when INP is bad, the attribution object names the event target, the handler script, and the blocking time.

Check: the RUM endpoint receives entries with `attribution` populated; the dashboard ranks pages by p75 INP and lists top contributing elements per page.

### Sample rate

| Traffic | Sample rate |
|---------|-------------|
| High-traffic | 10 to 25 percent (plenty for trend tracking) |
| Low-traffic | 100 percent |

The sample decision happens once per session (`Math.random() < 0.1`), stored in a session-scoped variable, applied to all metrics for that session.

Check: sample rate is documented; beacon volume per day is tracked as a cost line item; sample rate is reviewed when traffic grows by more than 5x.

## INP Field Attribution Recipe

Field INP regressions are often invisible in the lab and are the leading score-dropped cause for 2024+ Core Web Vitals. Use two `PerformanceObserver`s: an `event` observer with `durationThreshold: 200` and a `long-animation-frame` observer with `durationThreshold: 50`.

```js
const slowEvents = new PerformanceObserver((list) => {
  for (const entry of list.getEntries()) {
    if (entry.duration < 200) continue;
    queue.push({
      type: 'slow-event',
      name: entry.name,
      duration: entry.duration,
      processingStart: entry.processingStart,
      processingEnd: entry.processingEnd,
      target: entry.target?.tagName + (entry.target?.id ? `#${entry.target.id}` : ''),
    });
  }
});
slowEvents.observe({ type: 'event', durationThreshold: 200 });

const loaf = new PerformanceObserver((list) => {
  for (const entry of list.getEntries()) {
    if (entry.blockingDuration < 50) continue;
    queue.push({
      type: 'loaf',
      duration: entry.duration,
      blockingDuration: entry.blockingDuration,
      scripts: entry.scripts.map((s) => ({
        invoker: s.invoker,
        sourceURL: s.sourceURL,
        sourceFunctionName: s.sourceFunctionName,
        forcedStyleAndLayoutDuration: s.forcedStyleAndLayoutDuration,
      })),
    });
  }
});
loaf.observe({ type: 'long-animation-frame', durationThreshold: 50 });
```

The `event` observer skips entries with `entry.duration < 200` (200ms). The `long-animation-frame` observer (LoAF, Chrome 123+) skips entries with `entry.blockingDuration < 50` and reports every animation frame longer than 50ms with script attribution.

Mapping field INP to root cause:

- The metrics library reports INP value, rating, and the slow event entry (target, handler script).
- The LoAF observer, firing around the same time, names the scripts that ran in the blocking frame.
- Cross-reference by timestamp: the slow event plus the LoAF entries in the same animation frame are the same incident.
- `scripts[].sourceURL` and `scripts[].sourceFunctionName` give the file and function, resolved to original code via uploaded source maps.

Check: the dashboard, queried with "show me all field INP entries above 500ms in the last week", returns a ranked list grouped by `attribution.eventTarget` plus `scripts[].sourceFunctionName`, with actionable top entries.

## Reporting API

The Reporting API standardises browser-to-server delivery of CSP violations, deprecations, interventions, and network errors. It runs in parallel to the error tracker and catches things JS cannot see. The `Reporting-Endpoints` header (replacing the older `Report-To` header from 2024 onward) names destinations; CSP, NEL, Document-Policy, and Permissions-Policy violations each route to a named endpoint.

```text
Reporting-Endpoints: default="https://reports.your-domain.example/", csp-endpoint="https://csp.your-domain.example/"
Content-Security-Policy: default-src 'self'; ...; report-to csp-endpoint;
Document-Policy: ...; report-to=default
NEL: {"report_to":"default","max_age":604800}
```

Reports arrive as POSTed JSON arrays, content-type `application/reports+json`. Each report has `type`, `url`, `user_agent`, `body`, and a timestamp.

| Category | Trigger | Body fields |
|----------|---------|-------------|
| csp-violation | a blocked load or inline execution | `blocked-uri`, `effective-directive`, `original-policy` |
| deprecation | a feature scheduled for removal | deprecation id, message, source |
| intervention | a feature the browser disabled (autoplay, slow script) | intervention id |
| network-error | a failed resource load (DNS failure, TLS error, HTTP error) | protocol, method, status code |

Check: endpoints receive reports within minutes of a deploy violating an unrolled-out policy; the CSP report stream is monitored during rollout and goes silent before promotion to enforcing.

Browser support: Chromium and Edge ship the full API; Firefox supports CSP reporting and a subset of NEL; Safari ships CSP reporting through the older `report-uri` directive, so keep `report-uri` alongside `report-to` for coverage.

## CrUX and PageSpeed Insights

The Chrome User Experience Report (CrUX) is the public field dataset used for the Core Web Vitals ranking signal; the PageSpeed Insights (PSI) API exposes it per origin and per URL.

```bash
curl -s "https://www.googleapis.com/pagespeedonline/v5/runPagespeed?url=https://your-domain.example&strategy=mobile&key=YOUR_KEY" \
  | jq '.loadingExperience.metrics'
```

This returns p75 LCP, INP, CLS, FCP, TTFB over a rolling 28-day window. Each metric is categorised `FAST`, `AVERAGE`, or `SLOW`.

Gate: run the PSI query on every deploy and fail the deploy if any metric crosses from `FAST` to `AVERAGE` or worse, or if p75 INP grows by more than 50ms week-over-week.

Check: CI has a "perf budget" job that pulls CrUX and asserts the budget; the budget is documented per route in pre-launch.md evidence; regression alerts go to the on-call rotation.

Limitations: CrUX requires opt-in Chrome users, low-traffic origins return `null`, and authenticated routes behind login never appear, so RUM is the only signal for these.

## Session Replay Tradeoffs

Session replay records the DOM, network, and input so engineers can scrub a real session. Powerful for diagnosis, expensive in privacy surface.

Costs:

- Bytes: recording libraries are 30 to 100 KB gzipped on the wire; even deferred-init bundles are non-trivial.
- CPU: continuous DOM diffing and serialisation, modest but measurable in INP if the page is already close to the threshold.
- Storage: minutes of mutation logs per session.
- Privacy: recordings are personal data subject to right-of-access, right-to-erasure, lawful-basis disclosure, and data-processing agreements (GDPR surface).

PII redaction (defaults are not safe enough):

- Default block: treat every input, textarea, contenteditable, and `[data-pii]` element as masked by default; opt in to capture only for demonstrably safe fields.
- Network redaction: redact request and response bodies for endpoints that touch user data; keep headers minus Authorization and Cookie.
- URL redaction: strip query strings containing tokens such as `?access_token=` and `?reset=`.
- Console redaction: redact log lines matching known PII patterns (emails, credit-card-like, JWT-like).

Check: a deliberate session with a fake email and fake card number is replayed with both fields as redacted placeholders; the vendor data processing agreement names the PII categories captured and the retention.

Sample, do not capture everything: replay on 100 percent of error sessions and 1 percent of clean sessions.

## Alert Thresholds

Observability without alerts is a museum exhibit. Set thresholds, page on-call when they trip.

Page load failure rate (navigations that never reach a usable state):

| Threshold | Action |
|-----------|--------|
| < 0.5% | Healthy |
| 0.5% to 1% | Warning, investigate within a day |
| > 1% | Page on-call, treat as incident |

Measure: count navigations where `PerformanceNavigationTiming.loadEventEnd > 0` against total navigations, or distinct sessions with no successful page view against distinct sessions that started one.

JS error rate per session (sessions emitting at least one uncaught error or unhandled rejection):

| Threshold | Action |
|-----------|--------|
| < 1% | Healthy |
| 1% to 3% | Warning, file an issue |
| > 3% | Page on-call |

INP p75 vs CWV bar: the CWV bar for INP is 200ms; alert when origin p75 INP crosses 200ms (regression from passing) or grows by more than 20 percent week-over-week.

CWV passing rate per route: group routes by template (home, product, checkout) and track the passing rate as the fraction of sessions where all three CWVs (LCP, INP, CLS) hit the "Good" threshold; a drop is the leading indicator of a deploy regression before CrUX picks it up.

Check: alerts route to a paging system; each alert has a runbook in debug-recipes.md; alert fatigue is reviewed monthly.

## Synthetic Monitoring vs RUM

Synthetic and RUM measure different things; a robust story uses both.

Synthetic catches: availability (200 from a probe location), lab-Lighthouse performance under controlled conditions, regression of a critical user flow (login, checkout) under fixed browser and network, and third-party dependency outages.

RUM catches: real-user device distribution (slow Android on 3G), field-INP regressions invisible in the lab, regional performance variance, and real-user error rates (a widget crashing only on iOS Safari 17.2).

The split:

| Question | Use |
|----------|-----|
| Is the site up? | Synthetic |
| Did the latest deploy regress the home-page LCP? | Synthetic + RUM |
| Are users on slow Android phones suffering? | RUM |
| Did a third-party script start throwing yesterday? | Synthetic + RUM |
| What is the field INP distribution on the checkout page? | RUM |
| Did the staging deploy break the login flow? | Synthetic |

Synthetic cost is fixed per probe; RUM cost is proportional to traffic. Check: synthetic checks run every 5 minutes against the top three routes from at least three geographies; RUM captures every CWV and every error from at least 10 percent of sessions; both feed the same on-call dashboard.

## See Also

- [performance.md](performance.md) for lab-side instrumentation (DevTools Performance, Lighthouse, perf budgets) that pairs with field RUM
- [lighthouse.md](lighthouse.md) for `valid-source-maps`, `errors-in-console`, and audit rows that map to observability hygiene
- [debug-recipes.md](debug-recipes.md) for the named diagnosis loops that consume observability data
- [pre-launch.md](pre-launch.md) for the observability gate in the evidence manifest
