---
title: PWA and Offline
purpose: Decide when a Progressive Web App earns its engineering cost, then implement service workers, offline shells, install prompts, push, and background sync without stranding users on a stale build.
load-when:
  task-keywords: [PWA, offline, service worker, SW, install prompt, push, background sync, manifest, render strategy, checklist]
  symptoms: [score dropped, slow page, third-party script slow]
prereq: SKILL.md
related: [performance.md, observability.md, pre-launch.md, security.md]
size: ~381 lines
---

# PWA and Offline

A PWA is a contract: the surface stays useful when the network does not, updates roll out without breaking the open tab, and the device treats the site like an app. Pay this cost only when the surface earns it.

## When to Invest in PWA

Ship if at least one rule holds; treat PWA as a first-class capability with its own pre-launch gate (see pre-launch.md) when two of three hold. If none apply, ship a fast website.

| Rule | Concrete check |
| --- | --- |
| Cross-session reuse | Users return to the same surface within seven days more than 30 percent of the time. A convert-once landing page does not pay back a service worker. |
| Install desire | Users would benefit from a launcher icon and a non-tab window (kiosk surfaces, internal tools, daily-use consumer apps, productivity tools). If they would never install it, do not ship `manifest.json`. |
| Offline-essential surface | Some workflow must keep working without connectivity (reading saved content, drafting a message, viewing already-loaded data, recording a measurement). If everything requires the server, offline support is theatre. |

- Anti-pattern: turning a static marketing site into a PWA because the audit gives points for it. The service-worker maintenance, kill-switch, install-prompt UX, and push-etiquette cost outweighs the benefit.

## Service Worker Lifecycle

A service worker is a separate JavaScript context with its own lifecycle, not a page script. Understand it or strand active tabs on a stale build.

| Event | Fires | Use it to |
| --- | --- | --- |
| `install` | Once per worker version, on first sight of a new file (worker not yet controlling pages) | Precache the app shell and version-pinned assets |
| `activate` | After `install` succeeds and any old worker stops controlling clients (worker now eligible to control pages) | Clean up old caches |
| `fetch` | Every network request from a controlled page | Decide cache, network, or both |

- Default lifecycle: a new worker waits until every controlled page closes before activating. This protects open tabs but leaves long-lived tabs on an outdated cache until closed.
- On the page, prompt the user before reloading for a new worker so unsaved input is not lost.

```javascript
// In the service worker
self.addEventListener('install', (event) => {
  self.skipWaiting();
  event.waitUntil(
    caches.open('shell-v3').then((cache) => cache.addAll(SHELL_URLS))
  );
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    Promise.all([
      caches.keys().then((keys) => Promise.all(
        keys.filter((k) => k !== 'shell-v3' && k !== 'runtime-v1')
            .map((k) => caches.delete(k))
      )),
      self.clients.claim()
    ])
  );
});
```

```javascript
// On the page: prompt before reload, never lose guarded input
navigator.serviceWorker.register('/sw.js').then((reg) => {
  reg.addEventListener('updatefound', () => {
    const next = reg.installing;
    next.addEventListener('statechange', () => {
      if (next.state === 'installed' && navigator.serviceWorker.controller) {
        showUpdateBanner(() => next.postMessage({ type: 'SKIP_WAITING' }));
      }
    });
  });
});

navigator.serviceWorker.addEventListener('controllerchange', () => {
  window.location.reload();
});
```

- Verify: open the surface in two tabs, deploy a new worker, observe one banner per tab, accept on one, confirm the other tab reloads cleanly without losing guarded user input.

## Offline Shell Pattern

The app shell is the minimum HTML, CSS, and JavaScript needed to render the application chrome (top bar, navigation, empty content slot) without any server data. Cache it at `install` so the surface paints offline on first navigation.

Precache:

- Shell HTML (a single document or a per-route entry document).
- Critical CSS bundled with the shell.
- Application logo and any chrome icons.
- A fallback offline page for routes that need network data.
- A small set of recently used dynamic assets if the surface depends on them.

- Hard cap the shell at 500 KB compressed; anything heavier means the user pays the cost on every install and update.

Runtime `stale-while-revalidate` for assets that tolerate a one-version-stale read:

```javascript
self.addEventListener('fetch', (event) => {
  const { request } = event;
  if (request.destination === 'image' || request.destination === 'style') {
    event.respondWith(staleWhileRevalidate(request));
  }
});

async function staleWhileRevalidate(request) {
  const cache = await caches.open('runtime-v1');
  const cached = await cache.match(request);
  const fetchPromise = fetch(request).then((response) => {
    if (response.ok) cache.put(request, response.clone());
    return response;
  }).catch(() => cached);
  return cached || fetchPromise;
}
```

Navigation Preload: worker startup can add 100 to 400 ms before a navigation fetch begins. Preload starts the fetch in parallel with worker startup.

