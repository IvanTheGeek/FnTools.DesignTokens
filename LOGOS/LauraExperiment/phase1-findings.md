---
area: Experiment
status: complete — 2026-05-03
phase: 1 — Read
source: Design mocks.penpot archive, REST get-file, direct JSON extraction
---

# Phase 1 Findings — Read

Source: `Design mocks.penpot` archive extracted to `/tmp/penpot-extract/`.
Dashboard page: `32e906fd-569f-8017-8007-7eebe49699ef` in file
`5de02dba-212a-8144-8007-7dc064438707`.

---

## Archive structure confirmed

The `.penpot` archive contains **two Penpot files**:

| File ID | Name | Is Shared |
|---|---|---|
| `06a9b178-5157-81ae-8007-7dc819a6fb68` | System library | yes |
| `5de02dba-212a-8144-8007-7dc064438707` | Design mocks | no |

Pages in Design mocks: Dashboard, Landing page, Email, Thumbnail.

Each page's shapes are stored as **individual JSON files** in
`files/<file-id>/pages/<page-id>/<shape-id>.json`. This is the cleanest way to
read shape data — no transit+json parsing required.

---

## Dashboard shape inventory

| Metric | Count |
|---|---|
| Total shapes | 146 |
| Frames | 58 |
| Rects | 51 |
| Text | 23 |
| Paths | 14 |
| Shapes with `appliedTokens` | 120 (82%) |
| Component instances | 28 |

---

## Distinct component types

All components are defined in the System library (`componentFile: 06a9b178...`).

| Component name | Instances | Distinct variants used |
|---|---|---|
| `icon / core ui` | 12 | 8 different icon components |
| `pattern / bubble` | 5 | 1 (same component, reused) |
| `pattern / card` | 5 | 4 different card variants |
| `pattern / list / icon + link` | 5 | 1 |
| `core element / button` | 1 | 1 |

The naming convention is `category / subcategory` — useful pattern for mapping to
Fun.Blazor module/component name hierarchy.

---

## Token surface on Dashboard

### Property → token paths

| CSS property | Token paths used |
|---|---|
| `fill` | `color.background.{accent,alt,body,default}`, `color.button.primary.{background.default,icon,text}`, `color.icon.{accent,default}`, `color.text.{bright,main,muted}` + raw `palette.*` (see below) |
| `strokeColor` | `color.border.{accent,default}`, `color.button.primary.border` |
| `strokeWidth` | `stroke.hairline` |
| `typography` | `typography.{button,default,heading.level-2,meta,quote,strong}` |
| `p1/p2/p3/p4` (padding) | `spacing.{3xs,xs,sm}` |
| `m1/m2/m4` (margin) | `spacing.{2xs,xl}` |
| `r1/r2/r3/r4` (radius) | `radius.{3xs,xs,sm,full}` |
| `columnGap`, `rowGap` | `spacing.{micro,3xs,xs,sm,md,xl}` |
| `width`, `height` | `breakpoint`, `size.3xl` |

### Raw palette references

Some shapes bind `fill` directly to `palette.default.*`, `palette.primary.*`,
`palette.secondary.*` (050–950 stops). These are the **color swatch demonstration
shapes** on the page, not UI components. Real UI components consistently use
semantic tokens (`color.background.*`, `color.text.*`, etc.).

**Key observation**: the semantic color layer is complete for UI components. Raw
palette tokens appear only in demonstration/documentation shapes.

---

## Token → CSS property mapping for Fun.Blazor

Penpot's `appliedTokens` keys map to CSS as follows:

| `appliedTokens` key | CSS property |
|---|---|
| `fill` | `background-color` (or `color` for text) |
| `strokeColor` | `border-color` |
| `strokeWidth` | `border-width` |
| `r1/r2/r3/r4` | `border-radius` (top-left / top-right / bottom-right / bottom-left) |
| `p1/p2/p3/p4` | `padding-top / right / bottom / left` |
| `m1/m2/m4` | `margin-top / right / bottom` (m3 not used here) |
| `columnGap` | `column-gap` |
| `rowGap` | `row-gap` |
| `width` | `width` |
| `height` | `height` |
| `typography` | composite: `font-family`, `font-size`, `font-weight`, `line-height` |

---

## Prototype interactions

None found on the Dashboard page. The file uses static mockups, not prototyped
flows. Prototype path (Phase 7) will need to check other pages or design intent
from the transcript.

---

## Component token binding pattern

Every component instance carries its own `appliedTokens` — the token binding is
on the **instance**, not inherited from the master component. This means:
- Each instance can override token bindings independently
- All five `pattern / bubble` instances use identical token bindings (none overridden)
- `pattern / card` instances share the same binding shape but point to different
  component IDs (different card variants in the system library)

A Fun.Blazor component can use CSS custom properties for all properties in
`appliedTokens`. The component does not need to know which theme is active —
the CSS vars change at the `:root` level when the theme switches.

---

## Reading method assessment

| Method | Result | Notes |
|---|---|---|
| `.penpot` archive JSON | **Best for offline/CI** | Clean JSON per shape, no transit parsing |
| REST `get-file` + transit parsing | Works but complex | 800KB response; `fdata/objects-map` feature stores shapes inline as escaped strings within transit — doubly encoded, hard to parse with regex |
| MCP `execute_code` | **Best for live session** | Clean JS, live data, requires browser open + plugin connected |

**Finding**: for reading shapes, the archive JSON is the easiest path. For pushing
changes, REST is the only headless option. MCP is best for interactive validation
after a push.

---

## Open questions surfaced

- What do the System library's component master definitions look like? (variants,
  their names, what properties differ between variants — this tells us Fun.Blazor
  parameter types)
- The `typography` token is a composite — what fields does it carry in the token
  library? How does Penpot resolve it to individual CSS properties?
- `width: breakpoint` — what value does `breakpoint` resolve to in each breakpoint
  theme? This drives the responsive layout.
- `size.3xl` — what px value does this resolve to?
