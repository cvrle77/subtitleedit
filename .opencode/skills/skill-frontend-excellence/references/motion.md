---
title: Motion and Animation
purpose: Framework-agnostic motion that conveys meaning, holds 60fps, respects user preferences, and uses modern platform primitives (View Transitions, WAAPI, scroll-driven, @starting-style). Every above-the-fold animation ships only inside the LCP/TBT/CLS budget.
load-when:
  task-keywords: [motion, animation, transition, easing, View Transitions, scroll-driven, WAAPI, will-change, "@starting-style", reduced motion, INP, CLS]
  symptoms: [INP regression, slow interaction, CLS regression, score dropped]
prereq: SKILL.md
related: [performance.md, accessibility.md, ui-ux.md, design.md]
size: ~688 lines
---

# Motion and Animation

Motion is a tool, not decoration. It earns its place only by doing one of four jobs; otherwise remove it.

## Why Motion

- Communicate cause and effect: a button press triggers a state change and the motion confirms it.
- Maintain spatial continuity: show where an element came from and where it goes.
- Direct attention: animate what the user should look at next.
- Express brand personality: spring bounces feel playful, linear fades feel refined.
- If a motion serves none of cause/effect, spatial continuity, attention, or brand: remove it.

## The Four Properties of UI Motion

| Property | Default | Range / Rule |
|----------|---------|--------------|
| Duration | 200-250ms (UI), 300-400ms (modal/page transition) | 100-500ms; rarely above 500ms in UI |
| Easing | `ease-out` entering, `ease-in` exiting | `linear` forbidden in UI |
| Trigger | User action or state change | Time alone (idle) is decorative |
| Interruptibility | Always interruptible | Never block input |

## Duration

| Interaction | Duration |
|-------------|----------|
| Hover, focus, press feedback | 100-150ms |
| Toggle, switch | 150-200ms |
| Disclosure, accordion | 200-300ms |
| Tab change, segmented control | 150-250ms |
| Modal/sheet entrance | 250-350ms |
| Modal/sheet exit | 200-250ms (faster than entrance) |
| Page/route transition | 300-400ms |
| Onboarding sequence | 400-600ms per beat |
| Skeleton shimmer | 1500ms loop, low contrast |

- Above 500ms feels slow in UI: reserve for one-off cinematic moments.
- Exit motions should be 60-70% of entrance duration so getting content out of the way feels responsive.

## Easing

| Curve | Use |
|-------|-----|
| `ease-out` (`cubic-bezier(0, 0, 0.2, 1)`) | Entering: element decelerates as it arrives. |
| `ease-in` (`cubic-bezier(0.4, 0, 1, 1)`) | Exiting: element accelerates as it leaves. |
| `ease-in-out` (`cubic-bezier(0.4, 0, 0.2, 1)`) | Symmetric state change in place. |
| `ease` (`cubic-bezier(0.25, 0.1, 0.25, 1)`) | Default; usable but generic. |
| Spring | Playful or characterful: use a physics-based library (stiffness, damping, mass). |
| `linear` | NEVER for UI motion. Reserved for continuous loops (spinners) and progress bars. |

Material Design 3 standard curves (usable defaults):

- Standard: `cubic-bezier(0.2, 0, 0, 1)`
- Decelerated (entering): `cubic-bezier(0, 0, 0, 1)`
- Accelerated (exiting): `cubic-bezier(0.3, 0, 1, 1)`
- Emphasized: `cubic-bezier(0.2, 0, 0, 1)` (longer durations, more dramatic)

Apple-style spring curves (CSS):

- Soft snap: `cubic-bezier(0.34, 1.56, 0.64, 1)` (slight overshoot, soft return)
- Quick snap: `cubic-bezier(0.4, 1.5, 0.5, 1)` (quicker overshoot)
- Reduced motion: `cubic-bezier(0.4, 0, 0.2, 1)` (no overshoot)

For genuinely physics-based motion, use a library with stiffness, damping, mass; that natural feel is one bezier curves rarely match.

### CSS `linear()` for spring approximations

`linear()` (Baseline 2024) takes a list of stops and interpolates linearly between them, approximating a spring or any arbitrary curve in pure CSS, no library and no JS physics simulation.

