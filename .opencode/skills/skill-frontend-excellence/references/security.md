---
title: Security Beyond CSP
purpose: Frontend security headers, isolation primitives, supply-chain hygiene, and secrets discipline at depth. Covers CSP, Trusted Types, SRI, COOP, COEP, CORP, Permissions-Policy, Referrer-Policy, frame-ancestors, and dependency audits.
load-when:
  task-keywords: [security, CSP, COOP, COEP, CORP, cross-origin isolation, SRI, Trusted Types, Permissions-Policy, Referrer-Policy, frame-ancestors, dependency]
  symptoms: [score dropped, third-party script slow, broken on Safari, broken on Firefox, stale SRI beacon]
prereq: SKILL.md
related: [lighthouse.md, build-hygiene.md, embed-patterns.md, pre-launch.md]
size: ~321 lines
---

# Security Beyond CSP

The Best Practices audit flags the obvious holes (mixed content, missing CSP, broken HTTPS). The real surface is wider: the headers, policies, and process disciplines that keep a frontend defensible after the audit passes. Every rule below is a principle plus a concrete check at the standard level (CSP directive, HTTP header, browser API), not at the library level.

## Threat Model Overview

Five threat categories cover almost every real frontend incident.

| Threat | Mechanism | Mitigation |
| --- | --- | --- |
| DOM-based XSS | Untrusted strings reach a script-executing sink | Trusted Types or a sanitiser at every sink |
| MITM | Network attacker injects/observes/modifies traffic | HSTS, HTTPS everywhere, upgrade-insecure-requests |
| Clickjacking | Third party frames your origin to harvest clicks | frame-ancestors, SameSite cookies |
| Supply chain | A trusted dependency/build tool/CDN ships malice | Integrity pinning, lockfile discipline, provenance |
| Secrets exfiltration | A secret lands in client code, storage, or a URL | Keep secrets server-side, scan, rotate |

DOM-XSS dangerous sinks where untrusted strings must not flow: `innerHTML`, `eval`, `document.write`, `setAttribute('on*', ...)`, `<script>.src`.

Checks:

- DOM-XSS: every dynamic write to a script-executing sink (`innerHTML`, `outerHTML`, `document.write`, `eval`, `new Function`) is wrapped in a Trusted Types policy or sanitiser. Audit: `grep -rnE 'innerHTML\s*=|outerHTML\s*=|document\.write\(|eval\(|new Function\(' src/` returns a known, audited list.
- MITM: mitigations are HSTS, HTTPS everywhere (no mixed content), and `upgrade-insecure-requests` in CSP.
- HSTS: `Strict-Transport-Security: max-age=63072000; includeSubDomains; preload` is present on every HTML response, and the site is on the HSTS preload list (hstspreload.org).
- Clickjacking: `Content-Security-Policy: frame-ancestors 'none'` (or an explicit allow-list) is present. `X-Frame-Options` is allowed as a redundant belt-and-braces header but is superseded by `frame-ancestors`.
- Supply chain: every third-party CDN script has an `integrity="sha384-..."` attribute or is self-hosted; `npm ci --ignore-scripts` succeeds in CI; `npm audit --omit=dev` returns 0 high or critical findings, or each has a tracked waiver.
- Secrets: `grep -rIE 'sk_live|sk_test|AKIA|ghp_|xox[abp]-' dist/` returns nothing; production source maps are uploaded privately and never published on the public CDN (see observability.md).

## CSP at Depth

Content-Security-Policy is the highest-leverage frontend security header and the easiest to deploy badly. Three flavours, in order of preference.

### Build-time script hashes (static hosts)

When the build outputs a fixed set of inline or external scripts, compute their SHA-256 hashes at build time and emit them as `'sha256-<base64>'` source expressions in `script-src`/`style-src`. No per-request work, cacheable header, no nonce-rotation cost.

```text
Content-Security-Policy: default-src 'self';
  script-src 'self' 'sha256-AbC123...' 'sha256-DeF456...';
  style-src 'self' 'sha256-GhI789...';
  img-src 'self' data: https://images.cdn.example;
  font-src 'self' data:;
  connect-src 'self' https://api.site.example;
  frame-ancestors 'none';
  base-uri 'self';
  form-action 'self';
  object-src 'none';
  upgrade-insecure-requests;
```

Check: every `<script>` tag the browser executes resolves to one of the hashes in the CSP, with zero CSP violations in the DevTools Console after a deploy.

### Nonces (dynamic hosts)

