---
title: UI/UX Principles
purpose: Framework-agnostic interaction patterns with concrete thresholds for state coverage, hit targets, modals, popovers, tooltips, navigation, loading, undo, and notification timing. The functional companion to design.md.
load-when:
  task-keywords: [UI, UX, interaction, modal, popover, dialog, drawer, menu, tooltip, toast, touch target, navigation]
  symptoms: [popover not dismissing, scroll lock side shift, focus trap leak, focus not visible, inert leak, rubber-band scroll]
prereq: SKILL.md
related: [design.md, accessibility.md, forms.md, motion.md]
size: ~600 lines
---

# UI/UX Principles

Framework-agnostic patterns for interfaces that feel professional, predictable, and considered.

## The Hierarchy of UX Concerns

When a UI is "off" but you cannot say why, walk this list top-down. The first failing rung is the problem.

| Step | Question | Checks |
|------|----------|--------|
| 1 | Can the user perceive it? | contrast, size, position, language |
| 2 | Can the user understand it? | vocabulary, hierarchy, affordance |
| 3 | Can the user operate it? | touch target, keyboard, focus, gesture, feedback |
| 4 | Does the user trust it? | consistency, predictability, recovery, transparency |
| 5 | Does it feel good? | polish, motion, sound, haptics, density |

Steps 1 to 3 are correctness, step 4 is reliability, step 5 is craft.

## State Coverage (the most-skipped quality bar)

Every screen and component needs an intentional design for each state. A feature shipped with only the happy path is half-built.

| State | When | What to show |
|-------|------|--------------|
| Empty | No data yet | Specific, helpful message plus a primary action to fix it. Never a blank space. |
| Loading | Async fetch in progress | Skeleton for >300ms loads, spinner for shorter. Match the eventual layout. |
| Success | Action completed | Brief confirmation (toast, inline check, color flash). Move on quickly. |
| Error | Action failed | Cause plus how to fix plus recovery action. Never just "Something went wrong". |
| Partial | Some data, some loading | Render what you have, skeleton the rest. |
| Disabled | Action not available | 40-50% opacity, cursor not-allowed, programmatically disabled, optional explanation. |
| Read-only | Visible, not editable | Visually distinct from BOTH editable AND disabled. |
| Stale | Data older than fresh | Indicate freshness (timestamp, "updated 5m ago"), offer refresh. |
| Offline | No network | Disable network-dependent actions, show offline indicator, queue mutations if possible. |
| Unauthorized | User lacks permission | Explain why, link to upgrade or request access. Never silently disable. |
| Limit reached | Quota exhausted | Explain the limit, show progress, link to upgrade. |
| Initial / first-run | New user | Reduced surface, onboarding hints, sample data. |
| Done, no further action | Complete | Brief celebration, clear next step. |

### Cross-page consistency

A shared component (cards, CTAs, headers, forms, modals) is done only when every state behaves the same on every route that hosts it: match the canonical contract, not the page. When a state differs across two routes the cause is page-local CSS, duplicated markup, or a near-duplicate component. Fix the contract. Extraction and drift detection: see components.md. Cross-page audit procedure: see audit-workflow.md.

### Mobile drawer scroll lock

A mobile drawer (slide-out nav, bottom sheet, full-screen menu) must lock body scroll while open (`overflow: hidden` on the document root) and restore the prior scroll position via `window.scrollTo` on close. Drawer scroll-lock failure is a standard geometry-sweep check: see defects.md.

## Touch and Pointer Interaction

### Hit targets

| Platform | Minimum |
|----------|---------|
| iOS / Web on touch | 44x44 CSS pixels (WCAG 2.5.5 Target Size Enhanced, Level AAA, from WCAG 2.1) |
| Android | 48x48 dp |
| Web on pointer | 24x24 CSS pixels minimum (WCAG 2.5.8 Target Size Minimum, Level AA, added in WCAG 2.2); 44x44 strongly recommended |

Expand the hit area on icon-only buttons that look smaller:

```css
.icon-btn {
  width: 24px;
  height: 24px;
  padding: 10px; /* total 44x44 hit area */
  background-clip: content-box;
}
```

