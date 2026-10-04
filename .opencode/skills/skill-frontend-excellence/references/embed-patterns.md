---
title: Embed Patterns (Host and Guest)
purpose: Embed-as-host and embed-as-guest rules for iframes: sandboxing, allow-lists, viewport coupling, theme handshakes, viewport reporting, top-window navigation, origin verification, and cross-origin isolation impact.
load-when:
  task-keywords: [embed, iframe, sandbox, postMessage, host, guest, third-party widget, frame-ancestors, Permissions-Policy, cross-origin isolation, COOP, Storage Access API]
  symptoms: [third-party script slow, viewport overflow, broken on Safari, broken on Firefox]
prereq: SKILL.md
related: [security.md, auth.md, performance.md, accessibility.md]
size: ~326 lines
---

# Embed Patterns (Host and Guest)

Every site plays both roles: it embeds third-party widgets (maps, video, payment, support chat, dashboards) AND its own widgets get embedded elsewhere. Roles have different obligations.

- HOST embeds someone else's origin: bound the guest's capabilities (sandbox, allow-list, storage isolation, validate inbound messages).
- GUEST runs inside someone else's iframe: report viewport, adopt theme, verify origin on every inbound message, degrade when storage is partitioned.

Confusing the roles ships XSS surface (host trusting guest) or a widget that only works first-party (guest assuming first-party storage).

## Embed as Host: You Embed Third-Party

You render an iframe pointing at code you cannot audit at request time. Bound its capabilities.

### Sandbox flags

The `sandbox` attribute is opt-in trust. With no value the iframe has zero capabilities: no scripts, no forms, no top-level navigation, no same-origin treatment of its own origin. Add capabilities only as the embedded content requires them.

| Flag | Grants |
|---|---|
| `allow-scripts` | The iframe may run JavaScript. |
| `allow-same-origin` | The iframe may treat its own origin as same-origin (cookies, storage). |
| `allow-forms` | The iframe may submit forms. |
| `allow-popups` | The iframe may open new windows. |
| `allow-popups-to-escape-sandbox` | Windows opened by the iframe are not themselves sandboxed. |
| `allow-top-navigation` | The iframe may navigate the top window. |
| `allow-top-navigation-by-user-activation` | Top navigation only inside a user gesture. |
| `allow-modals` | The iframe may show `alert`, `confirm`, `prompt`. |
| `allow-storage-access-by-user-activation` | The iframe may call `requestStorageAccess`. |

### Minimum-trust set and untrusted content

- Minimum-trust set: for a typical script-driven third-party widget, use `<iframe src="https://widget.example/partner" sandbox="allow-scripts allow-same-origin" loading="lazy" title="Partner widget">`. Scripts and own-state persistence only, no forms, popups, top navigation, or modals.
- Verify: load the embed with the network panel filtered on the guest origin, confirm the guest does its job, then confirm `window.top.location.href` is unreadable from the guest.
- Untrusted content (user-submitted embeds, ad creatives, sandboxed previews): drop `allow-same-origin` so the iframe origin is treated as `null` and cannot read its own cookies or storage.
- Verify: an untrusted preview iframe cannot read its own `document.cookie` (returns empty string) and cannot reach into the top-window storage.

### Allow-list via the `allow` attribute

- The `allow` attribute delegates Permissions-Policy features to the iframe; without explicit delegation the iframe cannot access camera, microphone, geolocation, payment, autoplay, or fullscreen even if its own origin would allow them.
- Delegate explicitly, e.g. `allow="geolocation 'self' https://map.example"` alongside `sandbox="allow-scripts allow-same-origin"`.
- Verify: enumerate every iframe and confirm each `allow` list contains only the capabilities the widget requires; a bare `<iframe>` with no `allow` is the correct default.

### Viewport coupling

Iframes do not size to content automatically. In order of preference:

