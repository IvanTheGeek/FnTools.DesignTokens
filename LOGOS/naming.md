---
area: Token Naming
status: current — 2026-05-02
reference: https://medium.com/eightshapes-llc/naming-tokens-in-design-systems-9e86c7444676
---

# Token Naming Analysis

Analysis of our naming conventions against the EightShapes framework (Nathan Curtis,
"Naming Tokens in Design Systems").

---

## EightShapes anatomy (reference)

The framework defines four level groups, applied left-to-right:

| Group | Levels |
|---|---|
| Namespace | System (`esds-`, `orbit-`), Theme (`ocean`, `sands`), Domain (`consumer`, `retail`) |
| Base | Category (color, font, space), Property (text, background, border), Concept (feedback, action) |
| Modifier | Variant (primary, success, error), State (hover, focus, disabled), Scale (50–900, N1–N5), Mode (on-light, on-dark) |
| Object | Component group, Component, Element |

Example anatomy: `$esds-color-feedback-background-error`
= namespace(esds) + category(color) + concept(feedback) + property(background) + variant(error)

Key principles:
- Each path segment = one level. Never fuse two levels into one camelCase segment.
- "Avoid homonyms" — `type` means both typography and variable type; use `font`.
- "Theme ≠ Mode" — themes are alternative visual expressions; modes are light/dark luminance adaptation.
- "Start Within, Then Promote" — keep component tokens local; promote only when 3+ components share them.
- System namespace needed only when tokens propagate across multiple systems on the same page. "Small teams working in one namespace with limited collaborators need not worry."

---

## How our system maps

### Category (top level) — aligned

| Our token | Category | Notes |
|---|---|---|
| `color.*` | color | Good |
| `font.*` | font | Good — avoids the `type` homonym the article warns against |
| `spacing.*` | spacing | Good |
| `radius.*` | radius | Good |
| `shadow.*` | shadow | Good |
| `duration.*` | duration | Good |
| `easing.*` | easing | Good |

### Property level in color semantics — aligned

`color.text.*`, `color.surface.*`, `color.border.*` follow `category → property → variant` exactly.

```
--color-text-primary       category + property + variant  ✓
--color-surface-raised     category + property + variant  ✓
--color-border-default     category + property + variant  ✓
```

### State modifier last — aligned

`color.accent.hover` puts state at the end, after variant. Correct.

### Primitives separate from semantics — aligned

`cb.tokens.json` = raw palette (primitives, no aliases).
`ll.tokens.json` = semantic layer (aliases into cb). The article calls this aliasing and treats it as best practice.

---

## Known gaps

### 1. ~~Fused compound segments~~ — resolved 2026-05-02

`successSubtle`, `dangerSubtle`, `infoSubtle`, `focusRing`, `lineHeight`, `letterSpacing` were
renamed as a single breaking commit. The feedback group was restructured to use `.default` +
`.subtle` sub-tokens so the hierarchy is consistent.

| Was | Now | CSS var |
|---|---|---|
| `color.feedback.success` | `color.feedback.success.default` | `--color-feedback-success-default` |
| `color.feedback.successSubtle` | `color.feedback.success.subtle` | `--color-feedback-success-subtle` |
| `color.feedback.dangerSubtle` | `color.feedback.danger.subtle` | `--color-feedback-danger-subtle` |
| `color.feedback.infoSubtle` | `color.feedback.info.subtle` | `--color-feedback-info-subtle` |
| `shadow.focusRing` | `shadow.focus-ring` | `--shadow-focus-ring` |
| `font.lineHeight` | `font.line-height` | `--font-line-height-*` |
| `font.letterSpacing` | `font.letter-spacing` | `--font-letter-spacing-*` |

F# binding names for `FocusRing`, `LineHeight.*`, `LetterSpacing.*` are unchanged — the
hyphenated segments join to the same PascalCase identifiers. Only the CSS var values changed.

### 3. `color.accent.on` — implicit property

`on` is the text/foreground color to use on top of an accent-colored surface. But nothing
in `color.accent.on` communicates that it is a *text* color — it reads like another accent fill
variant. It's grouped alongside `color.accent.default/hover/subtle` (fills) rather than with
`color.text.*` where it semantically belongs.