```css
.bounce {
  transition: transform 600ms linear(
    0, 0.009, 0.035, 0.078, 0.137, 0.21, 0.296, 0.394, 0.502,
    0.62, 0.745, 0.876, 1, 1.062, 1.114, 1.156, 1.186, 1.207,
    1.217, 1.219, 1.213, 1.2, 1.18, 1.156, 1.13, 1.103, 1.078,
    1.056, 1.039, 1.024, 1.014, 1.007, 1.003, 1.001, 1
  );
}
```

Do not hand-write the stops. Generate them once from a spring (stiffness, damping, mass) with a `linear()` generator tool, then paste into CSS. The result runs on the compositor with no main-thread cost and respects `prefers-reduced-motion` through the normal CSS path. Use it in place of hand-rolled cubic-bezier-fit-to-spring and JS spring libraries for the common snap-with-overshoot feel.

## What to Animate (and What NOT to Animate)

Animate (compositor-only, run on the GPU, hold 60fps, no layout/paint):

- `transform` (translate, rotate, scale)
- `opacity`
- `filter` (sparingly)
- `clip-path` (modern browsers)

NEVER animate:

- `width`, `height`, `top`, `left`, `right`, `bottom`, `margin`, `padding`, `font-size`: they trigger layout, then paint, then composite.
- `box-shadow` size (only its `opacity` is cheap).
- Anything that changes layout for surrounding elements.

For expanding/collapsing, use `transform: scale()` plus `clip-path`, or the `view-transition` API for layout transitions.

### When you must animate layout: FLIP

1. Measure the element's First position.
2. Apply the change.
3. Measure the Last position.
4. Compute the Invert transform.
5. Animate transform from inverted to identity (Play).

For full route or DOM transitions, the `view-transition` API is the right tool.

## Web Animations API

CSS animations are the default for declarative, repeatable motion. Reach for the Web Animations API (`element.animate()`, `Animation`, `getAnimations()`) the moment motion becomes stateful, interruptible, dynamic, or coordinated across elements at runtime.

```js
const anim = card.animate(
  [{ transform: 'translateY(20px)', opacity: 0 }, { transform: 'translateY(0)', opacity: 1 }],
  { duration: 400, easing: 'cubic-bezier(0.2, 0, 0, 1)', fill: 'forwards' }
);

anim.onfinish = () => anim.commitStyles();
```

What WAAPI does that CSS cannot:

- Interruptible: `anim.pause()`, `anim.cancel()`, `anim.reverse()`, `anim.playbackRate = 0.5`. CSS cannot interrupt cleanly.
- Coordinated: `getAnimations()` returns every running animation on the page, to pause all motion on a route change or gate an is-animating check.
- Stateful: `commitStyles()` writes current values to inline style, so the element ends in its end state without `fill: forwards` quirks.
- Dynamic keyframes: accepts arbitrary computed keyframe objects with runtime DOM-measured values, which CSS cannot.

Reach for WAAPI when:

- You start an animation then interrupt it on a user gesture (cancel on click, reverse on hover-out, pause during a drag).
- You run a FLIP layout animation whose keyframes depend on measured before/after positions.
- You coordinate animations across multiple elements with shared timing or a single global controller.

Stay with CSS animations for hover effects, static reveals, skeleton loops, simple modal entrances: anything where the same animation always plays the same way.

## Reduced Motion

Always respect `prefers-reduced-motion: reduce`. The global stomp is the floor:

```css
@media (prefers-reduced-motion: reduce) {
  *, *::before, *::after {
    animation-duration: 0.01ms !important;
    animation-iteration-count: 1 !important;
    transition-duration: 0.01ms !important;
    scroll-behavior: auto !important;
  }
}
```

Better: tier the motion.

- Essential (loading spinner conveying activity, progress bar showing percent): keep.
- Helpful (slide-in modal, fade-in card): replace with instant transitions.
- Decorative (parallax, scroll-triggered reveals, hero text staggered animations): remove entirely.

```css
@media (prefers-reduced-motion: reduce) {
  .hero-letters span {
    opacity: 1;
    transform: none;
    animation: none;
  }
}
```

For JS animations, check the preference first:

```js
const prefersReduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
if (!prefersReduced) {
  // Run the animation
}
```

See accessibility.md for full reduced-motion treatment.