Apply the 44px (or 48px) minimum to STANDALONE controls: buttons, toggles, menu triggers, icon buttons, form controls, CTA links. Inline text links, breadcrumb trails, and footer text-link lists are exempt under WCAG 2.5.8 (AA, 24px) because they flow with text. Give them only small vertical padding, not 44px height (a 44px in-prose link reads as broken).

### Spacing between targets

Minimum 8px gap between adjacent interactive targets to prevent mistaps and give focus rings room.

### Hover, press, focus, active

| State | When | Visual change |
|-------|------|--------------|
| Resting | No interaction | The base style |
| Hover | Pointer over, not touch | Subtle elevation, color shift, or underline |
| Focus | Keyboard or programmatic | Visible ring, 2-4px, contrast 3:1 against surface |
| Active / pressed | Mouse down or touch | Slight inset/scale (0.97 transform), darker background, depressed shadow |
| Disabled | Action unavailable | 40-50% opacity, cursor not-allowed, no hover/active response |

Touch devices skip hover. Do not put critical info in a hover-only tooltip.

### Cursor

- `cursor: pointer` on every clickable element. Anti-pattern: a `<button>` with default cursor.
- `cursor: text` on text inputs (native, do not override).
- `cursor: not-allowed` on disabled.
- `cursor: grab` / `grabbing` on draggables.
- `cursor: zoom-in` / `zoom-out` for image zoom.
- `cursor: help` for elements with a `title` or tooltip.

### Press feedback timing

Visual response within 80-150ms of press. Below 80ms feels disconnected, above 200ms feels laggy.

Suppress the grey tap-highlight flash globally, but only when a proper `:focus-visible` ring exists:

```css
:where(a, button, summary, [role="button"], [tabindex]) {
  -webkit-tap-highlight-color: transparent;
}
```

### Click vs tap vs hover

- Click/tap: primary actions only.
- Hover: never the only path to an action. Must also be reachable on touch (long-press, kebab menu, or always-visible).
- Long-press: secondary action on touch. Always provide a non-gesture alternative.
- Right-click: context menu only. Always provide an equivalent button or kebab menu.

### Gesture conflicts on scroll containers

A horizontal carousel inside a vertically scrolling page, a pinch-zoom canvas, or a swipeable stack all sit on a scrolling document. Allocate gestures explicitly or the browser fights your handlers.

- `touch-action: pan-y` on a horizontal swiper lets the page own vertical scroll while your code owns horizontal. `touch-action: none` disables browser gestures entirely (use only on canvas / pan-zoom surfaces).
- `overscroll-behavior: contain` on scrolling regions stops scroll chaining into the parent (the "scrolled the modal, then the body scrolled too" bug).
- `overscroll-behavior: none` on `<html>`/`<body>` suppresses pull-to-refresh and the iOS rubber-band bounce for app-shell layouts. Do not apply globally on content sites; users rely on the bounce as feedback.
- Test: scroll a horizontal reel near the top of a long mobile page. Pull-to-refresh must not fire, vertical scroll must continue past the reel, and the reel must not steal vertical drag.

## Visual Hierarchy

Hierarchy tells the user where to look and in what order. Build it from these tools, in priority order:

| # | Tool | Rule |
|---|------|------|
| 1 | Size | Bigger = more important |
| 2 | Weight | Bolder = more important |
| 3 | Contrast | Higher contrast against background = more important |
| 4 | Position | Top and left (in LTR) read first |
| 5 | Color | Warm and saturated draws the eye, muted recedes |
| 6 | Whitespace | Isolation makes things feel important |
| 7 | Motion | Anything that moves draws attention |

Anti-patterns:

- Five things shouting at once.
- Equal weight on everything.
- Color alone for hierarchy. Fails for colorblind users and in monochrome contexts.
- Borders everywhere. Borders compete with content.

### The "primary action" rule

Each screen has exactly one primary CTA. Secondary actions are visually subordinate (outline, ghost, text-only). Tertiary are quieter still (link or icon only). Destructive actions are visually separated AND use a danger color, but are not primary unless the screen is built for that destructive purpose.

### Scan patterns

- Sparse, hero-driven layouts: Z-pattern (top-left to top-right to diagonal to bottom-right). Brand top-left, primary nav top-right, primary CTA bottom-right of the hero.
- Text-heavy layouts: F-pattern (left-edge dominant scan). Important info on the leading edge, sub-headings every 200-400px as scan anchors.

