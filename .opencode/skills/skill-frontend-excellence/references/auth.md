---
title: Authentication Flows
purpose: Flow-level authentication patterns: passkeys and WebAuthn, OAuth redirect UX, magic links, session expiry, account recovery, CAPTCHA placement, sign-in and sign-up structure, cross-tab session sync, and Storage Access API for embedded sign-in. Input-level concerns (label, validation, autofill) live in forms.md.
load-when:
  task-keywords: [auth, authentication, login, signup, passkey, WebAuthn, OAuth, magic link, session, account recovery, CAPTCHA, Storage Access API]
  symptoms: [auth redirect loop, passkey not offered, focus not visible, focus trap leak]
prereq: SKILL.md
related: [forms.md, embed-patterns.md, security.md, accessibility.md]
size: ~367 lines
---

# Authentication Flows

Framework-agnostic patterns for authentication FLOWS: redirects, sessions, recovery, second factors, embedded sign-in. Input-level concerns (label, `autocomplete`, `aria-invalid`, error placement, multi-error focus return, password-manager detection) live in `forms.md`. These two age at different speeds: edit `forms.md` for label markup, edit this file for what happens after submit. Cross-link, do not duplicate.

## Passkeys and WebAuthn

Passkeys are the modern default for new sign-in flows: no phishing surface, no password-reset funnel for enrolled users.

| Principle | Check (with threshold/value) |
| --- | --- |
| Offer passkeys inline via conditional UI above the username field, not as a sibling "Sign in with passkey" button next to "Sign in with password" | With a registered passkey present, the password-manager popup offers the passkey at the username field, not after submit |
| `autocomplete="username webauthn"` on the username input | The `webauthn` token is the signal; without it the browser will not offer the passkey at this field |
| `mediation: 'conditional'` makes `navigator.credentials.get` non-modal (surfaced via autofill popup) | A normal `get` without conditional mediation pops a modal, the wrong sign-in UX |
| Trigger `navigator.credentials.create` only after the user is authenticated by another method, from a deliberate "Set up a passkey" affordance in account settings | Never silently on first sign-in |
| After a password sign-in, show a one-time inline prompt (not a modal) to enrol a passkey | Dismissing is one click; accepting triggers `create` |
| Support floor: `navigator.credentials.get` with `mediation: 'conditional'` runs in current Chromium, Safari 16+, Firefox 122+ | Feature-detect via `isConditionalMediationAvailable` and fall back to the password field without layout shift |

Conditional UI sign-in form markup:

```html
<form id="signin">
  <label for="username">Email</label>
  <input
    id="username"
    name="username"
    type="email"
    autocomplete="username webauthn"
    required
  />
  <label for="current-password">Password</label>
  <input
    id="current-password"
    name="password"
    type="password"
    autocomplete="current-password"
  />
  <button type="submit">Sign in</button>
</form>
```

The conditional `get` call:

```js
async function startConditionalPasskey() {
  if (!('credentials' in navigator)) return;
  if (!PublicKeyCredential.isConditionalMediationAvailable) return;
  const available = await PublicKeyCredential.isConditionalMediationAvailable();
  if (!available) return;

  const options = await fetch('/auth/passkey/challenge').then((r) => r.json());
  try {
    const cred = await navigator.credentials.get({
      publicKey: options,
      mediation: 'conditional',
    });
    await fetch('/auth/passkey/verify', {
      method: 'POST',
      body: JSON.stringify(cred),
      credentials: 'include',
    });
    location.assign('/');
  } catch (err) {
    // user dismissed the autofill popup, fall through to password
  }
}

document.addEventListener('DOMContentLoaded', startConditionalPasskey);
```

## OAuth Redirect UX

Three pages: your sign-in page, the provider's consent screen, your callback page. Flow rules govern what happens between them.

