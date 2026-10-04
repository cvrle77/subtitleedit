---
title: Accessibility Mastery
purpose: WCAG 2.2 AA discipline with semantic HTML, ARIA name patterns, keyboard and screen-reader flows, forced-colors mode, live regions, accessible drag-and-drop, and combobox/listbox APG markup. Verification stack is Lighthouse plus axe plus manual keyboard plus screen reader.
load-when:
  task-keywords: [accessibility, a11y, WCAG, ARIA, screen reader, keyboard, focus, contrast, semantic HTML, forced colors, reduced motion, axe]
  symptoms: [focus not visible, contrast fail, aria-hidden leak, inert leak, focus trap leak, duplicate id]
prereq: SKILL.md
related: [forms.md, motion.md, ui-ux.md, responsive.md]
size: ~477 lines
---

# Accessibility Mastery

WCAG 2.2 AA is the working bar, AAA where reasonable. Verification stack: Lighthouse plus axe plus manual keyboard plus screen reader. Framework-agnostic.

## The Four POUR Principles (WCAG)

- Perceivable: information must be presentable in ways users can perceive.
- Operable: interface components must be operable.
- Understandable: information and operation must be understandable.
- Robust: content must be robust enough to be interpreted by current and future user agents including assistive technologies.

## WCAG 2.2 New Success Criteria

The five most-missed of the nine added in WCAG 2.2:

| Criterion | Level | Rule and check |
|-----------|-------|----------------|
| 2.4.11 Focus Not Obscured (Minimum) | AA | A control receiving keyboard focus must not be entirely hidden by author content (sticky header/footer, consent overlay). Fix with `scroll-padding-top` matching sticky header height, or a layout gutter for the banner. Verify by tabbing the page with both a sticky header and a cookie banner visible: the focus ring must stay fully visible at every stop. |
| 2.5.7 Dragging Movements | AA | Every drag interaction needs a single-pointer alternative (button, keyboard shortcut, or tap-to-pick/tap-to-drop). Applies to slider drags, draggable list reorders, kanban moves, map pans. See "Accessible Drag-and-Drop" below. |
| 3.2.6 Consistent Help | A | A help mechanism (contact link, chat, FAQ search) appears in the same relative order on every page where it appears. |
| 3.3.7 Redundant Entry | A | Info the user already entered in the same session must be auto-filled or re-pulled (no retyping address across shipping and billing). Exceptions: re-entry is essential, the info changed, or the previous answer is no longer valid. |
| 3.3.8 Accessible Authentication (Minimum) | AA | No cognitive function test (memorising a password, recognising specific characters, transcribing a CAPTCHA) without an alternative. Passkeys, OAuth, magic links, email/SMS codes, and password-manager autofill all qualify. See auth.md. |

## Forced-Colors Mode

Forced-colors environments override author colors with a system palette, exposed via the `forced-colors` media query.

- Use system-color keywords for any shipped color: `Canvas` (page background), `CanvasText` (body text), `LinkText` (links), `Highlight` (selected text background), `HighlightText` (selected text foreground), `ButtonText`, `ButtonFace`, `GrayText` (disabled text).
- `forced-color-adjust: none` opts an element out of the system palette. Use only for genuinely meaningful colors (brand logo, chart legend swatch, status indicator) with verified contrast against `Canvas`. Default to `auto` everywhere else.
- Fix an SVG icon that flattens to currentColor: set `fill: currentColor` plus `forced-color-adjust: auto`.
- Fix a custom focus ring invisible in forced-colors: add a forced-colors fallback that uses `Highlight` (`outline: 2px solid Highlight; outline-offset: 2px`).

```css
@media (forced-colors: active) {
  .focus-ring:focus-visible {
    outline: 2px solid Highlight;
    outline-offset: 2px;
  }
  .icon { fill: CanvasText; }
  .icon-link { fill: LinkText; }
}
```

Verify in a Chromium browser via DevTools Rendering, Emulate CSS media feature, `forced-colors: active`.

## Semantic HTML First