## Density

Density is the ratio of information to space. Match it to the user's task. Mixing densities within one surface feels wrong.

| Density | Surface | Rules |
|---------|---------|-------|
| Low | Hero-driven, single-focus | Generous whitespace, large type, one focus per section |
| Medium | Balanced application screens, mid-length content | Fits the majority of routine UI |
| High | Data tables, code editors, professional tools | Tight spacing, smaller type but still >= 14px, maximum information per pixel |

- Within one density mode, be consistent.
- Provide a "compact / cozy / comfortable" toggle for data-heavy apps.
- Never sacrifice contrast or hit target for density.

## Layout and Spacing

Use a multiplicative scale, never arbitrary values. Pick one and never deviate.

- 4-pt scale: 0, 4, 8, 12, 16, 24, 32, 48, 64, 96.
- 8-pt scale: 0, 8, 16, 24, 32, 48, 64 (a coarser variant, with halves at 4).

Anti-pattern: padding values like 7, 13, 17, 22. They feel random and break rhythm.

### Component spacing tiers

| Tier | Value | Use |
|------|-------|-----|
| Inline | 4-8 | Inside a tag/badge/icon-text pair |
| Tight | 8-12 | Inside a button, input, or card |
| Comfortable | 16 | Between siblings inside a card |
| Section | 24-32 | Between cards or rows in a list |
| Block | 48-64 | Between major sections of a page |
| Page | 96-128 | Around the page edge on large viewports |

Gestalt proximity: things close together read as related. If related items look unrelated, reduce the space; if unrelated items look unified, increase it.

## Typography in UI

See design.md for the type aesthetic. These are the functional rules.

| Use | Mobile | Desktop |
|-----|--------|---------|
| Body | 16px | 16px |
| Body small / caption | 14px | 14px |
| Body large / lead | 18-20px | 18-20px |
| H6 | 16px | 16px |
| H5 | 18px | 18-20px |
| H4 | 20-22px | 22-24px |
| H3 | 22-26px | 24-30px |
| H2 | 28-32px | 32-40px |
| H1 | 32-40px | 40-64px |
| Display (hero) | 40-56px | 56-96px |

Body text below 16px on mobile triggers iOS auto-zoom on focus. Avoid.

- Line height: body 1.5-1.75, headings 1.1-1.3 (tighter), buttons 1 (single line) or 1.2.
- Line length: optimal 60-75 characters, mobile 35-60, desktop cap at 75. Use `max-width: 65ch` (or similar) on long-form content.
- Letter spacing: body default, all-caps eyebrow text +0.05em to +0.1em, display headings -0.01em to -0.02em (slight tighten).

## Color in UI

See design.md for palette construction. This is the semantic system.

Never use raw hex in components. Use semantic tokens, mapped for both light and dark mode:

- `--color-background` (page background)
- `--color-surface` (card / elevated surface)
- `--color-surface-muted` (quieter surface)
- `--color-foreground` (primary text)
- `--color-foreground-muted` (secondary text)
- `--color-foreground-subtle` (tertiary text, hints)
- `--color-border` (resting border)
- `--color-border-strong` (border on focus or emphasis)
- `--color-primary` (brand action)
- `--color-primary-foreground` (text on primary)
- `--color-success`, `--color-warning`, `--color-danger`, `--color-info`
- `--color-focus-ring` (focus indicator)

### Dark mode

- Do not invert. Design dark mode independently.
- Use desaturated, lighter tonal variants of the brand color.
- Surfaces are typically `#0c0c10` or `#111`, not pure black (pure black on OLED creates harsh contrast).
- Borders are typically lighter than the surface, e.g. `rgba(255,255,255,0.08-0.12)`.
- Test contrast in dark mode separately. The 4.5:1 ratios are different.

## Iconography

- One icon set per product. Do not mix sets.
- SVG only. Never emoji as structural icons, never PNG.
- Variable icons (mini/outline/solid families) are fine across hierarchy levels but must be consistent within a level.
- Outline icons: 1.5px or 2px stroke, pick one and stay. Filled icons have no stroke. Do not mix outline and filled in the same hierarchy level.
- Match icon color to surrounding text or use a token (`--color-foreground-muted`). Use semantic color tokens for status, and always pair status icons with a text label (color alone is not enough).
- Alignment: in buttons to the text baseline, in inputs to the input's vertical center, in lists to the same x as adjacent rows.