| Principle | Check (with threshold/value) |
| --- | --- |
| Authorization Code with PKCE only | Implicit flow and password grant are deprecated |
| PKCE on the wire | Redirect URL contains `code_challenge` and `code_challenge_method=S256`; callback exchange POST includes `code_verifier`; no `response_type=token` anywhere |
| `state` is required, cryptographically random per redirect | Store in `sessionStorage` (not `localStorage`), verify on callback, cancel with an error on mismatch |
| One callback path per provider, registered exactly | No wildcards |
| Callback is a plain page in the trust boundary of your auth | No third-party scripts, no analytics, no advertising tags |
| Callback exchanges the code, clears `state` and verifier from `sessionStorage`, redirects onward | Callback URL is on the allow-list, returns only first-party assets, fires analytics only after the session is established |

The PKCE redirect:

```js
function startOAuth(provider) {
  const state = crypto.randomUUID();
  const verifier = generateVerifier(64);
  sessionStorage.setItem('oauth.state', state);
  sessionStorage.setItem('oauth.verifier', verifier);
  const url = new URL(`/oauth/${provider}/authorize`, ORIGIN);
  url.searchParams.set('state', state);
  url.searchParams.set('code_challenge', sha256base64url(verifier));
  url.searchParams.set('code_challenge_method', 'S256');
  url.searchParams.set('redirect_uri', `${ORIGIN}/oauth/callback`);
  location.assign(url);
}
```

Provider error states (returned on the callback query string). Each renders a distinct, plain-language message; none surface raw provider strings.

| `error` value | Frontend response |
| --- | --- |
| `access_denied` | "Sign-in cancelled. Try again or use another method." |
| `consent_required` | Redirect back to authorize with `prompt=consent` |
| `login_required` | Redirect back to authorize with `prompt=login` |
| `invalid_scope` | Engineering bug: log and show a generic error |
| `server_error`, `temporarily_unavailable` | "Provider is having issues. Try again in a minute." |

Token storage:

| Principle | Check (with threshold/value) |
| --- | --- |
| Never store access tokens or refresh tokens in `localStorage` | Cross-site scripting reads everything in `localStorage` |
| Session lives in a backend-set `httpOnly`, `Secure`, `SameSite=Lax` cookie (or `Strict` where the flow allows) | See `security.md` for cookie hardening |
| If the frontend must hold a token, keep it in memory in a closure for the page session only | Drop it on reload |
| Verify in DevTools Application tab after sign-in | `localStorage` and `sessionStorage` hold no token/JWT/refresh token; session cookie is `httpOnly` with `Secure` set |

## Magic Link UX

Magic links remove the password from the sign-in surface: low-friction sign-up, infrequent sign-in, clean fallback when a passkey is lost.

| Principle | Check (with threshold/value) |
| --- | --- |
| Magic link expires under 15 minutes from issuance | Longer is a credential |
| Single use | Invalidates on first successful exchange and on a second attempt |
| Link redeems a short-lived session token | It does not itself become the session |
| Second redemption rejects clearly | "Link already used" message that offers to send a new one |
| Landing page presents two affordances | "Continue on this device" (default) and "Use on another device" (handoff) |
| Handoff shows a six- or eight-digit numeric code and a QR code | Waiting tab on the original device polls a short endpoint and unlocks when the code is consumed |
| Link redeems a session on the device that initiated, not the device that clicked | Must not silently sign in on a new device; the cross-device flow is the explicit consent path |

## Session Expiry Handling

Frontends fail at expiry more than at sign-in: an idle tab clicks, gets a silent 401 or a redirect loop.

| Principle | Check (with threshold/value) |
| --- | --- |
| Single fetch interceptor: on 401, attempt one refresh, then retry the original request or escalate to sign-in | Throttle network to Offline, request, return online, retry: no redirect loop, no two stacked refresh calls |
| Refresh token rotates on every use; reuse revokes the family as a stolen-credential signal | Backend behavior; frontend keeps rotation atomic |
| Single-flight refresh via an in-flight guard reset in `finally` | Ten concurrent requests against an expired session result in exactly one `/auth/refresh` call while the other nine wait |
| Soft expiry: client-side inactivity timer triggers at 75 percent of the session window | Non-blocking banner "You have been idle. Sign back in to continue." offering one-click re-auth in place (passkey conditional UI in the modal) without losing form input |
| Hard expiry: server returns 401, full redirect to sign-in with a `next` param back to the original page | Both soft and hard must exist; hard-only causes lost-draft complaints |

