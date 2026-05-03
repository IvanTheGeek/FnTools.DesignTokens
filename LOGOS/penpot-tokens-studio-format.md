---
area: Integration
status: reference — 2026-05-03
source: Laura Calbach "Design Systems at Scale | Penpot × Tokens Studio" hands-on demo
        + direct analysis of Design mocks.penpot archive
---

# Tokens Studio Format — Penpot Native Internals

Reference document covering the Tokens Studio multi-set format as used natively inside Penpot.
Distinct from `penpot-api.md` which covers the REST/import API layer. This document describes
the **internal** structure of the `.penpot` zip archive and how it diverges from DTCG 2025.10.

---

## `.penpot` archive structure

A `.penpot` file is a zip archive. The `Design mocks.penpot` file from the demo contains
**two Penpot files** in one archive:

```
files/
  <file-id>.json               ← file manifest (name, features, migrations, revn)
  <file-id>/
    tokens.json                ← ALL token sets for this file (Tokens Studio format)
    media/                     ← uploaded images
    thumbnails/
    pages/
      <page-id>.json           ← page manifest (name, index)
      <page-id>/
        <shape-id>.json        ← one JSON per shape
```

Files in this archive:
- `06a9b178...` — "System library" (`isShared: true`), two pages: Typography + Color Demo,
  1286 shapes, 1019 with `appliedTokens`
- `5de02dba...` — "Design mocks", four pages: Dashboard, Landing page, Email, Thumbnail,
  318 shapes, 258 with `appliedTokens`

Both files carry the same `tokens.json` — when working in Penpot, the library syncs tokens
to every referencing file.

---

## `tokens.json` — Tokens Studio multi-set format

Penpot stores tokens in Tokens Studio's multi-set format, **not** DTCG 2025.10:

```json
{
  "<set-name>": {
    "<group>": {
      "<token>": {
        "$type": "<token-studio-type>",
        "$value": "<value-or-expression>",
        "$description": ""
      }
    }
  },
  "$themes": [ ... ],
  "$metadata": { ... }
}
```

Top-level keys are **set names** (e.g. `"Foundations/Base"`, `"Color/Dark Core"`), with
`$themes` and `$metadata` as special reserved keys.

### Tokens Studio types — not DTCG 2025.10

The `$type` values diverge from DTCG:

| Tokens Studio `$type` | DTCG 2025.10 `$type` | Notes |
|---|---|---|
| `fontFamilies` | `fontFamily` | Plural vs singular |
| `fontSizes` | `dimension` | TS keeps font sizes separate from dimensions |
| `fontWeights` | `fontWeight` | Matches DTCG |
| `spacing` | `dimension` | TS splits spacing from generic dimension |
| `borderWidth` | `dimension` | TS splits border widths too |
| `typography` | `typography` | Composite; field names differ (see below) |
| `color` | `color` | `$value` format differs (see below) |
| `number` | `number` | Matches DTCG |
| `dimension` | `dimension` | Used for breakpoint px values |

### Color values — HSL expressions, not objects

DTCG 2025.10 requires `$value` to be a color object:
```json
{ "colorSpace": "srgb", "components": [1.0, 0.0, 0.0], "hex": "#ff0000" }
```

Tokens Studio / Penpot uses:
- Static hex: `"#ff0000"`
- Alias to another token: `"{palette.red.500}"`
- **Math expression with HSL**: `"hsla({hue.blue},{saturation.colors},{lightness.600},1)"`

The HSL expression form is a Tokens Studio extension — the hue, saturation, and lightness
are themselves tokens, enabling the entire palette to be driven by 3 primitive values.

### Math expressions

Tokens Studio supports arithmetic in `$value` strings:

```json
"scale.xs": { "$value": "round({base} * pow({multiplier}, -1))", "$type": "number" }
"base":     { "$value": "16 * {zoom}", "$type": "number" }
```

Functions: `round()`, `pow()`, basic arithmetic (`*`, `/`, `+`, `-`).

DTCG 2025.10 has **no computation support** — all values must be concrete or alias references.
Math expressions are a Tokens Studio-only capability; our library does not parse them.

### Typography composite — field names differ

Tokens Studio:
```json
{
  "$type": "typography",
  "$value": {
    "fontSizes": "{font-size.md}",
    "fontFamilies": ["{font-family.default}"],
    "fontWeights": "500",
    "lineHeights": "1.1"
  }
}
```

