---
title: SEO Playbook
purpose: On-page and technical SEO covering indexability, structured data, AI answer engines, hreflang, pagination signals, third-party-cookie deprecation impact, image and video sitemaps, and CrUX field mapping. Hits the Lighthouse SEO category and earns rankings.
load-when:
  task-keywords: [SEO, indexing, canonical, sitemap, robots, structured data, JSON-LD, hreflang, Open Graph, meta description, title tag, AEO]
  symptoms: [score dropped, canonical mismatch, noindex with sitemap]
prereq: SKILL.md
related: [lighthouse.md, performance.md, accessibility.md, observability.md]
size: ~561 lines
---

# SEO Playbook

On-page and technical SEO that hits the Lighthouse SEO category and earns rankings. The foundation for AI search optimization (AEO/GEO/LLMO) and programmatic SEO at scale.

## Priority Order

1. Crawlability and indexation. If a crawler cannot find or index it, nothing else matters.
2. Technical foundations. Speed, mobile, HTTPS, structure.
3. On-page optimization. Title, description, headings, content, internal links.
4. Content quality. E-E-A-T, depth, intent match.
5. Authority and links. Off-page; outside the scope of this skill.

## Indexability

### Robots.txt

| Check | Detail |
| --- | --- |
| Location and status | Lives at `/robots.txt` and returns a 200 status. |
| Do not block assets | Do not disallow `/_next/` or `/static/` (blocks asset crawl, hurts rendering). |
| Do not block the site | Do not accidentally disallow the entire site with `Disallow: /`. |
| Include sitemap line | Do not forget the `Sitemap:` line. |

```
User-agent: *
Allow: /
Disallow: /api/
Disallow: /private/

Sitemap: https://example.com/sitemap.xml
```

### XML Sitemap

- Location: `/sitemap.xml` (or `/sitemap-index.xml` for large sites with sub-sitemaps).
- Lists only: canonical URLs, 200-status URLs, indexable URLs (no `noindex`), and URLs the site actually owns (no third parties).
- For large sites, split into sub-sitemaps of 50,000 URLs each, referenced from `sitemap-index.xml`.
- `changefreq` and `priority` are advisory and mostly ignored; `lastmod` is honored when accurate.
- Submit the sitemap to the search console and verify it is accessible from `robots.txt`.

```xml
<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
  <url>
    <loc>https://example.com/</loc>
    <lastmod>2026-05-08</lastmod>
    <changefreq>weekly</changefreq>
    <priority>1.0</priority>
  </url>
</urlset>
```

Footnote (retired submission endpoints): search-engine submission and ping endpoints get retired. A 410 Gone from a submission endpoint means remove the call, not retry it. Rely on the sitemap plus the search console; do not keep POSTing to a dead endpoint.

### Image and Video Sitemap Extensions

For sites where images or video are first-class content (gallery, recipe, product, news, video platform), these extensions improve discovery.

- Required `<urlset>` headers: `xmlns:image="http://www.google.com/schemas/sitemap-image/1.1"` and `xmlns:video="http://www.google.com/schemas/sitemap-video/1.1"`.
- Up to 1,000 image entries per page URL.
- Required video fields: `thumbnail_loc`, `title`, `description`, plus one of `content_loc` or `player_loc`.
- Build the image/video sitemap from the SAME source the page uses, so it stays in sync. A drifted image sitemap (pointing at images the page no longer uses) gets coverage warnings.

```xml
<url>
  <loc>https://your-domain.com/article/topic-deep-dive</loc>
  <image:image>
    <image:loc>https://your-domain.com/images/topic-hero.jpg</image:loc>
    <image:title>Topic hero illustration</image:title>
    <image:caption>Diagram showing the relationship between A and B.</image:caption>
  </image:image>
  <video:video>
    <video:thumbnail_loc>https://your-domain.com/images/topic-thumb.jpg</video:thumbnail_loc>
    <video:title>Topic walkthrough</video:title>
    <video:description>Five-minute walkthrough of the topic.</video:description>
    <video:content_loc>https://your-domain.com/video/topic.mp4</video:content_loc>
    <video:duration>312</video:duration>
  </video:video>
</url>
```