401 interception:

```js
async function fetchWithAuth(input, init = {}) {
  const res = await fetch(input, { ...init, credentials: 'include' });
  if (res.status !== 401) return res;

  const refreshed = await refreshSession();
  if (!refreshed) {
    location.assign(`/signin?next=${encodeURIComponent(location.pathname)}`);
    return res;
  }
  return fetch(input, { ...init, credentials: 'include' });
}
```

Single-flight refresh:

```js
let refreshInFlight = null;
function refreshSession() {
  if (!refreshInFlight) {
    refreshInFlight = fetch('/auth/refresh', {
      method: 'POST',
      credentials: 'include',
    }).then((r) => r.ok).finally(() => { refreshInFlight = null; });
  }
  return refreshInFlight;
}
```

## Account Recovery Beyond Password Reset

Password reset is the easy case. The hard cases: lost passkey, lost second factor, lost email, lost phone.

| Principle | Check (with threshold/value) |
| --- | --- |
| At second-factor enrolment, issue ten one-time recovery codes, each single-use, each replacing the second factor for one sign-in | Enrolment shows ten codes with "Copy all", "Print", "Save as .txt" affordances; the flow blocks until the user confirms "I have saved my codes" |
| "Trust this device for 30 days" survives in a long-lived first-party cookie; trusted device skips the second factor, any other device requires it | Device A trusted within 30 days needs no second factor; device B prompts |
| All factors lost: one-step-at-a-time identity verification (email confirmation, identity document where warranted), human review window stated up front ("Most reviews complete in 24 hours") | A user with no remaining factors learns within one screen what they need and how long it takes, with no buried links |
| Single visible status page the user can return to ("Your recovery request is in review since Monday at 2:14 PM") | SLA shown in the UI, not the help center |
| Lost second factor: offer recovery codes first | |
| Recovery codes also gone: email verification plus a 24-to-72-hour holding period before reset | For "lost phone, no recovery codes" the UI explains the holding period, sends a confirmation email, and states exactly when the reset completes |

## CAPTCHA Placement and Accessibility

CAPTCHA stops automated abuse, not legitimate users. Default posture: invisible challenge first, visible challenge only on suspicion.

| Principle | Check (with threshold/value) |
| --- | --- |
| Invisible first, visible only on suspicion (failed attempts from an IP, known-bad fingerprint, anomalous device) | A fresh sign-in page renders without a CAPTCHA; it appears only after a failed attempt or a server-flagged risk score |
| When shown, CAPTCHA goes before the submit button in focus order | Tab order is username, password, CAPTCHA, submit, with no backward jumps or focus traps |
| Surface invisible-CAPTCHA failure as the same inline error pattern any other validation uses (see `forms.md`) | Blocked third-party domain produces an inline error within two seconds, not a stuck spinner |
| Prefer providers (Turnstile, hCaptcha) that run zero-interaction for trusted browsers via Privacy Pass / Private Access Tokens | In a clean profile the CAPTCHA resolves with no visible puzzle for 95 percent of attempts; the 5 percent that escalate present an accessible audio fallback and a visible refresh affordance |

Accessibility floor for the widget:

- Visible focus ring on the widget container.
- Keyboard-operable challenge (Space, Enter, arrow keys).
- Audio fallback for visual puzzles.
- `aria-describedby` on the submit button pointing at the CAPTCHA status text while a challenge is in progress.
- Both keyboard-only and screen-reader-only (VoiceOver / NVDA) users can complete the flow (see `accessibility.md`).

## Sign-In Form Structure