When the server renders HTML per request and inline scripts vary, mint a fresh cryptographic nonce per request and reflect it on each `<script nonce="...">` plus the CSP header. `'strict-dynamic'` grants trust transitively to scripts loaded by an already-nonced script, so you need not enumerate every dependency.

```text
Content-Security-Policy: default-src 'self';
  script-src 'self' 'nonce-Rk9yLW5vbi1jb29sLW5vbmNlcw==' 'strict-dynamic';
  ...
```

Check: the nonce is generated per request from a cryptographic RNG (`crypto.randomBytes(16).toString('base64')`, not `Math.random()`), is at least 128 bits, and is never reused.

### `'unsafe-inline'` as documented fallback

If a third party demands inline event handlers and refuses nonces, document the exemption in a security note, narrow to `script-src-elem` (not `script-src-attr`), and plan removal.

Check: every `'unsafe-inline'` in production CSP has a tracked issue and an owner. The Lighthouse `csp-xss` audit (see lighthouse.md) red badge is acceptable only with a written waiver.

### Report-only rollout

Deploy a new CSP with `Content-Security-Policy-Report-Only:` for two weeks, collect violations via the Reporting API (see observability.md), iterate, and promote to enforcing only when the violation stream is clean.

## Trusted Types

CSP blocks loading malicious scripts; Trusted Types blocks dynamic creation of new injection sinks. They compose.

### The directive

```text
Content-Security-Policy: require-trusted-types-for 'script'; trusted-types default dompurify;
```

Enforcing this makes every assignment to `Element.innerHTML`, `HTMLScriptElement.src`, `eval()`, `setTimeout(string)` throw a TypeError unless the value is a `TrustedHTML`, `TrustedScript`, or `TrustedScriptURL` minted by a named policy.

### The policy

Run early, before any code that touches `innerHTML`.

```js
if (window.trustedTypes && trustedTypes.createPolicy) {
  trustedTypes.createPolicy('default', {
    createHTML: (input) => sanitize(input, { RETURN_TRUSTED_TYPE: true }),
    createScript: () => { throw new Error('inline script forbidden'); },
    createScriptURL: (input) => {
      const url = new URL(input, location.origin);
      if (url.origin !== location.origin) {
        throw new Error('cross-origin script forbidden');
      }
      return url.toString();
    },
  });
}
```

Checks the policy enforces:

- Every HTML sink runs through the sanitiser as an audited list, no exceptions.
- Inline script creation is rejected outright; if needed, it gets a named policy with reviewed allow-rules.
- Script-URL loads are restricted to same-origin; cross-origin needs an explicit second policy and a reason.

### Browser support and rollout

Chromium and Edge fully support Trusted Types; Firefox and Safari evaluate via a polyfill (Web Components polyfill plus a `trusted-types` shim). Deploy report-only first to catch unintended sinks; promote after the violation stream is clean.

Check: `trustedTypes.defaultPolicy` is defined at page load, the CSP header includes `require-trusted-types-for 'script'`, and the Reporting API endpoint (see observability.md) receives zero violations for two weeks before promotion.

## Subresource Integrity (SRI)

When you load a script from a CDN you do not control, the CDN can swap the file. SRI pins the bytes.

```html
<script
  src="https://cdn.vendor.example/widget-1.2.3.js"
  integrity="sha384-abc123...xyz789"
  crossorigin="anonymous"
></script>
```

Requirements and checks:

- The CDN must serve `Access-Control-Allow-Origin: *` (or an explicit origin) so the browser can read the body to verify the hash.
- `crossorigin="anonymous"` must be set to opt the script into CORS so the integrity check runs.
- Use SRI only when self-hosting is genuinely impossible (vendor demands the CDN URL for license enforcement, or the script is too large to redeploy per release); most scripts should be self-hosted, SRI is the fallback.
- Every off-origin `<script src="https://...">` has an `integrity` attribute, CI fails the build when a third-party script is added without one, and `grep -rnE '<script[^>]+src="https?://' dist/` returns only entries with `integrity=`.
- Refresh on vendor update: a pinned hash blocks vendor fixes. Track the vendor's release notes, recompute the hash on every version bump, and treat the integrity update like any other dependency upgrade (PR, review, deploy).

## COOP, COEP, CORP, and Cross-Origin Isolation

The isolation triad unlocks `SharedArrayBuffer`, high-precision `performance.now()`, and OffscreenCanvas in workers, and blocks a class of cross-origin side-channel attacks.