| Size | Token | Use |
|------|-------|-----|
| 16px | xs | Inline with body text, tight UI |
| 20px | sm | Default in buttons and inputs |
| 24px | md | Nav items, section headers |
| 32px+ | lg | Hero / feature illustrations |

## Buttons

| Level | Visual | Use |
|-------|--------|-----|
| Primary | Filled, brand color | One per screen. The primary action. |
| Secondary | Outlined or quiet filled | Alternative paths. |
| Tertiary | Text-only or ghost | Low-stakes actions. |
| Destructive primary | Filled, danger color | Only on screens built for the destructive action (delete confirmation). |
| Destructive secondary | Outlined, danger color | Listed alongside other actions. |
| Icon-only | Icon, no text | Compact UI. Always include `aria-label`. |

Anatomy:

- Minimum 44x44 touch area.
- Padding: 12-16 horizontal / 8-12 vertical (small); 16-24 horizontal / 10-12 vertical (medium); 24-32 horizontal / 12-16 vertical (large).
- Icon spacing from text: 8px.
- Loading: replace icon with a spinner OR add a spinner before/after text, disable the button, and maintain width to prevent layout shift.
- Disabled: 40-50% opacity, cursor not-allowed, programmatically disabled.

## Forms (functional principles)

Full treatment: see forms.md.

- Visible label for every input. Placeholder is not a label.
- Error inline, near the field, with cause plus fix.
- Validate on blur, not on every keystroke.
- After a submit error, focus the first invalid field.
- Mark required fields (asterisk plus `aria-required="true"`).
- Use the right `type` and `autocomplete` attributes.
- Group related fields with `<fieldset>` plus `<legend>`.
- Long forms autosave drafts. Multi-step forms show progress.

## Modals and Overlays

### Modal

- Reserved for blocking decisions (confirmation, login, critical data entry).
- Always escapable: Esc, backdrop click, close button.
- Focus trapped inside, restored on close.
- Background `inert` or `aria-hidden`.
- Backdrop scrim 40-60% black for legibility.
- Animate from trigger when possible (scale plus fade).

Scroll-lock without a sideways jump: setting `overflow: hidden` on the body hides the scrollbar and widens the page, shifting fixed and centered content sideways. Reserve the width:

```js
const gap = window.innerWidth - document.documentElement.clientWidth;
document.body.style.paddingRight = gap + "px";
// restore overflow and paddingRight on close
```

The gap is 0 on overlay-scrollbar systems, so this is safe everywhere.

### `<dialog>` and `inert` done right

The native `<dialog>` element gives focus management, Escape-to-close, the top layer (z-index is irrelevant), and a real backdrop pseudo-element.

- `dialog.showModal()` is modal mode: the browser makes the rest of the document inert automatically (no manual `inert` on the background), traps focus, intercepts Escape, and renders the backdrop. Use for confirmation, login, critical data entry.
- `dialog.show()` (or `<dialog open>`) is non-modal: no backdrop, no focus trap, the page stays interactive. Use for a non-blocking inspector, a transient picker, an in-page side panel. Do not pair it with `inert` on the background.
- `inert` belongs on the single ancestor containing everything you want to silence (typically the root layout container, with `<dialog>` outside it). Do not sprinkle `inert` on individual elements: you will miss focusable nodes and create the inert-leak symptom (a stray button still tabbable behind a modal).
- Programmatic `dialog.open = true` is a trap: it skips all modal behavior (no backdrop, no focus, no Escape). Always call `showModal()` for modal intent.
- Test: open the dialog, Tab through every control confirming focus never escapes, press Escape, confirm the dialog closes and focus returns to the trigger.

### Sheet / drawer

- Use for non-blocking secondary content (filters, details, settings).
- Slide from edge: left/right for navigation, bottom for mobile contextual.
- Same focus and escape rules as a modal.
- Confirm before dismissing if there are unsaved changes.

### Popover / tooltip

