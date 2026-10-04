---
title: Anti-Patterns Index
purpose: A single index of every pattern the skill names as an anti-pattern, with its standard fix and a deep link. An agent answering "is this an anti-pattern?" lands here, not in a dozen scattered "What to Avoid" sections.
load-when:
  task-keywords: [anti-pattern, what to avoid, mistake, audit, polish, accessibility, axe, SRI, errors-in-console]
  symptoms: [score dropped, contrast fail, duplicate id, horizontal scroll, focus trap leak, hydration mismatch, errors-in-console, stale SRI beacon]
prereq: SKILL.md
related: [quick-reference.md, defects.md, components.md]
size: ~161 lines
---

# Anti-Patterns Index

## Why this exists

The skill names dozens of anti-patterns across its reference files: a "Common Mistakes" section in `forms.md`, a "What to Avoid" table in `design.md`, a row in `defects.md`, a sentence buried in `performance.md`. An agent asking "is this an anti-pattern?" should not scan twelve files.

This file is the single index. Every row names one pattern, says why it is bad in one phrase, names the standard fix, and links to the deep treatment. It restates nothing; it routes to the file that does.

Search by symptom (the column you scan first) or by category (the section heading). Add a row when a new anti-pattern is named in a deep reference; do not invent rows here that the deep references do not back up.

## How to read a row

- **Pattern.** The shape of the anti-pattern as it shows up in code or markup. Concrete.
- **Why bad.** One phrase. The reason a reviewer would reject it.
- **Standard fix.** The named replacement, in the project's own vocabulary.
- **Deep link.** The reference file that explains the fix at depth.

## Performance

| Pattern | Why bad | Standard fix | Deep link |
|--------|---------|--------------|-----------|
| Hardcoded asset path that breaks on hash change | The next deploy invalidates every preload at once | Preload by the emitted URL, resolved at build time | [performance.md](performance.md) |
| SPA without route-level code-split | Every route ships the whole app | Split by route; lazy-load route bundles | [performance.md](performance.md) |
| Eager hydration of below-the-fold widgets | First-load JS budget pays for things the user never sees | Defer hydration until viewport intersection or interaction | [performance.md](performance.md) |
| LCP image with `loading="lazy"` | Lazy defers the most important paint | `loading="eager"` plus `fetchpriority="high"` on the hero only | [performance.md](performance.md) |
| LCP image with no declared dimensions | CLS budget burns when the image arrives | Declare `width` and `height` attributes; `aspect-ratio` on the container | [performance.md](performance.md) |
| Preloading four font weights | Preload steals bandwidth from the LCP image | Preload at most one critical weight; `font-display: swap` for the rest | [performance.md](performance.md) |
| `font-display: block` on body text | Invisible text until the webfont arrives | `swap` for body, `optional` for display fonts | [performance.md](performance.md) |
| Animation that touches `width`, `height`, `top`, `left`, `margin` | Triggers layout on every frame | Animate `transform` and `opacity` only | [performance.md](performance.md), [motion.md](motion.md) |
| Third-party script loaded synchronously in `<head>` | Blocks parsing; budget hostage | `async` or `defer`, `fetchpriority="low"`, audit count monthly | [performance.md](performance.md) |
| `@import` chains in CSS | Serializes requests; blocks render | Concatenate at build time; one `<link>` per critical sheet | [performance.md](performance.md) |
| Cache-busting URL on a never-changing asset | Wastes bandwidth on every reload | Content-hash the URL; serve `Cache-Control: immutable` for a year | [performance.md](performance.md) |
| Render a long list without virtualization | Main thread chokes on 10000 nodes | Virtualize with intersection observers or a windowed renderer | [performance.md](performance.md) |
| `setTimeout` in a render path to defer expensive work | Hides the cost; INP regresses | Schedule via `scheduler.yield` or move to a worker | [performance.md](performance.md) |
| CSS-delivery change shipped without a full-site axe re-run | Inlining, bundling, or reordering can flip a computed color via cascade source order with no authored-rule change | After any css-delivery change, re-run axe on every route and eyeball both audit viewports | [lighthouse.md](lighthouse.md), [testing.md](testing.md) |
| Third-party beacon auto-injected with a pinned SRI integrity hash plus crossorigin | When the vendor rolls the asset forward the pinned hash goes stale, the CORS-mode fetch fails, and errors-in-console fires for every visitor | Self-inject the plain vendor tag (no integrity, no crossorigin, no pinned version) so it loads no-cors; edge auto-injectors skip injection when the tag is already present, so the plain tag pre-empts the broken pinned one | [debug-recipes.md](debug-recipes.md), [lighthouse.md](lighthouse.md) |

## Accessibility

