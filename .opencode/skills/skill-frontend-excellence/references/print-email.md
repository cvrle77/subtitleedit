---
title: Print and Email
purpose: Print stylesheets for invoices, receipts, and statements, plus transactional and marketing email HTML. Both surfaces demand a stricter rulebook than the screen.
load-when:
  task-keywords: [print, email, "@page", page-break, transactional email, Outlook, accessibility, contrast]
  symptoms: [score dropped, broken on Safari, dark mode broken]
prereq: SKILL.md
related: [design.md, ui-ux.md, accessibility.md, seo.md]
size: ~255 lines
---

# Print and Email

Print and email are first-class shipping surfaces. Invoices print, receipts save as PDF, transactional email lands in clients that have not changed their rendering engine in 15 years.

Core principle: design for the destination surface (print page, email client), not for the screen you wrote it on.

## Print Stylesheets

### Media block, chrome, content

| Principle | Check |
| --- | --- |
| Every print-relevant page has a `@media print` block | Block hides chrome and exposes content |
| Hide chrome | Hide: navigation, search bars, share buttons, floating action buttons, cookie banners, toast regions, social proof badges, anything below-the-fold unrelated to the printed artifact |
| Show link destinations | Show full URLs in links so the printed page is self-documenting |
| Suppress redundant URL suffix | Skip the printed URL suffix for fragment links (`#section`), `mailto:`, and `tel:`; the surface text already conveys the destination |

```css
@media print {
  nav, header, footer, aside,
  .banner, .share-button, .floating-action,
  .skip-link, .cookie-consent, .toast-region {
    display: none !important;
  }

  body {
    color: #000;
    background: #fff;
    font-size: 11pt;
    line-height: 1.4;
  }

  main {
    max-width: none;
    padding: 0;
  }

  a[href]:not([href^="#"]):not([href^="javascript:"])::after {
    content: " (" attr(href) ")";
    font-size: 90%;
    color: #555;
  }
}
```

### @page rules

The `@page` at-rule controls page size, margins, and layout outside the content box. Set document defaults with it.

| Need | Rule |
| --- | --- |
| Document default | `@page { size: A4; margin: 20mm 18mm; }` |
| US-letter sources | Replace `size: A4` with `size: letter` |
| Landscape (wide tables, charts) | `size: A4 landscape` |

### Page-break control

Use the modern property names `break-before`, `break-after`, `break-inside` in new code rather than the older `page-break-*` family. Use `break-inside: avoid` on any visually-coherent unit that should not split across pages: invoice line items, addresses, signature blocks, table rows, captioned figures.

```css
@media print {
  h1, h2 { break-after: avoid; }
  table, figure, .invoice-row { break-inside: avoid; }
  .invoice-summary { break-before: page; }
}
```

### Color in print

Browsers strip backgrounds and images when printing by default. For surfaces where color is meaningful (status badges, brand bars, signature blocks), force color through with `print-color-adjust: exact`. The standard property is `print-color-adjust`; ship both `color-adjust` and `print-color-adjust` for compatibility with older Firefox.

```css
@media print {
  .status-paid {
    background: #d4edda;
    color: #155724;
    color-adjust: exact;
    print-color-adjust: exact;
  }
}
```

Plan the printed design to stay readable in monochrome; never rely on color alone to convey state (WCAG 1.4.1 use of color, see accessibility.md).

### Headers, footers, page numbers

```css
@page {
  size: A4;
  margin: 18mm;

  @top-right {
    content: counter(page) " / " counter(pages);
    font-size: 9pt;
    color: #666;
  }
}

@page :first {
  @top-right { content: ""; }
}

@page :left { margin-left: 22mm; }
@page :right { margin-right: 22mm; }
```

Browser support for `@page` margin boxes (`@top-right`, `@bottom-center`, etc.) is uneven: Chromium honours the page counter, Firefox honours much of the syntax, Safari covers a subset. For invoices that must render identically across browsers, use a server-side PDF generator (headless Chromium, Prince, WeasyPrint) rather than the browser print pipeline.

### Orphans and widows

Set `p, li { orphans: 3; widows: 3; }` in `@media print` to keep at least three lines together at each page break. Three is the conventional minimum; for body-heavy documents (legal, contracts) consider four.

### Print emulation and verification

Use DevTools print emulation: open the command menu (Cmd+Shift+P / Ctrl+Shift+P), run "Show Rendering", set "Emulate CSS media type" to "print". Run the emulation on every invoice, receipt, and report template before shipping.

Print-preview checklist: scroll every page and confirm headers and totals are visible, no line items split across pages, full URLs appear next to text-link references.

## Email HTML Constraints

### Client matrix and what you lose

| Client | Engine | What you lose |
| --- | --- | --- |
| Outlook 2007 to 2019 (Windows) | Word HTML engine | flex, grid, modern CSS, background images on most elements, `<style>` partly stripped |
| Outlook 365 (Windows) | Word HTML engine | Same as Outlook 2007 to 2019 |
| Outlook for Mac | WebKit | Mostly fine |
| Outlook.com (web) | Modern engine | Mostly fine, some `<style>` stripping |
| Gmail (web, app) | Modern, `<style>` stripped above size threshold | Inline CSS only is safe; media queries supported in `<style>` if it survives |
| Apple Mail (macOS, iOS) | WebKit | Mostly fine; dark-mode auto-inversion |
| Yahoo, AOL, ProtonMail | Mixed | Inline CSS, no JS, sandboxed |
| Mobile Gmail (Android, iOS) | Webview | Inline CSS plus responsive media queries |