- Tooltip: hover-reveal of supplementary info. Plain text, no interactive content.
- Popover: click-reveal of richer content, can contain interactive elements.
- Both must be keyboard-accessible and dismissible (Esc, click outside).

Wire BOTH dismissal paths on every menu, popover, and disclosure: Escape, and a pointer outside the element. Use `pointerdown` (not `click`) for the outside-dismiss listener: it fires before focus moves and before a `click` that might re-toggle the trigger, and is more reliable on touch. Add the listener only while open, remove on close. Escape returns focus to the trigger.

#### Native HTML Popover API

The `popover` attribute (Baseline 2024, broad support across current browsers) is the default choice for new popovers, menus, and disclosures. It replaces almost every hand-rolled focus-trap plus click-outside plus Escape combo.

```html
<button popovertarget="filters">Filters</button>
<div id="filters" popover>
  <p>Filter content here.</p>
</div>
```

What you get for free:

- Top-layer rendering, so the popover sits above every stacking context. No z-index arms race.
- Light-dismiss: a click outside or Escape closes it.
- Focus return: when the popover closes, focus returns to the invoker.
- `popover="manual"` for popovers you control yourself (still top layer, no light-dismiss).

Reach for a JS focus-trap library only when you need behaviour the platform does not provide (a non-closing tour step, or a popover anchored across a scrolling iframe boundary).

#### CSS Anchor Positioning (progressive enhancement)

CSS Anchor Positioning lets the popover position itself relative to its trigger without JavaScript.

```css
.trigger { anchor-name: --filters-anchor; }

#filters {
  position-anchor: --filters-anchor;
  top: anchor(bottom);
  left: anchor(start);
  position-try-fallbacks: flip-block, flip-inline;
}
```

Support floor is current evergreen-engine only, with two major engines not yet shipping. Gate the CSS path on `@supports (anchor-name: --x)`, ship a JS portal plus positioning-library fallback, and do not ship anchor-only positioning until the floor includes all major stable engines.

#### Tooltip dismissal and `aria-describedby` timing

WCAG 1.4.13 (Content on Hover or Focus) requires three properties from every tooltip:

- Hoverable: the user can move the pointer onto the tooltip itself without it disappearing.
- Dismissible: Escape (or a similarly easy gesture) closes it without moving pointer focus.
- Persistent: it stays open while the trigger remains focused or hovered. Do not auto-dismiss on a timer.

`aria-describedby` on the trigger should point at the tooltip's `id` only while the tooltip is visible. Add and remove the attribute as the tooltip opens and closes (or use the native Popover API). Setting it permanently on a `display: none` tooltip makes screen readers announce nothing or stutter stale content.

### Toast / snackbar

- Brief notification of system events.
- Auto-dismiss in 3-5s for non-critical; persist for critical with explicit dismiss.
- Do not steal focus. Use `aria-live="polite"` (or `assertive` only for blocking errors).
- Maximum one or two on screen at a time. Stack newer below older, or replace.

## Navigation

| Pattern | Use |
|---------|-----|
| Top app bar | Primary global navigation (web/desktop) |
| Bottom tab bar | Top-level navigation on mobile (max 5 items) |
| Side drawer / sidebar | Secondary nav, deep hierarchy, large screens |
| Hamburger menu | Mobile when bottom nav does not fit; not for primary on desktop |
| Breadcrumbs | Hierarchy 3+ levels deep |
| Tabs | Switching between related views of the same context |
| Segmented control | Two to four mutually exclusive options |
| Stepper / wizard | Multi-step linear flows |

Rules:

- Show where the user is: active tab/route highlighted.
- Predictable back: browser back, swipe-back (iOS), system back (Android) all do the same thing.
- Preserve scroll and state on back navigation.
- Deep linking: every screen has a URL, and sharing a link works.
- Do not mix navigation patterns at the same hierarchy level (no tabs plus sidebar plus bottom nav all primary).
- Persistent: core nav reachable from deep pages.
- Adaptive: large screens prefer sidebar, small screens use bottom/top.
- Destructive actions (delete account, sign out) visually separated from regular nav.

## Loading and Async

Any user action triggering async work needs feedback within 100ms (button state change, immediate spinner, optimistic UI) or the user assumes nothing happened.