ARIA is the polyfill, semantics are the standard. The native element gives keyboard, focus, semantics, and screen-reader support automatically.

| Use this | Not this |
|----------|----------|
| `<button>` | `<div onclick>` |
| `<a href>` | `<span onclick>` (for navigation) |
| `<nav>` | `<div class="nav">` |
| `<main>` | `<div id="main">` |
| `<header>` / `<footer>` | `<div class="header">` |
| `<section>` with heading | bare `<div>` |
| `<article>` for self-contained content | `<div>` |
| `<aside>` for tangentially related | `<div class="sidebar">` |
| `<form>` with `<label>` | `<div>` with floating placeholder |
| `<input type="email">` | `<input type="text" pattern>` |
| `<details>`/`<summary>` | div + click handler |
| `<dialog>` (HTMLDialogElement API) | div with role="dialog" + custom focus trap |
| `<table>` for tabular data | div grid |
| `<ul>`/`<ol>` for lists | div with line breaks |
| `<time datetime>` | `<span>` |
| `<address>` for contact info | `<div>` |

## ARIA Rules

The five rules:

1. If you can use a native element, use the native element.
2. Don't change native semantics unless necessary (`<button role="link">` is suspicious).
3. All interactive ARIA elements must be keyboard-accessible.
4. Don't use `role="presentation"` or `aria-hidden="true"` on a focusable element.
5. All interactive elements must have an accessible name.

### Accessible name source per pattern

| Pattern | Accessible name |
|---------|----------------|
| Button with text | The text content |
| Icon-only button | `aria-label="..."` |
| Link with text | The text content |
| Link with image only | `<img alt="...">` or `aria-label` on the link |
| Input | `<label for="id">` or `aria-labelledby="id"` or (last resort) `aria-label` |
| Disclosure | `aria-expanded="true"`/`"false"` on the trigger |
| Tab | `role="tab"`, `aria-selected`, `aria-controls` referencing the panel |
| Modal | `role="dialog"`, `aria-modal="true"`, `aria-labelledby` referencing the title |
| Tooltip | `aria-describedby` on the trigger pointing to the tooltip |
| Live region | `aria-live="polite"` (or `assertive` for urgent), `role="status"` for non-interrupting |
| Alert | `role="alert"` (implies `aria-live="assertive"`) |
| Listbox | `role="listbox"`, `aria-activedescendant` for the current option |
| Combobox | `role="combobox"`, `aria-expanded`, `aria-controls`, `aria-autocomplete` |
| Meaningful inline SVG (chart, diagram, informative icon) | `role="img"` PLUS a name (a `<title>` element or `aria-label`) |
| Decorative inline SVG | `aria-hidden="true"` |

Inline-SVG trap: a meaningful inline SVG needs `role="img"` AND an accessible name; a decorative one needs `aria-hidden="true"`. Adding `role="img"` WITHOUT a name is worse than hiding it: it exposes a nameless image to assistive tech and can trip image-alt, dropping both Accessibility and SEO. Check: every inline SVG is either `aria-hidden="true"` or `role="img"` with a name; no inline SVG has a role and no name.

### Combobox and Listbox (ARIA APG 1.2)

APG 1.2 recognises three valid combobox markups. Pick one and apply it end-to-end; do not blend.

1. Editable text input + popup listbox: `<input role="combobox" aria-expanded aria-controls="listbox-id" aria-activedescendant="option-id">` paired with a sibling `<ul role="listbox" id="listbox-id">`. The popup may be a listbox, grid, tree, or dialog, declared via the `aria-controls` target's role.
2. Editable text input + inline popup (chips, badges): same `role="combobox"` on the input, but the popup is inline rather than overlay.
3. Without a textbox (select replacement): `<button role="combobox" aria-expanded aria-controls aria-haspopup="listbox">` plus the listbox. Use when the user picks from a fixed list and does not type.

`aria-autocomplete` declares the behaviour:

| Value | Behaviour |
|-------|-----------|
| `none` | No autocomplete; the popup may still suggest. |
| `list` | The popup filters to matches as the user types. |
| `both` | The popup filters AND the input value is autocompleted inline (the unmatched suffix is selected so the next keystroke replaces it). |
| `inline` | The input value is autocompleted inline, no popup filtering. |

