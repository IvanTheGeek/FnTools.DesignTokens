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

## Gap 5 — HSL expression palette colors (mechanical fix + design decision)

`Color/Palettes and Scales` — the entire palette set — uses Tokens Studio's HSL
expression syntax rather than hex literals:

```
palette.blue.100: hsla({hue.blue},{saturation.colors},{lightness.100},1)
palette.blue.200: hsla({hue.blue},{saturation.colors},{lightness.200},1)
...
```

Where the alias targets are `number` tokens:
```
hue.blue        = 203
saturation.colors = "70%"   (Tokens Studio stores as string with %)
lightness.100   = 95
lightness.200   = 88
...
```

**110 tokens (36%)** use this pattern — the entire palette. DTCG 2025.10 `color` type
requires hex, named color, or `{alias}` references. HSL expressions are not valid.

**Implemented: B — evaluate at import time.** The shim builds a flat alias index
across all sets, resolves `{hue.blue}` → `203`, `{saturation.colors}` → (alias chain)
→ `80`, `{lightness.100}` → `95`, then computes `hsl(203, 80%, 95%)` → `#d4e8f5` (hex).
All 139 palette color tokens resolve correctly. Saturation values are bare numbers
(0–100), not `%` strings — the `%` strip was not needed.

---

## Gap 6 — `"transparent"` as color value (design decision)

Two tokens use the CSS keyword `"transparent"` as their `color` value:
```
Color/Dark Component.color.button.primary.border  = "transparent"
Color/Light Component.color.button.primary.border = "transparent"
```

DTCG 2025.10 does not list `transparent` as a valid `color` value (requires hex or alias).

**Options:**
- Map `"transparent"` → `"#00000000"` (transparent black, 8-digit hex DTCG form) — lossless
- Map `"transparent"` → `"#ffffff00"` (transparent white) — equally valid, different intent
- Keep as `"transparent"` with type coerced to `string` — but loses type semantics
- Emit `Skipped` — safest but breaks two real tokens

**Implemented:** emit structured DTCG color object `{colorSpace: "srgb", components: [0,0,0], alpha: 0}`
instead of 8-digit hex. The hex form `#00000000` was tried first but the Validation module's
`hexRegex` only accepts 6-digit hex. The structured object is the cleaner DTCG 2025.10 form
and bypasses the validation issue without modifying the Validation module.

---

## What parses after shim (as of 2026-05-03)

Against `FnTools.DesignTokens` `Api.import` (DTCG 2025.10), after `TokensStudio.shim`:

| Set | Tokens | Single-set result |
|---|---|---|
| Color/Palettes and Scales | 139 | ✓ all parse — HSL evaluated to hex |
| Breakpoints/Desktop | 2 | ✓ |
| Breakpoints/Mobile | 2 | ✓ |
| Breakpoints/Tablet | 2 | ✓ |
| Text zoom/100% | 1 | ✓ |
| Text zoom/150% | 1 | ✓ |
| Text zoom/200% | 1 | ✓ |
| All Brand/* sets | per-set | ✗ cross-set ref `{stroke.hairline}` |
| Color/Dark-Light Core/Accent/Component | per-set | ✗ cross-set refs to `{palette.*}` |
| Foundations/Base | 13 | ✗ `PreserveMath` expressions fail `Api.import` |
| Foundations/Spacing, Sizing, Radius | per-set | ✗ cross-set refs to `{scale.*}` |
| Typography | 18 | ✗ cross-set refs to `{scale.*}` |
| Components/Button | per-set | ✗ cross-set refs to `{typography.*}` |

**148 of 305 tokens (49%) parse in single-set mode.** The remaining 157 are not shim
failures — they are cross-set alias references that the multi-set resolver handles
when all sets are loaded together. `Foundations/Base` math-expression tokens parse
only when `SkipMath` is used or when evaluated by the CSS emitter.

**Additional transforms discovered during implementation** (beyond the original 7-item spec):
- Tokens Studio stores `number` `$value` as JSON **strings** (e.g. `"203"`), not numbers.
  Shim converts to JSON numbers where possible.
- DTCG 2025.10 `dimension` `$value` format is `{value: float, unit: string}` (not
  `"16px"` string — that was the older spec's string upgrade path, which is not applied
  for V2025_10 documents).
- Typography `fontWeight`: `"400 Italic"` (combined weight+style) → extract numeric part
  `400`. The italic suffix is a Tokens Studio non-standard; DTCG has no `fontStyle` in
  `typography` composite.

---

## Shim implementation (built 2026-05-03)

Project: `src/FnTools.DesignTokens.TokensStudio/` — standalone layer, depends on Foundation only.

Entry points:
- `TokensStudio.shim json` — default config (PreserveMath)
- `TokensStudio.shimSingleFile config json` — explicit config
- Returns `ShimResult` with `Sets: Map<string, string>` (set name → DTCG JSON text)
  + `Themes: TokensStudioTheme list` + `Metadata: TokensStudioMetadata` + `Warnings`

Implemented transforms (11 total, up from original 9-item spec):
1. **Type rename** — `fontFamilies`→`fontFamily`, `spacing`/`borderRadius`/`fontSizes`/`borderWidth`→`dimension`
2. **fontFamily array unwrap** — `["X"]` → `"X"` at token and typography composite level
3. **Typography field rename** — `fontFamilies/fontSizes/fontWeights/lineHeights` → DTCG names
4. **Dimension object emit** — bare number strings → `{value: N, unit: "px"}` objects
   (DTCG 2025.10 format; alias refs pass through as strings)
5. **number string→number** — `"203"` → `203` JSON number; math expressions handled by policy
6. **Math expression policy** — `PreserveMath` (keep as string) | `SkipMath` (omit + warn)
7. **HSL evaluation** — flat alias index → resolve chain → `hsl(h, s%, l%)` → hex
8. **`"transparent"` normalization** — DTCG `{colorSpace:"srgb", components:[0,0,0], alpha:0}`
9. **Typography `fontWeight` coercion** — `"600"` → `600`; `"400 Italic"` → `400` (style suffix dropped)
10. **Typography `lineHeight` coercion** — `"1.1"` → `1.1` JSON float
11. **`$themes` / `$metadata` extraction** — `TokensStudioTheme list` + `TokensStudioMetadata`

`$metadata.activeThemes` is preserved as the most reliable record of the designer's
intended active theme combination at export time.

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

## Plugin comparison

### Juanfran — Color styles to JSON
Output: empty (89 bytes, `$themes: []`, `$metadata` all empty).
Reason: reads Penpot **native color styles** (Assets panel → Colors), not the Tokens tab.
Laura's file uses Tokens Studio tokens exclusively — no native color styles defined.
**Finding**: native Penpot color styles and Tokens Studio tokens are completely separate
storage systems. A plugin targeting one is blind to the other.

### Design Token Manager (Elhombretecla)
Reads from the Tokens tab. Sees all 22 sets with correct token counts (matches our
analysis exactly: 305 tokens total). Has a built-in math expression evaluator.

**Author:** Juan de la Cruz — Co-founder of Penpot. This is not a community plugin.
**License:** MIT
**First commits:** February 2026 (after native tokens already shipped). Last touched March 2026.
**Total commits:** 31

**Export claim vs reality:** README states "Import / Export: Upload and download token sets
as JSON files" but zero files in the codebase mention `export` or `download`. The feature
was documented but never implemented. No export functionality exists in the UI.

**Math evaluator — Design Token Manager syntax:**
- Basic operators: `+`, `-`, `*`, `/`
- Function: `roundTo(value, decimals)`
- Constraint: spaces required around operators; cannot mix units; no composite token math

**Syntax mismatch with Laura's tokens:**
Laura's math: `round({base} * pow({multiplier}, -1))` — uses `round()` and `pow()`.
DTM documents `roundTo()` only, no `pow()`. Incompatible syntax — DTM would show raw
expression strings for Laura's scale tokens, not resolved values.

**Verdict:** Not useful for our workflow. No export, math syntax mismatch with Laura's
tokens, and superseded by the native Tokens panel for everything else. The comparison
view and alias resolution UI are the only features not in the native panel, but they
don't add value to our pipeline.

### Color Tokens Plugin (colorTokens)
Reads and writes colors only. Lets you paste a color palette in JSON and push it into
the Tokens panel. Export produces a flat JSON file.

**Export structure** (`samples/color-tokens-plugin-export.json`, 18 tokens):
```json
{
  "primary.pale.100": { "$type": "color", "$value": "#c4d5e6" },
  "primary.pale.200": { "$type": "color", "$value": "#b2c8df" },
  ...
}
```

The keys use dot-notation as flat strings — NOT nested objects. This is **incompatible**
with FnTools.DesignTokens (and DTCG): token names cannot contain `.` and the parser
expects nested JSON where the dot-path is expressed as object nesting:
```json
{
  "primary": {
    "pale": {
      "100": { "$type": "color", "$value": "#c4d5e6" }
    }
  }
}
```

`Api.import` rejects all 18 tokens immediately (invalid token name `"primary.pale.100"`).

**Use case**: quick palette injection into an empty Tokens panel. Not useful for
extracting tokens back out — wrong structure for DTCG consumers.

### 72F Design System Generator
Manifest: `https://72f-studio.github.io/72f-design-system-generator/manifest.json`
Permissions: `content:read`, `content:write`, `library:read`, `library:write`

Reads tokens via `penpot.library.local.tokens` (the same Plugin API we use). Sees all
22 sets and all 305 tokens. Its export handler:

```javascript
const i = [];
o.sets.forEach(a => {
  (a.tokens ?? []).forEach(f => {
    i.push({ name: f.name, type: f.type, value: f.value });
  });
});
// sends: { type: "export-tokens", tokens: i }
```

**What this loses vs the native Penpot export:**
- Set context — no set name on each token; resolution order gone
- `$themes` — no theme definitions
- `$metadata` — no `tokenSetOrder` / `activeThemes`
- Result is a flat array of `{name, type, value}` with no DTCG structure

**What it does not fix:** same Tokens Studio types (`borderWidth`, `spacing`, etc.),
same HSL expressions (`hsla({hue.red},{saturation.colors},{lightness.950},1)`), same
`transparent` color values, same math expressions. All the gaps from the native export
are present here too.

**Verdict:** Strictly less information than the native Tokens Studio export. This plugin
is a *generator* — its value is creating design systems from templates. The export is
a secondary feature intended for round-tripping its own generated tokens, not for
extracting an existing system.

**UX note:** The interaction model is worth studying as inspiration. It offers a
structured, form-driven workflow for token creation, set management, and theme
definition — more approachable than editing raw JSON. A version of this UX built on
top of true DTCG-compliant tokens (rather than Tokens Studio format), with CSS
ingestion and conversion flows, would serve the design system app vision well. See
`design-system-app-vision.md`.

### Design MD Skills (TypeUI)
Manifest: `https://penpot-design-skills-plugin.vercel.app/manifest.json`
Permissions: `content:read`, `allow:downloads`

The `plugin.js` reads only `penpot.theme` — for UI theming of the plugin window.
No token API calls. All work happens in the plugin iframe: a manual form where you
enter design system details (color tokens, spacing, typography, etc.) and it generates
a `skill.md` file for AI tools (Claude, Codex, Gemini, Cursor).

**Verdict:** Not a token extractor — a manual documentation generator. Interesting
concept (structured design system markdown for AI consumption) but no Penpot token
integration. Requires manual data entry.

### UI Color Palette (Tokens Studio-compatible)
Paywalled. Requires account. Not tested. Manifest:
`https://ui-color-palette.com/penpot/manifest.json`

---

## Open questions

- Should the shim live in `FnTools.DesignTokens.Format` (codec concern) or in a new
  `FnTools.DesignTokens.Adapters` project (third-party format concern)?
- Math evaluator: implement a minimal `round(x * pow(y, n))` evaluator, or use a
  general expression parser? The expressions in this file are structurally limited —
  only `round(a * pow(b, n))` and `a * b` patterns appear.
- Is `fontFamily` value always a single-element array in Tokens Studio, or can it be
  multi-element? If multi-element, join with comma (CSS font-family stack convention).
- HSL evaluator: `saturation.colors` in Laura's file is stored as `"70%"` (string with
  `%`). Is that always the case, or does Tokens Studio store it as a bare number?
  Determines whether the shim strips the `%` or converts from a 0–1 float.
- `"transparent"` → `"#00000000"` mapping: confirm that no information is lost given
  the actual usage (button border at zero opacity where channel values are irrelevant).