## Choreography

When multiple elements animate, choreograph them; do not let everything animate at once.

### Stagger

- Stagger list/grid entries by 30-50ms so the user sees a sequence, not a flash.
- Cap stagger at 8-10 items so the last item is not obviously waiting.

```css
.list > * { animation: rise 400ms ease-out backwards; }
.list > *:nth-child(1) { animation-delay: 0ms; }
.list > *:nth-child(2) { animation-delay: 50ms; }
.list > *:nth-child(3) { animation-delay: 100ms; }
```

```js
items.forEach((item, i) => {
  item.style.animationDelay = `${i * 50}ms`;
});
```

### Sequence

Hero load: heading first, then sub-heading, then CTAs, then image. Each beat 100-200ms after the previous start.

### Coordinated entry/exit

Modal: backdrop fades in (200ms), then dialog scales up + fades in (250ms). On exit: dialog scales down + fades out (180ms), then backdrop fades out (150ms).

## Scroll-Triggered Motion

### CSS-only (preferred)

```css
.fade-in-on-scroll {
  opacity: 0;
  transform: translateY(20px);
  animation: rise 600ms ease-out forwards;
  animation-timeline: view();
  animation-range: entry 0% cover 30%;
}

@keyframes rise {
  to { opacity: 1; transform: translateY(0); }
}
```

Use `IntersectionObserver` as a fallback to cover most cases.

### `scroll()` vs `view()` (both Baseline 2024)

- `view()` ties an animation to an element entering/leaving the viewport (is-this-element-visible).
- `scroll()` ties an animation to a scroll container's position, 0% (top) to 100% (bottom) (where-in-the-document-am-I).
- Both run on the compositor at 60fps and respect `prefers-reduced-motion`.

Canonical `scroll()` uses:

- Reading progress bar, zero JS:

```css
.progress {
  position: fixed; top: 0; left: 0; height: 3px;
  background: var(--brand);
  transform-origin: left;
  animation: fill linear;
  animation-timeline: scroll(root);
  animation-range: 0% 100%;
}

@keyframes fill { from { transform: scaleX(0); } to { transform: scaleX(1); } }
```

- Sticky-fade header: background opacity rises from 0 to 1 over the first 100 to 200px of scroll, no scroll listener.
- Section-position indicator: a side-nav dot moves down a vertical track as the page scrolls.

### Intersection Observer (universal)

```js
const observer = new IntersectionObserver((entries) => {
  entries.forEach((entry) => {
    if (entry.isIntersecting) {
      entry.target.classList.add('in-view');
      observer.unobserve(entry.target);
    }
  });
}, { threshold: 0.2 });

document.querySelectorAll('.reveal').forEach((el) => observer.observe(el));
```

```css
.reveal {
  opacity: 0;
  transform: translateY(20px);
  transition: opacity 600ms ease-out, transform 600ms ease-out;
}

.reveal.in-view {
  opacity: 1;
  transform: translateY(0);
}
```

### Anti-patterns

- Every section animating on scroll: the page feels like it is still loading at the bottom.
- Parallax on every section: disorienting and often hurts CLS.
- Long animations triggered by scroll: they fire after the user has moved past the trigger.
- Fix: animate selectively, one or two key sections per page.

## Page Transitions

SPA route change:

```css
::view-transition-old(root),
::view-transition-new(root) {
  animation-duration: 200ms;
  animation-timing-function: cubic-bezier(0.4, 0, 0.2, 1);
}

::view-transition-old(root) { animation-name: fade-out; }
::view-transition-new(root) { animation-name: fade-in; }
```

Shared element transition: matching `view-transition-name` on both elements makes the browser auto-morph between them.

```css
.card-image { view-transition-name: card-image; }
.detail-image { view-transition-name: card-image; }
```

### Cross-document View Transitions (MPA)

Ships in Chrome 126+ and Safari 18+; Firefox is rolling it out, so treat it as progressive enhancement. One line opts the whole site into cross-page transitions, no JS, router, or framework:

```css
@view-transition {
  navigation: auto;
}
```

The browser captures the outgoing document, fetches and parses the incoming one, then crossfades as the new page renders. Shared elements across the boundary auto-morph by matching name:

```css
.product-image { view-transition-name: product-hero; }
```