| Header | Value | Effect |
| --- | --- | --- |
| Cross-Origin-Opener-Policy | `same-origin` | Severs the link to any cross-origin opener so it cannot script your DOM through `window.opener`; required for isolation |
| Cross-Origin-Embedder-Policy | `require-corp` | Requires every cross-origin subresource (images, scripts, iframes) to opt in via CORP or CORS, stopping drive-by loading of non-consenting resources |
| Cross-Origin-Resource-Policy | `same-origin` (or `same-site`, `cross-origin`) | On a response, tells the browser which origins may embed it; send on every static asset you control |

All three must align to enable isolation:

```text
Cross-Origin-Opener-Policy: same-origin
Cross-Origin-Embedder-Policy: require-corp
Cross-Origin-Resource-Policy: same-origin
```

When set: `self.crossOriginIsolated === true`, `SharedArrayBuffer` becomes constructible, `performance.now()` returns microsecond precision, and OffscreenCanvas transfers cleanly to workers.

### Migration plan and checks

The triad breaks naive embeds: any iframe, image, font, or script from a third-party origin without CORP or CORS fails to load.

- Inventory every cross-origin resource: add CORP to the ones you control, CORS plus a `crossorigin` attribute to the ones you do not.
- Replace embeds you cannot fix with same-origin proxies.
- Deploy COOP and COEP report-only (`Cross-Origin-Opener-Policy-Report-Only`, `Cross-Origin-Embedder-Policy-Report-Only`) for two weeks.
- Promote to enforcing.

Check: `self.crossOriginIsolated` is `true` on every route needing `SharedArrayBuffer` or high-precision timers, and the Reporting API receives zero `coep` or `coop` violation reports for a full deploy cycle before flipping enforcing.

## Permissions-Policy

`Permissions-Policy` gates browser-feature access by origin: default-deny, then opt in.

```text
Permissions-Policy: camera=(), microphone=(), geolocation=(), payment=(),
  usb=(), serial=(), bluetooth=(), magnetometer=(), gyroscope=(), accelerometer=(),
  fullscreen=(self), picture-in-picture=(self), autoplay=(self),
  interest-cohort=()
```

Syntax: empty parentheses `()` mean no origin (including self) may use the feature; `(self)` means the top-level document can; `(self "https://trusted.example")` adds an allow-listed origin.

Iframe propagation is controlled by the `allow` attribute:

```html
<iframe src="https://embed.vendor.example" allow="camera 'none'; microphone 'none'"></iframe>
```

Checks:

- Every feature the site does not use is denied, so a future XSS calling `navigator.geolocation.getCurrentPosition` fails silently and a compromised iframe cannot ask for the camera.
- The top-level `Permissions-Policy` header denies every unused feature, and every iframe `allow` attribute is an explicit allow-list, not an empty default.

## Referrer-Policy

`Referrer-Policy` controls what the browser sends in the `Referer` header on outbound navigation and subresource loads.

```text
Referrer-Policy: strict-origin-when-cross-origin
```

This is the W3C default since 2020 and the right starting point for almost every site. Behaviour:

- Same-origin requests get the full URL (path and query).
- Cross-origin HTTPS requests get only the origin (scheme + host + port).
- HTTPS-to-HTTP requests get nothing.

Checks:

- Use `no-referrer` on sensitive pages (auth flows, reset-password) where even the origin is privacy-leaking, and `same-origin` for embedded admin tools that must never leak referrer externally.
- Response headers include `Referrer-Policy: strict-origin-when-cross-origin` (or stricter), and no page sets `unsafe-url` without a documented reason.

## frame-ancestors (Clickjacking)

`frame-ancestors` in CSP supersedes the older `X-Frame-Options` header: CSP-level, same delivery and enforcement model, more expressive.

Pages that should never be framed get `'none'`:

```text
Content-Security-Policy: frame-ancestors 'none';
```

`X-Frame-Options: DENY` may stay as a redundant header for ancient clients but adds no protection beyond `frame-ancestors 'none'`. Embeddable pages get an explicit allow-list:

```text
Content-Security-Policy: frame-ancestors 'self' https://partner.example https://*.site.example;
```

Check: top-level CSP includes `frame-ancestors` matching the site's framing intent (typically `'none'`), and pages designed to be embedded have a written allow-list reviewed quarterly.

## Third-Party Script Supply Chain

The biggest frontend security incidents of the last decade were supply chain. Tooling shifts the slope.

### Lockfile review

Review every lockfile diff like code:

- Newly added transitive dependencies (a small direct addition can pull in hundreds).
- Version-range widening (a `^` bump 1.2.0 to 1.9.0 is a different review than a `~` bump 1.2.0 to 1.2.3).
- Ownership changes (`npm view <pkg>` includes `maintainers`).

