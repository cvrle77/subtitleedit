---
title: Debug Recipes
purpose: Symptom-driven recipes for the regressions that recur on every project. Each recipe runs Symptom, Reproduce, Isolate, Hypothesise, Verify so the path from sighting to fix is deterministic.
load-when:
  task-keywords: [debug, recipe, hydration mismatch, INP, CLS, layout overflow, focus trap, font-swap CLS, axe, lighthouse, errors-in-console]
  symptoms: [hydration mismatch, INP regression, CLS regression, focus trap leak, horizontal scroll, viewport overflow, font swap CLS, Lighthouse score drop]
prereq: SKILL.md
related: [defects.md, performance.md, observability.md, lighthouse.md]
size: ~332 lines
---

# Debug Recipes

Recipes for the regressions that recur on every project. Each fits one shape: Symptom (what you saw), Reproduce (smallest path back to it), Isolate (turn the suspect off), Hypothesise (canonical root causes ranked by frequency), Verify (the metric or assertion that says it is fixed). Pick by symptom; never run a recipe whose symptom you have not observed.

Verification stack assumed throughout: a headless browser of your choice (Puppeteer, Playwright, or equivalent), Lighthouse, the `web-vitals` library (or `web-vitals/attribution` where stated), DevTools (Performance, Rendering, Coverage, Animations, Lighthouse panels), and `axe`.

## Hydration mismatch

Symptom: a console warning on first paint that the server-rendered DOM did not match the client. Tells: a brief re-render flicker, first-click handlers that fail (listener on the discarded tree), text that flips after a frame.

Reproduce:

- Hard-reload with cache disabled (DevTools Network, "Disable cache" ticked).
- Open the Console BEFORE reload so the warning is captured.
- Confirm the warning fires on every cold load, not one in five. Flaky = date/random-number drift; deterministic = structural.
- Note the exact element path the warning points at (most frameworks log the parent component and the divergent child).

Isolate:

- View source (Cmd+U, View Page Source), find the divergent element in the raw HTML the server sent, then compare to the Elements panel after hydration.
- Classify the divergence: tag mismatch (`<span>` server vs `<div>` client), attribute drift (a class or `data-*` differs), content drift (text differs).
- Disable client-side state hydration for the suspect subtree via the framework's "render on client only" or "suppress hydration warning" escape hatch; if the warning vanishes, the subtree is the suspect.

Hypothesise (cause -> fix, ranked):

| Cause | Fix |
| --- | --- |
| Locale/timezone drift: server formatted a date in UTC, client in the user's locale | Format on the server with an explicit locale and timezone, or render a placeholder server-side and fill in client-side after mount |
| `window`/`document` accessed during render via a `typeof window !== "undefined"` branch | Render the same tree on both sides, then patch in an effect after mount |
| Random IDs from `Math.random()` or `crypto.randomUUID()` during render | Use a stable id source (framework `useId` equivalent, deterministic hash of props, or a prop passed from above) |
| Third-party script (consent banner, analytics tag, extension) mutates the DOM before hydration | Render the third party AFTER hydration completes, or wrap the host element in a stable shell the hydrator can ignore |
| Conditional render based on `localStorage` or cookie (server cannot read `localStorage`) | Move the decision to a cookie the server can read, or render the neutral tree first and patch after mount |
| HTML the parser silently normalises (a `<p>` containing a `<div>` is auto-closed, forbidden content model) | Validate markup with the HTML validator and rewrite to a legal content model |

Verify:

- Cold reload three times in a row; the warning must not fire on any of them.
- Click the first interactive element within 500ms of paint; it must respond on the first click (no discarded-tree handler).
- Record a Lighthouse run; INP attribution must not point at the suspect subtree as the slow event target.
- If using RUM, the hydration error count for the affected route should drop to zero over the next 24 hours.

## INP regression

Symptom: a surface that felt responsive now lags after input. Field metric: 75th-percentile INP rose above 200ms (the WCV threshold) or above the project's own bar.

Reproduce:

- Pick the exact interaction the field data points at (tap a button, type into an input, scroll a list); field data without attribution is a smell, insist on it.
- Throttle the CPU 4x in DevTools Performance and replay the interaction five times; if the lag does not reproduce under throttle the regression is field-specific (device, network, or third party), so record a real-device session.
- Disable browser cache and replay; some INP regressions only surface when a script must be parsed cold.

Isolate:

- Install `web-vitals/attribution` (or an observer exposing `PerformanceEventTiming.target` and the `interactionId`); the payload names the target element and the slowest script.
- Record a Performance trace covering the interaction, identify the long task that overlaps the input, note the script URL and function name.
- Walk back from the long task: which event handler started it, which library frame is on the call stack, which network request blocked the response.
- Disable the suspect script via Network "Request blocking" and replay; if INP returns to baseline the script is the suspect.

Hypothesise (cause -> fix, ranked):

| Cause | Fix |
| --- | --- |
| A third-party script (analytics, A/B test, chat widget, tag manager) shipped a costlier build | Defer, gate behind interaction, or remove |
| A handler ships synchronous compute (sort, parse, format, recompute a derived list) on every keystroke | Move work behind a microtask boundary (`scheduler.yield`, `requestIdleCallback`, or a worker), debounce, or precompute |
| A render cascade: one state change re-renders a tree large enough to blow the budget | Lift the state, memoise, or split the tree at the boundary where the change matters |
| Long Animation Frames stack because a scroll/pointer listener works every frame | Subscribe with `{ passive: true }`, throttle to `requestAnimationFrame`, or move work off the main thread |
| Hydration straggles so the first interaction pays the hydration cost | Hydrate earlier (visible-on-viewport), or render the surface server-only and skip hydration entirely |

Verify:

- Replay the interaction five times with CPU 4x throttle; the slowest event must come in under the project's INP bar.
- RUM (`web-vitals` with attribution) shows 75p INP under 200ms for the route over the next 7 days at minimum population.
- The Lighthouse TBT row does not regress and the long task that caused the regression no longer appears in the trace.

## CLS root-cause hunt

Symptom: Lighthouse reports CLS > 0.1 (mobile) or > 0.05 (desktop), with the score dropping after a recent deploy.

Reproduce:

- Run Lighthouse against the affected URL in a private window with a cold cache (Application, Clear storage).
- Open the Lighthouse "Avoid large layout shifts" row and note every element and its individual shift contribution; the largest one is the suspect.
- Record the Performance panel (reload, stop after settle) and switch to the "Experience" lane where every layout shift draws a red marker.

Isolate:

- Click the largest shift marker in the Performance panel; the bottom pane shows the node that shifted and the node that pushed it.
- Take a screenshot at the start of the recording and at the marker timestamp, then diff visually to see what appeared between the two frames.
- Enable Rendering > Layout Shift Regions; on reload the shift regions paint in blue and the visible blue blob is the culprit.

Hypothesise (cause -> fix, ranked):

| Cause | Fix |
| --- | --- |
| Image without dimensions (an `<img>` with no `width` and `height` attributes reserves zero space) | Declare both `width` and `height` attributes, or set `aspect-ratio` on the parent |
| Web font swap (fallback and webfont have different metrics, pushing content below) | Apply the Font-swap CLS recipe below |
| Late-injected banner, cookie consent, or promo appearing at the top after first paint | Render an empty reserved-height shell on the server and fill it on the client, or render the banner below the fold |
| Embed (video, iframe, social, map) loads without reserved space | Wrap in a fixed-`aspect-ratio` container |
| Animations that move `width`, `height`, `top`, `left`, or `margin` shift content below | Animate `transform: scaleY()` or `opacity` instead and reserve final height up front |
| Async content swap where a skeleton of one height swaps for content of a different height | Size the skeleton to the realistic content height and reuse the same container |

Verify:

- Re-run Lighthouse in a private window with cold cache; the "Avoid large layout shifts" row must report less than 0.1 (mobile) and less than 0.05 (desktop).
- The Performance recording shows no red shift marker after the initial paint.
- The reserved-space fix shows layout settled by the time the LCP element paints (no late nudge below it).

## Accessibility false positive

Symptom: an `axe` rule fires on an element, but a manual screen-reader and keyboard test does not reproduce the failure. You believe the markup is correct.

Reproduce:

- Run `axe` again with a fixed seed (same page, viewport, `axe` version) to confirm the failure is deterministic; if it flakes the page is mutating between scan start and assertion, so capture a stable snapshot first.
- Read the rule's description on the Deque rule page to find the exact assertion it makes (e.g. "every `<button>` must have an accessible name").
- Verify against the spec the rule cites (WCAG SC, ARIA Authoring Practices, HTML5 spec); the rule is a heuristic over the spec and the spec is ground truth.

