# DTCG Spec — Research Context

This file captures everything learned about the spec in the session that designed this library (2026-04-28). A fresh session should read this before touching any code.

Spec site: https://www.designtokens.org/tr/2025.10/
Community group repo (cloned): `/home/ivan/nexus/VARIOUS/community-group/`
JSON schemas (authoritative, 2025.10 only): `/home/ivan/nexus/VARIOUS/community-group/www/public/schemas/2025.10/`
Technical reports (all versions): `/home/ivan/nexus/VARIOUS/community-group/www/public/TR/`
Implementation plan: `/home/ivan/.claude/plans/how-might-we-turn-toasty-deer.md`

---

## Version history

The library supports all four published versions. Files are auto-detected on parse and upgraded to the 2025.10 domain model.

| Version | Date | Status | TR path |
|---|---|---|---|
| First Editors' Draft | 2021-09-23 | Superseded | `TR/drafts/format/` |
| Second Editors' Draft | 2022-06-14 | Superseded | `TR/second-editors-draft/format/` |
| Third Editors' Draft | 2025-08-04 | Superseded | `TR/third-editors-draft/format/` |
| **2025.10** | 2025-10-28 | **Stable** | `TR/2025.10/format/` |

JSON schemas exist only for 2025.10. Older versions are identified by structural heuristics (see `Format.fs` version detection logic).

### What each version introduced

**First Editors' Draft (2021-09-23)**
- 5 types: `color` (hex string), `dimension` ("12px" string), `font`, `duration` ("100ms" string), `cubic-bezier`
- Properties named `type` and `value` — no `$` prefix
- No composite types, no Color module, no Resolver module

**Second Editors' Draft (2022-06-14)**
- Properties renamed to `$type` and `$value`
- `font` renamed to `fontFamily`; `cubic-bezier` renamed to `cubicBezier`
- `fontWeight` type added (numeric or keyword)
- Color and dimension still use string format
- No composite types, no Color module, no Resolver module

**Third Editors' Draft (2025-08-04)**
- **Color format overhauled**: hex string → color object with `colorSpace`, `components`, `alpha`, `hex`
- **Dimension format changed**: `"12px"` → `{value: 12, unit: "px"}`
- **Duration format changed**: `"100ms"` → `{value: 100, unit: "ms"}`
- 7 new types added: `number`, `shadow`, `border`, `transition`, `gradient`, `typography`, `strokeStyle`
- Color module introduced (14 color spaces, `none` keyword)
- No Resolver module

**2025.10 (stable)**
- Resolver module added
- `inset` boolean field added to shadow
- Minor refinements throughout

### Upgrade paths (all lossless)

**First ED → 2025.10**
- Rename `type`→`$type`, `value`→`$value`
- Rename `font`→`fontFamily`, `cubic-bezier`→`cubicBezier`
- Parse dimension string: `"12px"` → `{Value=12.0; Unit=Px}`
- Parse duration string: `"100ms"` → `{Value=100.0; Unit=Milliseconds}`
- Parse color hex: `"#ff00ff"` → `{ColorSpace=SRGB; Components=(Channel 1.0, Channel 0.0, Channel 1.0); Alpha=None; Hex=Some "#ff00ff"}`

**Second ED → 2025.10**
- Same as First ED except no property renaming (already `$type`/`$value`)

**Third ED → 2025.10**
- Shadow objects without `inset` field: default to `Inset=None` (spec default false)
- Otherwise identical domain model — Third ED and 2025.10 share the same type shapes

### Version detection heuristics (for files without `$schema`)

1. Properties use `type`/`value` (no `$`) → First Editors' Draft
2. `$type`/`$value` present + color value is a bare string → Second Editors' Draft
3. `$type`/`$value` + color value is an object → Third Editors' Draft or 2025.10
4. Resolver file present with `"version": "2025.10"` → 2025.10

---

## What the spec is

A vendor-neutral JSON standard for design token interchange between tools (Figma, Penpot, Sketch, code generators, etc.). Three modules:

- **Format** — defines `.tokens.json` file structure
- **Color** — defines the color value object with 14 color spaces
- **Resolver** — defines `.resolver.json` for multi-context theming (light/dark, mobile/desktop, etc.)

The spec is a W3C Community Group report, not a W3C Standard. It is stable at 2025.10.

---

## Format module

### File

- MIME type: `application/design-tokens+json` (preferred) or `application/json`
- Extensions: `.tokens` or `.tokens.json` (recommended, not required)
- Encoding: JSON (RFC 8259). No YAML, TOML, XML — JSON only.

### Node types

A `.tokens.json` file is a JSON object. Every property whose key does not start with `$` is either a **Token** or a **Group**:

- **Token**: has a `$value` property. Leaf node. Represents an actual design decision.
- **Group**: no `$value`. Organizational container. Can nest other groups and tokens.

### Token name rules

- Cannot start with `$`
- Cannot contain `.`, `{`, `}`
- Case-sensitive
- Regex: `^[^${}\.][^{}\.]*$`

### Metadata properties (shared by tokens and groups)