Constraints:

- Same-origin only: the browser will not transition across origins.
- Both navigations must opt in (`@view-transition { navigation: auto }` in the CSS of both pages).
- Firefox without support degrades to a normal navigation, no transition; the page still works.

## Loading and Progress Indicators

### Spinner

For < 1s waits: linear rotation, infinite, 800-1000ms per revolution. Add `aria-label="Loading"` (or wrap in a container with one).

```css
@keyframes spin { to { transform: rotate(360deg); } }
.spinner { animation: spin 800ms linear infinite; }
```

### Skeleton screen

For 300ms+ waits: match the eventual layout, animate a subtle shimmer. Mark with `aria-busy="true"` and `aria-live="polite"` so screen readers announce when content arrives.

```css
.skeleton {
  background: linear-gradient(
    90deg,
    var(--surface-muted) 0%,
    var(--surface) 50%,
    var(--surface-muted) 100%
  );
  background-size: 200% 100%;
  animation: shimmer 1500ms ease-in-out infinite;
}

@keyframes shimmer {
  0% { background-position: 200% 0; }
  100% { background-position: -200% 0; }
}
```

### Progress bar

For determinate progress, animate `transform: scaleX()` from 0 to 1, not `width`.

```css
.progress-bar {
  transform-origin: left;
  transform: scaleX(0);
  transition: transform 200ms linear;
}

.progress-bar.at-50 { transform: scaleX(0.5); }
```

## Hover and Press Effects

- Button hover: shift background by one tone, change border, raise by 1-2px.
- Card hover: lift slightly, subtle shadow change.
- Press scale: subtle 0.95-0.98 scale on press, restore on release.
- Mobile: rely on `:active`; the browser handles touch feedback.

```css
.btn {
  transition: transform 150ms ease-out, background 150ms ease-out;
}
.btn:hover { transform: translateY(-1px); background: var(--primary-hover); }
.btn:active { transform: translateY(0) scale(0.98); transition-duration: 50ms; }

.card {
  transition: transform 200ms ease-out, box-shadow 200ms ease-out;
}
.card:hover { transform: translateY(-2px); box-shadow: var(--shadow-elevated); }
```

## Modal / Sheet Entrance

### Open-from-closed (`@starting-style`)

Animating an element toggling `display: none` to `display: block` (the natural state for popovers, dialogs, toasts) is now native via three features together, the canonical way to animate open from closed:

- `@starting-style { ... }` declares the styles the element takes the moment it enters the DOM (or leaves `display: none`); the browser interpolates from there to the active styles.
- `transition-behavior: allow-discrete` lets the browser animate discrete properties (including `display`) over the duration instead of snapping at the boundaries.
- `transition: display ...` combined with the two above makes `display: none` to `display: block` actually play.

```css
[popover] {
  opacity: 0;
  transform: scale(0.96);
  transition:
    opacity 200ms ease-out,
    transform 200ms ease-out,
    display 200ms allow-discrete,
    overlay 200ms allow-discrete;
}

[popover]:popover-open {
  opacity: 1;
  transform: scale(1);
}

@starting-style {
  [popover]:popover-open {
    opacity: 0;
    transform: scale(0.96);
  }
}
```

Baseline-modern (Chrome, Safari, Edge; Firefox shipping). Use for popovers, dialogs, dropdowns, toasts, tooltips. Fallback without support is an instant snap that degrades gracefully.

### Modal (centered)

Order: backdrop fades in, then modal fades + scales in 50ms after.

```css
.modal {
  opacity: 0;
  transform: scale(0.95);
  transition: opacity 200ms ease-out, transform 200ms ease-out;
}
.modal.open { opacity: 1; transform: scale(1); }

.modal-backdrop { opacity: 0; transition: opacity 200ms ease-out; }
.modal-backdrop.open { opacity: 1; }
```

### Sheet (slides from edge)

`translateY(100%)` puts it just below the visible area; the cubic-bezier approximates the iOS bottom-sheet overshoot.

```css
.sheet-bottom {
  transform: translateY(100%);
  transition: transform 250ms cubic-bezier(0.32, 0.72, 0, 1);
}
.sheet-bottom.open { transform: translateY(0); }
```

### Dropdown / popover