### Meta Robots

```html
<meta name="robots" content="index, follow" />
```

- Use `noindex, nofollow` on: pages behind authentication, internal search result pages with no indexable value, duplicate/near-duplicate pages, thin content (tags, archives, pagination beyond page 1), and print stylesheets/embed-only pages.
- `X-Robots-Tag` HTTP header is equivalent and works for non-HTML resources (PDFs, images): `X-Robots-Tag: noindex, nofollow`.
- Pair the signals: a `noindex` page must be EXCLUDED from the sitemap (a `noindex` page listed in the sitemap triggers a "Submitted URL marked noindex" issue). Indexable pages get `index` plus a sitemap listing; non-indexable pages get `noindex` plus absent from sitemap.

### AI Answer Engines (AEO / GEO)

Generative search is a real discovery and citation surface. Make the site quotable, not just rankable.

- Publish `/llms.txt`: a short overview of the site plus a flat index of canonical pages (title and URL per line).
- Optionally publish `/llms-full.txt`: the load-bearing facts an answer engine quotes verbatim (what the product does, numbers, pricing, data model), in plain prose, consistent with rendered pages and structured data.
- Ensure major AI crawler user-agents are NOT blocked in `robots.txt` and that `robots.txt` references the sitemap.
- Precedence: a named user-agent group fully REPLACES the `*` group for that agent (rules do not merge), so a dedicated AI group must REPEAT every `Disallow` you still want enforced. A blanket `Allow: /` in a named group silently exposes the `/api/` or `/private/` paths the `*` group disallows. Add a dedicated group only to set DIFFERENT rules than `*`.
- State load-bearing facts in server-rendered text: an engine quotes rendered text and valid structured data, so facts only in an image, only in client-rendered JS, or only in the JSON-LD are at risk.

```
# Only needed if these agents need DIFFERENT rules than User-agent: *.
# A named group REPLACES the * group for that agent, so repeat your Disallows.
User-agent: GPTBot
User-agent: ClaudeBot
User-agent: PerplexityBot
User-agent: Google-Extended
User-agent: CCBot
User-agent: Applebot-Extended
Disallow: /api/
Disallow: /private/
Allow: /

Sitemap: https://example.com/sitemap.xml
```

The AEO check: `llms.txt` exists and lists every canonical page; `robots.txt` does not block AI user-agents and references the sitemap; facts an engine would quote appear in server-rendered HTML, not only in images or JS.

### Canonical Tag

Every indexable page has a self-referencing canonical.

```html
<link rel="canonical" href="https://example.com/pricing" />
```

| Rule | Detail |
| --- | --- |
| Absolute | Includes protocol and domain. |
| Normalized | Lowercased, with the consistent trailing-slash convention you have chosen. |
| No tracking params | No `utm`, `fbclid`, `gclid`. |
| Paginated lists | Each page canonicals to itself; do not canonicalize page 2 to page 1. |
| Filter/facet variants | Canonicalize variants to the unfiltered root only when the filter does not change indexable content. |

### Pagination Signals (post rel=prev/next deprecation)

`rel=prev` and `rel=next` were deprecated as indexing signals in 2019.

- Self-canonical every paginated page: page 2 of `/blog` carries `<link rel="canonical" href="https://your-domain.com/blog?page=2">`. Do NOT canonicalize page 2 to page 1 (drops page-2 content from the index).
- Each paginated page is indexable (no `noindex` on page 2 onwards), except when page-N pages add no incremental value (thin long-tail matches): then `noindex` and exclude from sitemap.
- Internal-link signal replaces the deprecated rel attributes: each paginated page links to siblings (previous, next, first, last, range around current) via real `<a href>` tags, not JS-firing buttons, so crawlers follow them.
- The canonical URL shape must be stable: `/blog?page=2` vs `/blog/page/2` vs `/blog/2` are different URLs; pick one and 301 the others. The sitemap lists every paginated page or none.

## Titles and Descriptions

### Title Tag

```html
<title>Page topic - Brand</title>
```

