---
title: Design Aesthetics
purpose: Framework-agnostic aesthetic direction covering typography, color systems (OKLCH, P3 wide gamut), palette construction, elevation, atmosphere, brand voice, and dark mode. States concrete thresholds and CSS properties so an interface reads intentional, not generic.
load-when:
  task-keywords: [design, typography, color, palette, OKLCH, P3, spacing, composition, atmosphere, dark mode, brand, variable font]
  symptoms: [dark mode broken, contrast fail, score dropped]
prereq: SKILL.md
related: [ui-ux.md, responsive.md, motion.md, accessibility.md]
size: ~543 lines
---

# Design Aesthetics

Build interfaces that look intentional and distinctive, the opposite of generic AI styling. Every rule below is a principle plus a concrete check.

## The Generic AI Look (avoid all of these)

If a page reads like this list, restart with an actual aesthetic direction.

- Inter (or Geist, or Space Grotesk) on pure white with text-slate-600 body text.
- Purple-to-pink linear gradients on hero CTAs.
- Glassmorphism without context: backdrop-blur cards floating on a colorful background.
- Centered-everything layouts: hero text centered, three feature cards centered, centered CTA.
- Three-card pricing grid with the middle card highlighted via gradient border.
- Lucide icons in 1.5px outline at 20px above every section heading.
- A "trusted by" logo strip with five evenly-spaced grayscale logos.
- A vague abstract gradient background with low opacity blobs.
- Lorem-style placeholder copy that says nothing specific.
- text-balance on every heading with no concern for what is balanced.
- A FAQ section using <details> as the last block before the footer.
- A footer with five columns of links that nobody clicks.

## Pick a Direction (and commit)

Pick ONE aesthetic lane and execute it precisely. Intentionality, not intensity, is the key.

| Lane | Personality |
|------|------------|
| Refined minimal | Quiet, generous whitespace, muted palette, distinctive type |
| Editorial | Magazine-like, serif display, asymmetric grids, considered photography |
| Brutalist | Raw, unstyled-feeling, monospace, hard borders, no shadows |
| Maximalist / chaotic | Layered textures, decorative elements, generous color, dense |
| Retro-futuristic | 80s/90s aesthetic, neon, CRT scan lines, monospace, deep blacks |
| Organic / natural | Soft curves, earthy palette, hand-drawn elements, pastels |
| Industrial / utilitarian | Engineering-feeling, technical drawings, fixed-width type, schematic |
| Luxury / refined | Restrained, elegant serifs, high contrast, generous space |
| Playful / toy-like | Bright, rounded, illustrated, animated micro-interactions |
| Geometric / Bauhaus | Primary colors, geometric shapes, grid-driven, sans-serif |
| Magazine / softlit | Pastel surfaces, large photography, warm shadows, lifestyle |
| Cyberpunk / dark futurist | Deep saturated darks, hard accent colors, glitch elements |

Per chosen lane, commit to all five:

- One typeface family for headings, one for body (sometimes the same).
- A constrained palette: one dominant, 1-2 accents, neutral scale.
- A specific elevation/shadow language.
- A specific border-radius scale.
- A specific motion language.

## Typography

Type carries the most personality. Pair a distinctive display face with a refined body face, or use one excellent variable font for both. Anti-pattern: pairing two similar sans-serifs; they fight without contrasting.

### Face requirements

