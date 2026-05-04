---
area: Experiment
status: complete — 2026-05-04
phase: 4b — Penpot export comparison
---

# Phase 4b Findings — Penpot Export Comparison

Goal: Compare three paths for getting component code out of Penpot for the same shape.
Subject: `pattern / card` (board, id `ff4c9b71-2c36-8013-8007-85a4f62f0d06`) from the
Dashboard page of Design Mocks. This component has 9 token bindings:

| CSS property        | Token path               |
|---------------------|--------------------------|
| `fill`              | `color.background.default` |
| `strokeColor`       | `color.border.default`   |
| `strokeWidth`       | `stroke.hairline`        |
| `r1/r2/r3/r4`       | `radius.sm`              |
| `columnGap`         | `spacing.3xs`            |
| `rowGap`            | `spacing.3xs`            |
| (via padding tokens)| `spacing.xs`             |

Token resolution chain (Dark theme active):

```
color.background.default
  → palette.default.800
  → hsla({hue.default},{saturation.default},{lightness.800},1)
  → #1d1f20

color.border.default
  → palette.default.700
  → hsla({hue.default},{saturation.default},{lightness.700},1)
  → #303336

stroke.hairline → scale.hairline → 1   (used as 1px border)
radius.sm       → scale.sm       → round({base} * pow({multiplier},0))  → 16px
spacing.3xs     → scale.3xs      → round({base} * pow({multiplier},-3)) → 11px
spacing.xs      → scale.xs       → round({base} * pow({multiplier},-1)) → 14.28px
```

---

## Path A — MCP `export_shape` → SVG

Invocation: `export_shape(shapeId, format: "svg")`

Result: `/tmp/pattern-card.svg` (saved 2026-05-04)

### What the SVG contains

- Inlined `@font-face` declarations with URLs pointing to `http://penpot-frontend:8080/...`
- Image assets referenced as `href="http://penpot-frontend:8080/assets/by-file-media-id/..."` (internal media UUIDs)
- All visual properties resolved to concrete values:
  - Background: `fill: rgb(29, 31, 32)` — #1d1f20 in RGB form
  - Border: `stroke-width: 2; stroke: rgb(48, 51, 54)` — #303336 in RGB form
  - Radius: `ry="16" rx="16"` — geometry attribute, not CSS
- Layout properties absent — gap, padding, grid are layout concepts; SVG encodes them as geometry only
- Token paths: completely absent — zero traceability

### What is lost

- All token names — no way to know `background.default` controlled the fill
- Spacing/padding — not expressible in SVG; encoded as absolute geometry
- Font URLs not portable — `penpot-frontend:8080` is an internal hostname
- Image asset URLs not portable — media IDs are instance-specific
- Component boundaries — children are flattened into SVG groups
- Variant metadata — no component name, no props

### Design tab Export → SVG

Verified identical to `export_shape`. The Design tab Export → SVG goes through the same
render pipeline — same `@font-face` URLs, same `fill: rgb(...)` values, same image media
IDs. The only difference is a session-scoped UUID in one clip-path ID, which is cosmetic.
There is no separate token-aware SVG format in Penpot.

### When to use

Visual snapshot only. Valid for thumbnails, documentation screenshots, or pixel-comparison
tests. Not usable as a production web component.

---

## Path B — Inspect Tab HTML/CSS (Code view)

Penpot Inspect panel → Code view for the selected `pattern / card` shape.

### CSS output (abbreviated — full CSS for the outer frame rule)

```css
/* pattern / card */
.pattern-85a4f62f0d06 {
  position: relative;
  width: 100%;
  background: #1d1f20FF;
  border: 1px solid #303336FF;
  border-start-start-radius: 16px;
  border-start-end-radius: 16px;
  border-end-start-radius: 16px;
  border-end-end-radius: 16px;
  display: grid;
  align-items: start;
  align-content: stretch;
  justify-items: start;
  justify-content: stretch;
  gap: 11px;
  padding-inline-start: 14.28px;
  padding-inline-end: 14.28px;
  padding-block-start: 14.28px;
  padding-block-end: 14.28px;
  flex-direction: column-reverse;
  flex-wrap: nowrap;
  flex: 1;
  flex-grow: 1;
  grid-template-rows: 1fr auto;
  grid-template-columns: 1fr;
  grid-auto-flow: column;
  max-width: 350px;
  max-inline-size: 350px;
}
```

Font declarations include `src: url(http://localhost:9001/internal/gfonts/...)` — local
instance URL.

### Inspect Styles view (not Code view)

The Styles view shows a hybrid: some token names preserved, others resolved:

| Property        | Styles view shows    | Code view shows       |
|-----------------|---------------------|-----------------------|
| border-radius   | `radius.sm`          | `16px`                |
| border-width    | `stroke.hairline`    | `1px solid ...`       |
| fill            | `#1d1f20 100%`       | `#1d1f20FF`           |
| border-color    | `#303336 100%`       | `#303336FF`           |
| gap             | `11px`               | `11px`                |
| padding         | `14.28px`            | `14.28px`             |

Border radius and border width retain token names in Styles view; color and spacing are
always resolved. Code view resolves everything.

### What is present

- Layout structure faithfully translated: `display: grid`, `grid-template-*`, `gap`, `padding-inline-*`
- Active theme values applied — Dark theme colors in use
- Token Sets & Themes section shows active sets at time of inspection:
  `Brand/Core, Global/Always-on, Color mode/Dark, Breakpoint/Tablet, Text zoom/100%`
- Child shapes included (nested CSS classes for image frame, text frame, text nodes)