Keyboard model, pick one:

- Active-descendant (preferred for combobox): DOM focus stays on the input; the active option is marked via `aria-activedescendant="option-id"` on the input; arrow keys move it; Enter commits its value; the SR announces the active descendant.
- Focus-roving: DOM focus moves to the active option inside the listbox and the input loses focus. Older pattern that breaks the combobox model because the input no longer receives keystrokes.

Common defects: `aria-expanded` not flipping when the popup opens; `aria-activedescendant` pointing to a changed or nonexistent id; `role="combobox"` on the wrapping `<div>` instead of the input or button.

### Label vs labelledby vs describedby

- `aria-label`: a short string. Use only when no visible text exists.
- `aria-labelledby`: references existing on-page text by id. Preferred over `aria-label` because it stays in sync with visible UI.
- `aria-describedby`: supplemental description (helper text, error message). Announced after the label.

## Color and Contrast

### Targets

| Element | AA minimum | AAA |
|---------|-----------|-----|
| Body text (< 18px regular or < 14px bold) | 4.5:1 | 7:1 |
| Large text (>= 18px regular or >= 14px bold) | 3:1 | 4.5:1 |
| UI components and graphical objects (meaningful icons, focus rings, form borders) | 3:1 | n/a |

### Verification

- Use the browser DevTools color picker to see the live contrast ratio for the current foreground vs computed background.
- Test in light AND dark mode independently; inverting a palette rarely preserves contrast.
- Test with `prefers-contrast: more` if your design system supports it.

### Traps

- `text-slate-400` on a white background fails 4.5:1.
- `text-slate-400` on `bg-slate-950` passes; on `bg-slate-900` it is marginal.
- Placeholder text (`color: gray`) often fails. Treat placeholders as decorative; never put critical info there.
- Disabled state contrast does not need to meet 4.5:1 but should still be perceivable.
- Brand colors over photographic backgrounds need a scrim or darkening overlay.

### Color is never the only signal

Pair color with text, icon, or pattern:

- Required field: red asterisk + the word "required" in helper text.
- Error: red border + error icon + error message.
- Status: colored dot + status word.
- Chart series: color + pattern/dash + label.

## Keyboard

Every interactive element must be:

- Reachable by Tab in a logical order.
- Operable by Enter, Space, or arrow keys per the WAI-ARIA Authoring Practices.
- Visible when focused, with a focus ring at 3:1 contrast against the surface.

### Tab order

- Use natural document order; avoid `tabindex` > 0 (it overrides natural order and creates surprising flows).
- `tabindex="0"` makes a non-focusable element focusable.
- `tabindex="-1"` makes an element programmatically focusable (for moving focus into a modal) but not in tab order.

### Keys per pattern

| Pattern | Keys |
|---------|------|
| Button | Enter or Space |
| Link | Enter |
| Checkbox | Space |
| Radio group | Arrow keys move within group, Tab moves out |
| Tabs | Arrow keys move between tabs (if focus follows), Tab moves to panel |
| Combobox | Arrow keys, Enter to select, Esc to close |
| Menu / menubar | Arrow keys, Enter to activate, Esc to close |
| Modal | Esc to close, Tab loops within, focus restored on close |
| Slider | Arrow keys for fine, Page Up/Down for coarse, Home/End for min/max |
| Tree | Arrow keys, Enter/Space to activate, Right to expand, Left to collapse |

### Focus management

- Modal open: move focus to the modal (close button or first form field), save the previously-focused element, restore focus to it on close.
- SPA route change: move focus to the main heading or to a `<main tabindex="-1">` so screen readers announce the new page.
- Form errors: move focus to the first invalid field (or to a top summary with anchor links to each error).
- Form success: move focus to the success message and announce it via `role="status"`.

### Focus visibility

- Never `outline: none` without a replacement.
- Use `:focus-visible` (not `:focus`) so mouse users don't see a ring when clicking but keyboard users always do.
- Focus ring: 2-4px, contrast 3:1 against both the surface and the resting state of the element.