```javascript
self.addEventListener('activate', (event) => {
  event.waitUntil(
    self.registration.navigationPreload &&
    self.registration.navigationPreload.enable()
  );
});

self.addEventListener('fetch', (event) => {
  if (event.request.mode === 'navigate') {
    event.respondWith((async () => {
      const preloaded = await event.preloadResponse;
      if (preloaded) return preloaded;
      return fetch(event.request).catch(() => caches.match('/offline.html'));
    })());
  }
});
```

- Verify: cold-start on a throttled connection, measure first navigation, compare with and without preload enabled.

## Caching Strategies Cookbook

Pick one named strategy per resource class, never per request inside a class.

| Strategy | Behaviour | Best for |
| --- | --- | --- |
| Cache-first | Return cache if present, else fetch and cache | Immutable, versioned assets (`/assets/main.abc123.js`, fonts, icons) |
| Network-first | Try network, fall back to cache | HTML and per-request JSON where freshness beats offline |
| Stale-while-revalidate | Return cache immediately, refresh in background | Non-critical images, theme assets, infrequently-updated JSON |
| Network-only | Always fetch, never cache | Analytics beacons, authentication endpoints, anywhere stale answers are dangerous |
| Cache-only | Always return cache | Shell-precached resources where the build pipeline guarantees freshness on deploy |

```javascript
async function cacheFirst(request) {
  const cached = await caches.match(request);
  if (cached) return cached;
  const response = await fetch(request);
  const cache = await caches.open('immutable-v1');
  cache.put(request, response.clone());
  return response;
}

async function networkFirst(request) {
  try {
    const response = await fetch(request);
    const cache = await caches.open('runtime-v1');
    cache.put(request, response.clone());
    return response;
  } catch (err) {
    const cached = await caches.match(request);
    return cached || caches.match('/offline.html');
  }
}
```

Decision matrix:

| Resource class | Strategy |
| --- | --- |
| Versioned static (`abc123.js`, `abc123.css`) | Cache-first |
| Shell HTML | Cache-first with periodic background revalidation |
| Per-request HTML | Network-first with offline fallback |
| Read-mostly API JSON | Stale-while-revalidate |
| Write API (POST, PUT, DELETE) | Network-only (queue for Background Sync) |
| User avatars, thumbnails | Stale-while-revalidate |
| Analytics, telemetry | Network-only |
| Auth tokens, session endpoints | Network-only |

## Manifest Correctness for Installability

The browser installs the site when the user opens it on a supported platform, has engaged, and the manifest passes every required field. Miss one field, no install prompt.

| Field | Concrete check |
| --- | --- |
| `name` | Full application name, 45 characters or fewer. |
| `short_name` | Launcher label, 12 characters or fewer; longer truncates. |
| `icons` | At least one 192x192 PNG and one 512x512 PNG. The 512 must be square and look correct after the platform mask (maskable on some platforms, rounded corner on others). |
| `start_url` | Where the launcher opens. Absolute path (`/?source=pwa`) so tracking distinguishes installed traffic. |
| `display` | One of `standalone`, `fullscreen`, `minimal-ui`. `standalone` for most apps; `fullscreen` only for games or kiosks. |
| `theme_color` | Matches the top window-chrome color the device renders. |
| `background_color` | Shown while the app shell loads (the splash screen). |
| `id` | Stable identifier so the browser does not treat a `start_url` change as a new application. Set once and never change it. |

```json
{
  "name": "Product Name",
  "short_name": "Product",
  "id": "/",
  "start_url": "/?source=pwa",
  "display": "standalone",
  "theme_color": "#1a1a1a",
  "background_color": "#fafafa",
  "icons": [
    { "src": "/icons/192.png", "sizes": "192x192", "type": "image/png" },
    { "src": "/icons/512.png", "sizes": "512x512", "type": "image/png" },
    { "src": "/icons/512-maskable.png", "sizes": "512x512", "type": "image/png", "purpose": "maskable" }
  ]
}
```

- Verify: open the browser DevTools Application panel, Manifest view. Every error and warning is a defect.

## Install Prompt Timing

The install event fires when the browser decides the site is installable. Capture it, suppress the default, and surface an affordance only after the user has clearly engaged.

```javascript
let deferredPrompt = null;

window.addEventListener('beforeinstallprompt', (event) => {
  event.preventDefault();
  deferredPrompt = event;
  document.querySelector('#install-button').hidden = false;
});

document.querySelector('#install-button').addEventListener('click', async () => {
  if (!deferredPrompt) return;
  deferredPrompt.prompt();
  const choice = await deferredPrompt.userChoice;
  deferredPrompt = null;
  document.querySelector('#install-button').hidden = true;
});
```

- Value-demonstration rule: show the affordance only after a moment of value (completed a workflow, saved an item, returned for a second session). The first-load install banner is a defect.
- Never block content: the affordance is a small button or dismissible chip, never a modal, takeover, or blocking overlay. Dismiss once means hidden for the rest of the session.
- Verify: open as a new user, perform the first meaningful action, observe the affordance appear afterward, dismiss it, navigate, confirm it stays dismissed.

## Push Permission Timing

The push permission prompt is a one-shot grant. If denied, the surface cannot ask again without the user manually re-enabling it in browser settings. Treat the prompt as precious.