Isolate:

- In the DevTools Accessibility panel inspect the flagged element's computed Name, Role, State, and Description; if they satisfy the spec the rule is firing on a heuristic that does not apply.
- Reproduce the user task with a real screen reader (VoiceOver, NVDA, TalkBack) and keyboard-only navigation; confirm the user can complete the task without confusion.
- Compare your markup to the WAI-ARIA Authoring Practices reference example for the same pattern; if it matches, the heuristic is wrong about your case.

Hypothesise (cause -> fix, ranked):

| Cause | Fix |
| --- | --- |
| Custom widget with a platform-supplied name (a styled `<select>` carries a programmatic name from its `<label>` but the rule reads the visual wrapper) | The user is fine; the rule reads the wrong node, treat as false positive |
| Decorative element flagged as missing alt (an ornament with `aria-hidden="true"` is correctly hidden) | The rule does not understand `aria-hidden` on the element; treat as false positive |
| Dynamic content invalidates the snapshot (element removed/re-inserted between scan and assertion) | The rule fires on the stale node; capture a stable snapshot first |
| A shadow DOM boundary the rule does not traverse (accessible name comes from a slotted node) | Mark the host with `aria-label` redundantly if the audit tool requires it |
| A genuinely incorrect pattern that happens to work for one assistive tech | Confirm on at least two screen readers (VoiceOver tolerance is not a defence; NVDA or TalkBack may still fail) before declaring a false positive |

Resolve: if at least two screen readers complete the user task AND the spec is satisfied, the rule is a false positive for this case.

- Add an `axe` ignore at the smallest scope (the specific selector or rule on the specific element), not a global suppression.
- Comment the ignore inline with: the rule id, the spec citation, the screen readers verified, the date, and the reviewer initials.
- Re-run `axe` and confirm only the intended ignore is silenced and the rule still fires on other elements.

Verify:

- Manual screen-reader and keyboard pass on the route completes the task without confusion.
- `axe` returns zero violations excluding the documented ignore.
- The next reviewer can read the ignore comment and understand why it is there without asking.

## Layout overflow

Symptom: the page scrolls horizontally on mobile (375 CSS pixels wide) and the defects sweep flags `viewport-bleed` with `scrollWidth` greater than `innerWidth`.

Reproduce:

- Open the page in DevTools Device Toolbar at the iPhone preset (375 wide), confirm horizontal scroll, and drag the document right to note which content reveals.
- Resize the viewport up by 10 CSS pixels at a time until the scroll vanishes, noting the breakpoint at which overflow appears (often a fixed-width child of a flex/grid parent).

Isolate: outline every element and find the one that protrudes.

```css
* { outline: 1px solid red !important; }
```

- Inject the rule via DevTools onto `<html>` so every element draws a red box, then scroll right to find the box extending past the viewport. Inspect that element: compute its width, padding, margin, compare to its parent's content box.
- Alternative: run the defects.md geometry sweep, which flags any element whose `scrollWidth` exceeds its `clientWidth` while `overflow-x` is `visible` (see defects.md).

Hypothesise (cause -> fix, ranked):

| Cause | Fix |
| --- | --- |
| Fixed-width child of a percentage parent (`width: 600px` inside `width: 100%` smaller than 600) | `max-width: 100%`, or `width: 100%` with `max-width: 600px` |
| Long unbreakable string (URL, email, token with no spaces) overflows | `overflow-wrap: anywhere` on the parent, or `word-break: break-word` |
| Image without `max-width: 100%` escapes its container | Declare `max-width: 100%; height: auto` on the image |
| `width: 100vw` inside a padded container (`100vw` includes scrollbar and parent padding) | Use `width: 100%` (which respects the parent), or compute the gap |
| Unstyled `<iframe>` or `<video>` defaults to the source's natural width | `max-width: 100%; width: 100%` |
| Table without `table-layout: fixed` sizes columns by content and one long cell stretches it | `table-layout: fixed; width: 100%; word-break: break-word` |
| Negative margin or transform (e.g. `margin-left: -2rem`) extends past the viewport on small screens | Clamp the negative margin to viewport-aware values, or remove on small breakpoints |

Verify:

- The page does not scroll horizontally at 320, 375, and 425 CSS pixels wide.
- The geometry sweep returns zero `viewport-bleed` issues.
- No element's `scrollWidth` exceeds `document.scrollingElement.clientWidth`.

