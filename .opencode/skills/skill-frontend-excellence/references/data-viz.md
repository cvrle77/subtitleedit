---
title: Data Visualization
purpose: Chart selection, axes, color, accessibility for charts and tables, Canvas vs SVG vs WebGL rubric, timezone and DST handling, annotations, streaming chart performance, geographic projections. Every rule pairs a principle with a concrete threshold.
load-when:
  task-keywords: [chart, data viz, axis, legend, colorblind, Canvas, SVG, WebGL, timezone, DST, accessibility, responsive]
  symptoms: [contrast fail, slow page, slow interaction]
prereq: SKILL.md
related: [accessibility.md, responsive.md, motion.md, performance.md]
size: ~512 lines
---

# Data Visualization

Library-agnostic guidance on charts, tables, and data-dense interfaces.

## The Hierarchy of Data Display

Walk this list; the first that fits is the right choice. If a chart and a table or summary number both work, use the simpler thing.

| Need | Display |
|------|---------|
| One number with context | One number, e.g. "$48,231 monthly recurring revenue", over a one-point chart |
| Comparison or ranking (top 5, top 10) | List with bars (beats a pie chart) |
| Trend over time | Line chart |
| Distribution | Histogram or box plot |
| Composition | Stacked bar or treemap |
| Relationship between two variables | Scatter plot |
| Geographic | Map |
| Detailed records | Table |

## Choosing the Right Chart

| Data | Best chart | Avoid |
|------|-----------|-------|
| Single value | Stat card with sparkline | Chart |
| Trend over time | Line chart | Pie chart |
| Compare 2-10 categories | Bar (horizontal if labels long) | Pie chart |
| Compare 10-30 categories | Horizontal bar | Vertical bar (cramped on mobile) |
| Compare > 30 categories | Top N + "Other" or table | Bar chart with all |
| Composition (parts of whole, 2-5 categories) | Pie or donut | Pie with > 5 |
| Composition over time | Stacked area | Pie carousel |
| Distribution | Histogram or box plot | Bar chart of raw values |
| Two-variable relationship | Scatter plot | Two side-by-side bar charts |
| Multiple metrics over time | Multi-line, small multiples | Stacked line (hides individual values) |
| Geographic | Choropleth map | Pie chart |
| Hierarchical composition | Treemap or sunburst | Stacked bar |
| Funnel (sequential drop-off) | Funnel chart | Bar chart |
| Network / relationships | Graph / network diagram | Table |

### Pie / donut rules