- Grant rate by entry point: first-load prompts grant under 5 percent; in-context prompts grant 40 to 70 percent.
- Never on first load: the user has no value signal, so the safe answer is no.
- Ask after a clear value action that obviously benefits from notifications (subscribes to a thread, saves a search, sets a reminder), with the enable affordance in the follow-up.
- In-app preview: before calling `Notification.requestPermission()`, show an in-app modal previewing exactly what the notifications look like and what triggers them. Only call the real permission API once the user confirms the in-app modal.

```javascript
async function offerPush() {
  const inAppConfirmed = await showPushPreviewModal();
  if (!inAppConfirmed) return;
  const permission = await Notification.requestPermission();
  if (permission === 'granted') await subscribeToPush();
}
```

- Verify: run a permission-grant funnel in analytics. The in-app preview should lift grant rate by 3x to 10x relative to a cold prompt.

## Background Sync

Background Sync queues a request when connectivity drops and replays it when the network returns. Best for write operations that must eventually succeed (sending a message, posting a comment, recording a measurement).

```javascript
// On the page
async function sendWithRetry(payload) {
  try {
    await fetch('/api/messages', { method: 'POST', body: JSON.stringify(payload) });
  } catch (err) {
    await saveToIDB('outbox', payload);
    const reg = await navigator.serviceWorker.ready;
    await reg.sync.register('flush-outbox');
  }
}

// In the service worker
self.addEventListener('sync', (event) => {
  if (event.tag === 'flush-outbox') {
    event.waitUntil(flushOutbox());
  }
});
```

- Support is uneven across engines (some Chromium-based engines ship it; others do not, as of 2026). For unsupported engines, fall back to a foreground retry queue with `online` event listening.
- Verify: load the surface, switch to airplane mode, perform a write, return online, confirm the write completes without user intervention.

## Service Worker Kill-Switch

A buggy worker can take a site down for every returning user until a new worker takes over. Ship the kill-switch on day one.

Deploy a static page at a known URL (`/unregister-sw`) that unregisters every registration and deletes every cache:

```html
<!DOCTYPE html>
<meta charset="utf-8">
<title>Reset Storage</title>
<script>
  (async () => {
    if ('serviceWorker' in navigator) {
      const regs = await navigator.serviceWorker.getRegistrations();
      await Promise.all(regs.map((r) => r.unregister()));
    }
    if ('caches' in window) {
      const keys = await caches.keys();
      await Promise.all(keys.map((k) => caches.delete(k)));
    }
    document.body.textContent = 'Done. You can close this tab.';
  })();
</script>
```

- Serve the unregister page with the strongest reset header:

```text
Clear-Site-Data: "cache", "cookies", "storage"
```

- Respect `?nosw=1`: the active worker checks every navigation URL and unregisters itself, falling through to the network:

```javascript
self.addEventListener('fetch', (event) => {
  const url = new URL(event.request.url);
  if (url.searchParams.has('nosw')) {
    self.registration.unregister();
    return;
  }
});
```

- Never cache the service worker file itself; serve `/sw.js` so a buggy worker does not stay alive after the fix lands:

```text
Cache-Control: no-cache, max-age=0, must-revalidate
```

- Verify: deploy a worker with a deliberate fault, confirm the unregister page recovers a stuck client, confirm `?nosw=1` recovers without manual cache clear.

## Web Share API

`navigator.share()` opens the platform share sheet so the user picks the target. Prefer it over a custom share menu when targets are known to the platform but unpredictable to your code.

```javascript
async function share(payload) {
  if (!navigator.share) return fallbackShareMenu(payload);
  try {
    await navigator.share({
      title: payload.title,
      text: payload.summary,
      url: payload.url
    });
  } catch (err) {
    if (err.name !== 'AbortError') reportShareFailure(err);
  }
}
```

- Prefer the platform sheet for any share button on touch platforms: it is faster, knows the user's frequent contacts, and respects accessibility settings.
- Keep a custom menu when sharing has side effects in your app (creates a server-side record, changes document permissions); then the menu is part of the workflow, not an OS handoff.
- Verify: tap the share affordance on a touch platform; the native sheet should open in under 200 ms.

## File System Access API

The File System Access API (`window.showOpenFilePicker`, `showSaveFilePicker`, `showDirectoryPicker`) gives durable handles to user-chosen files and folders without round-tripping through downloads.

- Available in Chromium-based browsers since 2020; other engines fall back to upload-and-download. Use it for editors that benefit from save-in-place, gate it behind feature detection, and ship a download fallback.

## See Also

- [performance.md](performance.md): caching headers, resource hints, and the bundle budgets that govern precache size.
- [observability.md](observability.md): instrumenting service-worker errors, push grant rates, and offline-render telemetry.
- [pre-launch.md](pre-launch.md): the PWA pre-launch gates (kill-switch deployed, manifest valid, install timing reviewed).
- [security.md](security.md): `Clear-Site-Data` headers, scope-isolation rules, and service-worker supply-chain considerations.