- Fixed height (known size that stays): best for badges, ratings, small status widgets.
- Internal scrolling (iframe as a window into a scrollable region): acceptable for embedded apps, ugly for content.
- Resize via postMessage (guest reports content height, host adjusts): the only way to make an embedded form or article feel native. See "Viewport reporting" below.

### Theme inheritance

- CSS custom properties do not cross the iframe boundary; a host `--color-bg` is invisible to the guest. Theme via URL parameter or via postMessage.
- Theme via URL parameter (e.g. `theme=dark` or a color in the iframe `src`): coarse but simple and cacheable.
- Theme via postMessage (host sends a `theme` message after load, guest applies it): finer-grained and reactive to host theme changes. See "Theme adoption" below.
- Verify: switch the host theme and confirm the embedded iframe updates without a reload.

### Cross-origin storage post-3PCD

- After third-party cookie deprecation an iframe gets its own storage partition keyed by the top-level site, so the guest's cookies on widget.example from your site do NOT match its cookies on the same domain from a different host site.
- A widget that expects a shared session across multiple host sites cannot rely on its own cookies post-3PCD.
- The guest must request storage access via the Storage Access API on a user gesture (see auth.md).
- For widgets you control on both sides, use partitioned cookies (CHIPS) so the cookie carries `Partitioned` and lives per top-level site without third-party scope (see auth.md).
- Verify: in DevTools Application, the embedded widget's cookie shows a `Partition Key` matching the host top-level site, not a blank or shared partition.

### CSP `frame-src` discipline

The host CSP `frame-src` directive declares which origins may be embedded, e.g. `Content-Security-Policy: frame-src 'self' https://map.example https://widget.example;` (full CSP treatment in security.md).

- Never use `frame-src *` in production CSP.
- Use one `frame-src` directive listing every legitimate embed source, no more.
- When you remove a third-party embed, remove its origin from `frame-src` in the same commit.
- Verify: the deployed CSP `frame-src` lists exactly the iframe origins the page actually uses, with no leftovers from removed widgets.

## Embed as Guest: Your Widget Gets Embedded

Your widget runs inside someone else's iframe. The host controls the chrome, theme, viewport, and storage partition. Be a polite, robust guest.

### postMessage handshake protocol

Define a small, versioned protocol and enforce it on every message.

```js
const HOST_ORIGIN = 'https://host.example';
const PROTOCOL_VERSION = 1;

function send(type, payload) {
  window.parent.postMessage(
    { protocol: 'partner-widget', version: PROTOCOL_VERSION, type, payload },
    HOST_ORIGIN,
  );
}

window.addEventListener('message', (event) => {
  if (event.origin !== HOST_ORIGIN) return;
  const msg = event.data;
  if (!msg || msg.protocol !== 'partner-widget') return;
  if (msg.version !== PROTOCOL_VERSION) return;

  switch (msg.type) {
    case 'theme': applyTheme(msg.payload); break;
    case 'resize-request': send('viewport', getViewport()); break;
    default: /* unknown type, ignore */
  }
});

send('ready', { version: PROTOCOL_VERSION });
```

- Verify `event.origin` is the expected host on every inbound message.
- Discriminate messages by an explicit `protocol` field so other widgets on the host page do not collide.
- Negotiate version: if the host is on a newer protocol than the guest, ignore unknown types rather than guessing.
- Send `ready` once on init so the host knows it can begin sending theme and configuration.
- Verify: in the embedded context, every inbound `message` event handler returns immediately for any origin other than the configured host.

### Viewport reporting

Guest uses a `ResizeObserver` on the widget root, plus an initial report on `window` `load`.

```js
const root = document.getElementById('widget-root');
const ro = new ResizeObserver(() => {
  send('viewport', { height: root.scrollHeight, width: root.scrollWidth });
});
ro.observe(root);

window.addEventListener('load', () => {
  send('viewport', { height: root.scrollHeight, width: root.scrollWidth });
});
```