Optimize for the "works everywhere" intersection.

### Table-based layout

Do email layout with `<table>` elements (single-cell centering wrapper, nested inner tables), not `<div>` plus flex or grid. Add `role="presentation"` on every layout table so screen readers do not announce them as data tables (see accessibility.md table semantics).

```html
<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
  <tr>
    <td align="center">
      <table role="presentation" width="600" cellpadding="0" cellspacing="0" border="0">
        <tr><td><!-- header --></td></tr>
        <tr><td><!-- body --></td></tr>
      </table>
    </td>
  </tr>
</table>
```

### Inline CSS

Inline every style directly on the element via `style="..."` since most clients strip or partially strip `<style>` blocks. Author templates with `class` attributes, then run a build-time CSS inliner (Juice for Node, Premailer for Ruby, Maizzle Tailwind-based, MJML) that converts classes to inline `style` declarations so shipped HTML has inline styles only. Keep one `<style>` block at the top of the document for media queries (responsive breakpoints, dark-mode overrides) since some clients honour media queries even when stripping layout selectors.

### Width and mobile stacking

The dominant canonical max-width for email layout is 600 pixels; stay at 600 unless content demands more, and test in every client. Achieve mobile responsiveness with a media query that turns side-by-side table cells into full-width stacked blocks.

```html
<style>
  @media (max-width: 600px) {
    .stack { display: block !important; width: 100% !important; }
  }
</style>
```

### Dark mode

Apple Mail honours `prefers-color-scheme`; Outlook on Windows inverts with its own (often bad) algorithm; Gmail mostly does not honour the media query. Design legible in both modes, then add overrides for clients that honour them. For Outlook on Windows set `<meta name="color-scheme" content="light dark">` and accept partial inversion; test in Litmus or Email on Acid before shipping a sensitive template.

```html
<style>
  :root { color-scheme: light dark; }
  @media (prefers-color-scheme: dark) {
    .bg { background: #1a1a1a !important; }
    .text { color: #fafafa !important; }
    .border { border-color: #444 !important; }
  }
</style>
```

### Outlook conditional comments (VML)

Use Outlook MSO conditional comments wrapping a `v:rect`/`v:textbox` VML fallback around the anchor so Outlook renders the button as a vector shape while modern clients render the standard anchor.

```html
<!--[if mso]>
<v:rect xmlns:v="urn:schemas-microsoft-com:vml" fillcolor="#1a1a1a" stroke="f" style="width: 200px; height: 48px;">
  <v:textbox inset="0,0,0,0">
<![endif]-->
<a href="https://your-domain/cta" style="display: inline-block; background: #1a1a1a; color: #fff; padding: 12px 24px; text-decoration: none; border-radius: 4px;">
  Confirm
</a>
<!--[if mso]>
  </v:textbox>
</v:rect>
<![endif]-->
```

### Images

| Rule | Detail |
| --- | --- |
| Absolute URLs | Use absolute URLs (`https://cdn.your-domain/img/logo.png`); email clients have no base path |
| No transparent PNG over color | Outlook adds a small black border on transparent edges; bake the background in or use a solid PNG |
| Meaningful `alt` | Every `<img>` has `alt` text that conveys meaning even if the image never loads (many clients block images until the user opts in) |
| Reserve space | Set `width` and `height` attributes on the `<img>` element |
| Retina | Use 2x resolution images scaled down with the width/height attributes |
| Size caps | Hard cap any single image at 100 KB and total payload at 500 KB; larger trips spam filters and Gmail clips messages over 102 KB |

### Tracking pixels and opt-out

A 1x1 tracking pixel is ethical for transactional and explicitly opted-in marketing but dubious for cold outreach and unsolicited messages.

```html
<img src="https://your-domain/o/pixel.gif?msg=abc123" width="1" height="1" alt="" style="display: block; border: 0;">
```

- When the `Sec-GPC: 1` request header (Global Privacy Control) is present, drop the tracking record on the server because the user signaled opt-out.
- Treat unsubscribe and tracking-opt-out as a single setting from the user's perspective; honouring one but not the other is a defect.

### Transactional vs marketing envelope

| Envelope | What it is | Consent | Footer |
| --- | --- | --- | --- |
| Transactional | Order confirmation, password reset, invoice, receipt, account notification | Implied by the user's action | Brief footer, support contact, account link |
| Marketing | Newsletter, promo, announcement, drip campaign | Explicit opt-in (GDPR), opt-out (CAN-SPAM) | Full mailing address, one-click unsubscribe, preferences link |

Keep transactional messages focused on the transaction; a transactional message that promotes other products becomes marketing and inherits marketing consent rules, so route promotional content through the marketing envelope.

Regulatory floors (minimums, not ceilings):

- CAN-SPAM (US): opt-out required, sender identification required, accurate subject lines, physical mailing address in the footer.
- GDPR (EU/UK): explicit opt-in for marketing, granular consent, easy withdrawal, data-access requests honoured.
- CASL (Canada): explicit opt-in for marketing, identification and unsubscribe required, retained consent records.

## See Also

- [design.md](design.md): typography and color decisions that translate into print color and email type scale.
- [ui-ux.md](ui-ux.md): unsubscribe and preference UX, notification etiquette governing marketing consent.
- [accessibility.md](accessibility.md): use-of-color rules (WCAG 1.4.1) for printed status badges and color-coded email.
- [seo.md](seo.md): canonical URL discipline for web versions of printed and emailed artifacts.