## Focus trap leak

Symptom: a modal/dialog is open but Tab moves focus out to background content, and the screen reader reads the background as well as the dialog.

Reproduce:

- Open the dialog, press Tab repeatedly, and confirm focus eventually lands on a background element (page header, an article-body link, a footer button).
- Note whether focus reaches the dialog's close button: if yes the trap is partial (escape only), if no the trap is absent.

Isolate:

- In the Elements panel find the node marked `aria-modal="true"` (or the dialog component root).
- Find the background node marked `inert` or `aria-hidden="true"`; if neither marker is present no trap is in place (absent-trap case).
- Enumerate the tab order; if background elements appear while the dialog is open, the inert boundary is wrong.

```js
const focusable = "a[href], button:not([disabled]), input:not([disabled]), select, textarea, [tabindex]:not([tabindex='-1'])";
[...document.querySelectorAll(focusable)]
  .filter(el => el.offsetParent !== null)
  .map(el => ({ tag: el.tagName, text: (el.textContent || el.value || "").trim().slice(0, 40) }))
  .forEach(row => console.log(row));
```

Hypothesise (cause -> fix, ranked):

| Cause | Fix |
| --- | --- |
| `inert` applied to the wrong ancestor (marked `<main>` inert but header/footer stay interactive) | Mark every sibling of the dialog inert, not just one |
| The dialog is a descendant of the inert region (marking `body` inert traps the dialog too) | Render the dialog outside the inert subtree, or remove it from the inert subtree when it opens |
| `contenteditable` inside the dialog (a rich-text editor steals focus from the trap library) | Detect editable nodes and add them explicitly to the trap's allow-list |
| A third-party widget (toast, tooltip, autocomplete popover) renders as a portal at body level | Mark the third-party container inert when the dialog opens, or render dialog and third party in the same portal root |
| The trap library disabled Tab interception during a focus event | Subscribe to `focusin` at the document level and bounce focus back if it leaves the dialog |
| A native `<dialog>` used with `show()` instead of `showModal()` skips the platform's focus trap | Use `showModal()` |

Verify:

- Tab through the open dialog at least twice the number of focusable elements; focus must cycle within the dialog and never reach background content.
- Shift+Tab from the first focusable element returns to the last, not to the background.
- Esc closes the dialog and returns focus to the trigger.
- A screen reader reading "from the top" while the dialog is open must read only the dialog content.

## Lighthouse flake triage

Symptom: two runs against the same URL score wildly differently (99 then 85) and the team cannot agree whether the page passed.

Reproduce:

- Run Lighthouse five times in a row from the same machine (cache disabled, private window, same network); if the spread is greater than five points across the five runs, the page or run environment is flaky.
- Note which audit category drives the spread; Performance is the usual suspect while Accessibility, Best Practices, and SEO are deterministic.

Isolate:

- Throttling settle time: run the first Lighthouse pass after a 5-second warm-up and do not score it.
- Hot vs cold cache: a Service Worker or stale `localStorage` flag can survive a cold cache; open Application > Clear storage before each run.
- Network variability from Wi-Fi, VPN, or a flaky upstream produces variance Lighthouse cannot smooth; run from a wired connection or a CI runner.
- Block third parties (Request blocking or a `--block-url-patterns` flag) to see if the score stabilises, since analytics or A/B tests ship different code per run.
- Disable auto-play animations/videos for the run, since continuous activity holds the main thread and Lighthouse measures the activity rather than the steady-state page.

Hypothesise (cause -> fix, ranked):

| Cause | Fix |
| --- | --- |
| Third-party scripts vary per run (different ads, A/B buckets, feature flags) producing a score difference greater than five points across runs | Block third parties |
| The page does not reach a stable state (a polling timer, animation, or live region keeps the main thread busy) so Lighthouse never sees the quiet period | Remove or pause the continuous activity for the run |
| The runner is shared (a CI machine running multiple jobs introduces variance) | Pin the runner type or run in a sandbox |
| The page depends on time of day (a cron, daily refresh, or market-hours conditional renders different content) | Pin the request time |

Recipe: errors-in-console flake (third-party beacon discriminator). Symptom: the Best Practices `errors-in-console` audit fails intermittently across runs. Two hypotheses, separated by a headless reproduce (run from a headless browser of your choice, Puppeteer, Playwright, or equivalent, capturing `console` errors on a clean profile):