Host listens for `message`, returns unless `e.origin === 'https://widget.example'`, and on `e.data.type === 'viewport'` sets `iframe.style.height = `${e.data.payload.height}px``.

```js
const iframe = document.querySelector('iframe.partner');
window.addEventListener('message', (e) => {
  if (e.origin !== 'https://widget.example') return;
  if (e.data.type === 'viewport') {
    iframe.style.height = `${e.data.payload.height}px`;
  }
});
```

- Verify: open the embed and confirm that as content loads (images, async data) the iframe resizes to the content height without internal scrollbars.

### Theme adoption

Guest `applyTheme(theme)` iterates `Object.entries(theme)` and calls `document.documentElement.style.setProperty(`--${key}`, value)` for each token.

```js
function applyTheme(theme) {
  const root = document.documentElement;
  for (const [key, value] of Object.entries(theme)) {
    root.style.setProperty(`--${key}`, value);
  }
}
```

- Define a small, stable set of theme tokens (background, foreground, primary, border, radius, font-family) and document them as the contract.
- Refuse to read theme from anything other than a verified postMessage; never sniff the host's stylesheet.
- Verify: send a `theme` message with `--color-bg: #111` and confirm the widget background updates without a reload.

### Account-linked vs anonymous flows

- Anonymous embed: needs no login, no cross-site identity, no third-party cookies.
- Account-linked embed: either the host passes a short-lived token via postMessage after a host-side auth check, or the widget runs its own sign-in inside the iframe via Storage Access API plus partitioned cookies (CHIPS). See auth.md.
- Verify: identify each embed's mode. Anonymous embeds make zero auth calls; account-linked embeds either accept a host token on init or run their own gated sign-in.

### Width responsiveness without media queries

- Inside an iframe the viewport is the iframe's box, not the host viewport, so `@media (min-width: 600px)` answers whether the iframe is at least 600px, not the host page. Use container queries instead.
- Query the widget's own container:

```css
.widget {
  container-type: inline-size;
}

@container (min-width: 480px) {
  .widget-header { font-size: 1.25rem; }
  .widget-actions { flex-direction: row; }
}
```

- Verify: resize the host browser window and confirm the widget reflows at its own breakpoints, not at the host viewport's breakpoints.

## Cross-Cutting Concerns

Applies to both host and guest.

### `X-Frame-Options` vs CSP `frame-ancestors`

- `X-Frame-Options` is the legacy framing header (`DENY`, `SAMEORIGIN`, `ALLOW-FROM`); CSP `frame-ancestors` is the modern replacement.
- Set `frame-ancestors` on every page that should not be framed.
- `frame-ancestors 'self'` permits embedding only on the same origin (the modern `SAMEORIGIN`).
- `frame-ancestors 'none'` blocks embedding entirely (the modern `DENY`).
- `frame-ancestors https://partner.example` permits embedding only on the listed origin.
- When `X-Frame-Options` and CSP `frame-ancestors` are both present and disagree, CSP wins in modern browsers.
- For sign-in pages, payment pages, and any page where clickjacking matters, send `frame-ancestors 'none'`; for the rest of the site set the policy explicitly even when the answer is `'self'`.
- Verify: the sign-in and payment routes return `Content-Security-Policy: frame-ancestors 'none'` and other routes return an explicit `frame-ancestors` directive listing the legitimate embedders.

### Top-window navigation from a sandboxed iframe

- A sandboxed iframe cannot navigate the top window by default; the browser ignores `window.top.location = ...` and `<a target="_top">` clicks from sandboxed content.
- `allow-top-navigation` permits any top navigation; use only for fully trusted content.
- `allow-top-navigation-by-user-activation` requires a user gesture; use it for widgets that need to navigate away on a click (sign-out, expand-to-fullscreen).
- Never combine `allow-scripts allow-same-origin allow-top-navigation` without `by-user-activation` on untrusted content: that is the classic clickjacking surface.
- Verify: a sandboxed widget with only `allow-scripts allow-same-origin` cannot navigate the host away, and adding `allow-top-navigation-by-user-activation` enables top navigation only inside click handlers.