| Property | Type | Notes |
|---|---|---|
| `$type` | string enum | Token type; inherited downward from parent groups |
| `$description` | string | Plain text |
| `$deprecated` | bool or string | `true` = deprecated; string = deprecated with message |
| `$extensions` | object | Vendor data; keys are reverse-domain (`"figma.com": {...}`) |

Groups additionally have:
- `$root` — a token (with `$value`) that provides the group's own value alongside its children
- `$extends` — a reference to another group; deep-merge inheritance (local overrides inherited)

### Type system

`$type` must be explicitly declared or inherited from the nearest parent group. **No type inference from value shape** — if the type is ambiguous or missing, the token is invalid.

Type precedence (highest to lowest):
1. Explicit `$type` on the token
2. `$type` on the nearest parent group
3. `$type` resolved from the referenced token (for aliases)

### Reference syntax (two kinds)

**Curly-brace reference** — references a whole token by dot-path:
```
"{color.brand}"         → path ["color", "brand"]
"{color.palette.blue}"  → path ["color", "palette", "blue"]
```

**JSON Pointer reference** — references a sub-property of a value (RFC 6901):
```json
{ "$ref": "#/color/brand/$value/components/0" }
```
Used when you need to reference one component of a color, one stop of a gradient, etc. Curly-brace syntax cannot do sub-property access.

### Circular references

Not allowed anywhere — aliases, `$extends` chains, resolver set references. Tools must detect and error. The token graph is a DAG (directed acyclic graph).

### `$extensions` preservation

Tools must preserve `$extensions` data they do not understand. Round-tripping a file must not drop unknown extensions.

---

## Primitive types

### color

Value is always an object — never a bare hex string.

```json
{
  "colorSpace": "srgb",
  "components": [1, 0, 0],
  "alpha": 0.8,
  "hex": "#ff0000"
}
```

- `colorSpace`: required, one of 14 values (see Color module below)
- `components`: required, always exactly 3 elements; each is a number or the string `"none"`
- `alpha`: optional, `[0, 1]`, defaults to 1.0
- `hex`: optional, 6-digit `#RRGGBB` only (no alpha encoding, no 3-digit shorthand)

The `"none"` keyword in components means missing/inapplicable — distinct from `0`. Critical in cylindrical color spaces (HSL, OKLCH) where hue `none` interpolates differently than hue `0`.

### dimension

```json
{ "value": 16, "unit": "px" }
```

- `value`: number (integer or float)
- `unit`: `"px"` or `"rem"` only. Unit required even when value is 0.

### fontFamily

```json
"Comic Sans MS"
```
or
```json
["Helvetica", "Arial", "sans-serif"]
```

Single string or ordered array of strings (preference list).

### fontWeight

```json
700
```
or
```json
"bold"
```

Number `[1, 1000]` or one of these exact case-sensitive keywords:
`thin`, `hairline`, `extra-light`, `ultra-light`, `light`, `normal`, `regular`, `book`, `medium`, `semi-bold`, `demi-bold`, `bold`, `extra-bold`, `ultra-bold`, `black`, `heavy`, `extra-black`, `ultra-black`

### duration

```json
{ "value": 200, "unit": "ms" }
```

- `unit`: `"ms"` or `"s"` only.

### cubicBezier

```json
[0.4, 0, 0.2, 1]
```

Exactly 4 numbers `[P1x, P1y, P2x, P2y]`:
- P1x, P2x: `[0, 1]`
- P1y, P2y: unbounded (can be negative or > 1)

### number

Bare JSON number. Unitless. Used for line heights, opacity, etc.

### strokeStyle

String shorthand:
```json
"dashed"
```
Allowed: `solid`, `dashed`, `dotted`, `double`, `groove`, `ridge`, `outset`, `inset`

Or object for custom dash patterns:
```json
{
  "dashArray": [{ "value": 4, "unit": "px" }, { "value": 2, "unit": "px" }],
  "lineCap": "round"
}
```
- `dashArray`: array of dimension values or references
- `lineCap`: `"round"`, `"butt"`, or `"square"`

---

## Composite types

Composite type fields can each be a **literal value** OR a **token reference**. This is the `ValueOrRef<'T>` pattern.

### border

```json
{
  "color": { "colorSpace": "srgb", "components": [0, 0, 0], "alpha": 1 },
  "width": { "value": 1, "unit": "px" },
  "style": "solid"
}
```

All three fields required.

### shadow

Single object or array of objects/references:

```json
{
  "color": "{color.shadow}",
  "offsetX": { "value": 0, "unit": "px" },
  "offsetY": { "value": 4, "unit": "px" },
  "blur": { "value": 8, "unit": "px" },
  "spread": { "value": 0, "unit": "px" },
  "inset": false
}
```

- `inset`: optional boolean (defaults to false)
- Array elements can themselves be references

### transition

```json
{
  "duration": { "value": 200, "unit": "ms" },
  "delay": { "value": 0, "unit": "ms" },
  "timingFunction": [0.4, 0, 0.2, 1]
}
```

All three fields required.

### gradient

**Important:** DTCG gradient is only the color stops — no direction, angle, or type (linear/radial). Those are platform-specific and not part of the spec.