| Rule | Detail |
| --- | --- |
| Unique | Unique per page across the entire site. |
| Length | 50-60 characters before truncation in the SERP, measuring RENDERED length, not raw HTML source. |
| Decode entities first | `&amp;` is one character in the SERP, not five; `&#39;` is one, not five. A naive `.length` on the raw `<title>` over-counts and triggers false "too long" fixes, so decode entities (or read `document.title`) before comparing to 60. |
| Intent placement | Primary intent term near the beginning when natural. |
| Brand | At the end (separator: a pipe or a hyphen); some sources advise omitting the brand on home/category pages where the SERP already attaches it. |
| Hygiene | No keyword stuffing, no all caps, no emoji unless the brand allows. |
| Templated overflow | On templated/detail pages, drop the trailing Brand suffix to avoid pushing past 60 chars (otherwise the SERP title gets rewritten); keep the brand only where the base title is short (home, top-level hubs). Measure the longest realistic generated title. |

### Meta Description

```html
<meta name="description" content="One sentence stating what this page covers and the value the reader gets, in plain language, with one explicit or implicit call to action." />
```

- Unique per page.
- 140-160 characters.
- Uses the primary intent term naturally, with a clear value proposition and an implicit or explicit call to action.

## Heading Structure

- One `<h1>` per page; the H1 is the primary intent.
- Sequential headings `<h2>` to `<h3>` and onward, no skipped levels.
- Headings describe content, not styling.
- Sections labeled by their heading via `aria-labelledby` for accessibility (also reinforces structure for some bots).

```html
<main>
  <h1>Page primary intent (one H1)</h1>
  <section aria-labelledby="section-1">
    <h2 id="section-1">First section heading</h2>
    ...
  </section>
  <section aria-labelledby="section-2">
    <h2 id="section-2">Second section heading</h2>
    <h3>Sub-section</h3>
    ...
  </section>
</main>
```

## URL Structure

| Rule | Detail |
| --- | --- |
| Lowercase | URLs are lowercase. |
| Hyphenated | Hyphen-separated words. |
| Descriptive | Keyword-rich where natural. |
| No tracking params | None in the canonical. |
| Trailing slash | One consistent policy across the site. |
| Short | Prefer `/topic/specific-slug` over `/2026/05/08/post-id-123`. |
| Stable | Once published, URLs do not change; if one must change, return 301 from old to new. |

### Clean URLs on a static host (three-part contract)

Clean (extensionless) URLs on a static host require three things kept consistent:

1. Emit extensionless files.
2. Keep one consistent no-trailing-slash policy.
3. Strip the extension from BOTH the canonical AND the sitemap, so the served URL is the canonical one.

Gotcha: when the build output is flat `page.html` files (not `page/index.html` directories), naive tooling (a raw static file server, a directory-index walker, a link checker, a Lighthouse run pointed at a bare static server) 404s or serves the wrong file for an extensionless request, producing phantom failures absent on the real host. Audit against the framework's own preview server (or the edge), which reproduces the host URL rewriting. Spot check that `/page` (no extension, no slash) returns 200 with the right document.

## Open Graph and Twitter Cards

```html
<meta property="og:type" content="website" />
<meta property="og:title" content="Page topic - Brand" />
<meta property="og:description" content="..." />
<meta property="og:url" content="https://example.com/page-path" />
<meta property="og:image" content="https://example.com/og/page.png" />
<meta property="og:image:width" content="1200" />
<meta property="og:image:height" content="630" />
<meta property="og:site_name" content="Brand" />
<meta property="og:locale" content="en_US" />

<meta name="twitter:card" content="summary_large_image" />
<meta name="twitter:title" content="..." />
<meta name="twitter:description" content="..." />
<meta name="twitter:image" content="https://example.com/og/page.png" />
<meta name="twitter:site" content="@brand" />
```

- Set `og:type` per page type, do not hardcode: `article` on articles/blog posts/detail pages (product, case study, item); `website` on the home page and section hubs.
- Article-typed pages should carry `article:published_time` and `article:modified_time` when available.
- Image: 1200x630 PNG or JPEG; under 5 MB, aim for under 500 KB; text legible at thumbnail size; do not render critical info only in the image (the description should stand on its own).