- Maximum 5 categories; beyond that, switch to bar.
- Slices sum to 100%.
- Order by size (largest first, clockwise from 12 o'clock).
- Direct-label slices when space allows; legend if not.
- Never 3D pie.
- Don't compare two pies side by side; use a stacked bar.

### Bar chart rules

- Bar baseline at zero, always.
- Vertical bars for short labels, horizontal for long.
- Order by value (descending) unless there is a natural order (months, ranks).
- Spacing between bars: about 30-40% of bar width.
- Categorical x-axis on horizontal bars; numerical on vertical bars.

### Line chart rules

- Time on the x-axis.
- Maximum 5 lines before they tangle.
- Distinct color or dash pattern per line.
- Direct-label lines at the right end where space allows.
- Connect data points; don't put markers on every point unless you want emphasis.

## Axes

### Labels and ticks

- Always label both axes.
- Include units in the axis label or as a tick-label suffix ("$", "%", "ms").
- Don't rotate labels 90 degrees on horizontal bars; use horizontal-bar layout instead.
- Auto-skip labels when they overlap (every other, or larger interval).
- Use round tick numbers (10, 20, 50, 100), not 7, 23, 47.
- 4-7 ticks per axis is usually right.
- Y-axis baseline at zero for bar charts; truncating exaggerates differences.
- For dramatic small variations, add a "broken axis" indicator (zigzag) to be honest about it.

### Time axis granularity

| Range | Granularity |
|-------|-------------|
| < 1 day | hours |
| 1-30 days | days |
| 1-12 months | weeks or months |
| 1-5 years | months or quarters |
| > 5 years | years |

- Allow user to change granularity (day/week/month).
- Show the date format consistent with the user's locale.

### Timezone and DST handling

- Store every timestamp in UTC across server, database, transport layer, JSON payload, log line, and exported CSV. A timestamp without a timezone is a defect.
- Display in the user's local timezone using `Intl.DateTimeFormat` with `timeZone` defaulting to `Intl.DateTimeFormat().resolvedOptions().timeZone`. For meaningful dashboards (operations, finance), expose a timezone picker and persist the choice.
- Use ISO 8601 with a timezone offset: `2026-03-08T07:00:00Z` (UTC) or `2026-03-08T02:00:00-05:00` (with offset). `2026-03-08 02:00:00` (no offset) silently falls back to the parsing machine's local timezone, which is how a server timestamp displays hours off.
- DST spring-forward jumps the clock from `01:59:59` to `03:00:00` local, leaving an empty `02:00` bucket. Bucket by UTC and label by local, or bucket by local and draw the DST transition as a vertical reference line.
- DST fall-back day has 25 hours with `01:00` to `02:00` happening twice; sum-by-day double-counts that hour, so always sum from UTC.
- Across DST boundaries, choose UTC ticks for operations or local ticks for analytics to match the user's mental model.

### Number formatting

- Locale-aware via `Intl.NumberFormat`.
- Compact notation for large numbers: `1.2M`, `4.7K`.
- Currency: respect locale and currency code.
- Percentages: 1 decimal (`23.4%`) for finance, no decimals (`23%`) for categorical.

## Color in Charts

| Encoding | Palette |
|----------|---------|
| Single series | Brand primary or a single neutral |
| Sequential (ordered, low to high) | Single hue light to dark, perceptually uniform scale (viridis, magma) |
| Diverging (centered, positive vs negative) | Two-hue diverging palette (blue-white-red) |
| Categorical (no order) | Qualitative palette, distinct hues, colorblind-safe |

- Add a slight gradient (top to bottom) only on filled area charts; never on bar charts (looks gimmicky).

### Colorblind-safe palettes

- Avoid red/green pairs alone.
- Tol's "bright" palette: blue, red, green, yellow, cyan, purple, gray (distinguishable in most colorblind types).
- ColorBrewer's "Set2" or "Dark2".
- Always supplement color with shape/pattern/label so meaning isn't lost.

### Pattern alongside color

For stacked bars or pies, color carries the primary signal and a subtle hatching/dot pattern carries redundancy:

```css
.series-a { fill: var(--color-1); }
.series-b { fill: var(--color-2); background-image: repeating-linear-gradient(45deg, transparent, transparent 4px, rgba(0,0,0,0.1) 4px, rgba(0,0,0,0.1) 8px); }
```

### Contrast

- Data marks (bars, lines): at least 3:1 against the chart background.
- Text labels: 4.5:1 against the chart background.
- In dark mode, lighten the data marks; don't just keep light-mode colors.

## Legends

- Always show a legend for multi-series charts.
- Place near the chart, not detached below a scroll fold.
- Order matches data (largest to smallest, or alphabetical).
- For interactive charts, legends are clickable to toggle series visibility.
- Direct labeling (at the line/bar) often beats a separate legend.

## Tooltips

- Show on hover (desktop) or tap (mobile).
- Show the exact value (numeric labels are too small for crowded charts).
- Label which series, the x value, and the y value.
- Anchor near the cursor; don't make the user scan.
- Persist long enough to read; don't dismiss on tiny mouse movement.
- Content must be reachable via keyboard (focus on data point reveals tooltip).

Content format: x-axis label header, then per-series lines:

```
[X-axis label, e.g., March 12]
Series A: 12,400 (+18%)
Series B:  8,200 (-3%)
```

## Direct Labeling

For small datasets, label values directly instead of forcing the eye to the axis:

- Bar chart with 3-5 bars: value on or above each bar.
- Line chart: value at the rightmost point of each line.
- Pie chart: values inside slices (or outside with a leader line if too small).

## Annotations and Reference Lines

Layering rule: data marks always on top, annotations behind, except labels which sit above data marks to stay readable. Annotations carry a 3:1 contrast budget against the chart background and a color distinct from any data series color (a target line in the same color as a data series is a defect). Build annotations from a structured list (source of truth) so the chart and the data pipeline can never disagree.

| Type | How |
|------|-----|
| Threshold bands | Shaded horizontal region (SLA 99 to 100 percent, page load good zone 0 to 2.5s) as a low-opacity `<rect>` behind the data marks; label the band, not just the boundary |
| Target lines | Single dashed horizontal reference line, distinct color, label at the right end ("Target: 250ms"); line behind data marks, label above or to the side |
| Deploy / event markers | Vertical lines at meaningful x-values, color/dash differing from data lines; label rotated 90 degrees along the line or a small icon at top with hover tooltip |
| Anomaly callouts | Circle plus arrow plus short text at a specific point ("Spike: payment system outage"); visible in the static chart, hover tooltip for the full story |
| Range brackets | Horizontal bracket spanning an x-axis range, label at the top of the bracket |

## Maps and Geographic Projections

Every flat map distorts something. The defect: a Mercator world map for a statistical comparison, accidentally making Greenland look bigger than Africa. Name the projection in code (`d3.geoEqualEarth()`, not "the default projection"); the default in most libraries is Mercator, which is rarely what a statistical chart wants.

| Projection | Preserves / distorts | Use for |
|------------|----------------------|---------|
| Mercator | Preserves shape and direction; inflates area at high latitudes (Greenland appears the size of Africa) | ONLY street maps and navigation; never statistical comparisons across latitudes |
| Equal Earth | Preserves area; mild shape distortion; designed 2018 for statistical maps | Modern default for world-scale choropleths and heatmaps |
| Albers Equal Area | Preserves area for a specific region (Albers USA, Albers Europe) | Standard for US state-level and country-level statistical maps |
| Robinson, Winkel Tripel | Compromise; distort everything slightly (Robinson and Winkel Tripel are National Geographic Society defaults) | When neither shape nor area is the primary question |
| Orthographic | Earth as a sphere from a fixed viewpoint | Animated globe views or hero illustrations, not analysis |

## Animation

| Phase | Timing |
|-------|--------|
| Entrance: bars rise from baseline | 400-600ms, staggered 30-50ms per bar |
| Entrance: lines draw left to right | 600-800ms total |
| Entrance: pies sweep from 12 o'clock clockwise | 800-1000ms |
| Update: old to new values, ease-out | 300-500ms |

- On update, animate the path's `d` attribute or use a library helper for smooth transitions.
- Disable on `prefers-reduced-motion: reduce` (skip entrance, show data immediately), on live-updating views (just update), and on print.

```js
const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
chart.options.animation = reduced ? false : { duration: 600, easing: 'easeOutQuart' };
```

## Accessibility

See accessibility.md for full chart and table a11y treatment.

### Screen readers

- Concise summary near the chart describing the key insight in one sentence (the trend or comparison).
- A data table inline or in a `<details>` toggle, with the same data the chart shows.
- For complex charts, use `aria-describedby` to link the chart to a longer description.

```html
<figure>
  <figcaption>Chart caption describing what is plotted</figcaption>
  <div class="chart" role="img" aria-labelledby="chart-summary">
    <svg>...</svg>
  </div>
  <p id="chart-summary">
    One sentence stating the key insight, in plain language, with the headline numbers.
  </p>
  <details>
    <summary>View data table</summary>
    <table>
      ...
    </table>
  </details>
</figure>
```

### Keyboard

- Interactive elements (data points, legend items) must be focusable.
- Arrow keys navigate between data points (left/right for adjacent, up/down for series).
- Enter/Space activates (toggles legend, drills down).
- Esc closes any popover or expanded view.

### Touch targets

Interactive chart elements need at least 44x44 touch area. For dense charts, expand the hit area beyond the visual element. Visible dot 4px, touch target 22px radius (44px diameter):

```html
<circle cx="100" cy="50" r="4" />
<circle cx="100" cy="50" r="22" fill="transparent" pointer-events="all" />
```

## Responsive Charts

See responsive.md for chart reflow patterns.

- Vertical bars become horizontal bars (long labels easier).
- Multi-line charts collapse to top-N or to small multiples.
- Pie charts stack labels below.
- Axis ticks reduce to 3-5 from 7-10.
- For SVG charts, listen to resize and re-render with new dimensions, throttling to 100-200ms.

```css
.chart-container { container-type: inline-size; }

@container (max-width: 480px) {
  .chart .legend { display: none; }
  .chart .axis-label { font-size: 0.75rem; }
}
```

## Loading and Error States

- Loading: show a skeleton or shimmer matching the chart's eventual shape; don't show a blank axis frame (looks broken).
- Empty data: show a message ("No data for this period yet."), optionally a greyed-out placeholder chart with the message overlaid, plus an action ("Try a different range" or "Connect data source").
- Error: show an error message with retry ("Couldn't load chart. Retry?"); don't show a broken/clipped chart.

## Large Datasets

For 1000+ data points:

- Aggregate: group by hour/day/week to reduce point count.
- Sample: show every Nth point.
- Summarize: show the trend, provide drill-down for detail.
- Virtualize: for long tables, only render visible rows.
- Server-side processing: aggregate on the server, ship summarized data.
- Web Workers: move client-side heavy aggregation off the main thread.

Anti-pattern: shipping 50,000 points to a `<canvas>` chart and expecting smooth interaction.

### Canvas vs SVG vs WebGL: rendering rubric

Pick the renderer by data-point count first, then by interaction model.

| Renderer | Data point ceiling | Interaction model | Accessibility | Use when |
|----------|-------------------|------------------|---------------|----------|
| SVG | About 500 nodes | DOM-native: each mark is an element, click and hover are free | Each `<rect>` or `<circle>` is a DOM node, focusable, screen-reader addressable | Bar charts, small line charts, rich per-mark interaction (tooltip, click-to-drill, focus-to-announce), DevTools-inspectable |
| Canvas | About 100,000 points | Pixel-based: hit-testing needs a hit-region map or quadtree; no DOM cost | Inherently inaccessible; pair with a `<table>` fallback or `role="img"` plus `aria-label` summary | Time-series with thousands of points, dense scatter clouds, heatmaps |
| WebGL | Above 100,000 points or when shaders are needed | Pixel-based, GPU-accelerated; same hit-testing problem as Canvas plus GPU memory budget | Same problem and fallback as Canvas | Million-point scatter, real-time streaming dashboards with many series, per-mark shading (instanced rendering, density estimation) |

Rule of thumb:

- Under 500 marks: SVG. DOM tax negligible, interaction free, accessibility best.
- 500 to 100,000 marks: Canvas (SVG drops frames at hover). Add a quadtree for hover hit-testing.
- Above 100,000 marks or shader effects: WebGL via a library that handles the boilerplate (deck.gl, regl-based wrappers); expect to write your own picking pipeline.

Hybrid: render the data layer in Canvas or WebGL for speed, render axes, legends, and overlay annotations in SVG on top so they stay accessible; both layers share a coordinate system.

## Tables

### Anatomy

```html
<table>
  <caption>Table caption describing the data</caption>
  <thead>
    <tr>
      <th scope="col">Row category</th>
      <th scope="col">Metric A</th>
      <th scope="col">Metric B</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <th scope="row">Row label</th>
      <td>4,200,000</td>
      <td>+12%</td>
    </tr>
    ...
  </tbody>
</table>
```

- `<caption>` describes the table.
- `<th scope="col">` for column headers, `<th scope="row">` for row headers.
- For complex tables (multi-level headers), use `headers="..."` to associate cells with their headers.

### Visual rules

- Tabular figures (`font-variant-numeric: tabular-nums`) for numeric columns.
- Right-align numbers, left-align text, center-align symbols/icons.
- Subtle low-contrast row dividers rather than alternating row colors (which look dated and reduce readability).
- Sticky header for long tables (`position: sticky` on `<thead>`).
- Hover state on rows for navigability.

### Sorting

- Click column header to sort ascending; click again for descending.
- Show current sort direction with an arrow icon.
- Use `aria-sort="ascending|descending|none"` on the column header.
- Multi-column sort: hold Shift to add additional sorts.

### Filtering

- Per-column filters: dropdown, search, or range slider depending on data type.
- Active filters visible as chips above the table.
- "Clear all" link.

### Selection

- Checkbox per row.
- Select-all checkbox in the header with an indeterminate state for partial selection.
- Show count of selected rows.
- Bulk actions appear when rows selected.

### Pagination vs infinite scroll

- Pagination for tables: predictable, accessible, supports deep linking.
- Infinite scroll for activity feeds: low-stakes, sequential reading.
- Don't mix.

### Mobile tables

For wide tables on small screens:

- Horizontal scroll with sticky first column (the row identifier).
- Card view: each row becomes a card, fields as label/value pairs.
- Collapse: show a few key columns, expand row to see all.

```css
.table-scroll {
  overflow-x: auto;
  scrollbar-width: thin;
}

.table-scroll table {
  min-inline-size: 720px;
}
```

## Stat Cards / KPI Tiles

The most important number gets a stat card. Anatomy and order:

```
[ Stat Card ]
[ Label: short description of the metric ]
[ Value: the formatted number, largest type ]
[ Change: direction icon + delta vs comparison period ]
[ Sparkline: subtle line chart of recent history ]
```

- Label first (small, muted).
- Value largest (display type).
- Change indicator with direction icon and color (green up, red down), paired with text so color isn't the only signal.
- Optional sparkline: subtle (single neutral color), no axis.
- Optional click affordance to drill down.

## Real-Time Updates

For live-updating data:

- Update without animation (or very subtle pulse on changed values).
- Don't shift other content; new rows arrive at the top with a brief highlight.
- Allow pause/resume so users can read without flicker.
- Show a last-updated timestamp.

### Streamed chart performance

Four composing techniques keep a chart smooth at high stream rates: rAF-coalesce caps the render rate, ring buffers cap the data size, OffscreenCanvas moves the work off-main-thread, visibility-driven throttling drops to background-rate.

- rAF-coalesce updates: buffer incoming events, redraw once per `requestAnimationFrame`, since the screen refreshes at 60 Hz (or 120 Hz on high-refresh displays) and rendering faster wastes CPU.

  ```js
  let pending = [];
  let scheduled = false;

  socket.addEventListener('message', (event) => {
    pending.push(JSON.parse(event.data));
    if (!scheduled) {
      scheduled = true;
      requestAnimationFrame(flush);
    }
  });

  function flush() {
    scheduled = false;
    for (const point of pending) ringBuffer.push(point);
    pending = [];
    renderChart(ringBuffer);
  }
  ```

- Ring buffers for fixed-size sliding windows: a fixed-size array with a head pointer so the data structure does not grow unbounded; old points fall off the back as new push on the front.
- OffscreenCanvas plus transfer to main: move rendering to a Web Worker holding an `OffscreenCanvas`, composite the result on the main thread so it stays responsive for input.
- Cap update rate to display refresh: render at 60 Hz even at 1000 messages/second; throttle further (30 Hz, 15 Hz) when off-screen, in a background tab (`document.visibilityState === 'hidden'`), or when the user interacts elsewhere.

## Common Data Viz Mistakes

- Pie chart with > 5 categories.
- Bar chart with truncated y-axis (exaggerates differences).
- Line chart with too many series tangling.
- 3D charts.
- Tooltip showing only a value with no series/x-axis context.
- Color-only encoding (red good / red bad without text or icon).
- Chart that is a bar chart on desktop and a different chart entirely on mobile (jarring).
- Chart with no axis labels.
- Chart with no units.
- Chart that loads with a flicker (no skeleton, then sudden content).
- Animation that runs on every data refresh (every 5s in a live data view).
- Chart in dark mode with light-mode colors.
- Inaccessible chart with no text alternative.
- Tables without sorting or filtering on data-heavy views.
- Tables with row colors so loud they distract from data.
- Numbers without locale formatting (US format in a German UI).
- Tooltip that doesn't work on touch.

## Self-Healing for Data Viz

Before declaring work complete:

- [ ] Right chart for the data
- [ ] Bar charts have zero baseline
- [ ] Pies have <= 5 categories
- [ ] Axes labeled with units
- [ ] Colorblind-safe palette
- [ ] Color is never the only signal (paired with shape/pattern/label)
- [ ] Legend shown and accessible
- [ ] Tooltip works on hover AND tap
- [ ] Chart has text alternative (summary or table)
- [ ] Interactive elements keyboard-accessible
- [ ] Touch targets >= 44x44
- [ ] Responsive: simplifies/reflows on small screens
- [ ] Loading skeleton matches eventual layout
- [ ] Empty state has a message and action
- [ ] Error state has retry
- [ ] Numbers locale-formatted
- [ ] Animation respects `prefers-reduced-motion`
- [ ] Tested in light AND dark mode
- [ ] Tables: sorting, sticky header, mobile-adaptive

## See Also

- [accessibility.md](accessibility.md) for chart and table accessibility
- [responsive.md](responsive.md) for chart reflow patterns
- [motion.md](motion.md) for chart animation
- [performance.md](performance.md) for streaming and large-dataset budgets