| Face | Requirements |
|------|-------------|
| Body | Optimized for screens (hinted, generous x-height); multiple weights (regular, medium, semibold, bold minimum); decent at small sizes (14-16px); wide language coverage if multi-locale; variable font preferred (one file replaces 3-5 weights) |
| Display | Distinctive at large sizes; less constrained on rendering quality (used 24-96px); has personality (if the hero heading could be any other product's logo, it is too generic) |

### Pairings by lane (worked examples)

| Lane | Display | Body |
|------|---------|------|
| Refined minimal | Söhne, Suisse Int'l, GT America | Söhne, Inter Display |
| Editorial | GT Sectra, Tiempos Headline, Fraunces (variable) | Source Serif, Tiempos Text, Newsreader |
| Brutalist | Söhne Mono, JetBrains Mono, Berkeley Mono | Söhne Mono, IBM Plex Mono |
| Maximalist | Migra, Pangea, Editorial New | Inter, Söhne |
| Retro-futuristic | VT323, Press Start 2P, IBM Plex Mono | IBM Plex Mono, Space Mono |
| Organic | Fraunces (variable), Saol, Reckless | Söhne, Untitled Sans |
| Industrial | Söhne Mono, Berkeley Mono | Söhne Mono |
| Luxury | Tiempos Headline, GT Sectra Display, Saol | Tiempos Text, GT America |
| Playful | Migra, Editorial New, ABC Diatype | Söhne, Untitled Sans |
| Bauhaus | Neue Haas Grotesk, Söhne, Inter | Same |
| Magazine | Tiempos Headline, GT Sectra, Newsreader | Newsreader, Source Serif |
| Cyberpunk | JetBrains Mono, Departure Mono, Berkeley Mono | Same |

Open-source equivalents:

- Sans (refined): Inter Display, Manrope, Public Sans, Geist.
- Sans (characterful): Outfit, Bricolage Grotesque, Bagoss Standard.
- Serif (refined): Newsreader, Source Serif 4, Lora, Crimson Pro.
- Serif (display): Fraunces (variable), DM Serif Display, Playfair Display.
- Mono: JetBrains Mono, IBM Plex Mono, Geist Mono, Departure Mono, VT323.
- Display / unusual: Bagoss Condensed, Migra, Editorial New (paid), Bricolage Grotesque (variable, free).

Budget: two families and four weights. Variable fonts stretch this without the byte cost.

### Variable-font axes beyond wght

Vary one or two axes intentionally per project. Animating three at once reads as a typography demo, not a product.

| Axis | Use |
|------|-----|
| wght (weight) | Familiar 100 to 900; emphasis, hierarchy, grade adjustment |
| wdth (width) | e.g., 75 narrow to 125 wide; constrained-width nav labels, poster headlines, subtle hierarchy |
| opsz (optical size) | Pair with `font-optical-sizing: auto;` so the browser picks the right shape for the current font-size |
| slnt (slant) | True italic-without-an-italic-cut when the font has no italic master |
| GRAD (grade) | Thickens strokes without changing horizontal metrics; dark-mode trick nudges grade up 50 to 100 units to compensate for light-on-dark optical thinning without reflow |
| Custom (MONO, CASL, CRSV, XOPQ, others) | Appear in many modern variable fonts (Recursive, Roboto Flex, Fraunces); read the spec sheet and use exposed knobs |

### Type scale (build it, do not pick by feel)

| Modular ratio | Use |
|--------------|-----|
| 1.125 (major second) | Dense UI, data-heavy surfaces |
| 1.2 (minor third) | Default for routine application UI |
| 1.25 (major third) | Generous, spacious layouts |
| 1.333 (perfect fourth) | Editorial, dramatic |
| 1.5 (perfect fifth) | Luxury, very dramatic |

A common 1.25 scale at base 16px: `12.8 -> 16 -> 20 -> 25 -> 31.25 -> 39 -> 48.8 -> 61 -> 76.3`. Round to whole pixels for crispness.

### Display vs body type rules

| Property | Display | Body |
|----------|---------|------|
| Line height | 1.0-1.2 (tighter) | 1.5-1.75 for paragraphs |
| Letter-spacing | Negative (-0.01em to -0.03em) | Default |
| Weight | Heavier at large sizes; medium or bold at very large | 400 body, 500 UI labels, 600 emphasis |
| Size | Used 24-96px | 16px minimum |
| Line length | n/a | 60-75 characters |
| Layout | May break the grid, overlap, or extend into margins | Standard flow |

### Numerics and wrapping

Tabular figures: for prices, percentages, timers, data tables, use `font-variant-numeric: tabular-nums` to prevent column shift between renders.

```css
.price, .stat {
  font-variant-numeric: tabular-nums;
}
```

- `text-wrap: balance`: only on H1/H2 (not every heading); browser balances lines for short text.
- `text-wrap: pretty`: on long-form copy to avoid orphans / one-word bottom lines. It is multi-pass (lay out, evaluate, re-lay out), so reserve it for callout/lead paragraphs and captions and leave routine body copy on default wrapping.

## Color

### Palette construction (start from purpose)

1. Background neutral: light usually `#FAFAFA`, `#FFFFFF`, or a faint warm/cool tint; dark `#0A0A0B`, `#111`, or a colored dark like navy `#0B1020`. Plan light and dark separately.
2. Foreground neutral: maximum contrast against background, light `#0A0A0B`, dark `#FAFAFA`; add muted variants by reducing opacity or mixing toward the background.
3. Surface scale: 3-5 elevation levels (background, surface, surface-elevated, surface-overlay, surface-modal), each lighter in dark mode or with more shadow in light mode.
4. Brand primary: one color, saturated enough to read on both backgrounds; check both light and dark mode contrast.
5. One or two accents: used sparingly for highlights, links, decorative elements; harmonize with the primary.
6. Semantic colors: success (green), warning (amber/yellow), danger (red), info (blue), each at 4.5:1 against surfaces.
7. Neutral scale: 9-12 stops (50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950); pick a tinted neutral (warm stone, cool slate, colored zinc/gray) and avoid pure neutral gray for character.

### OKLCH and OKLab for perceptual palettes

Hex and HSL produce perceptually uneven ramps. OKLCH/OKLab parameterise color in a perceptually uniform space so equal steps along lightness, chroma, or hue look equal.

```css
:root {
  --brand-500: oklch(62% 0.18 270);
  --brand-600: oklch(54% 0.18 270);
  --brand-400: oklch(70% 0.18 270);
}
```

`color-mix()` in OKLCH gives guaranteed-uniform steps:

```css
:root {
  --brand: oklch(62% 0.18 270);
  --brand-tint:   color-mix(in oklch, var(--brand) 20%, white);
  --brand-shade:  color-mix(in oklch, var(--brand) 80%, black);
}
```

Use OKLCH for:

- Lightness ramps in neutral/brand scales (50 through 950): step lightness by a constant delta, hold chroma and hue.
- Categorical chart hue ramps: hold lightness and chroma, step hue by a constant delta; twelve perceptually distinct colours fit comfortably (see data-viz.md).
- State variants (hover, active, focus): mix the base color toward foreground or background by 5 to 15 percent in OKLCH.

### P3 wide-gamut color

Modern displays render the P3 gamut, roughly 35 percent larger than sRGB. Saturated P3 brand colors fall back to a duller sRGB version on older monitors; serve both. Treat P3 as progressive enhancement: define every brand color with an sRGB fallback first, then override inside `@media (color-gamut: p3)`. A P3 token without a fallback renders as the unstyled default on older hardware. Test both modes.

```css
.brand-cta {
  background: #4b5cf0; /* sRGB fallback */
  background: color(display-p3 0.25 0.36 0.95); /* P3 on capable displays */
}

@media (color-gamut: p3) {
  :root {
    --brand: oklch(62% 0.22 270 / display-p3);
  }
}
```

### Color philosophy

- Dominant + sharp accents beats evenly-distributed: pick a dominant (the brand) and let it occupy 60-80% of accent area; accents at 10-30% for key calls; neutrals for the rest.
- The 60-30-10 rule: roughly 60% neutral, 30% secondary, 10% accent, adjusted per page type.
- Dark mode is its own design: do not invert; pick darker, desaturated brand variants (e.g., `#5B5DEF` in light becomes `#7376F5` in dark for proper contrast).
- Saturation for hierarchy: higher saturation reads as more important; desaturate less important elements.

### Color ratios (verify against accessibility.md)

Light mode:

| Element | Foreground | Background | Ratio |
|---------|-----------|------------|-------|
| Body text | `#0a0a0b` | `#ffffff` | 20:1 (AAA) |
| Body muted | `#525860` | `#ffffff` | 7:1 (AAA large) |
| Body subtle | `#71757b` | `#ffffff` | 4.6:1 (AA) |
| Borders | `#e4e4e7` | `#ffffff` | 1.4:1 (decorative; not text) |

Dark mode:

| Element | Foreground | Background | Ratio |
|---------|-----------|------------|-------|
| Body text | `#fafafa` | `#0a0a0b` | 19:1 (AAA) |
| Body muted | `#a1a1aa` | `#0a0a0b` | 7:1 (AAA large) |
| Body subtle | `#71717a` | `#0a0a0b` | 4.5:1 (AA) |
| Borders | `#27272a` | `#0a0a0b` | 1.4:1 (decorative) |

Dark mode borders are typically `rgba(255,255,255,0.08-0.12)` for subtle separation. Pure dark borders (`#000`) are invisible.

### Gradients

- Mesh gradients (multiple radial gradients overlaid) read more designed than two-stop linear gradients.
- Subtle: 2-3 stops with similar luminosity.
- Avoid pure purple-to-pink at 45deg (the AI-generated cliche).
- Brand-aware: use brand-adjacent colors (analogous on the color wheel).

### Atmosphere (subtle; dial down if it competes with content)

- Film grain: 1-3% opacity noise overlay.
- Soft radial gradient behind hero: off-center, low intensity.
- Decorative blur shapes outside the main content: 5-10% opacity.
- Dot or grid pattern: 4-8% opacity.
- Off-page large type ghost letters (the brand name as decorative background).

### prefers-reduced-transparency

Treat this query like `prefers-reduced-motion`: every translucent surface needs a flat fallback (opaque background, `backdrop-filter: none`, the same contrast ratio a contrast-sensitive user would get). The fallback is not "slightly less blur."

```css
.glass-card {
  background: rgba(255, 255, 255, 0.6);
  backdrop-filter: blur(12px);
}

@media (prefers-reduced-transparency: reduce) {
  .glass-card {
    background: var(--color-surface);
    backdrop-filter: none;
  }
}
```

## CSS Architecture

### @layer for design-system precedence

Cascade Layers replace specificity fights with one ordering rule: a later layer always wins, regardless of specificity.

```css
@layer reset, base, tokens, components, utilities;

@layer reset { /* normalize, your reset */ }
@layer base { /* element-level typography, body color, link defaults */ }
@layer tokens { :root { --brand: oklch(62% 0.18 270); } }
@layer components { .card { padding: var(--space-4); } }
@layer utilities { .m-0 { margin: 0; } }
```

- A high-specificity selector in `components` still loses to a low-specificity rule in `utilities`, so utilities reliably beat component CSS without `!important`. Specificity stays the tie-breaker inside a layer, not across layers.
- Anti-pattern: declaring layers without using them; a global unlayered `.card {}` beats a layered `@layer components { .card {} }`.
- Ceiling: five working layers; eight add mental overhead with no payoff.
- Put third-party CSS in its own layer at the right precedence via `@import url(...) layer(vendor);`; do not layer it into your component layer.

## Spacing and Composition

### Whitespace by lane

| Lane | Whitespace |
|------|-----------|
| Refined minimal / luxury | Very generous, sections separated by 96-128px |
| Editorial | Rhythmic, varied (some tight, some open) |
| Maximalist | Tight, deliberate density |
| Brutalist | Irregular; whitespace is a tool, not a default |

### Grids

- 12-column: most flexible default, works for almost everything.
- 8-column: tighter, denser layouts.
- 3, 5, 7: editorial or asymmetric layouts.
- No grid (bento, irregular): deliberately distinctive layouts.

### Asymmetry and grid-breaking

Break the grid for interest: a heading overlapping a column boundary, an image bleeding off the page edge, a diagonal flow, an element rotated 1-3 degrees. Use sparingly: one grid-break per section, not every section.

### Bento grid

A modular grid with variable card spans (one large hero card, two half-width, four quarter-width). Effective for multiple features at varying weights. Do not overdo it; bento does not fit every brand.

### Vertical rhythm

Lines of text should align to a baseline grid; set `line-height` so multiples create natural rhythm. With 16px body / 24px line-height, a 4 or 8 px spacing scale aligns naturally.

## Elevation and Shadow

Shadows convey depth, not decoration. Use a consistent scale.

### Light mode (worked examples)

| Level | Shadow |
|-------|--------|
| Resting card | `0 1px 2px 0 rgba(0,0,0,0.04)` |
| Hover card | `0 4px 12px -2px rgba(0,0,0,0.08), 0 2px 4px -1px rgba(0,0,0,0.04)` |
| Modal / overlay | `0 24px 48px -8px rgba(0,0,0,0.18), 0 12px 24px -8px rgba(0,0,0,0.10)` |
| Popover | `0 8px 24px -4px rgba(0,0,0,0.12), 0 4px 8px -2px rgba(0,0,0,0.06)` |

### Dark mode

Dark-mode shadows often look fake. Prefer:

- Lighter borders to indicate elevation instead of dark shadows.
- Subtle glow (1px outset shadow with a low-alpha brand color or white).
- Higher surface luminance for elevated elements (each step slightly lighter).

Tint dark-mode shadows with the brand color or a lifted neutral instead of pure black, alpha low (15 to 30 percent), so it reads as a colored haze rather than a black blob:

```css
:root[data-theme="dark"] {
  --shadow-card:
    0 4px 12px -2px color-mix(in oklch, var(--brand) 30%, transparent),
    0 2px 4px -1px color-mix(in oklch, var(--brand) 20%, transparent);
}
```

### Shadow anti-patterns

- Drop shadows on every element.
- Shadows in random directions (some bottom-right, some bottom-left).
- Strong opacity (0.5+) that looks like gameplay UI.

### Border radius

Pick a scale: `0`, `4px`, `8px`, `12px`, `16px`, `24px`, `9999px` (pill). Use it consistently. By lane:

| Lane | Radius |
|------|--------|
| Brutalist | 0 throughout |
| Refined minimal | 8-12px cards, 6-8px buttons, full pill on tags |
| Playful | 16-24px on everything |
| Luxury | 0-4px (sharper) |

## Iconography

Pick one set and stay; do not mix.

| Source | Style |
|--------|-------|
| Lucide | Refined outline, 1.5px stroke, 24x24 grid. Free, open-source |
| Heroicons | Outline + Solid + Mini, by the Tailwind team. Free |
| Phosphor | Six weights from thin to fill. Many use cases. Free |
| Iconoir | Slightly more characterful outline. Free |
| Tabler | Largest free set, 24x24, 2px stroke |
| Feather | Original 24x24 outline (predecessor to Lucide) |
| Remix Icon | Many domains covered. Free |
| Iconjar / Streamline / Untitled UI Icons | Premium, distinctive |

Custom: even 6-8 custom icons in heroes/sections elevate a page beyond "Lucide on white".

## Imagery and Illustration

Photography:

- High quality, brand-relevant, original where possible.
- Consistent treatment (color grade, contrast, crop ratio).
- Avoid generic stock (the smiling business team in front of a window).
- Subjects diverse, contexts varied.
- Portrait: directional lighting, intentional background.
- Product: clean, consistent, scaled.

Illustration:

- Custom beats stock illustration packs.
- Consistent style (one illustrator's hand, or one style guide).
- Match the brand; cheerful illustrations do not fit a serious tool.
- Do not over-illustrate; one key illustration beats 12 mediocre ones.

3D:

- Works when it matches the brand, is rendered at high quality, and serves a purpose (a product "tour", a metaphor).
- Anti-pattern: a generic 3D blob or low-poly mascot dropped in for visual interest.

## Motion and Atmosphere

Full motion guide: see motion.md.

- One well-orchestrated entrance animation on page load (staggered reveals, hero parallax) beats scattered micro-interactions everywhere.
- Subtle ambient motion (a slow gradient shift, a hero element drifting 1-2px) gives life without distraction.
- Hover states with surprise (button text shifts forward, icon swaps direction, card lifts and tilts).

Match intensity to lane:

| Lane | Motion |
|------|--------|
| Brutalist | Minimal, snap transitions |
| Refined minimal | Subtle, slow, considered |
| Playful | Spring-physics, generous, characterful |
| Cyberpunk | Glitch, scan, jitter |

## Layout Patterns That Read as "Designed"

Versus the AI default (centered three cards over hero text with gradient):

- Asymmetric hero with the heading on the left and a visual on the right (or vice versa).
- Editorial split with content on the left third, large image on the right two-thirds.
- Bento grid features with variable card sizes.
- Side-scrolling features with a fixed left column and horizontally scrolling cards on the right.
- Diagonal flow sections (alternating left-aligned and right-aligned content).
- Overlapping hero elements where the visual partially covers the heading.
- Decorative type as a section divider (a giant ghost letterform behind the section).
- Sticky aside with active section indicator (long content with side nav).
- Horizontal timeline for narrative content.
- Step-by-step mockup showing the product changing state across the page.

## Dark Mode

Do not invert a light-mode design; plan dark mode as its own design pass.

- Surfaces: background deep, slightly tinted (not pure black, not pure gray), e.g., `#0B0B0F` or `#0F1117`. Surfaces are slightly lighter than the background (not the inverse of light mode); multiple elevation levels by lightening, not by shadow.
- Brand primary: often needs to be lighter and slightly desaturated in dark mode; test contrast.
- Accent text: a saturated accent that clears 4.5:1 against white commonly drops below 4.5:1 against near-black, so define a separate lightened accent token for text and links on dark surfaces and verify both modes independently.
- Imagery: photography in dark mode often needs a slight darken or color grade; illustrations may need dark-mode variants.
- State indicators: borders, focus rings, and hover states all need separate planning in dark mode.

## Brand Expression

Pick a level and commit; mid-level brand expression often reads as confused.

| Level | Treatment |
|-------|-----------|
| Restraint (Apple-like) | Mostly neutral, brand appears rarely (logo, accent CTAs), product content is the hero |
| Expression (Notion/Linear) | Brand color in CTAs and key surfaces, decorative brand elements present but not dominant |
| Saturation (indie/DTC) | Brand expressed everywhere, high saturation, distinctive type; the product is the brand |

## Voice

Visual design and verbal voice must match; a mismatch is the biggest tell a design is unfinished.

| Lane | Voice |
|------|-------|
| Refined | Precise, considered, never gimmicky |
| Editorial | Thoughtful, well-written, references |
| Playful | Human, warm, occasionally winking |
| Industrial | Technical, accurate, terse |
| Cyberpunk | Edgy, punchy, knows the audience |

## Atmospheric Details

The design reads "considered" when at least 5-10 of these exist:

- A custom cursor on a hero or feature surface (sparingly; never globally).
- Hover-reveal of a hidden detail (a tiny annotation appearing on a chart hover).
- A delightful 404 page with a working interaction.
- A hand-coded animation in the empty state (not a stock Lottie).
- A custom illustration in the onboarding.
- A unique scroll-progress indicator.
- A custom selection color matching the brand.
- A signature link underline (animated thickness, custom dash, or position).
- Custom error illustrations.
- Carefully written microcopy with personality.

## Reference Comparison Heuristics

When polishing against a reference, judge each as a yes-or-no, supported by captured screenshots at both audit viewports. A site at reference level passes every test; a site at "fine" level fails three or more. Authoritative use lives in the multi-page polish workflow (audit-workflow.md Phase 16); this is a lookup for design review outside that workflow.

1. First viewport has a clear hierarchy and does not feel accidental.
2. The brand or product is visible immediately when relevant.
3. The next section peeks intentionally rather than crowding the hero.
4. Repeated cards in a row align on top edges, bottom edges, and CTA baselines.
5. Text blocks have readable line lengths and consistent rhythm.
6. Labels, eyebrows, and badges use one visual language.
7. Buttons use one size system and one radius system.
8. Icons sit optically centered, not just mathematically centered.
9. Borders are subtle and consistent.
10. Shadows support hierarchy but do not muddy the palette.
11. Alternate background bands feel deliberate.
12. Mobile layout feels designed, not merely stacked.
13. Footer spacing is calmer than the body, with no stray gaps or cramped links.
14. Empty states, legal pages, and 404 pages share the same polish as marketing pages.
15. Same-family widgets look like members of one system across pages.
16. Variants are recognizably intentional, not accidental drift.

## Common Design Mistakes

- Two similar sans-serifs paired (no contrast).
- Centered everything (nothing leads the eye).
- Equal-weight feature cards (no hierarchy).
- Random spacing values (no rhythm).
- Mixing icon styles (loses cohesion).
- Drop shadows in random directions.
- Multiple gradient buttons of different colors.
- Light mode and dark mode designed at the same time as inversions.
- Decoration that distracts from content.
- Stock illustrations from the same pack everyone uses.
- Every section using the same layout (visual monotony).
- Every section using a different layout (visual chaos).

## Self-Healing for Design

Before declaring work complete:

- [ ] One aesthetic lane chosen and visible throughout.
- [ ] Maximum 2 type families, 4 weights, distinctive choices.
- [ ] One dominant color, 1-2 accents, semantic tokens defined.
- [ ] Spacing scale chosen and used consistently.
- [ ] Border-radius scale chosen and used consistently.
- [ ] Shadow/elevation scale chosen and used consistently.
- [ ] Iconography from one source, one stroke width, consistent sizes.
- [ ] Light AND dark mode designed independently.
- [ ] Motion language consistent (timing, easing, scope).
- [ ] At least one section breaks the safe centered layout.
- [ ] At least 3-5 atmospheric details that would not appear in an AI-default page.
- [ ] Voice matches visual.

## See Also

- [ui-ux.md](ui-ux.md) for the functional interaction rules.
- [motion.md](motion.md) for motion language.
- [responsive.md](responsive.md) for layout systems.
- [accessibility.md](accessibility.md) for contrast verification.
