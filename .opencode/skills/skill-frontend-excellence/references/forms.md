---
title: Forms and Feedback
purpose: Input-level form discipline (label, type, validation, autocomplete), the constraintValidation API, field-level abandonment analytics, and address autofill depth. Flow-level auth lives in auth.md.
load-when:
  task-keywords: [form, validation, autofill, autocomplete, label, input, select, checkbox, radio, file upload, constraintValidation, touch target]
  symptoms: [contrast fail, focus not visible, duplicate id]
prereq: SKILL.md
related: [accessibility.md, ui-ux.md, motion.md, auth.md]
size: ~552 lines
---

# Forms and Feedback

Framework-agnostic patterns for forms that are easy to fill, easy to recover from, and accessible. Forms are where most products earn or lose users.

## Form Anatomy

```
[ Optional: page heading ]
[ Optional: short context: why we're asking ]

[ Field group ]
  [ Label (always visible) ]                [ Optional indicator: required, optional ]
  [ Input ]
  [ Helper text (always when complex) ]
  [ Error message (when invalid) ]

[ ... more field groups ]

[ Submit button ]                            [ Cancel / secondary action ]
```

## Labels

| Principle | Concrete check |
| --- | --- |
| Always visible | Label visible at all times, not just when the field is empty. |
| Associate | Wrap or associate: `<label for="email">Email</label> <input id="email" />` or `<label>Email <input /></label>`. |
| Float labels | Acceptable visually (label transitions to a small position when focused/filled) but the label must stay associated with the input. |
| Placeholder is not a label | The placeholder disappears when typing and most placeholders fail contrast. |
| Position default | Top-aligned is the default; easiest to scan. |
| Position dense | Left-aligned for compact dense forms (settings). |
| Position rare | Right-aligned rarely; harder to scan. |
| Mark the smaller set | If most fields are required, mark the optional ones, not the larger set. |
| Required indicator | Use both visual (asterisk) AND text (the word "required" somewhere visible). |
| Required programmatic | Use `aria-required="true"` (or the native `required` attribute, which implies it). |

Required field markup:

```html
<label for="email">
  Email <span aria-hidden="true" class="required">*</span>
</label>
<input id="email" type="email" required aria-required="true" />
```

## Input Types

Use the right `type` and `inputmode` for the data:

| Data | type | inputmode | autocomplete |
| --- | --- | --- | --- |
| Email | email | email | email |
| Phone | tel | tel | tel |
| URL | url | url | url |
| Numeric ID, code | text | numeric | one-time-code (for OTP) |
| Money | text | decimal | (use a formatted text input) |
| Date | date | (auto) | bday or other |
| Search | search | search | (none) |
| Password | password | (auto) | new-password / current-password |
| Name | text | text | given-name / family-name / name |
| Address | text | text | street-address, address-line1, postal-code, country |
| Credit card | text | numeric | cc-number / cc-exp / cc-csc / cc-name |

### autocomplete is critical

Set `autocomplete` so browsers and password managers fill fields automatically; without it users must type or copy-paste.

WHATWG `autocomplete` name tokens (commonly used):

- Name: `name`, `given-name`, `additional-name`, `family-name`.
- Contact: `email`, `tel`, `tel-national`.
- Address: `street-address`, `address-line1`, `address-line2`, `address-level1` (state), `address-level2` (city), `postal-code`, `country`.
- Credit card: `cc-name`, `cc-number`, `cc-exp`, `cc-csc`, `cc-type`.
- Auth: `username`, `current-password`, `new-password`, `one-time-code`.
- Organization: `organization`, `organization-title`.
- Birthday: `bday`, `bday-day`, `bday-month`, `bday-year`.

On multi-form pages, prefix the value with the form purpose (`shipping`, `billing`):

```html
<input autocomplete="shipping street-address" />
<input autocomplete="billing street-address" />
```

### Address autofill: structured vs single-line

| Choice | Check |
| --- | --- |
| Structured (multi-field) | Separate fields for `address-line1`, `address-line2`, `address-level2` (city), `address-level1` (state/region), `postal-code`, `country`, with `country` FIRST so downstream fields adapt; best for shipping and billing. |
| Single-line | One `<input autocomplete="street-address">` holding the whole address; use only when you do not need the components separately. |