- Hypothesis A (false positive, a client blocker): the console error only appears when a content/ad blocker or extension cancels a third-party beacon request. Check: a clean headless profile with no extensions reproduces NO error. Then it is a client-side blocker artefact, not a page defect; document it and exclude that origin.
- Hypothesis B (real bug, stale SRI auto-injection): a third party auto-injects a script whose integrity hash drifted, so the browser rejects it (a stale SRI beacon). Check: the clean headless profile DOES reproduce the integrity/SRI error every run. Then it is a real defect: pin or refresh the integrity hash when you control the injection point; when an edge platform auto-injects the tag and you cannot control the hash, self-inject the plain vendor tag (no integrity, no crossorigin) so the auto-injector skips its broken pinned one (see lighthouse.md and security.md for SRI).

Median-of-three workflow: run Lighthouse three times back-to-back, report the median score per audit category, and reject any individual run scoring more than 10 points off the median (likely a network or third-party spike). For the production-build requirement and the phantom-failure rule (a dev build inflating or deflating scores), see lighthouse.md.

Verify:

- Three consecutive runs land within five points of each other on every audit category.
- The median score meets the project's bar and CI runs the median-of-three workflow recording the median, not the single run.

## Font-swap CLS

Symptom: FCP and LCP are fine but CLS is above 0.1, with the shift happening after first paint as text reflows when the webfont arrives. The CLS row points at the heading or body text node.

Reproduce:

- Throttle the network to "Slow 4G" in DevTools so the font swap is visible to the eye, then reload with cache disabled and watch the fallback font paint, the webfont swap in, and lines reflow.

Isolate:

- Confirm `font-display: swap` is in use (not `block`, which delays paint instead of shifting it); switching `block` to `swap` may move the cost to FCP, so choose deliberately.
- Identify the fallback font (next entry in the `font-family` stack): macOS often system serif/sans, Windows Segoe UI or Arial, Linux varies.
- Measure both fonts' metrics with a font inspection tool (FontDrop, fontview, or the Browser Font panel): ascent, descent, line gap, x-height, cap height.

Hypothesise (cause -> fix, ranked):

| Cause | Fix |
| --- | --- |
| Fallback metrics differ from webfont metrics so the line-box height changes on arrival | Declare a `@font-face` for the fallback with metrics-override descriptors |
| The webfont is variable but loaded as multiple files so a different weight loads and shifts | Use one variable font file and tune the metrics-override for the typical weight |
| `size-adjust` not set (two fonts at the same `font-size` render at different visual sizes) | Measure the x-height ratio and apply `size-adjust` |

Metrics-override workflow:

- Download both the webfont and chosen fallback (actual files, not OS reference), open in a font inspector, and record `unitsPerEm`, `ascent`, `descent`, `lineGap`, `xHeight`, `capHeight` for each.
- Compute the four override values:
  - `ascent-override` = `(webfont.ascent / webfont.unitsPerEm) / (fallback.ascent / fallback.unitsPerEm) * 100%`
  - `descent-override` = same ratio for descent
  - `line-gap-override` = same ratio for line gap
  - `size-adjust` = `(webfont.xHeight / webfont.unitsPerEm) / (fallback.xHeight / fallback.unitsPerEm) * 100%`
- Declare a `@font-face` for the fallback with these overrides:

```css
@font-face {
  font-family: "Webfont Fallback";
  src: local("Arial");
  ascent-override: 92.5%;
  descent-override: 24.3%;
  line-gap-override: 0%;
  size-adjust: 105.7%;
}

:root {
  font-family: "Webfont", "Webfont Fallback", sans-serif;
}
```

The fallback now occupies the same visual box as the webfont; the swap does not shift.

Verify:

- With network throttled to Slow 4G, reload three times; the visible reflow on swap disappears.
- Lighthouse CLS for the affected page drops under 0.1.
- The Performance recording's Experience lane shows no shift after FCP for any text node.

## See Also

- [defects.md](defects.md) for the symptom-to-fix lookup and the geometry sweep
- [performance.md](performance.md) for the LCP, INP, CLS depth treatments these recipes assume
- [observability.md](observability.md) for the RUM, source-maps, and INP-attribution wiring that makes field reproduction possible
- [lighthouse.md](lighthouse.md) for the production-build requirement, the phantom-failure rule, and Lighthouse audit-id depth
