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

## Path D — .penpot Archive (export-binfile)

This path was not in the original plan but is the richest format available.

**Invocation**: `POST /api/rpc/command/export-binfile` with `fileId`, `includeLibraries: true`,
`embedAssets: true`. Returns a ZIP file identical to the Design tab → Download backup format.

### Archive structure relevant to tokens

```
files/<file-uuid>/
  tokens.json                       ← Tokens Studio format, aliases preserved
  pages/<shape-uuid>.json           ← one per shape, contains appliedTokens
```

`tokens.json` for Design Mocks has 19 sets, 8 themes, `$metadata` with `tokenSetOrder` and
`activeThemes`. Alias chains (`{scale.3xs}`) and math expressions
(`round({base} * pow({multiplier}, -3))`) stored as-is — our shim resolves them.

### Per-shape JSON

```json
{
  "appliedTokens": {
    "fill":        "color.background.default",
    "strokeColor": "color.border.default",
    "strokeWidth": "stroke.hairline",
    "r1": "radius.sm", "r2": "radius.sm",
    "r3": "radius.sm", "r4": "radius.sm",
    "columnGap":   "spacing.3xs",
    "rowGap":      "spacing.3xs"
  }
}
```

Token dot-paths are stored as-is, not resolved. 1277 shapes in Design Mocks have
non-empty `appliedTokens`.

### What is present

- All token path names (not resolved values) for every bound property
- Full alias chain for resolution via `tokens.json`
- Theme metadata (`$themes`, `activeThemes`) for multi-theme emission
- Shape geometry for layout/structural CSS
- Works headlessly — no browser required; pure REST call

### What is missing

Same structural gap as Path C: the archive gives token bindings and geometry, but not a
ready-to-use component CSS file. The code-gen step is still manual.

Also: `tokens.json` is Tokens Studio format with math expressions in `Foundations/Base`.
Our shim skips that set, producing 57 unresolved tokens (spacing, sizing, typography).
Those tokens show up in `appliedTokens` as unresolvable until the math expressions are
evaluated. This is the same gap documented in the multi-set resolution findings.

---

## Composite approach: generateStyle + shape.tokens

A newly confirmed Plugin API path (`penpot.generateStyle`) offers a useful complement:

```javascript
// Returns resolved CSS string — token names absent, but layout structure present
const css = penpot.generateStyle([shape], { type: "css", includeChildren: false });
```

`generateStyle` produces the same CSS as Inspect Code view (concrete hex/px values,
UUID class names). It is NOT token-aware. However, it faithfully generates the layout
properties (`display: grid`, `grid-template-rows: 1fr auto`, `flex-direction`, `gap`,
`padding`) that are geometry-derived and theme-invariant.

**Clean split**:
- `penpot.generateStyle()` → extract layout/structural CSS (`display`, `grid-template-*`,
  `flex-direction`, `flex-wrap`, `max-width`) — these don't change across themes
- `shape.tokens` → the token-bound properties (`fill`, `stroke`, `radius`, `gap`,
  `padding`) — replace resolved values with `var(--token-path)` references

The result: a component CSS block where layout is literal and theming is variable-driven.
This assembly step is the missing Phase 5 deliverable.

---

## Comparison Summary

| Dimension                    | Path A: SVG export    | Path B: Inspect CSS          | Path C: Our emitter          | Path D: .penpot archive      |
|------------------------------|-----------------------|------------------------------|------------------------------|------------------------------|
| Token names preserved        | No                    | Partial (Styles view only)   | Yes (as `--var` names)       | Yes (dot-paths in appliedTokens) |
| Theme switching at runtime   | No                    | No                           | Yes                          | Requires code-gen step       |
| Layout structure             | No (geometry only)    | Yes (grid/flex)              | No (vars only)               | Yes (shape geometry)         |
| Portable font URLs           | No (penpot-frontend)  | No (localhost:9001)          | N/A                          | N/A (tokens only)            |
| Portable image assets        | No (media IDs)        | No (inline SVG with media)   | N/A                          | Embedded (with embedAssets)  |
| Stable class/selector names  | N/A (SVG IDs)         | No (UUID fragments)          | Yes (author-controlled)      | Requires code-gen step       |
| Complete component output    | Visual only           | Mostly (needs URL fixes)     | No (vars only, no structure) | No (needs assembly step)     |
| Active-theme awareness       | Renders one theme     | Shows active theme values    | Emits all themes as overrides | Full theme metadata present  |
| Usable in CI/headless        | Yes                   | No (requires Penpot UI)      | Yes                          | Yes (pure REST)              |
| Math expression resolution   | Yes (opaque)          | Yes (opaque)                 | Yes (transparent chain)      | No (raw expressions stored)  |
| Alias chains preserved       | No                    | No                           | Yes (shim resolves)          | Yes (shim resolves)          |

---

## Implications for the FnTools / FnHCI pipeline

1. **Paths A + C + D are the CI-compatible paths.** Path B requires the Penpot UI open in
   a browser; it cannot be scripted. All three CI paths are pure HTTP.

2. **Path D (archive) is the richest input for a code generator.** It provides token
   dot-paths in `appliedTokens`, alias chains in `tokens.json`, theme metadata, and shape
   geometry — all in one REST call, headlessly, without a browser. It is the best starting
   point for a shape-to-component code generator.

3. **The composite approach for Phase 5**: 
   - `export-binfile` (or `get-file`) → shape `appliedTokens` + geometry
   - Our shim on `tokens.json` → resolved `--variable` map per theme
   - Per-shape: replace `appliedTokens` entries with `var(--token-path)`, take layout
     properties from geometry
   - Output: component CSS file with variable references + `:root`/theme override blocks

4. **`penpot.generateStyle()` is useful for layout extraction in interactive sessions.**
   When a browser is available, it provides the layout/structural CSS block in one call.
   For CI, read layout from shape geometry directly (it is simpler and scriptable).

5. **The remaining math expression gap affects spacing/sizing/typography tokens.** The
   `Foundations/Base` set uses `round({base} * pow({multiplier}, N))` expressions. Our
   shim skips it, leaving 57 unresolved tokens. These appear in `appliedTokens` but cannot
   be resolved without evaluating the math. This is the primary fidelity gap in the pipeline.

6. **Inspect Styles view is the best human inspection tool.** Token names for structural
   props (radius, stroke-width) are visible; Code view resolves everything. Neither is
   suitable for production use directly.