International address shapes vary; do not hard-code one country's shape:

- Some countries (Ireland, the UAE, Hong Kong before Eircode) have no postal code at all, so `postal-code` cannot be `required` for those countries.
- Some countries (Japan, China) write the address from the largest unit to the smallest (prefecture, city, street); UI field order should mirror that for the chosen country.
- Some countries (the UK, India) use `address-level2` (city) plus `address-level1` (county/state) that the user often leaves blank; do not block submission.
- A `country` field that adapts the downstream form is the simplest path to international correctness; drive label, required-flag, and regex per country from a single locale config.

`street-address` granularity: the WHATWG list breaks it into `address-line1`, `address-line2`, `address-line3`, `address-level1` through `address-level4`, `postal-code`, `country`, `country-name`. Declaring `street-address` on one line is valid and declaring the broken-down fields is valid, but mixing the two on the same form confuses autofill; pick a level and commit.

Two suggestion sources:

- Browser autofill uses the user's saved addresses from their browser or password manager; cost zero; triggers when the `autocomplete` attribute matches a known field; best for returning/known users.
- A geocoding service suggests addresses as the user types; cost a per-request API fee; triggers via explicit JS integration; best for first-time customers and catching typos.

Coexistence: let browser autofill run on focus, then surface geocoding suggestions as the user keeps typing. Do NOT disable browser autofill to force geocoding; `autocomplete="off"` is widely ignored by browsers and even when honoured it hurts password managers.

### Native vs custom controls

Native first:

- `<input type="date">` is fine on mobile; custom date pickers are appropriate for desktop ranges.
- Style `<input type="checkbox">` and `<input type="radio">` with `:checked` selectors plus pseudo-elements; use the ARIA pattern only when truly necessary.
- `<input type="range">` for sliders.
- `<select>` for short lists; combobox for long or async lists.
- `<input type="file">` for uploads; style the trigger with a `<label>` to match design.

Custom controls require: full keyboard support (arrow keys, Enter/Space, Esc); ARIA role and state; focus management; touch and mouse parity.

## Validation

### When to validate

| Trigger | Rule |
| --- | --- |
| Every keystroke | Don't; it's distracting and often wrong before enough is typed. |
| On blur | Validate when the user leaves the field. |
| On submit | Re-validate as a safety net; the server validates regardless. |
| Password strength meter | Update on keystroke since the user expects live feedback. |
| Confirm-password | Validate when the second field loses focus. |

### constraintValidation API

Every form control exposes a `ValidityState` object via `input.validity`; read its flags and set custom messages instead of hand-rolling parallel validation state.

| Flag | Meaning |
| --- | --- |
| `valueMissing` | `required` is set and the value is empty. |
| `typeMismatch` | Value does not match `type` (a `type="email"` with no `@`, a `type="url"` with no scheme). |
| `patternMismatch` | Value does not match `pattern`. |
| `tooShort` / `tooLong` | Outside `minlength` / `maxlength`. |
| `rangeUnderflow` / `rangeOverflow` | Outside `min` / `max`. |
| `stepMismatch` | Not aligned with `step`. |
| `badInput` | Browser cannot parse the value (a non-number in `type="number"`). |
| `customError` | A previous `setCustomValidity()` call set a message. |

```js
const email = document.querySelector('#email');

email.addEventListener('input', () => {
  if (email.validity.typeMismatch) {
    email.setCustomValidity('Enter an email like name@your-domain.com');
  } else if (email.validity.valueMissing) {
    email.setCustomValidity('Email is required');
  } else {
    email.setCustomValidity(''); // clear; the field is valid
  }
});

email.form.addEventListener('submit', (event) => {
  if (!email.form.checkValidity()) {
    event.preventDefault();
    email.form.reportValidity(); // shows browser UI for invalid fields
  }
});
```

The `invalid` event fires per field when the form fails to validate on submit; listen for it to render custom error UI. Never rely on the browser's default tooltip alone: it disappears on focus move, has no contrast guarantees, is not announced by every screen reader, and cannot be styled.

### Error placement

- Inline, below the field, because the error is about THIS field.
- `aria-invalid="true"` on the input for programmatic error state.
- `aria-describedby` on the input pointing to the error message id.
- `role="alert"` on the error so screen readers announce it.

