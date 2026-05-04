---
area: Experiment
status: complete — 2026-05-03
phase: 3 — Token flow inward (Penpot → our tokens)
source: Penpot Tokens panel → Tools → Export → both single and multi-file
---

# Phase 3 Findings — Token Flow Inward

Source files:
- `samples/laura-system-library.tokens.json` (single-file export)
- `samples/laura-system-library-multifile.tokens.zip` (multi-file export)
Exported from: System Library file, Tokens panel → Tools → Export
Export format: Tokens Studio JSON (Penpot's native token format)

---

## Export method

The stable, supported path for token export from Penpot is:

**Tokens panel → Tools → Export → JSON**

This produces a single JSON file. Penpot claims this output adheres to the W3C DTCG
format ("tokens can be exported and integrated into other tools directly, without
conversion"). That claim is **overstated** — see gap analysis below.

Alternatives considered and rejected:
- `.penpot` archive `tokens.json` — same data, but archive format is not guaranteed stable
- MCP Plugin API serialization — functional but `tokens` API officially "coming soon"; no
  built-in export method exists on the API object
- REST `get-file` with `design-tokens/v1` — same data, transit+json encoding, harder to parse

---

## File structure

```
{
  "Set/Name": { ...tokens... },   // 22 top-level set keys
  "$themes":   [ ...12 themes... ],
  "$metadata": { "tokenSetOrder": [...] }
}
```

- 22 token sets, 305 tokens total
- 12 themes in `$themes` (the full group/name/selectedTokenSets matrix)
- `$metadata` holds the canonical set ordering (resolution precedence)
- 36% of tokens (110) are alias references `{path.to.other.token}`

---

## Type inventory

| Type | Count | DTCG 2025.10 status |
|---|---|---|
| `color` | 174 | ✓ valid |
| `number` | 57 | ✓ valid (except 10 math expressions — see below) |
| `typography` | 18 | partial — composite field names differ |
| `dimension` | 12 | ✓ valid |
| `spacing` | 11 | ✗ non-DTCG → maps to `dimension` |
| `borderRadius` | 10 | ✗ non-DTCG → maps to `dimension` |
| `fontFamilies` | 9 | ✗ non-DTCG → maps to `fontFamily` |
| `fontSizes` | 9 | ✗ non-DTCG → maps to `dimension` |
| `borderWidth` | 5 | ✗ non-DTCG → maps to `dimension` |

**44 tokens (14%) have non-DTCG type names.**

---

## Gap 1 — Non-DTCG type names (mechanical fix)

Five Tokens Studio types need renaming. Some also need value transformation:

| Tokens Studio | DTCG | Value change |
|---|---|---|
| `fontFamilies` | `fontFamily` | `["Figtree"]` → `"Figtree"` (unwrap single-element array) |
| `spacing` | `dimension` | bare number → `"{n}px"` (add unit) |
| `borderRadius` | `dimension` | bare number → `"{n}px"` (add unit) |
| `fontSizes` | `dimension` | bare number → `"{n}px"` (add unit) |
| `borderWidth` | `dimension` | bare number → `"{n}px"` (add unit) |

When the value is an alias reference (`{stroke.hairline}`) rather than a literal,
no unit suffix is needed — units carry through resolution.

---

## Gap 2 — Math expressions (design decision required)

10 tokens in `Foundations/Base` use Tokens Studio math syntax:

```
base      = "16 * {zoom}"
scale.xs  = "round({base} * pow({multiplier}, -1))"
scale.sm  = "round({base} * pow({multiplier}, 0))"
scale.md  = "round({base} * pow({multiplier}, 1))"
...etc
```

The dependency chain:
```
zoom        ← Text zoom set: 1 (100%), 1.5 (150%), 2 (200%)
base        = 16 × zoom
multiplier  ← Breakpoint set: 1.1 (Mobile), 1.125 (Tablet), 1.25 (Desktop)
              OR Foundations/Base fallback: 1
scale.*     = round(base × multiplier^N)   where N ∈ {-3..5}
```

**Consequence**: `scale.*` tokens resolve to different concrete px values depending on
which Breakpoint and Text zoom sets are active. They cannot be given a single concrete
value without specifying a theme combination.

Example: `scale.md` at 100% zoom / Desktop breakpoint:
```
zoom = 1, base = 16, multiplier = 1.25
scale.md = round(16 * 1.25^1) = round(20) = 20px
```

Same token at 150% zoom / Mobile:
```
zoom = 1.5, base = 24, multiplier = 1.1
scale.md = round(24 * 1.1^1) = round(26.4) = 26px
```

**Shim policy options:**
- **A — Preserve as-is**: emit math expressions as string values with type `number`.
  Downstream tooling (or the resolver) must evaluate. Lossy only if the consumer
  can't handle math.
- **B — Evaluate at export time**: caller specifies active theme combination; shim
  resolves concrete values for that combination. Clean output, but single-theme snapshot.
- **C — Skip with warning**: omit math-expression tokens from output, emit `Skipped`
  entries. Safest for a parser that can't handle math.

Recommended: **A for the shim** (preserve), **B for the CSS emitter** (resolve per
theme when emitting `:root` + `@media` blocks). The two phases serve different purposes.

---

## Gap 3 — Typography composite field names (mechanical fix)

Tokens Studio composite `$value` for `typography` type uses different field names
than DTCG 2025.10:

| Tokens Studio field | DTCG field |
|---|---|
| `fontFamilies` | `fontFamily` |
| `fontSizes` | `fontSize` |
| `fontWeights` | `fontWeight` |
| `lineHeights` | `lineHeight` |

Example (Tokens Studio):
```json
{
  "$type": "typography",
  "$value": {
    "lineHeights": "1.1",
    "fontSizes": "{font-size.sm}",
    "fontWeights": "600",
    "fontFamilies": ["{font-family.default}"]
  }
}
```

After shim:
```json
{
  "$type": "typography",
  "$value": {
    "lineHeight": "1.1",
    "fontSize": "{font-size.sm}",
    "fontWeight": "600",
    "fontFamily": "{font-family.default}"
  }
}
```

Note: `fontFamilies` inside a composite value is also an array — same unwrap needed.

---

## Gap 4 — `$themes` and `$metadata` (structural, not token-level)

`$themes` is Tokens Studio's theme registry:
```json
{
  "id": "...",
  "name": "Always-on",
  "group": "Global",
  "selectedTokenSets": {
    "Foundations/Base": "enabled",
    "Typography": "enabled",
    ...
  }
}
```

DTCG 2025.10 has no theme concept. Options:
- Move to `$extensions.tokensStudio.themes` (standard extension slot)
- Preserve in a parallel file (e.g. `themes.json`) alongside the DTCG token file
- Discard if only the token values are needed

`$metadata.tokenSetOrder` is the canonical resolution precedence. This maps to the
resolver's `resolutionOrder`. Must be preserved — not in DTCG but essential for
correct resolution. Put in `$extensions.tokensStudio.metadata` or a parallel file.

---

## What parses cleanly today (no shim needed)

Against `FnTools.DesignTokens` `Format.parse` (DTCG 2025.10):
- All 174 `color` tokens with hex literals and alias references ✓
- All 12 `dimension` tokens with unit-bearing values ✓
- All `number` tokens with literal numeric values (not math expressions) ✓

**47 of 57 `number` tokens** pass as-is. The 10 math expressions would fail or require
special handling.

---

## Shim specification (what needs to be built)

Module: `TokenStudio` in `FnTools.DesignTokens` (or a separate `.Css` or `.Adapters`
module — TBD at layer split).

Input: Tokens Studio JSON (top-level set keys + `$themes` + `$metadata`)
Output: DTCG 2025.10 JSON + separated themes/metadata structure

Required transforms:
1. **Type rename pass** — map 5 TS types to DTCG equivalents
2. **fontFamily unwrap** — `["X"]` → `"X"` at token level and inside typography composite
3. **Typography field rename** — 4 field names in composite `$value`
4. **Dimension unit injection** — add `px` suffix to bare-number dimension values
   (only for literal values; alias references pass through unchanged)
5. **Math expression policy** — configurable: preserve / evaluate / skip
6. **`$themes` extraction** — separate from token output; preserve as resolver config
7. **`$metadata` extraction** — preserve `tokenSetOrder` as resolver `resolutionOrder`

Items 1–4 and 6–7 are mechanical transforms with no design ambiguity.
Item 5 requires a policy decision and potentially a math expression evaluator.

---

## Single-file vs multi-file export — comparison

Both formats were tested. Key findings:

**Token content: 100% identical.** Zero diffs across all 22 sets. Same values, same
types, same alias references. The format choice is purely structural.

**Structural difference:**

Single-file — set name is the top-level key:
```json
{
  "Brand/Core": { "font-family": {...}, "stroke": {...} },
  "Foundations/Base": { "scale": {...}, ... },
  "$themes": [...],
  "$metadata": {...}
}
```

Multi-file — set name is the file path; file contains tokens directly (no wrapper key):
```
Brand/Core.json          → { "font-family": {...}, "stroke": {...} }
Foundations/Base.json    → { "scale": {...}, ... }
$themes.json             → [...]
$metadata.json           → {...}
```

The folder hierarchy mirrors the `/` in set names (`Brand/Core` → `Brand/Core.json`).

**Multi-file is cleaner for the shim**: each file is a self-contained set with no
wrapping key to strip. The set name is derived from the file path. `$themes` and
`$metadata` are separate files rather than embedded keys.

**`$metadata` carries more than `tokenSetOrder` — key discovery:**

Both exports include `activeThemes` and `activeSets` in `$metadata`:

```json
{
  "tokenSetOrder": ["Foundations/Base", ...],
  "activeThemes": ["Brand/Eco Tools", "Color mode/Light", "Global/Always-on",
                   "Breakpoint/Tablet", "Text zoom/100%"],
  "activeSets":   ["Brand/Eco Tools", "Components/Button", "Typography", ...]
}
```

`activeThemes` and `activeSets` capture the **UI state at export time** — which theme
combination was active in the Tokens panel when the user clicked Export. This is the
highest-fidelity record of active state we have found. Contrast with:
- REST `get-file` `active-themes`: only contains `[/__PENPOT__HIDDEN__TOKEN__THEME__]`
  (not updated by `set.active` Plugin API calls)
- MCP `theme.isActive`: always empty (reads from same REST field)
- `$metadata.activeThemes`: accurate, captured from the live Tokens panel UI state

**Implication**: the export is the most reliable way to capture the intended active
theme state. If you need to know "what was the designer's intended theme combination
for this file", the exported `$metadata.activeThemes` is the answer.

**Implication for the shim**: support both formats as input. Multi-file is preferred
(cleaner structure); single-file is the fallback. Parse `$metadata.activeThemes` and
`$metadata.activeSets` as resolver configuration input.

---

## Open questions

- Should the shim live in `FnTools.DesignTokens.Format` (codec concern) or in a new
  `FnTools.DesignTokens.Adapters` project (third-party format concern)?
- Math evaluator: implement a minimal `round(x * pow(y, n))` evaluator, or use a
  general expression parser? The expressions in this file are structurally limited —
  only `round(a * pow(b, n))` and `a * b` patterns appear.
- Is `fontFamily` value always a single-element array in Tokens Studio, or can it be
  multi-element? If multi-element, join with comma (CSS font-family stack convention).