```css
.popover {
  opacity: 0;
  transform: translateY(-4px) scale(0.97);
  transform-origin: top;
  transition: opacity 150ms ease-out, transform 150ms ease-out;
  pointer-events: none;
}
.popover.open {
  opacity: 1;
  transform: translateY(0) scale(1);
  pointer-events: auto;
}
```

## Toast / Snackbar

Slide in from edge + fade. Auto-dismiss after 3-5s.

```css
.toast {
  transform: translateY(100%);
  opacity: 0;
  transition: transform 300ms cubic-bezier(0.32, 0.72, 0, 1), opacity 200ms ease-out;
}
.toast.show { transform: translateY(0); opacity: 1; }
.toast.hide { transform: translateY(100%); opacity: 0; }
```

## Hero Animations

### PRE-MERGE GATE (hero / above-the-fold)

Any above-the-fold or hero animation ships ONLY with a Lighthouse run on the LCP page showing LCP, TBT, and CLS still inside budget. Visual and accessibility sign-off is NOT performance sign-off. If it cannot hold the budget, make it static or cut it. The default suspect is a per-frame JS-driven hero animation (writing attributes/styles each frame): prefer a CSS keyframe / compositor animation, or static.

### Staggered text rise

Cap at 8-10 items; beyond that the animation feels slow.

```css
.hero-words span {
  display: inline-block;
  opacity: 0;
  transform: translateY(0.5em);
  animation: rise 600ms cubic-bezier(0.2, 0, 0, 1) forwards;
}

.hero-words span:nth-child(1) { animation-delay: 100ms; }
.hero-words span:nth-child(2) { animation-delay: 180ms; }
.hero-words span:nth-child(3) { animation-delay: 260ms; }

@keyframes rise {
  to { opacity: 1; transform: translateY(0); }
}
```

### Image reveal

```css
.hero-image {
  clip-path: inset(0 100% 0 0);
  animation: reveal 800ms cubic-bezier(0.6, 0, 0, 1) 200ms forwards;
}

@keyframes reveal {
  to { clip-path: inset(0 0 0 0); }
}
```

### Subtle parallax on hero

The hero image drifts slowly, capped to a few pixels. Throttle the scroll handler to 16ms (one frame).

```css
.parallax-hero {
  transform: translateY(0);
  transition: transform 100ms linear;
}
```

```js
window.addEventListener('scroll', () => {
  const offset = Math.min(window.scrollY * 0.3, 100);
  document.querySelector('.parallax-hero').style.transform = `translateY(${offset}px)`;
});
```

Anti-pattern: full-page parallax on every section is disorienting and CLS-risky.

## Continuous / Ambient Motion

Subtle ongoing motion gives a page life. Use sparingly.

```css
.ambient-bg {
  background: linear-gradient(120deg, #color1, #color2, #color3);
  background-size: 200% 200%;
  animation: shift 30s ease-in-out infinite;
}
@keyframes shift {
  0%, 100% { background-position: 0% 50%; }
  50% { background-position: 100% 50%; }
}

.floating { animation: float 4s ease-in-out infinite; }
@keyframes float {
  0%, 100% { transform: translateY(0); }
  50% { transform: translateY(-6px); }
}
```

A floating decoration drifts up/down 4-8px continuously. Disable continuous motion when:

- The page is not in the active tab (`document.visibilitychange`), to save battery.
- The user prefers reduced motion.
- The element is off-screen.

```js
const observer = new IntersectionObserver((entries) => {
  entries.forEach((entry) => {
    entry.target.style.animationPlayState = entry.isIntersecting ? 'running' : 'paused';
  });
});
```

## Motion Budget and Measurement

Motion is a budget like JS, CSS, and images: main-thread work per animation and the total sustained fps. Measure, do not guess.

### PRE-MERGE GATE (above-the-fold / hero)

Same gate as Hero Animations, restated as the budget rule: any above-the-fold or hero animation ships ONLY with a Lighthouse run on the LCP page showing LCP, TBT, and CLS still inside budget. Visual and accessibility sign-off is NOT performance sign-off. If it cannot hold the budget, make it static or cut it. A per-frame JS-driven hero animation (writing attributes/styles each frame) is the default suspect; prefer a CSS keyframe / compositor animation, or static.

### Audit, threshold, and fix