### postMessage origin verification

NEVER process a `message` event without checking `event.origin`; return unless `event.origin === 'https://expected-origin.example'` before reading `event.data`.

```js
window.addEventListener('message', (event) => {
  if (event.origin !== 'https://expected-origin.example') return;
  // safe to read event.data
});
```

- Do not check `event.source` instead of `event.origin`; the source is a window reference and an attacker can race a different window into the slot.
- Do not allow-list by `endsWith('.example.com')`; an attacker registers `evilexample.com` and matches. Use exact match against an allow-list.
- Never skip the target-origin on outbound messages: `postMessage(data, '*')` broadcasts to any origin in the slot, so always specify the expected target origin.
- Verify: grep the codebase for every `addEventListener('message')` and `postMessage(`; each handler has an `event.origin` check before reading `event.data` and each `postMessage` call passes an explicit target origin, not `*`.

### Cross-origin isolation impact on embeds

Full COOP / COEP / CORP picture in security.md.

- If the host is cross-origin isolated (`Cross-Origin-Opener-Policy: same-origin` plus `Cross-Origin-Embedder-Policy: require-corp` or `credentialless`), its embedded resources must opt in.
- A cross-origin guest must send `Cross-Origin-Resource-Policy: cross-origin` (or compatible CORS headers) or the host will refuse to embed it.
- When `crossOriginIsolated === true` the guest gains access to `SharedArrayBuffer` and high-resolution timers.
- If the guest cannot satisfy COEP, the host loses cross-origin isolation OR the embed breaks: choose the one you want, not both.
- Verify: on a cross-origin-isolated host every embedded resource returns either CORS headers or `Cross-Origin-Resource-Policy: cross-origin`, and the host page's `crossOriginIsolated` is `true` in DevTools.

### Iframe loading discipline

Lazy-load below-the-fold iframes (the heaviest single resource on most pages), e.g. `<iframe src="https://widget.example/embed" loading="lazy" title="Customer support chat" sandbox="allow-scripts allow-same-origin">`. See performance.md for lazy-loading and third-party discipline.

- The `title` attribute is required for accessibility; screen readers announce iframes by title and a missing title falls back to "frame".
- Verify: every iframe has a meaningful `title`, every below-the-fold iframe has `loading="lazy"`, and lazy iframes do not request in the Network panel until the user scrolls near them.

### Accessibility of embedded content

Focus management and keyboard treatment in full live in accessibility.md.

- The iframe `title` describes what is inside ("Map of office locations", "Customer support chat"), not "iframe" or the widget vendor's brand.
- The iframe focus indicator must be visible; the host CSS controls the outline on the `<iframe>` element itself when the browser focuses it on Tab.
- Keyboard focus enters the iframe on Tab then continues inside it, and tabbing out the bottom returns to the host's tab order; the host MUST NOT trap focus around the iframe.
- For dialog-like embeds the guest's own focus trap applies inside the iframe while the host's outer dialog still owns the escape behavior.
- Verify: tab through a page with an embedded iframe: focus enters with a visible outline, tabs through internal controls, and exits cleanly with no focus traps at the boundary.

### Resource hints for known embeds

For embeds loading on most visits, hint with `<link rel="preconnect" href="https://widget.example" crossorigin />` and `<link rel="dns-prefetch" href="https://widget.example" />`. See performance.md for hint budgets.

- Use `preconnect` for the iframe origin AND any third-party origins it pulls from (CDN, analytics, fonts), one `preconnect` per origin, no more than four total.
- Use `dns-prefetch` as a cheap fallback on older browsers.
- Do NOT preload the iframe document itself; that costs more than it saves.
- Verify: on Network throttled to "Fast 3G", confirm the third-party iframe origin's connection is open before the iframe element starts requesting.