## Screen Reader

Test on at least one combination:

- VoiceOver on macOS/iOS (Cmd+F5 to toggle).
- NVDA on Windows (free).
- TalkBack on Android.
- JAWS on Windows (paid; only test if your audience needs it).

Verify:

- Page structure announced correctly: headings, landmarks (`main`, `nav`, `aside`, `footer`), section labels.
- Reading order matches visual order top-to-bottom.
- Interactive controls have names and roles announced ("Submit, button" not "Submit, clickable element").
- State changes are announced (adding an item to a cart announces the new state via live region or focus move).
- Errors are announced via `role="alert"` or `aria-live="assertive"` for blocking errors, `aria-live="polite"` for advisories.

### aria-live regions

- `aria-live="polite"` for non-blocking updates (toast, sync status, autosave); announced when the SR finishes its current utterance.
- `aria-live="assertive"` for blocking errors only; interrupts the SR.
- `role="status"` is `aria-live="polite"` + `aria-atomic="true"`.
- `role="alert"` is `aria-live="assertive"` + `aria-atomic="true"`.
- The element must be present in the DOM at page load for SRs to monitor it; inserting it later sometimes fails.

### Live-region timing

- Debounce rapid updates: announce at most once per 250 ms. Wrap faster-changing values (search results count, slider value, character count) in a debouncer that coalesces intermediate states and flushes the final value.
- `aria-atomic="false"` for diff reads on long status text where only a small portion changes (counter inside a paragraph, timestamp at end of sentence). Default `aria-atomic="true"` is correct only for short status strings.
- `aria-relevant="additions text"` (default) announces inserted nodes and text changes; `"all"` adds removals (chat "user left"); `"additions"` alone suppresses text-only edits.
- One region per concern: two competing assertive regions race and the SR drops the loser. Use one assertive region for blocking errors, one polite region for status.

## Heading Hierarchy