| Pattern | Why bad | Standard fix | Deep link |
|--------|---------|--------------|-----------|
| Placeholder used as label | Placeholders disappear on input; SR support varies | Visible `<label for>` (or `aria-labelledby`); placeholder is a hint only | [accessibility.md](accessibility.md), [forms.md](forms.md) |
| `<div onclick>` as button | No role, no keyboard, no focus, no SR semantics | `<button type="button">` | [accessibility.md](accessibility.md) |
| `<span onclick>` for navigation | Same, plus no URL semantics | `<a href>` | [accessibility.md](accessibility.md) |
| `outline: none` without a replacement focus style | Keyboard users cannot see where they are | `:focus-visible` ring, 3:1 contrast against surface and resting state | [accessibility.md](accessibility.md) |
| Hardcoded DOM `id` in a reused component | Duplicate ids when the component renders twice | Per-instance id source (the framework's `useId` equivalent); namespace child ids | [components.md](components.md), [quick-reference.md](quick-reference.md) |
| `aria-hidden="true"` on a focusable element | Focus lands on a node the SR cannot announce | Remove from tab order with `inert` (or `tabindex="-1"` plus the rest of the inert pattern) | [accessibility.md](accessibility.md) |
| `tabindex` greater than zero | Overrides natural order; surprises every keyboard user | Use `tabindex="0"` or `tabindex="-1"` only; rely on document order | [accessibility.md](accessibility.md) |
| Skip link hidden with `position: absolute; left: -9999px` | Some browsers still include it in the layout box | `clip-path: inset(100%)` when hidden; reveal on `:focus` | [accessibility.md](accessibility.md), [defects.md](defects.md) |
| Heading levels skipped (H1 then H3) | Breaks document outline for SR users | Sequential descent; one H1 per page | [accessibility.md](accessibility.md) |
| `<button>` inside `<a>` (or vice versa) | Invalid content model; ambiguous activation | Pick one; the activation surface is the parent | [accessibility.md](accessibility.md) |
| Color as the only signal (red border for error, no icon, no text) | Colorblind users see no error | Pair color with icon and text on every status | [accessibility.md](accessibility.md), [design.md](design.md) |
| Live-region element added to the DOM at the moment of update | Some SRs do not announce; the region must be present at load | Render the live region empty at load; mutate its content on update | [accessibility.md](accessibility.md) |
| `<button>` with no accessible name (icon only, no `aria-label`) | SR announces "button" with no purpose | `aria-label` or visually-hidden text inside the button | [accessibility.md](accessibility.md) |
| Inline svg given `role="img"` with no accessible name | Exposes a nameless image to assistive tech and trips image-alt, dropping Accessibility and SEO | Decorative svg: `aria-hidden="true"`. Meaningful svg: `role="img"` plus a `title` or `aria-label` | [accessibility.md](accessibility.md) |
| Modal that traps Tab but not the screen reader | Background remains in the AT tree | Mark every sibling of the dialog `inert` while open | [accessibility.md](accessibility.md), [debug-recipes.md](debug-recipes.md) |

## SEO

| Pattern | Why bad | Standard fix | Deep link |
|--------|---------|--------------|-----------|
| Client-only content invisible to crawlers | The crawler sees a skeleton; nothing indexes | SSR, SSG, or streaming HTML that includes the content | [seo.md](seo.md) |
| Fabricated structured data (claims a review or rating that does not exist) | Search-engine manual action; removed from rich results | Only mark up content the user actually sees | [seo.md](seo.md) |
| `noindex` page still listed in `sitemap.xml` | Wastes crawl budget; mixed signals | Remove from the sitemap; the sitemap is the indexable index | [seo.md](seo.md) |
| Canonical that points to a different page than the URL the user lands on | The canonical chain is inconsistent; ranking signals leak | Canonical points at the URL the user reached, or the chosen one in a duplicate set | [seo.md](seo.md) |
| Title tag duplicated across the site | Search engines cannot tell the pages apart | One unique title per page, 50 to 60 characters | [seo.md](seo.md), [defects.md](defects.md) |
| Meta description missing on indexable pages | Search engines generate one; loses control of the snippet | One unique description per indexable page, 140 to 160 characters | [seo.md](seo.md) |
| Multiple H1 elements on a page | Outline is ambiguous; ranking signal weakens | One H1; subsequent sections use H2 | [seo.md](seo.md), [accessibility.md](accessibility.md) |
| Image with no `alt` attribute | Image cannot be indexed; SR cannot announce | `alt=""` for decorative; meaningful `alt` for content | [accessibility.md](accessibility.md), [seo.md](seo.md) |
| `hreflang` set on one locale but not its return reference | Search engines drop the cluster; no locale routing | Bidirectional `hreflang`; every locale references every other plus `x-default` | [seo.md](seo.md), [i18n.md](i18n.md) |
| robots.txt blocks JS or CSS the page needs to render | Crawler renders a broken page; ranking drops | Allow JS and CSS routes; only block sensitive paths | [seo.md](seo.md) |

## UI / UX

| Pattern | Why bad | Standard fix | Deep link |
|--------|---------|--------------|-----------|
| Scroll-lock without scrollbar compensation | Page shifts sideways when the modal opens | Set `body { overflow: hidden }` plus `padding-right` equal to scrollbar width | [ui-ux.md](ui-ux.md), [defects.md](defects.md) |
| Hover-only menu on touch | First tap opens, second tap navigates; users get stuck | Show on hover, focus, and tap; or use a press-to-open menu | [ui-ux.md](ui-ux.md) |
| Modal with no Esc handler | Keyboard users have no way out | Esc closes; focus restored to the trigger | [ui-ux.md](ui-ux.md), [accessibility.md](accessibility.md) |
| Custom dropdown that does not support arrow keys | Keyboard users cannot select | Implement the ARIA combobox or listbox pattern in full | [ui-ux.md](ui-ux.md), [accessibility.md](accessibility.md) |
| Tooltip that only opens on hover | Touch and keyboard users cannot read it | Open on hover, focus, and tap; close on Esc and outside | [ui-ux.md](ui-ux.md) |
| Toast that disappears too fast to read | Users miss critical confirmation | Minimum 5 seconds; allow pause on hover; persist destructive confirmations | [ui-ux.md](ui-ux.md) |
| Empty state that is genuinely blank (no message, no action) | User cannot tell whether the page is broken | Specific message, the condition that fills it, the action that resolves it | [ui-ux.md](ui-ux.md), [defects.md](defects.md) |
| Loading state that is a generic spinner with no context | User cannot tell whether the system is doing the right thing | Skeleton matched to the content; or a labelled progress indicator | [ui-ux.md](ui-ux.md) |
| Touch target under 44 by 44 CSS pixels | Fat-fingered users miss; WCAG 2.5.5 fail | 44 by 44 minimum; for inline links, the WCAG 2.5.8 inline exception | [ui-ux.md](ui-ux.md), [defects.md](defects.md) |
| Confirm dialog that uses "OK" and "Cancel" | Users do not know which is destructive | Verb labels on both buttons ("Delete account" and "Keep account") | [ui-ux.md](ui-ux.md) |

## Design

| Pattern | Why bad | Standard fix | Deep link |
|--------|---------|--------------|-----------|
| A geometric grotesque sans on white with a purple gradient | The default AI aesthetic; instantly recognised as generic | Pick a typeface with character (a serif, a humanist sans, a variable display); pair with a restrained palette | [design.md](design.md) |
| Raw hex values in component CSS | Brand palette drifts per component; dark mode breaks | Reference design tokens; tokens are the single source | [design.md](design.md), [components.md](components.md) |
| Brand accent that fails contrast in dark mode | Brand recognition costs accessibility | Compute the dark-mode brand variant against the dark surface; aim for 4.5:1 on text, 3:1 on UI | [design.md](design.md), [accessibility.md](accessibility.md) |
| Inconsistent corner radii (3, 4, 5, 6 px in the same view) | Visual noise; eye cannot anchor | Define a token scale (4, 8, 12, 16) and use only the scale | [design.md](design.md) |
| One typographic scale for marketing, another for product | Brand fractures at the seam | Single type scale across surfaces; loosen line-height and tracking for marketing only | [design.md](design.md) |
| Pure black (`#000`) on pure white (`#fff`) | Painful contrast; designers know to soften | Slightly off-black on off-white; preserve the contrast ratio | [design.md](design.md) |
| Icon set drawn at three different stroke widths | Visual incoherence | One icon family; tune stroke width once | [design.md](design.md), [components.md](components.md) |
| Decorative shadow at the same depth as elevation tokens | Shadows lose semantic meaning | Reserve elevation shadows for elevation; decorative blur is a separate token | [design.md](design.md) |
| Animated gradient background at 60fps in the hero | Main thread busy; the page never feels finished | Static or low-frame-rate gradient; reserve motion for affordance | [design.md](design.md), [motion.md](motion.md) |

## Components

| Pattern | Why bad | Standard fix | Deep link |
|--------|---------|--------------|-----------|
| Hardcoded DOM `id` in a reused component | Duplicate ids; broken ARIA references | Per-instance id source; namespace child ids | [components.md](components.md), [quick-reference.md](quick-reference.md) |
| Near-duplicate components with different names but the same purpose | The library fragments; users get different behaviour on different pages | Merge into one component with named variants | [components.md](components.md) |
| Prop explosion (a single component with twenty boolean props) | Combinatorial states; tests cannot cover; readers cannot reason | Slots and composition; pull the variants apart into smaller components | [components.md](components.md) |
| Inline styles in a shared component | Bypasses tokens; impossible to theme | All styles via tokens or themed classes; inline only for computed measurements | [components.md](components.md), [design.md](design.md) |
| Component reaches outside its DOM for data (`document.querySelector` of a sibling) | Coupling to page structure; the component breaks on reuse | Pass dependencies as props; expose events for the parent to react | [components.md](components.md) |
| Page-local CSS overriding a shared component | The override travels per page; the contract leaks | Add the variant to the component; no page-local overrides | [components.md](components.md) |
| Component that does not document its server vs client boundary | Renders break across surfaces; bugs land in production | Document the boundary on the contract; mark the file or function explicitly | [components.md](components.md) |
| Shared component with no isolated playground entry | No way to reproduce in isolation; bug reports route to the page that hosts it | Every shared component has a playground entry per variant | [components.md](components.md) |

## Forms

| Pattern | Why bad | Standard fix | Deep link |
|--------|---------|--------------|-----------|
| `<input type="text">` for an email | No mobile keyboard, no built-in validation | `<input type="email">` plus `autocomplete="email"` | [forms.md](forms.md), [accessibility.md](accessibility.md) |
| No `autocomplete` on a sign-up form | Password managers and autofill do not work | Every field has the standard `autocomplete` value | [forms.md](forms.md) |
| Validation that fires on every keystroke | Errors flash before the user finishes typing | Validate on blur for new errors; clear errors live as the user types | [forms.md](forms.md), [accessibility.md](accessibility.md) |
| Error message that only says "Invalid input" | User cannot fix what they cannot diagnose | Cause AND fix in one sentence ("Enter your work email") | [forms.md](forms.md) |
| Submit button disabled until the form is valid | Users do not learn what is wrong | Always submittable; show errors on submit; focus the first invalid field | [forms.md](forms.md) |
| Custom phone-number field that strips spaces | Pastes from contacts fail; users retype | Accept any reasonable format; normalise on the server | [forms.md](forms.md) |

## Motion

| Pattern | Why bad | Standard fix | Deep link |
|--------|---------|--------------|-----------|
| Ignoring `prefers-reduced-motion` | Vestibular users get sick | Wrap non-essential motion in the media query; provide a static alternative | [motion.md](motion.md), [accessibility.md](accessibility.md) |
| Auto-playing video in the hero with no pause control | Motion plus audio with no opt-out; WCAG 2.2.2 fail | Provide pause and mute controls; respect reduced-motion | [motion.md](motion.md) |
| Endless animation in the periphery | Distracts every user; never returns to a rest state | Cap to a few cycles, or trigger on intent (hover, scroll into view) | [motion.md](motion.md) |
| Animating `box-shadow` on hover | Repaints the whole element every frame | Pre-render the shadow; cross-fade two layers via `opacity` | [motion.md](motion.md), [performance.md](performance.md) |

## Defects

| Pattern | Why bad | Standard fix | Deep link |
|--------|---------|--------------|-----------|
| Body scroll lock without `padding-right` compensation | Page jumps sideways when the modal opens | Measure scrollbar width; set `body { padding-right: <gap> }` while locked | [defects.md](defects.md), [ui-ux.md](ui-ux.md) |
| `width: 100vw` inside a padded container | Element extends past the viewport on mobile | `width: 100%` (which respects the parent) | [responsive.md](responsive.md), [defects.md](defects.md) |
| Unstyled `<iframe>` or `<video>` | Default intrinsic width overflows | `max-width: 100%; width: 100%` | [defects.md](defects.md), [responsive.md](responsive.md) |
| Table without `table-layout: fixed` on mobile | One long cell stretches the table past the viewport | `table-layout: fixed; width: 100%; word-break: break-word`; or transform to cards | [defects.md](defects.md), [responsive.md](responsive.md) |
| Focus ring clipped by `overflow: hidden` | Keyboard users cannot see focus | Move the ring to `outline` (paints outside) or add internal padding | [accessibility.md](accessibility.md), [defects.md](defects.md) |
| Webfont flash on first paint | Visible swap that shifts layout | `font-display: swap` plus metrics-override fallback | [debug-recipes.md](debug-recipes.md), [performance.md](performance.md) |

## See also

- [quick-reference.md](quick-reference.md) for the rules these anti-patterns negate
- [defects.md](defects.md) for the symptom-to-fix lookup that pairs with this index
- [components.md](components.md) for the component-contract patterns whose violations dominate this index