| Duration | Pattern |
|----------|---------|
| < 300ms | Nothing. The result will appear. |
| 300ms - 1s | Spinner or progress indicator. |
| 1s+ | Skeleton screen matching the eventual layout. |
| 5s+ | Progress indicator (percent or step count) plus estimated remaining. |
| 10s+ | Allow the user to leave or background the operation. |

- Optimistic UI: for high-confidence operations (like a heart, save a draft), update immediately and reconcile on response. Roll back on error with a clear message.
- Debounce/throttle: search input debounce 200-300ms; auto-save debounce 500-1000ms; window resize throttle 100-200ms; scroll throttle 16ms (one frame) for animation, longer for analytics.

## Empty States

The empty state is content. Treat it as a designed surface.

- Specific message: "No items yet" beats "No data".
- Why it is empty: state the condition that fills it, "Items appear here once you create one."
- Primary action: offer the action that resolves it, e.g. "Create item".
- Optional illustration matching the brand. Do not use a generic stock illustration.

Anti-patterns: blank space; "No results found" with no follow-up; a hopeful illustration with no action.

## Error Recovery

- Cause AND fix in plain language: "Email address is required" beats "Validation error"; "Couldn't connect to server, check your network and try again" beats "Network error".
- Provide the recovery action (retry button, edit field, contact support link).
- Do not blame the user: "We couldn't save your draft" not "You didn't save".
- Preserve user input on error. Never make them retype.
- For 404/500: brand-consistent design, search box if applicable, link home, link to support, optionally a delightful detail.

### Undo and redo beyond the toast

A toast with an "Undo" button is the minimum bar for destructive actions. For composing surfaces (editors, canvas tools, multi-step forms, inline-edit tables) a real undo/redo system is part of the contract.

- Command pattern: every state change is a Command object with `do()` and `undo()` methods. The handler that mutates state always goes through a command, never direct mutation.
- History stack: push executed commands, pop and call `undo()` to reverse. Keep a forward stack for redo, and clear the redo stack on any new command.
- Keyboard hook: Cmd+Z (Ctrl+Z) for undo, Cmd+Shift+Z (or Ctrl+Y) for redo, at document level. Skip when focus is inside a native `<input>` or `<textarea>` so the browser's built-in text undo continues.
- ARIA: use a polite live region to announce "Undid: delete row" so screen reader users get the same feedback as the toast.
- Persistence boundary: decide whether undo crosses save (most editors say no, a save is the commit point and the history clears). State the boundary in the UI.
- Bound the history: cap at 50 to 200 commands depending on memory cost, dropping the oldest when full.

## Notifications and Badges

- Use sparingly. Notification fatigue is real.
- Numeric badges for unread/pending counts, capped at 99+ for sanity.
- Dot badges for "something new" without a count.
- Clear notifications when the user has acknowledged them.
- Group similar notifications (5 likes from 5 people = "5 people liked your post").
- Provide a notification preferences page.

### Notification permission timing

The native push permission prompt is a one-shot resource: a "Deny" verdict is usually irreversible without the user digging into browser settings. Treat the prompt as something you spend.

- Never on load: a prompt within the first 5 seconds of arriving is the canonical dark pattern, and browsers now block prompts that fire without a user gesture.
- After value demonstration: ask only after an action implying the user wants updates (subscribed to a channel, started a chat, opted into alerts, finished onboarding a long-running job).
- In-app preview before native prompt: show an in-app dialog explaining the value ("Get notified when your build finishes") with a primary "Enable" button before calling `Notification.requestPermission()`. This defers the irreversible browser prompt.
- Settings re-entry point: provide a path with copy explaining how to re-enable in the browser if the site was permanently blocked.

## Search

- Search box prominent: top of page or in the global header.
- Show recent searches when empty.
- Show suggestions while typing (debounced).
- Show results in real-time or with a clear submit button.
- Empty result state: "No results for [query]" plus spelling check plus alternate suggestions.
- Search filters: visible, removable individually, with a "clear all".
- Provide recent and saved searches.

## Data Tables