### Common embed regressions

| Symptom | Cause | Fix |
|---|---|---|
| Embed renders but is unstyled | Host's CSP `frame-src` allows the iframe but the guest's CSS host is blocked | Add the CSS origin to `frame-src` AND the guest's own CSP |
| Embed appears, no theme | Host did not send the `theme` message OR the guest does not handle the message type | Verify the postMessage handshake; log received types |
| Embed grows but never shrinks | Guest uses `scrollHeight` for resize; deleted content does not shrink the value | Switch to `getBoundingClientRect().height` or observe a known wrapper |
| Embed is blank on Safari | Third-party cookies blocked; guest requires same-origin storage | Request storage access on a user gesture; fall back to a top-level pop-out |
| Embed signs the user out on host theme change | Theme handler reloads the iframe | Apply theme via CSS custom properties only, never via reload |
| Console error: "Refused to display in a frame" | Embedded page sends `X-Frame-Options: DENY` or `frame-ancestors 'none'` | Cannot fix from the host; the guest must allow the host as an ancestor |

- Verify: walk through this common-embed-regression table during pre-launch for every third-party embed.

## Anti-Patterns

- Do not start from `sandbox=""` and add flags until "it works"; start from the minimum and justify each flag.
- Do not use `sandbox` with `allow-scripts allow-same-origin allow-top-navigation` on untrusted content (clickjacking surface).
- Do not send `postMessage(data, '*')` outbound; specify the target origin.
- Do not skip `event.origin` checks inbound (XSS surface).
- Do not use `endsWith('.example.com')` origin checks; use exact match against an allow-list.
- Do not ship an iframe with no `title` (inaccessible).
- Do not sniff the host's stylesheet to copy theme; use the documented handshake.
- Do not use media queries inside an embedded widget; use container queries.
- Do not lazy-load the iframe but eagerly load its `preconnect`; defer both or neither, consistent.
- Do not use `frame-src *` in production CSP; allow-list only.
- Do not assume first-party storage inside an iframe; plan for partitioning.
- Do not reload the iframe to apply a theme change.

## Self-Healing for Embed Patterns

- [ ] Every third-party iframe has a `sandbox` attribute with the minimum required flags
- [ ] Every third-party iframe has an `allow` attribute listing only required Permissions-Policy features
- [ ] Every iframe has a meaningful `title` for screen readers
- [ ] Below-the-fold iframes use `loading="lazy"`
- [ ] CSP `frame-src` lists exactly the origins actually embedded
- [ ] CSP `frame-ancestors` set on every embeddable page, with `'none'` on sign-in and payment
- [ ] postMessage handshake versioned and the guest sends `ready` on init
- [ ] Every `message` listener verifies `event.origin` against an exact allow-list
- [ ] Every `postMessage` call passes an explicit target origin, never `*`
- [ ] Guest reports viewport via `ResizeObserver` and host applies height on `viewport` messages
- [ ] Guest accepts `theme` messages and maps them to CSS custom properties without reload
- [ ] Guest uses container queries for responsiveness, not viewport media queries
- [ ] Cross-origin guests send appropriate `Cross-Origin-Resource-Policy` for isolated hosts
- [ ] Top-window navigation from a sandboxed iframe requires `allow-top-navigation-by-user-activation`
- [ ] Storage Access API requested only inside a user gesture, with CHIPS or pop-out fallback documented
- [ ] Focus enters and exits the iframe cleanly with no traps at the boundary
- [ ] Preconnect hints in place for known third-party embed origins

## See Also

- [security.md](security.md): CSP, COOP, COEP, CORP, Trusted Types, Permissions-Policy
- [auth.md](auth.md): Storage Access API, CHIPS, sign-in inside an embed
- [performance.md](performance.md): iframe lazy-loading, preconnect budgets, third-party script discipline
- [accessibility.md](accessibility.md): focus management, ARIA, keyboard navigation