### Generating Per-Template OG Images

Generate at build time so every page ships a distinct, on-brand 1200x630 image.

- Pipeline: render a template to SVG, then rasterize the SVG to PNG, as a prerendered per-template endpoint, for zero runtime cost on static hosts.
- Font gotcha: the SVG text layout step needs a `ttf`, `otf`, or `woff` font and cannot read `woff2`. Source a `woff` or `ttf` explicitly, since variable-font packages often ship `woff2` only.
- Bundler gotcha: a native-addon rasterizer must be marked external to the bundler (the SSR external list plus the dependency-optimizer exclude list), or the build fails trying to bundle a binary.
- A single designed static image is a fine default and home-page image; generate per-template images for the long tail (articles, items, detail pages), kept under 500 KB and legible at thumbnail size.

## Structured Data (JSON-LD)

Add to `<head>` as one `<script type="application/ld+json">` block per type, or a graph block with multiple types.

### Organization (site-wide, home and about)

```html
<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "Organization",
  "name": "Brand",
  "url": "https://example.com",
  "logo": "https://example.com/logo.png",
  "sameAs": [
    "https://twitter.com/brand",
    "https://github.com/brand",
    "https://linkedin.com/company/brand"
  ]
}
</script>
```

### WebSite with SearchAction (home only)

```html
<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "WebSite",
  "url": "https://example.com",
  "potentialAction": {
    "@type": "SearchAction",
    "target": "https://example.com/search?q={search_term_string}",
    "query-input": "required name=search_term_string"
  }
}
</script>
```

### BreadcrumbList (every interior page)

```html
<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "BreadcrumbList",
  "itemListElement": [
    { "@type": "ListItem", "position": 1, "name": "Home", "item": "https://example.com" },
    { "@type": "ListItem", "position": 2, "name": "Section", "item": "https://example.com/section" },
    { "@type": "ListItem", "position": 3, "name": "Current page" }
  ]
}
</script>
```

Build this JSON-LD from the SAME ordered array that renders the visible breadcrumb UI, so the structured data and the on-page trail never disagree (mismatched breadcrumbs are a common Rich Results warning). Emit it on every interior page that shows a breadcrumb, including legal and utility pages.

### Article (blog posts, news)

```html
<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "Article",
  "headline": "...",
  "description": "...",
  "image": "https://...",
  "datePublished": "2026-05-08",
  "dateModified": "2026-05-09",
  "author": { "@type": "Person", "name": "...", "url": "..." },
  "publisher": { "@type": "Organization", "name": "Brand", "logo": { "@type": "ImageObject", "url": "..." } },
  "mainEntityOfPage": "https://..."
}
</script>
```

### FAQPage (FAQ sections)

```html
<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "FAQPage",
  "mainEntity": [
    {
      "@type": "Question",
      "name": "Question text exactly as displayed?",
      "acceptedAnswer": { "@type": "Answer", "text": "Answer text exactly as displayed." }
    }
  ]
}
</script>
```

- Only use `FAQPage` when the FAQ is genuinely on the page and visible to users.
- SERP-feature note: FAQ rich results were rolled back (limited, then effectively retired between 2023 and 2026) and HowTo rich results were retired (2023). Keep accurate, parity-correct `FAQPage`/`HowTo` markup (still valid, feeds AI answer engines) but do not plan around them as winnable rich-result features.
- Highest-ROI structured data for most sites today: Organization, WebSite, BreadcrumbList, Article, Product or SoftwareApplication, and VideoObject.

### Product / SoftwareApplication / HowTo / Recipe / Event / etc.

Use the schema.org type that best matches the page content, and validate every JSON-LD with the Rich Results Test before declaring it complete.

### Never Fabricate Structured Data

Structured data is a verifiable, penalizable claim. Emit only what is real.