- Sortable columns indicate the current sort with an arrow icon and `aria-sort`.
- Sticky header on long tables.
- Row selection with checkboxes, with select-all in the header.
- Bulk actions appear when one or more rows are selected.
- Inline actions on hover (desktop) or always-visible (touch).
- Pagination or infinite scroll, never both.
- Empty state when no rows.
- Loading skeleton matching the table structure.

## Settings and Preferences

- Group related settings.
- Use the right control: toggle for boolean, radio for one-of-N, checkbox for any-of-N, dropdown for many-of-N, slider for continuous.
- Apply on change, not on submit (confirm only if the change is destructive).
- Show the saved state ("Saved 2s ago" microcopy).
- Reset to default per setting; never a global "reset all".

## Toolbars and Action Bars

- Group actions by relationship.
- Show only the top 3-5 actions; overflow into a kebab menu.
- Destructive actions in the overflow menu unless they are the primary action of the screen.
- Tooltip on icon-only actions.

## Onboarding

- Do not onboarding-dump. Show value first, teach as needed.
- Use empty states to teach (one feature per empty state).
- Use coachmarks sparingly, only for novel patterns the user cannot discover.
- Skippable, always.
- Resumable: the user can return to onboarding from settings.

## Sample Data and First-Run Experience

For surfaces that come alive only after the user contributes data (data tools, editors, analytics views):

- Provide a sample dataset or demo workspace so the user sees the surface working before contributing real data.
- Make it easy to switch between sample and real data.
- The surface should be useful without 30 minutes of setup.

## Microcopy

The text in the UI is part of the design.

- Concrete, not abstract: "Save draft" not "Save", "Delete user" not "OK".
- Active voice: "Choose a plan" not "A plan must be chosen".
- Present tense: "Settings saved" not "Settings have been saved".
- No jargon for end users. Write to the user's vocabulary.
- Consistent: "Sign in" everywhere, not "Log in" / "Sign in" / "Login" mixed.
- Honest: "We can't process this card" not "Something went wrong".
- Brief, cut adjectives: "Saving" not "Now saving your changes".

## Localization

- Do not bake English-specific assumptions into layouts: German is 30% longer, Japanese can be 60% shorter.
- Use logical CSS properties (`margin-inline-start`, `padding-block`) for RTL support.
- Format date, time, number, currency via `Intl.*` APIs, not custom code.
- Use ICU MessageFormat (or framework equivalent) for pluralization.
- Extract translatable strings to a single source. Never inline string concatenation for translatable text.

## Common UX Mistakes

- Hover-only actions on touch devices.
- Modals without focus traps or Esc to close.
- Tooltips with critical information.
- Required fields not marked.
- Error messages without a fix.
- Loading states that disappear before the user perceives them.
- Empty states that are blank.
- Disabled buttons that do not say why.
- Confirmation dialogs for non-destructive actions.
- No confirmation for destructive actions.
- Auto-advancing carousels with no pause.
- Carousels at all (low engagement; static heroes outperform).
- Modal interrupting the user's task.
- Toast that disappears before the user can read it.
- Hamburger menu on a desktop hero where horizontal space is plentiful.
- Search results that change layout shape on every query.

## Self-Healing for UX

Before declaring work complete:

- [ ] Every screen has empty, loading, success, error, and disabled state designs
- [ ] Every interactive element has hover, focus, active, disabled states with visible focus
- [ ] Touch targets >= 44x44 with 8px gaps
- [ ] One primary CTA per screen
- [ ] Visual hierarchy: scan the page; the most important thing is the largest/boldest
- [ ] Density matches the user's job
- [ ] Spacing follows the chosen scale; no random values
- [ ] Iconography: one set, one stroke width, consistent sizes
- [ ] Modals: focus trapped, Esc closes, focus restored
- [ ] Forms: labeled, validated on blur, error inline plus accessible
- [ ] Navigation: where am I, how do I get back, all key screens deep-linkable
- [ ] Loading: feedback within 100ms, skeleton at 300ms
- [ ] Microcopy: concrete, active, present tense, no jargon
- [ ] Tested on smallest target viewport, 200% zoom, dark mode, reduced motion, keyboard only

## See Also

- [design.md](design.md) for aesthetic and brand expression
- [forms.md](forms.md) for form-specific patterns
- [motion.md](motion.md) for state transitions
- [data-viz.md](data-viz.md) for tables and charts