DTCG 2025.10:
```json
{
  "$type": "typography",
  "$value": {
    "fontSize": { "value": 16.0, "unit": "px" },
    "fontFamily": ["Figtree"],
    "fontWeight": { "value": 500.0 },
    "lineHeight": { "value": 1.1 }
  }
}
```

Field keys differ (`fontSizes` vs `fontSize`, `fontFamilies` vs `fontFamily`, etc.) and
value shapes differ (strings/expressions vs typed objects).

---

## `$themes` — multi-dimensional theme system

`$themes` is an array of theme objects; each names a group of sets to activate:

```json
[
  { "id": "...", "name": "Always-on", "group": "Global",
    "selectedTokenSets": { "Foundations/Base": "enabled", "Typography": "enabled", ... } },
  { "id": "...", "name": "Light", "group": "Color mode",
    "selectedTokenSets": { "Color/Light Core": "enabled", "Color/Light Component": "enabled" } },
  { "id": "...", "name": "Dark", "group": "Color mode",
    "selectedTokenSets": { "Color/Dark Core": "enabled", ... } },
  { "id": "...", "name": "Mobile", "group": "Breakpoint",
    "selectedTokenSets": { "Breakpoints/Mobile": "enabled" } },
  { "id": "...", "name": "Desktop", "group": "Breakpoint",
    "selectedTokenSets": { "Breakpoints/Desktop": "enabled" } }
]
```

Theme groups: `"Global"`, `"Color mode"` (pick one), `"Brand"` (pick one),
`"Breakpoint"` (pick one), `"Text zoom"` (pick one).

`$metadata.tokenSetOrder` defines the resolution order — **later set wins**. The breakpoint
sets override `multiplier`; the brand sets override `hue`; `Always-on` sets the base values.

### Relation to DTCG resolver

`$themes` + `$metadata.tokenSetOrder` is conceptually the same as DTCG's `.resolver.json`
`resolutionOrder` array. Our resolver handles the merge; Tokens Studio implements the same idea
with a different syntax. The key insight: **theme switching is resolver switching**.

---

## Architecture of Laura's design system

The demo file implements a complete scalable system using five layers:

### Layer 1: Foundations/Base — the mathematical root

```
base = 16 * zoom          ← base font size × accessibility zoom multiplier
multiplier = 1            ← default (overridden by breakpoint themes)
scale.xs = round(base * pow(multiplier, -1))
scale.sm = round(base * pow(multiplier,  0))
scale.md = round(base * pow(multiplier,  1))
...
scale.3xl = round(base * pow(multiplier, 5))
```

One change to `multiplier` (e.g. `1.1` on mobile, `1.25` on desktop) reshapes the entire
typographic and spacing scale. One change to `zoom` (1.0 / 1.5 / 2.0) scales everything
for browser text-zoom accessibility testing.

`pow()` is used **instead of** chained references (e.g. `scale.md = scale.sm * multiplier`)
because chained references create a sequential dependency — each step must resolve before
the next starts. `pow()` resolves all values in one pass, no sequential dependency.

### Layer 2: Foundations/Spacing + Foundations/Radius + Foundations/Sizing

Pure aliases into the scale:
```
spacing.xs = {scale.xs}
spacing.md = {scale.md}
radius.sm  = {scale.xs}
```

All spacing and sizing values automatically follow the breakpoint multiplier.

### Layer 3: Color/Palettes and Scales — HSL math palette

```
hue.blue = 210             ← overridden by Brand themes
saturation.colors = 80
lightness.600 = 40

palette.blue.600 = hsla({hue.blue}, {saturation.colors}, {lightness.600}, 1)
```

The entire color palette is driven by one hue value. Swapping `hue.default` from `210`
(Core/blue) to `120` (Eco Tools/green) to `140` shifts every palette color.

### Layer 4: Color/Light Core + Color/Dark Core — semantic layer

```
color.background.default = {palette.default.050}   ← light mode
color.background.default = {palette.default.800}   ← dark mode (same name, different set)
```

Semantic names reference palette entries. Dark/Light mode is set switching, not separate
named tokens.

### Layer 5: Breakpoints — multiplier overrides