### What is lost

- CSS custom properties — zero runtime token traceability
- Token names in Code view — all resolved to concrete hex/px
- Class names encode UUID fragments (`.pattern-85a4f62f0d06`) — not a stable API; not
  reusable across file versions or instances
- Font URLs not portable — `localhost:9001`
- `flex-direction: column-reverse` emitted alongside `display: grid` — Penpot's internal
  layout model leaks through; a grid does not use `flex-direction`
- No component semantics — variant props, component name, and PATHS state are absent

### When to use

Quick inspection of resolved values and layout structure for a specific shape under a
specific active theme. Good for verifying what Penpot thinks the final CSS should look
like. Not suitable for production use as-is.

---

## Path C — Raw API → Our Emitter

Our pipeline: `importTokensStudioThemed` → resolved token map → `CssEmitter.emitThemed`.

### What our emitter produces (abbreviated `:root` and Dark theme override)

```css
:root {
  /* ... all base tokens ... */
  --color-background-default: #someLightColor;
  --color-border-default: #someLightBorderColor;
  --stroke-hairline: 1px;
  --radius-sm: 16px;
  --spacing-3xs: 11px;
  --spacing-xs: 14px;
  /* ... */
}

[data-theme="Color mode/Dark"] {
  --color-background-default: #1d1f20;
  --color-border-default: #303336;
  /* stroke.hairline, radius.sm, spacing.* are theme-invariant — not repeated */
}
```

### What the component CSS would look like (using our variables)

The `shape.tokens` map for `pattern / card` tells us which CSS property uses which token.
A code generator consuming `shape.tokens` + our emitted variables would produce:

```css
.pattern-card {
  background: var(--color-background-default);
  border: var(--stroke-hairline) solid var(--color-border-default);
  border-radius: var(--radius-sm);
  gap: var(--spacing-3xs);
  padding: var(--spacing-xs);
  /* layout properties from shape geometry, not from tokens: */
  display: grid;
  grid-template-rows: 1fr auto;
  grid-template-columns: 1fr;
  max-width: 350px;
}
```

This CSS works across theme switches: changing `data-theme` on an ancestor element updates
`--color-background-default` and `--color-border-default` automatically. The layout
properties (grid structure, max-width) are geometry-derived and not token-controlled.

### What is present

- Full token hierarchy as CSS custom property names
- Theme switching at runtime — no re-render needed
- Stable class names — the component author names the class
- Math expressions resolved: HSL palette + `round(base * pow(multiplier, N))` chains
  produce the final px/hex values
- Token paths traceable: `--color-background-default` directly names the token

### What is missing (gap between our emitter and a complete component)

Our emitter produces the variable declarations. It does NOT produce the component CSS that
uses them. To close the loop, a shape-to-component CSS generator step is needed:

1. Read `shape.tokens` (via Plugin API `execute_code`) → `{ fill: "color.background.default", strokeColor: "color.border.default", ... }`
2. Map each entry to a CSS property → custom property binding
3. Read layout properties from the shape geometry (width, height, gap, padding, grid template)
4. Emit the complete component CSS block

This step is currently manual. The `shape.tokens` map gives all the token bindings;
the gap is the layout/geometry extraction.

---

## Comparison Summary

| Dimension                    | Path A: SVG export    | Path B: Inspect CSS          | Path C: Our emitter          |
|------------------------------|-----------------------|------------------------------|------------------------------|
| Token names preserved        | No                    | Partial (Styles view only)   | Yes (as `--var` names)       |
| Theme switching at runtime   | No                    | No                           | Yes                          |
| Layout structure             | No (geometry only)    | Yes (grid/flex)              | Partial (tokens only, not layout) |
| Portable font URLs           | No (penpot-frontend)  | No (localhost:9001)          | N/A (not in scope)           |
| Portable image assets        | No (media IDs)        | No (inline SVG with media)   | N/A                          |
| Stable class/selector names  | N/A (SVG IDs)         | No (UUID fragments)          | Yes (author-controlled)      |
| Complete component output    | Visual only           | Mostly (needs URL fixes)     | No (vars only, no structure) |
| Active-theme awareness       | Renders one theme     | Shows active theme values    | Emits all themes as overrides |
| Usable in CI/headless        | Yes                   | No (requires Penpot UI)      | Yes                          |
| Math expression resolution   | Yes (opaque)          | Yes (opaque)                 | Yes (transparent chain)      |

---

## Implications for the FnTools / FnHCI pipeline

1. **Path A (SVG) + Path C (our emitter) are the CI-compatible paths.** Path B requires
   the Penpot UI; it cannot be scripted.

2. **The missing step is shape-to-component CSS generation.** Our emitter produces the
   variable file. A second pass reading `shape.tokens` (via Plugin API or REST) would
   produce the component CSS that references those variables. This is the natural Phase 5
   deliverable.

3. **Layout properties are geometry, not tokens.** Gap and padding values appear in
   `shape.tokens` (e.g. `columnGap → spacing.3xs`) but display/grid-template/flex-direction
   are pure layout metadata. A complete code generator needs both: token bindings from
   `shape.tokens` AND layout metadata from the shape's geometry fields.

4. **Inspect Styles view is the best human inspection tool.** It shows token names for
   structural tokens (radius, stroke) while resolving colors — a useful hybrid for
   manual review. Code view is for copying resolved CSS into a prototype.

5. **Class name stability is a blocker for Inspect CSS in production.** UUID-fragment
   class names (`.pattern-85a4f62f0d06`) break on any file copy, rename, or re-import.
   Our approach uses author-named classes that reference stable variable names.