- No invented `aggregateRating` or `review`. Add a rating object only when real, on-site or first-party reviews back it; fabricated ratings risk manual action and are stripped when they do not match visible content.
- `sameAs` lists only profiles that actually exist and belong to the entity. Build from a real list and emit nothing when empty (no placeholder social URLs).
- Emit `WebSite` `SearchAction` only when a working search endpoint exists at the `target` URL. A `SearchAction` pointing at a non-existent search is a defect, not an enhancement.
- Parity rule: every value in the JSON-LD should be derivable from visible page content. If it is not on the page, it does not belong in the markup.

### Validation

| Tool | Use |
| --- | --- |
| Rich Results Test (https://search.google.com/test/rich-results) | Renders JS, finds JSON-LD, shows what can be extracted. |
| Schema.org Validator (https://validator.schema.org/) | Structural validation. |
| Lighthouse SEO category | Does NOT validate JSON-LD content; use the dedicated tools above. |

## Image SEO

- Descriptive file names: `topic-comparison-chart.png`, not `IMG_2034.png`.
- `alt` text describing the image (for accessibility AND SEO).
- Compress to AVIF/WebP per [performance.md](performance.md).
- For images that are themselves content (infographics, charts), provide a long-form description nearby.
- For decorative images, `alt=""`; search engines understand this signal.

## Internal Linking

- Every important page should be reachable within 3 clicks of the home page.
- Use descriptive anchor text, not "click here", "read more", "learn more"; use the destination's title or a topical phrase.
- Avoid orphan pages (no internal links pointing to them).
- Avoid over-linking (every navigation item duplicated 5x in body).
- Use a hub-and-spoke model: cluster pages link to a central topical hub.

## Hreflang (Multilingual)

For each locale variant of a page, list all variants including the page itself.

```html
<link rel="alternate" href="https://example.com/pricing" hreflang="en" />
<link rel="alternate" href="https://example.com/de/pricing" hreflang="de" />
<link rel="alternate" href="https://example.com/ja/pricing" hreflang="ja" />
<link rel="alternate" href="https://example.com/pricing" hreflang="x-default" />
```

- Mutual: every variant lists every other variant. Self-referencing: each variant lists itself.
- Include `x-default` for the unmatched/default version.
- Use valid BCP 47 codes (`en`, `en-US`, `de`, `pt-BR`).

### URL Strategy: ccTLD vs Subdirectory vs Subdomain

Pick once and stay.

| Strategy | Example | Geo signal | Tradeoffs |
| --- | --- | --- | --- |
| ccTLD | `your-brand.de`, `your-brand.jp` | Strongest country signal (the TLD itself geo-targets) | Highest cost (separate domain, separate SEO authority per TLD); needed for jurisdictions that require local presence |
| Subdirectory | `your-brand.com/de/`, `your-brand.com/ja/` | No automatic country signal; set via search console international targeting or via `hreflang` | Cheapest; parent domain authority flows down; preferred default for most sites |
| Subdomain | `de.your-brand.com`, `ja.your-brand.com` | Treated as a separate site; geo signal via search console settings | Mid cost; authority partially inherits; harder to maintain consistent UX |

Do not mix strategies (one ccTLD, several subdirectories, and a stray subdomain). Audit the inventory before adding the next locale.

### Content-Language vs hreflang

- `Content-Language: en-US` HTTP header (and `<meta http-equiv="content-language">` and `<html lang>`) declares the language of the CURRENT page. Useful for content-negotiation proxies and screen readers, but not a primary SERP-targeting signal.
- `hreflang` declares the FULL SET of language and region variants and is the SERP-targeting signal used to pick which variant to surface. When `hreflang` and `Content-Language`/`<html lang>` disagree, `hreflang` is trusted. Set both correctly.

### Geo-IP Redirects Are a Policy Violation

Auto-redirecting based on the user's IP location is a policy violation: the crawler crawls from US IPs, so it never sees the localized variants and may de-rank or de-index them.

- Show a banner offering the localized variant; do not force-redirect.
- Persist the user's locale choice in a cookie or in the URL; honour it on the next visit.
- For multi-region commerce (currency, shipping), let the user pick the region from a visible control; do not infer-and-redirect.

## Mobile-Friendly

- Responsive design (no separate `m.` site).
- `<meta name="viewport" content="width=device-width, initial-scale=1">`.
- No horizontal scroll at 320px width.
- Touch targets >= 44x44 CSS pixels.
- Body text >= 16px on mobile (avoids iOS auto-zoom).
- Same content as desktop (mobile-first indexing).

## Page Speed (CrUX, not Lighthouse)

The search console uses CrUX (Chrome User Experience Report) p75 over 28 days for the Page Experience signal. Lighthouse is a lab proxy; CrUX is the real signal.

Targets (CrUX p75):

| Metric | "Good" threshold |
| --- | --- |
| LCP | <= 2.5s |
| INP | <= 200ms |
| CLS | <= 0.1 |

A page can be Lighthouse 95 in the lab and still "Needs Improvement" in CrUX if real users have slower devices/networks. Instrument the field with a `web-vitals` collector and track p75.

### Search Console Page Experience and CrUX Mapping

- 28-day rolling p75: CrUX aggregates the trailing 28 days of real-user data. A fix shipped today shows up incrementally; full credit lands about 28 days later when the pre-fix window has rolled off. The dashboard after a ship is mostly old code for a month.
- CrUX origins map to URL groups, not individual URLs: the search console groups URLs by template (homepage, article, product page) and reports CWV per group. The group's p75 triggers the warning, and a fix must land on enough pages in the group to move the p75.
- The PageSpeed Insights API exposes the same CrUX data per URL and per origin. Pull it daily into your observability stack to watch the trend without waiting for the UI to refresh. See [observability.md](observability.md) for the field-monitoring pipeline.

## Third-Party-Cookie Deprecation Impact

Chrome's third-party cookie deprecation (3PCD), reversed and re-staged through 2024 to 2026, has already moved Safari and Firefox to no-3PC by default. Plan for it as the baseline.

### What Breaks When 3PCs Are Blocked

| Surface | Breakage | Mitigation |
| --- | --- | --- |
| Cross-domain analytics | GA4, Adobe Analytics lose session stitching across subdomains/partner domains | First-party measurement: server-side tagging, GA4 Measurement Protocol, first-party `_ga` cookie on your domain |
| A/B testing platforms | Optimizely, VWO setting assignment cookies as third parties lose stable assignment | Move assignment to a first-party cookie set by your server, or to a server-rendered query parameter |
| Cross-domain embeds | YouTube, Vimeo, Twitter, social embeds lose logged-in state, view tracking, recommendations | The embed still loads; personalization does not |
| Federated auth iframes | OAuth/SSO silent re-auth and session-bridge cookies break; user must re-authenticate | Storage Access API + CHIPS migration path (below) |
| Cross-site retargeting and attribution | Display ads, retargeting pixels, last-click cross-domain attribution lose fidelity | Privacy Sandbox APIs (Topics, Protected Audience, Attribution Reporting) where available |

### What Still Works

- First-party cookies on your own origin: the browser trusts cookies set by the same registrable domain the user is visiting.
- Server-side analytics: logs, server-side GA4 via the Measurement Protocol, edge-collected events with a first-party cookie carrier.
- First-party identifiers in URLs: magic-link tokens, signed query parameters, server-rendered IDs.

### Storage Access API and CHIPS

- Storage Access API: a third-party iframe (help widget, auth iframe) calls `document.requestStorageAccess()` in response to a user gesture; the browser prompts the user; granted access lets the iframe read and write its own cookies. Use for federated auth iframes and consented embeds; design for the permission-prompt friction. See [auth.md](auth.md) for the auth-flow integration.
- CHIPS (Cookies Having Independent Partitioned State): a `Set-Cookie` with the `Partitioned` attribute stores a separate cookie per top-level site that embeds the third party. Useful for third-party widget session state (support chat, embed) without cross-site tracking; invisible to other top-level sites that embed the same third party.
- Sites hosting embedded surfaces (a SaaS dashboard users embed in intranets, a chat widget) should adopt `Partitioned` cookies now, so the same widget code works across legacy and post-3PCD browsers.

```http
Set-Cookie: __Host-session=abc; Path=/; Secure; HttpOnly; SameSite=None; Partitioned
```

## Content Quality (E-E-A-T)

Quality raters apply the E-E-A-T framework:

| Pillar | Signal |
| --- | --- |
| Experience | First-hand experience visible (case studies, original screenshots, "I tested this and..."). |
| Expertise | Author has demonstrable subject knowledge. |
| Authoritativeness | Cited by others, recognized in the space. |
| Trustworthiness | Accurate facts, transparent business, contact info, privacy policy, secure (HTTPS). |

For YMYL (Your Money Your Life) topics (medical, legal, financial), expertise and trustworthiness are critical: author bios with credentials, editorial policy, sources cited inline, updated/published dates, contact and About pages. For non-YMYL, expertise is useful but less strict.

## Render Primary Content on the Server

Crawlers (search and AI) index what is in the HTML response. Text that appears only after client-side hydration (inside an island, a partial-hydration region, or any JS-only render) may never be indexed and is invisible to crawlers that do not execute JS.

- Server-render (SSR or SSG) all primary content and any text inside interactive components, then hydrate for interactivity.
- For tabbed content (for example terms and privacy panels), server-render every panel's content in static HTML so all of it is indexable, then enhance into tabs (roving tabindex, a deep-linkable active tab; see accessibility.md Tabs).
- Verify by viewing source (not the DevTools DOM, which shows post-JS state) or by loading with JavaScript disabled. Every load-bearing sentence, price, and heading must be present.
- A partially-hydrated widget rendering only its active state in static HTML (for example a toggle showing one of three values) is acceptable for the visible state, but put the full set of facts somewhere server-rendered (a static list, an `sr-only` block, or the structured data and prose).

## Common SEO Mistakes

- Same title or same description on every page.
- No canonical, or canonical pointing to a different URL by mistake.
- Multiple H1s per page; skipped heading levels.
- Important content hidden behind JS that does not render server-side, or in images without alt text.
- Slow LCP because the hero image is not optimized; CLS from a late-loading hero or font swap.
- Internal links with anchor text "click here" or "read more".
- Sitemap listing 404 or `noindex` URLs.
- `robots.txt` blocking CSS or JS (the crawler needs to render the page).
- `noindex` on a page that should be indexed (typo or stale config).
- Tracking params in the canonical (creates infinite duplicates).
- Migrating URLs without 301 redirects.
- Blocking the staging domain in `robots.txt` while allowing production by mistake.

## Pre-Publish SEO Checklist

For every public-visible page:

- [ ] Unique `<title>` 50-60 chars
- [ ] Unique `<meta name="description">` 140-160 chars
- [ ] One `<h1>` matching primary intent
- [ ] Sequential headings, no skipped levels
- [ ] Self-referencing `<link rel="canonical">`
- [ ] `<meta name="robots" content="index, follow">` if indexable; `noindex` if not
- [ ] Open Graph tags (og:title, og:description, og:image, og:url, og:type)
- [ ] Twitter card tags
- [ ] Lang attribute on `<html>`
- [ ] Structured data validated via Rich Results Test (where applicable)
- [ ] Image alt text on every meaningful image
- [ ] Internal links use descriptive anchor text
- [ ] Page reachable from home in <= 3 clicks
- [ ] Listed in sitemap.xml (if indexable)
- [ ] HTTPS, no mixed content
- [ ] Mobile-friendly (responsive, viewport meta, no horizontal scroll)
- [ ] CrUX p75 LCP/INP/CLS in "Good" zone (verify after 28 days of traffic)
- [ ] No render-blocking content critical to indexing (JS is rendered but slowly; SSR/SSG preferred for primary content)

## See Also

- [lighthouse.md](lighthouse.md) for the SEO category audits in detail
- [performance.md](performance.md) for Core Web Vitals optimization
- [accessibility.md](accessibility.md) for the accessibility/SEO overlap (alt text, headings, labels)
- [observability.md](observability.md) for the CrUX and field-monitoring pipeline