The highest-converting form in most products. Non-negotiable structure.

| Principle | Check (with threshold/value) |
| --- | --- |
| One `<form>`, one submit button, `autofocus` on the username field for the sign-in route only, no off-form floating fields | Cursor lands in the username field; pressing Enter from either field submits |
| Social sign-in above the form (first in tab order), recovery links below (last); the form holds only fields plus submit and recovery links | Tab order from page load: "Skip to content", social options, username, password, submit, "Forgot password", "Create account", with no surprises |
| Multi-error submit: error summary at the top of the form, focused, each item linked to its field (pattern in `forms.md`) | Pressing Enter on the first summary link moves focus to the first invalid field |
| Single-error submit: skip the summary, move focus directly to the invalid field | Inline error renders below the field |

## Sign-Up Progressive Disclosure

The wall-of-fields form is dead. Reveal one or two fields at a time and ask only for the next step.

| Principle | Check (with threshold/value) |
| --- | --- |
| Step 1 collects only email with a "Continue" button; server returns new vs existing; form transitions to step 2 (password) in place, no route change | Entering a new email and clicking Continue reveals a password field with a strength meter without navigation; the browser back button returns to the email step |
| Terms-of-service consent is a required single checkbox directly above submit, agreement text visible; avoid "by clicking submit you agree" | Submitting without the checkbox shows an inline error pointing at the checkbox, not a generic "agree to terms" error |
| Strength meter maps to actual requirements (length, character classes, common-password check); no moving emoji, animated fire icons, or congratulatory copy | Three to five bars across, monochrome with a single accent color, status text underneath ("Strong enough", "Add a longer word", "Avoid common passwords") |
| Strength check runs client-side against a small bloom filter of common passwords AND server-side on submit | Typing "password123" makes the meter immediately flag it as common, with no animation |

Terms consent markup:

```html
<label>
  <input type="checkbox" required name="terms" />
  I agree to the Terms of Service and Privacy Policy.
</label>
<button type="submit">Create account</button>
```

## Cross-Device and Cross-Tab Session Sync

Users open three tabs and sign in on one. The others need to know.

| Principle | Check (with threshold/value) |
| --- | --- |
| `BroadcastChannel('auth')` posts `signed-in` and `signed-out` so other tabs reload or redirect | Signing in on tab A reloads tab B to signed-in within a second; signing out on tab A redirects tab B to sign-in |
| Routes with in-progress state: non-disruptive banner "You signed in on another tab. Refresh to continue." instead of auto-reload | The draft survives the reload via the draft-recovery pattern (see `forms.md`) |
| Fall back to the `storage` event on the sentinel key `auth.signal` where `BroadcastChannel` is unavailable (some WebViews) | The storage-event path produces the same cross-tab behavior |

BroadcastChannel signal:

```js
const auth = new BroadcastChannel('auth');

function signalSignedIn(user) {
  auth.postMessage({ type: 'signed-in', user });
}

function signalSignedOut() {
  auth.postMessage({ type: 'signed-out' });
}

auth.onmessage = (event) => {
  if (event.data.type === 'signed-in') {
    location.reload();
  } else if (event.data.type === 'signed-out') {
    location.assign('/signin');
  }
};
```

Storage-event fallback:

```js
window.addEventListener('storage', (e) => {
  if (e.key === 'auth.signal' && e.newValue) {
    const msg = JSON.parse(e.newValue);
    if (msg.type === 'signed-out') location.assign('/signin');
  }
});

function signalSignedOut() {
  localStorage.setItem('auth.signal', JSON.stringify({ type: 'signed-out', t: Date.now() }));
}
```

## Storage Access API for Embedded Sign-In

Third-party cookies are deprecated. An embedded sign-in widget (your auth in an iframe on a partner site) must request storage access on a user gesture.