| Tool / metric | What it shows | Fix |
|---------------|---------------|-----|
| DevTools Performance > Animations panel | Every running animation; anything not compositor-only (transform, opacity, filter) shows up in red | Move to composited properties |
| Performance panel: Long Animation Frames (LoAF), tasks > 50ms | Locate the animation's stack frames | Cut the long task in that frame |
| Frame budget 16.7ms (60fps) | Any frame past that drops below 60fps; two consecutive long frames are perceptible | Shorten or composite the animation |
| CPU throttle 4x or 6x in Performance settings | Reproduces jank on a fast machine | Record, inspect red long frames, move to a composited property or off-main-thread, re-record until green |
| Target | Every motion holds 60fps on a 4x throttled CPU profile, which means it holds on the median real device | n/a |

### `will-change` hygiene

`will-change` promotes an element to its own compositor layer ahead of an animation, removing the layer-creation pause when it starts. Misused, it inflates GPU memory and slows the page.

- Declare `will-change` ONLY immediately before the animation starts (on hover-intent, the pointer-down beginning a drag, the keypress opening a panel) via class or `style.willChange`.
- Remove it as soon as the animation ends (on `animationend`, `transitionend`, or the WAAPI `Animation.finished` resolve); leaving it on consumes memory and degrades other animations.
- Never apply `will-change: transform` globally to large element classes (e.g., every card); the browser allocates a separate layer for each.
- Animating `transform` and `opacity` (composited) rarely needs `will-change` at all. If a property triggers layout, `will-change` does not save you; rewrite the animation.

## INP and Animation

Composited-only animations (transform, opacity) don't impact INP; layout-triggering and JS-driven animations can. If INP regresses after adding animation:

| Cause | Fix |
|-------|-----|
| Animation triggers layout | Move to transform/opacity |
| JS animation runs every frame | Use CSS animations or `web-animations-api` |
| Animation runs during page load | Defer until after `load` |

See performance.md for INP attribution and budgets.

## CLS and Animation

- Animations that change element size cause layout shift: avoid them.
- If you must animate size, use `transform: scale()` (compositor-only) and accept it is a visual approximation.
- For accordion/disclosure, pre-measure content height and animate `max-height` to that exact value, or use `interpolate-size: allow-keywords` plus `transition: height` to animate to `auto`.

See performance.md for full CLS treatment.

## Common Motion Mistakes

- Using `linear` for UI transitions.
- Animating `width`, `height`, `top`, `left`.
- 800ms+ durations on common UI.
- Animations that block input (modal opening can't be cancelled mid-animation).
- Different durations for opening and closing the same component.
- Decorative animation on every section.
- Parallax that causes CLS.
- Hover-only effects with no touch equivalent.
- `prefers-reduced-motion` ignored.
- Animating during page load on the LCP element.
- Continuous animation on a hero, draining battery.
- Spinner with no `aria-label`.
- Skeleton without `aria-busy`.

## Self-Healing for Motion

Before declaring work complete:

- [ ] All animations use `transform` and/or `opacity` (no layout-triggering)
- [ ] Durations 100-500ms with intentional choice per interaction
- [ ] Easing chosen per direction (`ease-out` for in, `ease-in` for out)
- [ ] Exit faster than entrance (60-70%)
- [ ] `prefers-reduced-motion` removes or shortens non-essential motion
- [ ] No animation blocks user input (modals are interruptible)
- [ ] Loading states have skeleton at 300ms, spinner before, progress for determinate
- [ ] Spinner / skeleton have accessible names / `aria-busy`
- [ ] Stagger capped at 8-10 items
- [ ] Continuous motion paused when off-screen and tab inactive
- [ ] No animation regresses LCP, INP, or CLS
- [ ] PRE-MERGE GATE: every above-the-fold / hero animation has a Lighthouse run on the LCP page showing LCP, TBT, and CLS inside budget; per-frame JS-driven hero animations replaced with CSS keyframe / compositor or made static; if budget cannot hold, motion is cut

## See Also

- [performance.md](performance.md) for animation cost, INP attribution, and CLS budgets
- [accessibility.md](accessibility.md) for reduced motion
- [ui-ux.md](ui-ux.md) for state transitions
- [design.md](design.md) for motion-driven brand expression