```html
<label for="email">Email</label>
<input
  id="email"
  type="email"
  required
  aria-invalid="true"
  aria-describedby="email-error"
/>
<p id="email-error" role="alert" class="error">
  Enter a valid email address (example: name@example.com)
</p>
```

### Error message content

- State cause AND fix: "Email is required" beats "Validation error"; "Enter a valid email address" beats "Invalid".
- Plain language; don't expose backend codes ("ERR_INVALID_INPUT"), translate them.
- Don't blame the user: "Couldn't save your draft" not "You haven't filled everything in".
- Stay positive when possible: "Try a longer password" beats "Password too short".

### Multi-error submission

- Move focus to the first invalid field (or to a top-of-form summary).
- Show a summary at the top with anchor links to each error.
- Each invalid field also shows its inline error.

```html
<div role="alert" aria-live="assertive" class="error-summary">
  <h2>3 issues to fix:</h2>
  <ul>
    <li><a href="#email">Enter a valid email</a></li>
    <li><a href="#password">Password must be at least 8 characters</a></li>
    <li><a href="#terms">Accept the terms to continue</a></li>
  </ul>
</div>
```

### Server-side errors

- Show inline near the relevant field if known.
- Show a top-of-form alert if the error doesn't map to a field (network failure).
- Provide a recovery action (retry, contact support).
- Preserve user input; never make them retype.

## Helper Text

Persistent text below the field, before any error:

- Format expectations ("3 to 30 characters, letters and numbers").
- Why we're asking ("Used for password reset only; never shown publicly").
- Examples ("e.g., +1 555 123 4567").

Place between input and error; when error shows, error replaces or appears below helper.

## Submit Button

| Concern | Check |
| --- | --- |
| Location | Primary submit at the bottom, aligned to the form's primary text direction. |
| Mobile | Full-width is acceptable for the primary submit. |
| Label | Specific verb-noun: "Create account" beats "Submit", "Save draft" beats "Save". |
| Label match | Match the action: "Send invoice" if that's what happens. |
| Double-submit | Disable while submitting to prevent double-submit. |
| Spinner | Show a spinner inside the button during submission. |
| No shift | Maintain button width to prevent layout shift. |
| Live label | Optionally update the label ("Creating account" instead of "Create account"). |

```html
<button type="submit" disabled aria-busy="true">
  <span class="spinner" aria-hidden="true"></span>
  Creating account
</button>
```

### Cancel

Provide a secondary action (cancel, back) when the user might back out. Confirm if there are unsaved changes:

```html
<button type="button" onclick="confirmExit()">Cancel</button>
```

```js
function confirmExit() {
  if (formIsDirty()) {
    if (confirm('Discard your changes?')) {
      // navigate away
    }
  } else {
    // navigate away
  }
}
```

## Multi-Step Forms

Stepper:

```
[Step 1: Account] -> [Step 2: Profile] -> [Step 3: Payment]
   Done              In progress          Upcoming
```

- Current step visually distinct.
- Completed steps clickable (allow back navigation).
- Upcoming steps not clickable.

Save state and validation:

- Auto-save draft at each step transition.
- Restore state on return.
- Allow exit without losing data.
- Validate the current step on "Next".
- Don't validate steps the user hasn't reached.
- Show validation errors before advancing.

## Long Forms

For very long forms (> 10 fields):

- Group fields with `<fieldset>` + `<legend>`:

```html
<fieldset>
  <legend>Shipping address</legend>
  <!-- fields -->
</fieldset>
```

- Use visual section headings with horizontal lines or background variation.
- Auto-save drafts every 30-60s or on blur of significant fields.
- Show a "Saved 5s ago" microcopy.

Autosave UX:

- Don't interrupt the user's typing with the save indicator.
- Show status in a fixed corner ("Saving...", "Saved", "Failed to save - retry?").
- Use `aria-live="polite"` so screen readers announce changes without interrupting.

## Form Analytics and Field-Level Abandonment

Form-level conversion (submitted vs viewed) tells you the form is broken; field-level analytics tells you WHICH field. Instrument these events per field and compute the funnel:

| Event | Signal |
| --- | --- |
| Focus | The user reached the field; logs which fields are attempted. |
| Blur with value | The user filled the field and moved on. |
| Blur without value | The user touched the field, did not fill it, moved on: the abandonment signal. |
| Submit attempt with this field invalid | The user tried to submit and this field rejected. |

```js
function instrumentField(field) {
  let focusedAt = 0;
  field.addEventListener('focus', () => {
    focusedAt = performance.now();
    track('field_focus', { name: field.name });
  });
  field.addEventListener('blur', () => {
    const filled = field.value.trim().length > 0;
    track('field_blur', {
      name: field.name,
      filled,
      dwellMs: Math.round(performance.now() - focusedAt),
    });
  });
}

document.querySelectorAll('form input, form select, form textarea').forEach(instrumentField);
```

Common findings:

- A `phone` field with 40 percent blur-without-value is the abandonment culprit; consider making it optional or removing it.
- A `birthday` field with high focus-then-leave looks invasive; explain why you need it inline.
- A `password` field with high "submit attempt with this field invalid" means the strength rule is harsh; surface the rule before submission.
- A field with high re-focus count means format confusion; add helper text.

Send events via `navigator.sendBeacon` so abandonment events survive the unload; sample if the form sees high traffic. See observability.md for the RUM pipeline.

## Confirmation and Destructive Actions

Confirm before destructive ("Delete account", "Cancel subscription", "Discard draft"):

- Require an explicit confirmation.
- Modal with a clear title ("Delete account?").
- Body states consequences ("This permanently deletes your account and all data. This cannot be undone.").
- Two buttons: "Cancel" (primary) and the destructive action (destructive secondary, danger color).
- For very destructive actions, require typing the resource name to confirm ("Type your account email to confirm").

Don't confirm trivial actions:

- Don't confirm "Save" (non-destructive).
- Don't confirm "Send message" unless the user requested confirm-before-send.
- Don't confirm form resets if "Cancel" already exists.

Undo: for undoable destructive actions prefer undo over confirm (toast "Item deleted. Undo (5s)"); it is faster, less interruptive, and works with screen readers. Use confirm only for actions that genuinely cannot be undone (account deletion, payment).

## File Upload

- Show a drop zone (visible on hover/dragover).
- Click-to-browse via a styled `<label for="file-input">`.
- Show a thumbnail/preview of selected files.
- Show a progress bar during upload.
- Allow cancel during upload.
- Show an error per file (size, type, network).
- Accept multiple files where appropriate.

```html
<label for="resume" class="dropzone">
  <input id="resume" type="file" accept=".pdf,.doc,.docx" hidden />
  Drag and drop your resume, or click to browse.
  PDF, DOC, or DOCX, up to 5 MB.
</label>
```

Hide the native input; style the label as the drop zone.

## Search Forms

- Single field with a magnifier icon.
- Submit on Enter (native `<form>` behavior).
- Optionally submit on type with a 300ms debounce.
- Show recent searches when empty.
- Show suggestions as the user types.
- Show a clear-input button when populated.
- Show results in real-time or after submit.

```html
<form role="search" action="/search">
  <label for="q" class="sr-only">Search</label>
  <input id="q" name="q" type="search" autocomplete="off" />
  <button type="submit">Search</button>
</form>
```

`role="search"` on the form is a landmark for screen readers.

## Modern Auth Pointer

This file keeps INPUT-level concerns: the `<input>` element, its label, its validation, its autofill hint, its error message. Flow-level auth concerns (passkeys and WebAuthn, conditional UI, OAuth redirect UX, magic-link flow, account recovery, session-expiry, CAPTCHA placement) live in auth.md. If the question is "how should this input be marked up and validated", look here; if it is "how should this whole sign-in flow behave", look in auth.md.

## Login and Sign-Up

```html
<form>
  <label for="username">Email</label>
  <input id="username" name="username" type="email" autocomplete="username" required />

  <label for="current-password">Password</label>
  <input id="current-password" name="password" type="password" autocomplete="current-password" required />

  <button type="submit">Sign in</button>

  <a href="/forgot">Forgot password?</a>
  <a href="/signup">Create account</a>
</form>
```