```
Breakpoints/Mobile:  multiplier = 1.1,   breakpoint = 360px
Breakpoints/Tablet:  multiplier = 1.125, breakpoint = 768px
Breakpoints/Desktop: multiplier = 1.25,  breakpoint = 1200px
```

Only two values per breakpoint. Everything else follows from the base layer.

---

## `appliedTokens` — shape binding

Every shape that uses design tokens has an `appliedTokens` object:

```json
{
  "name": "content",
  "type": "frame",
  "appliedTokens": {
    "fill": "color.background.default",
    "fontSize": "typography.font-size.md",
    "typography": "typography.strong",
    "p1": "spacing.md",
    "p2": "spacing.md",
    "p3": "spacing.md",
    "p4": "spacing.md",
    "r1": "radius.sm",
    "r2": "radius.sm",
    "r3": "radius.sm",
    "r4": "radius.sm",
    "columnGap": "spacing.md",
    "rowGap": "spacing.sm",
    "strokeColor": "color.border.default",
    "strokeWidth": "stroke.hairline",
    "width": "breakpoint",
    "height": "sizing.xl"
  }
}
```

Binding attributes: `fill`, `fontSize`, `typography` (composite), `p1`/`p2`/`p3`/`p4`
(padding sides), `m1`/`m2`/`m3`/`m4` (margin sides), `r1`/`r2`/`r3`/`r4` (border-radius
corners), `columnGap`, `rowGap`, `strokeColor`, `strokeWidth`, `width`, `height`.

Values are token paths in dot notation (e.g. `"spacing.md"`), matching the `tokens.json`
nested structure flattened to dots.

---

## Penpot API-first workflow

Both surfaces are live in **Penpot 2.14**:
- **Plugins API** (`penpot.tokens`) — available since 2.14, confirmed working
- **REST API** — `update-file` with `set-token-set` / `set-token` change ops, confirmed working in 2.14.4
- **Penpot MCP server** (`@penpot/mcp`) — wraps the Plugins API, requires browser open

The correct workflow is NOT import/export via file — it is:
1. Maintain tokens in DTCG 2025.10 format in our library
2. Convert to Tokens Studio hex format via `serializePenpot` (one set)
3. Push directly to Penpot via REST `update-file` or via MCP `execute_code`
4. Query token application via Plugins API / MCP — "which shapes use `color.text.main`?"

The MCP server enables Claude to:
- List token sets in a file
- Read which shapes use a given token
- Validate: "after pushing the new token set, are all components updated?"

This is the correct level of interaction — not file round-trips.

---

## What our library can / cannot handle from this format

| Tokens Studio feature | Our library | Notes |
|---|---|---|
| Standard DTCG types (color, dimension, fontFamily, etc.) | Parses OK | After stripping Tokens Studio wrapper |
| Tokens Studio type names (`fontFamilies`, `fontSizes`, etc.) | Not parsed | Would need a TS→DTCG type-name mapping layer |
| Math expressions (`round({base} * pow(...))`) | Not parsed | TS-only; no DTCG equivalent |
| HSL expressions (`hsla({hue},{sat},{light},1)`) | Not parsed | TS-only dynamic color |
| `$themes` + `$metadata` | Not parsed | Maps to DTCG resolver semantics (different syntax) |
| Multi-set file structure | Not parsed | `Format.parse` expects DTCG top-level structure |
| Single set content (DTCG-compatible types only) | Parses via `serializePenpot` import path | Strip set wrapper first |
| Round-trip: DTCG → `serializePenpot` → Penpot import | Works for color/dimension/fontFamily | Confirmed working |

---

## Experiments worth running

1. **Parse DTCG-compatible subsets from Tokens Studio format** — write an F# function that
   reads a Tokens Studio `tokens.json`, strips non-DTCG types and math expressions, and
   returns parseable DTCG JSON for each set. Use Laura's file as the test input.

2. **Token resolution via API** — call Penpot REST API to read the current token state of
   the imported Laura file; verify that the resolved values match what Tokens Studio
   would compute.

3. **MCP round-trip** — push our `ivanthegeek.tokens.json` via API, then use the Penpot MCP
   server to verify that the shapes in a test frame have the correct applied tokens.

4. **Theme-aware CSS emitter** — given a set of active theme names (e.g. `["Dark", "Mobile"]`),
   resolve the token merge in our library and emit two CSS blocks (`:root` base + media query
   override). Tests: same output as Penpot's resolved values for those themes.