| Principle | Check (with threshold/value) |
| --- | --- |
| Ceremony: render a "Sign in" click target; on click call `document.requestStorageAccess()` before running the sign-in flow so the iframe gets first-party storage on grant | The button is visible immediately on load; `requestStorageAccess` is not in the network or call stack until the user clicks |
| Never call `requestStorageAccess` outside a click handler | The browser denies it without a user gesture; render the button first, put the call inside the click handler |
| Long-lived embedded sessions without top-level access use partitioned cookies (CHIPS) carrying `Partitioned`, scoped per top-level site, surviving reloads on the same host without third-party scope | In DevTools the session cookie shows `Partitioned: true` and a `Partition Key` matching the host site |
| Denied storage access: open sign-in in a new top-level window with `window.open`, complete the flow, post the result back via `postMessage` (see `embed-patterns.md`) | The widget transitions to a "Sign in in a new window" affordance whose pop-out, on close, returns the embed to signed-in |

Request-storage-access ceremony:

```js
button.addEventListener('click', async () => {
  try {
    if ('requestStorageAccess' in document) {
      await document.requestStorageAccess();
    }
    await runSignInFlow();
  } catch (err) {
    showFallback();
  }
});
```

## Anti-Patterns

- Storing access tokens or refresh tokens in `localStorage`: cross-site scripting reads them.
- Implicit-flow OAuth: the token leaks in the URL fragment.
- A "Sign in with passkey" button as a sibling to "Sign in with password": bury the password, surface the passkey inline.
- Magic links that expire after 24 hours: that is a credential.
- Silent redirect loops on 401: always escalate to sign-in with a `next` param.
- CAPTCHA on the first interaction: add only on suspicion.
- "By signing in you agree to terms": make consent explicit and visible.
- Strength meters that animate: theatre that wastes the user's time.
- Cross-tab sign-in that auto-reloads a tab with unsaved input: show a banner, let the user choose.
- Embedded sign-in that calls `requestStorageAccess` on page load: the browser denies it and the UX dies.

## Self-Healing Checklist

- [ ] Passkey conditional UI offered above the username field, not as a separate flow
- [ ] `autocomplete="username webauthn"` on the username input
- [ ] OAuth uses Authorization Code with PKCE; no implicit flow anywhere
- [ ] OAuth `state` and `code_verifier` in `sessionStorage`, cleared on callback
- [ ] Tokens never in `localStorage`; session in `httpOnly`, `Secure` cookies
- [ ] Magic link expires under 15 minutes and is single-use
- [ ] Magic link supports cross-device handoff (numeric code or QR)
- [ ] 401 interception with single-flight refresh, no redirect loops
- [ ] Soft-expiry banner before hard expiry, no lost work
- [ ] Recovery codes issued at second-factor enrolment, ten codes, copy / print / save affordances
- [ ] Trusted-device option present and respected
- [ ] Lost-second-factor path includes a documented holding period (24 to 72 hours)
- [ ] CAPTCHA invisible by default, visible only on suspicion, before submit in tab order
- [ ] CAPTCHA failure surfaces as an inline error within two seconds
- [ ] Sign-in form: single submit, autofocus the first field, no Tab traps
- [ ] Multi-error submit shows a top summary, focuses the first invalid field via summary link
- [ ] Sign-up progressively discloses (email, then password); browser back works
- [ ] Terms consent is an explicit checkbox above submit
- [ ] Password strength meter is functional, not theatrical
- [ ] `BroadcastChannel` cross-tab signal in place, with `storage` event fallback
- [ ] Storage Access API requested only inside a user click handler
- [ ] CHIPS partitioned cookies for embedded session, or pop-out fallback if denied

## See Also

- [forms.md](forms.md): input-level concerns (label, validation, autofill, error placement, draft recovery)
- [embed-patterns.md](embed-patterns.md): postMessage handshake, pop-out window, viewport reporting
- [security.md](security.md): cookie hardening, CSP, COOP, COEP, CORP, Trusted Types, Permissions-Policy
- [accessibility.md](accessibility.md): focus management, ARIA, live regions, screen-reader flows