Check: PRs that modify `package.json` always modify the lockfile in the same commit, CI fails when only one changes, and reviewers read the lockfile diff not just the manifest.

### Dependency auditing

Run `npm audit --omit=dev` or `pnpm audit --prod` on every PR. Treat high and critical findings as blocking, track moderate findings with an owner and deadline, and triage waivers explicitly. Tooling beyond the built-in audit: a software-composition-analysis service, a dependency-update bot, or a runtime-behaviour analyzer (postinstall scripts, network calls). Pick one, run it, act on the output.

### Integrity-pinned releases

When shipping a frontend library to other teams, publish with provenance (`npm publish --provenance` since npm 9.5) so consumers can verify the package came from your CI workflow, not a stolen token.

Check: published packages show a green provenance badge and releases come from CI, not developer laptops.

### npm-token-theft mitigations

Stack three:

- 2FA on every publisher account (`npm access set 2fa=publish`).
- Granular per-package tokens with expiry, rotated quarterly.
- Integrity pinning in consumers via `package-lock.json` `integrity` hashes, with CI using `npm ci` (never `npm install`).

Check: CI uses `npm ci --ignore-scripts`, the lockfile is committed, publishing tokens have 90-day expiry, and provenance is on.

## Secrets in Frontend Code

Frontend code ships to every user. There are no secrets in it.

Anything in the JS bundle, the HTML, env-injected build-time variables, or runtime config is public, so treat it as published. Publishable keys (`pk_*`, public DSNs) are designed for client exposure; secret keys (`sk_*`) are not.

Checks:

- `grep -rIE 'sk_live|sk_test|AKIA|ghp_|xox[abp]-|password|passwd' dist/` returns nothing, and only `PUBLIC_*` or framework-prefixed public variables are embedded via a reviewed build-time env allowlist.
- When a secret ships: rotate immediately and leave git history (generate a new key, deploy, revoke the old within the hour) as the default; rewrite git history with a history-filtering tool only when the secret cannot be rotated. Rotate first either way.
- The leaked-secret incident playbook documents the rotation contact for every credential type, mean time to rotate is under one hour, and the postmortem captures the leak source and prevention (pre-commit hook, CI secret scanner).
- Run a secret scanner at the pre-commit hook and in CI on the full diff, block the commit and PR on a credential pattern hit, and maintain an allow-list for genuine false positives; repos with historical leaks are scanned and leaks tracked to closure.

## Dependency Cost at Audit Time

Security and performance overlap on dependencies: every transitive add is a new supply-chain entry and a new bundle line item (the bundle side lives in build-hygiene.md). Per major dependency, capture:

- Bytes: measured via a bundle-size service or local `size-limit`. Above 10 KB gzipped, the dependency needs justification.
- Maintenance: last release date, open-issue count, single-maintainer risk. A sole maintainer with no release in 18 months is a supply-chain risk.
- License: SPDX identifier, verified compatible with your distribution license.
- Alternatives: could a 30-line same-file utility replace it? Could the platform (`Intl.NumberFormat` instead of a number-formatting library, native `fetch` instead of an HTTP-client library)?

Check: dependency review is a recurring quarterly task whose output is a list of dependencies to remove, replace, or pin tighter, feeding the prelaunch evidence manifest (see pre-launch.md).

## Headers Quick Reference

Every production HTML response should include at least these eight headers. Tune the values; do not omit any without a written reason.

```text
Strict-Transport-Security: max-age=63072000; includeSubDomains; preload
Content-Security-Policy: default-src 'self'; ... ; frame-ancestors 'none'; ...
Cross-Origin-Opener-Policy: same-origin
Cross-Origin-Embedder-Policy: require-corp
Cross-Origin-Resource-Policy: same-origin
Permissions-Policy: camera=(), microphone=(), geolocation=(), ...
Referrer-Policy: strict-origin-when-cross-origin
X-Content-Type-Options: nosniff
```

Check: `curl -sI https://site.example | grep -iE 'strict-transport|content-security|cross-origin|permissions-policy|referrer-policy|x-content-type'` lists all eight, each value matches the intended policy, and the pre-launch.md evidence manifest records the curl output as an artefact.

## See Also

- [lighthouse.md](lighthouse.md): Best Practices audit row mapping (csp-xss, valid-source-maps, no-vulnerable-libraries)
- [build-hygiene.md](build-hygiene.md): dependency cost discipline at bundle time
- [embed-patterns.md](embed-patterns.md): cross-origin isolation interactions with iframes
- [pre-launch.md](pre-launch.md): the headers gate in the evidence manifest