- Email/username must use `autocomplete="username"` (counterintuitive, but the spec).
- Login password must use `autocomplete="current-password"`.
- Always use a `<form>` (not just floating fields) so password managers detect it.

Sign-up:

- New password uses `autocomplete="new-password"`.
- Show password strength inline.
- Show a "Show / hide" toggle for the password field.
- After submit, send to verification or onboarding, not back to a login screen.

Password reset:

- Email field uses `autocomplete="username"`.
- Submit reveals "Check your email" regardless of whether the email exists (security best practice).
- The reset link page uses `autocomplete="new-password"`.

One-time codes:

- `autocomplete="one-time-code"` so mobile suggests codes from SMS.
- `inputmode="numeric"` for numeric codes.
- `pattern="\d*"` for native validation on numeric codes.
- Auto-submit when the expected number of digits is entered.

## Payment Forms

- Card number: `autocomplete="cc-number"`, `inputmode="numeric"`, `pattern` for digits.
- Expiry: `autocomplete="cc-exp"`, `inputmode="numeric"`.
- CVC: `autocomplete="cc-csc"`, `inputmode="numeric"`, `maxlength="4"`.
- Cardholder name: `autocomplete="cc-name"`.
- Address autofill: `autocomplete="postal-code"` etc.
- Use a hosted payment element or platform payment request API in production; never collect raw card data.

## Mobile-Specific

- Set the right `inputmode` and `type` so the right mobile keyboard appears.
- For numeric codes, `inputmode="numeric"` shows the number pad on mobile.
- For dates, `<input type="date">` shows the native date picker on most platforms.
- Mobile browsers auto-zoom when an input has font-size below 16px; set inputs to >= 16px to prevent this.
- Inputs at least 44px tall (`height: 44px` or sufficient padding); same for buttons. See accessibility.md for hit-target detail.

Sticky submit bar (with safe-area padding):

```css
.sticky-submit {
  position: sticky;
  bottom: 0;
  padding-bottom: max(16px, env(safe-area-inset-bottom));
}
```

## Form Accessibility

- Every input has a programmatic label.
- Required fields are marked.
- Error messages associated via `aria-describedby`.
- Errors announced via `role="alert"` or `aria-live`.
- Focus moves to first invalid field on submit error.
- Group with `<fieldset>` + `<legend>`.
- Use `<form>` so password managers and screen reader landmarks detect it.

## Common Form Mistakes

- Placeholder as label.
- No `autocomplete` attributes.
- Placeholders that look like real values ("name@example.com") confused with filled state.
- Validating on every keystroke.
- Errors only at the top, no inline indication.
- Generic errors ("Invalid", "Error").
- "Required" indicator on all fields when most are required.
- Submit button labeled "Submit".
- Disabled submit when fields are empty (instead, validate on submit attempt and show errors).
- No loading state on submit; user clicks twice and creates duplicates.
- No autosave on long forms.
- Fields without `type` (default `type="text"` shows the wrong keyboard on mobile).
- Asking for unnecessary data ("Why does signup need my phone?").
- Multi-line text in a single-line input.
- Cancel button styled as primary, submit as secondary.

## Self-Healing for Forms

Before declaring work complete:

- [ ] Every input has a visible, programmatic label
- [ ] Right `type` and `inputmode` for the data
- [ ] `autocomplete` set per WHATWG spec
- [ ] Required fields marked visually + `aria-required`
- [ ] Errors inline, near the field, with cause + fix
- [ ] Errors announced via `role="alert"` or `aria-live`
- [ ] On submit error, focus moves to first invalid field
- [ ] Submit button has loading state, disabled during submission
- [ ] Submit button label is specific (verb-noun)
- [ ] Helper text for complex fields
- [ ] Long forms autosave; "Saved" indicator visible
- [ ] Multi-step forms show progress and allow back navigation
- [ ] Destructive actions confirm or provide undo
- [ ] Mobile: 16px+ font size, 44px+ touch targets
- [ ] `<form>` element used; password manager detects fields
- [ ] Tested with keyboard only, screen reader, and password manager

## See Also

- accessibility.md for input accessibility and hit-target detail
- ui-ux.md for state design (loading, error, success)
- motion.md for transitions on validation feedback
- auth.md for flow-level auth (passkeys, OAuth, magic link, recovery)