- One `<h1>` per page (the page's primary intent).
- Sequential descent H1 -> H2 -> H3; never skip a level.
- Headings describe sections; not used for typography.
- Section landmarks should be labeled by their heading: `<section aria-labelledby="hero-title"><h2 id="hero-title">...</h2></section>`.

## Forms

Every input has a programmatic label:

```html
<!-- Best -->
<label for="email">Email</label>
<input id="email" type="email" autocomplete="email" required />

<!-- Acceptable when wrapping is impractical -->
<label>Email <input type="email" /></label>

<!-- Last resort -->
<input type="email" aria-label="Email" />
```

`placeholder` is not a label; placeholders disappear when the user types and have low contrast by default.

Input attributes:

- `type` matching the data (`email`, `tel`, `url`, `number`, `search`) affects mobile keyboards and validation.
- `autocomplete` per the WHATWG list (`given-name`, `email`, `street-address`, `cc-number`, `one-time-code`, etc.) for mobile UX and password managers.
- `inputmode` overrides the keyboard without changing semantics: `numeric` for codes, `decimal` for prices.
- `required`, `min`, `max`, `pattern` for native validation.
- `aria-invalid="true"` when invalid; `aria-describedby` pointing to the error message.

Error handling:

- Show errors inline, near the field.
- Use `role="alert"` or `aria-live="polite"` so SRs announce them.
- The error message must say cause AND fix ("Email is required" is OK; "Enter your work email" is better).
- For multi-error submission show a summary at the top with anchor links to each invalid field.
- After a submit error move focus to the first invalid field.

See forms.md for the deep dive.

## Images and Media

### Alt text

- Decorative image: `alt=""` (empty string is required; missing alt is worse).
- Functional image (icon button): describe the action not the image, e.g. `<button><img alt="Close" src="x.svg" /></button>`.
- Informational image: describe what conveys information; concise, prefer 8-12 words.
- Complex image (chart, diagram): short alt + long description nearby (`figure` + `figcaption` or `aria-describedby` to a hidden description).
- Logo image: `alt="Brand"` not `alt="Brand logo"` (the word "logo" is redundant).
- CSS background images should be decorative only; anything informational should be an `<img>`.

### Inline SVG (decorative vs meaningful)

- Meaningful inline SVG (chart, diagram, informative icon): `role="img"` PLUS an accessible name (a `<title>` element or `aria-label`).
- Decorative inline SVG: `aria-hidden="true"`.
- Never add `role="img"` without a name: it exposes a nameless image to assistive tech, can trip image-alt, and drops both Accessibility and SEO. Worse than hiding it.
- Check: every inline SVG is either `aria-hidden="true"` or `role="img"` with a name; no inline SVG has a role and no name.

### Video

- Provide captions (`<track kind="captions">`) for any spoken content.
- Provide a transcript for long-form video.
- Provide audio description for video where visual information is critical.
- Don't autoplay video with sound.
- Don't autoplay-loop a hero video without a pause control.

### Audio

- Provide a transcript.
- Don't autoplay audio.

## Motion and Reduced Motion

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

This is the floor. Better practice: actually remove non-essential animation rather than just shortening it. For essential motion (loading spinner, video playback) keep it, but consider whether a static skeleton suffices or whether the user can pause. See motion.md.

## Dynamic Type / Zoom

- Use relative units (`rem`, `em`, `%`) for font size, line height, padding; avoid `px` for type.
- Test at 200% browser zoom: layout should remain usable with no horizontal scroll and no clipped content.
- Test with `font-size: 24px` set on `<html>`: components should scale.
- Don't disable user zoom; `user-scalable=no` and `maximum-scale=1` are forbidden.

## Touch and Hit Targets

- Minimum touch target 44x44 CSS pixels (iOS HIG) or 48x48 dp (Material Design).
- Minimum 8 px spacing between adjacent targets.
- For small icons, expand the hit area with padding or `::before` extension while keeping the visual size small.
- Apply the 44px (or 48px) minimum to standalone controls: buttons, toggles, menu triggers, icon buttons, form controls, CTA links.
- WCAG 2.5.8 (target size, AA, 24px): inline sentence links, breadcrumb trails, and footer text-link lists are exempt from the larger target size. Give them a small comfortable hit area (a little vertical padding); do not inflate to 44px tall (a 44px breadcrumb row or in-prose link reads as broken).

## Language

- `<html lang="en">` (or your locale, e.g. `lang="en-US"`, `lang="ja"`).
- `lang` on inline elements when language changes mid-content: `<span lang="fr">déjà vu</span>`.
- `dir="rtl"` on `<html>` for right-to-left languages, with logical properties (`margin-inline-start`) so layout adapts. See i18n.md.

## Lang, Main, and Skip Link

Three structural basics every route must satisfy (checked in the multi-page audit, see audit-workflow.md):

- [ ] `<html lang>` is present and valid (e.g. `lang="en"`, `lang="en-US"`, `lang="ja"`).
- [ ] Exactly one primary `<main>` per route; nested `<main>` elements break landmark navigation.
- [ ] A skip link is the first focusable element on the page, hidden until focused, jumps to `<main>`, and does not create viewport overflow while hidden.

The skip-link no-overflow requirement is the most-skipped: the `position: absolute; left: -9999px` pattern works only when paired with a focus state that brings the link back on-screen without growing the page. Use the `.sr-only-focusable` pattern below.

Progressive enhancement: a JS-driven mobile menu must leave its links reachable without JavaScript (a `<noscript>` fallback nav or static links that enhance). See responsive.md.

## Skip Links

```html
<a href="#main" class="sr-only-focusable">Skip to main content</a>
...
<main id="main" tabindex="-1">...</main>
```

```css
.sr-only-focusable {
  position: absolute;
  left: -9999px;
}
.sr-only-focusable:focus {
  position: fixed;
  top: 1rem;
  left: 1rem;
  z-index: 1000;
  padding: 0.5rem 1rem;
  background: white;
  color: black;
  border: 2px solid currentColor;
}
```

## Modal and Dialog

### Native dialog (preferred)

```html
<dialog id="confirm">
  <h2>Confirm action</h2>
  <p>Are you sure?</p>
  <form method="dialog">
    <button value="cancel">Cancel</button>
    <button value="confirm">Confirm</button>
  </form>
</dialog>
<button onclick="document.getElementById('confirm').showModal()">Open</button>
```

`<dialog>` with `showModal()` provides auto focus management (focus moves into the dialog), Esc to close, a stylable backdrop (`::backdrop`), and inert background (modal pattern).

### Custom modal

If you must build one (rich content, framework-managed):

- `role="dialog"` (or `alertdialog` for blocking errors), `aria-modal="true"`.
- `aria-labelledby` pointing to the title.
- Focus trap: Tab and Shift+Tab loop within the modal.
- Esc closes.
- On open, focus moves to the modal (close button or first field).
- On close, focus returns to the trigger.
- Background is `inert` (or has `aria-hidden="true"` and `tabindex="-1"` plus `pointer-events: none`).

## Accessible Drag-and-Drop

WCAG 2.5.7 requires every drag interaction to have a single-pointer alternative. The canonical keyboard pattern for a reorderable list (or kanban, or sortable grid) is pick up, move, drop, driven by Enter, arrows, and Esc.

- Make each item a button or `tabindex="0"` listitem so it receives keyboard focus directly; a draggable `<div>` with no role is not reachable.
- Enter (or Space) on a focused item enters grab mode: set `aria-grabbed="true"` (deprecated in ARIA 1.2 but still announced by some SRs) or, preferred, push to a live region: "Picked up Item A. Use arrow keys to move, Enter to drop, Esc to cancel."
- Arrow keys move the item up/down within the list (or across kanban columns); each move announces the new position via the live region: "Item A, moved to position 3 of 7."
- Enter (or Space) again drops the item at its current position: "Dropped Item A at position 3."
- Esc cancels the drag and restores the original position: "Cancelled. Item A returned to position 1."
- The pointer drag interaction stays available for users who prefer it; both paths land at the same end-state mutation.
- Touch alternative for touch-only users without a keyboard: long-press to grab, tap to drop, or a per-row "Move" button opening a dialog with arrow buttons.

The live region drives the entire experience for SR users; a drag-and-drop without that region is functionally inaccessible even if the keyboard works.

## Common Accessibility Mistakes

- `<div onClick>` instead of `<button>`: loses keyboard, focus, semantics.
- `<a href="#">` for buttons: use `<button>`.
- `placeholder` as label: disappears when typing.
- `aria-label` on a button that already has visible text: overrides the visible text.
- `aria-hidden="true"` on a focusable element: creates a "ghost" focus.
- `outline: none` without a replacement: removes keyboard focus indication.
- Custom dropdown without arrow-key support.
- Modal without focus management.
- Toast that announces every minor event with `aria-live="assertive"`: use `polite`.
- Loading spinner without an accessible name: add `aria-label="Loading"` on the spinner element.
- Drag-and-drop without a keyboard alternative.

## Self-Healing for Accessibility

After any change, verify:

- [ ] Lighthouse Accessibility = 100
- [ ] axe DevTools shows zero violations
- [ ] Tab through the full page; everything reachable, no traps
- [ ] Esc closes any open modal/menu
- [ ] Screen reader pass on the primary user flow (VO or NVDA)
- [ ] Light AND dark mode contrast checked separately
- [ ] 200% browser zoom: layout intact, no horizontal scroll
- [ ] `prefers-reduced-motion` respected
- [ ] Forms: every input labeled, errors announced, focus moves to first invalid field on submit
- [ ] Modals: focus trapped, Esc closes, focus restored on close
- [ ] Images: alt text present, decorative images have `alt=""`
- [ ] Headings: one H1, sequential, no skipped levels
- [ ] Color is never the only signal

## See Also

- [forms.md](forms.md) for form-specific accessibility
- [motion.md](motion.md) for reduced-motion implementation
- [ui-ux.md](ui-ux.md) for state and interaction patterns
- [responsive.md](responsive.md) for the JS-free mobile nav requirement