```json
[
  { "color": { "colorSpace": "srgb", "components": [1, 0, 0], "alpha": 1 }, "position": 0 },
  { "color": { "colorSpace": "srgb", "components": [0, 0, 1], "alpha": 1 }, "position": 1 }
]
```

- Array of `gradientStop` objects (minimum 2)
- `position`: number `[0, 1]`, or a reference

### typography

```json
{
  "fontFamily": ["Helvetica", "Arial", "sans-serif"],
  "fontSize": { "value": 16, "unit": "px" },
  "fontWeight": 400,
  "letterSpacing": { "value": 0, "unit": "px" },
  "lineHeight": 1.5
}
```

All five fields required. `lineHeight` is a unitless number.

---

## Color module — 14 color spaces

| colorSpace | Components | Ranges |
|---|---|---|
| `srgb` | R, G, B | [0,1] each |
| `srgb-linear` | R, G, B | [0,1] each |
| `display-p3` | R, G, B | [0,1] each |
| `a98-rgb` | R, G, B | [0,1] each |
| `prophoto-rgb` | R, G, B | [0,1] each |
| `rec2020` | R, G, B | [0,1] each |
| `hsl` | H, S, L | H:[0,360), S:[0,100], L:[0,100] |
| `hwb` | H, W, B | H:[0,360), W:[0,100], B:[0,100] |
| `lab` | L, A, B | L:[0,100], A/B: unbounded |
| `lch` | L, C, H | L:[0,100], C: unbounded, H:[0,360) |
| `oklab` | L, A, B | L:[0,1], A/B: unbounded |
| `oklch` | L, C, H | L:[0,1], C: unbounded, H:[0,360) |
| `xyz-d65` | X, Y, Z | [0,1] each |
| `xyz-d50` | X, Y, Z | [0,1] each |

The spec defers gamut mapping algorithm to implementers. Hex fallback loses color space precision and cannot encode alpha — it is for legacy tool compatibility only.

---

## Resolver module

### File structure

`.resolver.json` — required fields: `version` (must be exactly `"2025.10"`), `resolutionOrder`.

```json
{
  "version": "2025.10",
  "name": "My Design System",
  "sets": { ... },
  "modifiers": { ... },
  "resolutionOrder": [ ... ]
}
```

### Sets

A set is a named collection of token sources that merge in array order (last wins on conflict):

```json
{
  "base": {
    "sources": [
      { "$ref": "#/$defs/base-tokens" },
      "./tokens/primitives.tokens.json"
    ]
  }
}
```

### Modifiers

A modifier enables conditional token inclusion. Each modifier maps context names (at least 2) to token sources:

```json
{
  "theme": {
    "contexts": {
      "light": { "sources": ["./tokens/light.tokens.json"] },
      "dark":  { "sources": ["./tokens/dark.tokens.json"] }
    },
    "default": "light"
  }
}
```

Modifiers **cannot** reference other modifiers.

### Resolution order

An ordered array of set/modifier references. Later entries override earlier ones:

```json
"resolutionOrder": [
  { "set": "base" },
  { "modifier": "theme" },
  { "modifier": "size" }
]
```

### Four-stage resolution algorithm

1. **Validate inputs** — every key in the caller's input must name a known modifier; required modifiers (no default) must be present
2. **Order** — walk `resolutionOrder`; collect token sources in sequence
3. **Merge** — fold token files left-to-right; deep merge at group level, replace at token level
4. **Alias resolution** — DFS follow `Alias` values to terminal literals; abort on cycle

### I/O separation

`TokenSource.FileRef` loading is caller-supplied. The library `resolve` function takes a `loadFile: string -> Result<string, string>` parameter. This keeps all I/O outside the library core.

---

## F# type design decisions

### `ValueOrRef<'T>`

Composite type fields (border.color, shadow.offsetX, etc.) can be literal values or token references. Model as:

```fsharp
type ValueOrRef<'T> =
    | Literal   of 'T
    | Reference of TokenRef
```

### `ColorComponent`

The `"none"` keyword in color components must be distinct from `0.0`:

```fsharp
type ColorComponent =
    | Channel of float
    | None
```

### `TokenNode` mutual recursion

Groups contain children which are themselves tokens or groups — requires `and`:

```fsharp
type TokenNode =
    | TokenLeaf of Token
    | Group     of GroupData

and GroupData = {
    ...
    Children : Map<TokenName, TokenNode>
}
```

### `TokenName` private constructor

Validate on construction; downstream code gets a guarantee:

```fsharp
type TokenName = private TokenName of string
```

### Error collection, not short-circuit

Parse and validation collect all errors before returning (`Error list`), not `Result<_, Error>`. Callers get the full picture in one pass.

### `Deprecated` DU

```fsharp
type Deprecated =
    | Deprecated
    | DeprecatedWithMessage of string
```

Not `bool * string option` — the DU makes the two states explicit.

---

## What is NOT in scope for this library

- File I/O (load files from disk/network) — caller responsibility via `loadFile` callback
- Gamut mapping — deferred to implementers per spec
- JSON5 / JSONC parsing — spec says standard JSON only for `.tokens.json`
- Plugin/extension system for custom types