Options:
- Move to `color.text.on-accent` — groups with other text colors, property is explicit
- Rename to `color.accent.foreground` — stays in accent group, property implied

No consensus yet. Noted for next naming revision.

### 4. Missing property level in `color.feedback.*`

`color.feedback.success` is used as a background fill, but `color.text.success` covers the
text version. The asymmetry:

```
--color-text-success           ← property is "text"
--color-feedback-success       ← property is implied (background fill)
```

The article's example would be `color.feedback.background.success`. We've omitted the property
level, meaning the token's CSS usage (fill vs. text vs. border) is only discoverable by convention.

This is lower priority — functional and consistent within the feedback group — but worth
addressing when the feedback group grows.

---

## System / Brand / Theme / Mode assessment

### System namespace

The article recommends a system prefix (`esds-`, `orbit-`) when tokens propagate across multiple
design systems on the same page.

**Our situation:** Single-product deployment. `LaundryLog.UI` is the only consumer of these tokens.
No multi-system collision risk exists. The article explicitly states: "Small teams working in
one namespace with limited collaborators need not worry about levels for namespace."

**Decision:** No system prefix. If `cb.tokens.json` is ever published as a shared base library
used across multiple independent products, a `cb-` or `fnh-` prefix would be needed to prevent
`--color-text-primary` from colliding with another system's token of the same name.

### Brand / Domain

The article's "domain" level handles business-unit scoping (`consumer`, `retail`, `credit-card`).

**Our situation:** The cb/ll two-tier resolver is our brand/domain split:
- `cb` = CheddarBooks — base design system layer
- `ll` = LaundryLog — product extension layer

This separation is at the **file level** (resolver determines which layer wins), not at the
emitted name level. A token like `--color-machine-washer-default` is clearly LL-specific by
concept, but it carries no brand prefix.

**Decision:** Provenance is not encoded in emitted names. This is the right call for a
single-product system — adding a `ll-` prefix to every semantic token would add noise without
benefit. The resolver provides the architectural separation; names stay clean.

If a second product were built on the same `cb.tokens.json` base, its semantic tokens would
live in its own `{product}.tokens.json` without conflicting.

### Theme

The article defines a theme as an alternative visual expression of the same component
catalog — e.g., Marriott's JW vs. Courtyard vs. Renaissance, each with a different color palette
but the same component structure.

**Our situation:** One theme only. LaundryLog has a single visual identity. No multi-brand
or multi-tenant expression is planned.

**Note on attribute naming:** We use `data-theme="dark"` on `<html>` for dark mode. Strictly
speaking, this is a *mode* attribute, not a *theme* attribute (the article distinguishes them
clearly). Renaming to `data-mode="dark"` would be more precise but is a cosmetic change —
browsers and CSS don't care about the attribute name.

### Mode

The article treats mode (light/dark) as a first-class modifier level, encoded in token files
with `on-light`/`on-dark` variants and emitted as separate CSS override blocks.

**Our situation:** Dark mode is handled via a manually maintained `[data-theme="dark"]` CSS
override block in `tokens.css`. The token files (`cb.tokens.json`, `ll.tokens.json`) have no
mode awareness — they define light-mode values only.

**Gap:** Dark mode overrides are not round-trippable through the token pipeline. Editing
`cb.tokens.json` requires manually syncing the dark-mode block in `tokens.css`. This is
manageable at current scale but breaks down as the token set grows.

**Future direction:** The emitter should support a second resolver pass that emits a
`[data-mode="dark"]` block from dark-mode token file(s). No design yet.

---

## Remaining work

The "fix required" items were completed 2026-05-02. Remaining lower-priority items:

- `color.accent.on` — property is implicit; decide between `color.text.on-accent` (more explicit)
  or `color.accent.foreground` (stays in accent group). No urgency until the accent palette grows.
- `color.feedback.*` missing property level — `--color-feedback-success-default` is used as a
  fill/background but the token name doesn't say so. Revisit when the feedback group expands.
- Dark mode in token files — currently manual in `tokens.css`. Emitter needs a second pass for
  multi-mode output before this can be automated.
