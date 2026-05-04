---
area: Experiment
status: complete — 2026-05-04
phase: 3 — Token flow inward (Penpot → our tokens)
---

# Phase 3 Findings — Token Flow Inward

Goal: characterise what `shimSingleFile` + `Format.parse` produces from Laura's
Tokens Studio export (`laura-system-library.tokens.json`).
Input file: 22 sets, 12 themes, 305 token slots across all sets.

Scripts: `scripts/phase3-inward.fsx`, `scripts/phase3-detail.fsx`.

---

## Result

**All 22/22 sets shim and parse cleanly — 0 warnings, 0 errors.**
The shim + `Format.parse` pipeline accepts the entire Laura token file without
any special handling or subset restriction.

---

## Set inventory

| Set | Tokens | Notes |
|---|---|---|
| Foundations/Base | 15 | math expressions — see below |
| Foundations/Spacing | 11 | alias refs into Foundations/Base |
| Foundations/Radius | 10 | alias refs into Foundations/Base |
| Foundations/Sizing | 9 | alias refs into Foundations/Base |
| Color/Palettes and Scales | 139 | palette colors |
| Color/Light Core | 11 | aliases into palette |
| Color/Light Accent | 10 | aliases into palette |
| Color/Light Component | 10 | aliases into palette |
| Color/Dark Core | 11 | aliases into palette |
| Color/Dark Accent | 12 | aliases into palette |
| Color/Dark Component | 10 | aliases into palette |
| Typography | 26 | composite typography + font-size aliases |
| Components/Button | 1 | alias |
| Brand/Core | 7 | hue + font-family + stroke |
| Brand/NeonBooks | 7 | same paths as Core |
| Brand/Eco Tools | 7 | same paths as Core |
| Breakpoints/Mobile | 2 | breakpoint + multiplier |
| Breakpoints/Tablet | 2 | breakpoint + multiplier |
| Breakpoints/Desktop | 2 | breakpoint + multiplier |
| Text zoom/100% | 1 | zoom = 1 |
| Text zoom/150% | 1 | zoom = 1.5 |
| Text zoom/200% | 1 | zoom = 2 |
| **Total** | **305** | |

After flat (non-themed) import with last-set-wins: **250 resolved tokens**.
55 tokens are shadowed by later sets with the same path.

---

## Token type distribution

| Type | Count | Source |
|---|---|---|
| `color` | 174 | palette + semantic color sets |
| `number` | 57 | scale steps, zoom, hue values |
| `dimension` | 47 | spacing, radius, sizing, breakpoint (TS type → DTCG renamed) |
| `typography` | 18 | composite typography tokens |
| `fontFamily` | 9 | brand font-family tokens |
| **Total** | **305** | |

---

## Path overlap

41 paths appear in more than one set. The primary overlap groups:

| Path | Appears in | Last-wins (global index) |
|---|---|---|
| `zoom` | Text zoom/100%, 150%, 200% | zoom = 2 (200% last) |
| `breakpoint` | Breakpoints/Mobile, Tablet, Desktop | 1200px (Desktop last) |
| `multiplier` | Foundations/Base + 3 Breakpoints/* | Breakpoints/Desktop value |
| `hue.default` / `hue.primary` / `hue.secondary` | Color/Palettes + 3 Brand | Eco Tools |
| `font-family.default` / `.alt` / `.code` | 3 Brand sets | Eco Tools |
| `color.background.default` (etc.) | Color/Light Core, Dark Core | Dark Core |

The 55 shadowed tokens come from these overlapping groups: brand (Core/NeonBooks shadowed
by Eco Tools), color-mode (Light Core shadowed by Dark Core), breakpoints (Mobile/Tablet
shadowed by Desktop), zoom (100%/150% shadowed by 200%).

---

## Shim transforms

### Math expressions → concrete floats

`Foundations/Base` raw:
```json
"base":    { "$type": "number", "$value": "16 * {zoom}" }
"scale.sm": { "$type": "number", "$value": "round({base} * pow({multiplier}, 0))" }
"scale.md": { "$type": "number", "$value": "round({base} * pow({multiplier}, 1))" }
"scale.3xs": { "$type": "number", "$value": "round({base} * pow({multiplier}, -3))" }
```

After `shimSingleFile` (global flat index, zoom=2 from Text zoom/200% last):
```json
"base":    { "$type": "number", "$value": 32 }
"scale.sm": { "$type": "number", "$value": 32 }
"scale.md": { "$type": "number", "$value": 32 }
"scale.3xs": { "$type": "number", "$value": 32 }
```

All scale steps collapse to 32 because:
- Global index: `zoom = 2` (Text zoom/200% is last in tokenSetOrder)
- `base = 16 * 2 = 32`
- `multiplier = 1` (Foundations/Base value; Breakpoints/Desktop also defines it — value TBD)
- `round(32 * pow(1, N)) = 32` for all N

The only exceptions are literal values (`scale.hairline = 1`, `scale.micro = 2`).

**This is the math-evaluator theme-bleed in `shimSingleFile`**. The fix in
`importTokensStudioThemed` (`shimSingleFileWithMathIndex` with per-theme set list)
gives correct values — zoom=1 for the 100% theme → base=16. But the scale spread
also requires the correct breakpoint's `multiplier` to be in the same per-theme index;
combining the zoom and breakpoint themes in a single `resolveNamesWithIndex` call is
needed to get distinct scale steps (e.g. round(16 * pow(1.25, 3)) = 31 for `scale.xl`).

**The expression string is not preserved.** Re-export emits the concrete float as a
literal; math expressions cannot be round-tripped through the shim.

### Dimension-family type renames (lossless)

TS legacy types are renamed to DTCG `dimension`, and bare-number values get the
`{value, unit}` object form with `"px"` unit:

```
"$type": "spacing"      → "$type": "dimension", "$value": {"value": 16, "unit": "px"}
"$type": "borderRadius" → "$type": "dimension", "$value": {"value": 8, "unit": "px"}
"$type": "fontSizes"    → "$type": "dimension"  (alias refs preserved as strings)
"$type": "borderWidth"  → "$type": "dimension"
```

### Typography: field renames + type coercions

Raw TS composite:
```json
{
  "fontFamilies": ["{font-family.default}"],
  "fontSizes":    "{font-size.lg}",
  "fontWeights":  "600",
  "lineHeights":  "1.1"
}
```

Shimmed DTCG:
```json
{
  "fontFamily": "{font-family.default}",
  "fontSize":   { "value": 32, "unit": "px" },
  "fontWeight": 600,
  "lineHeight": 1.1
}
```

- `fontFamilies: [alias]` → `fontFamily: alias` (alias preserved, array unwrapped)
- `fontSizes: alias` → `fontSize: {value, unit}` (alias resolved eagerly at shim time; value wrong due to math-bleed, structure correct)
- `fontWeights: "600"` → `fontWeight: 600` (string → JSON integer)
- `lineHeights: "1.1"` → `lineHeight: 1.1` (string → JSON float, unitless ratio)

### fontFamily tokens: array joining

```
RAW:     "$value": ["Figtree", "sans-serif"]
SHIMMED: "$value": "Figtree, sans-serif"
```

Multi-element arrays joined with `", "`. Single-element alias arrays unwrapped to the
bare alias string. The array boundary is lost — this is a simplification.

### `transparent` → DTCG structured color (lossless for rendering)

```
RAW:     "$value": "transparent"
SHIMMED: "$value": { "colorSpace": "srgb", "components": [0, 0, 0], "alpha": 0.0 }
```

### Empty `$description` dropped

All of Laura's tokens have `"$description": ""`. Empty descriptions are silently dropped
from the shimmed output — only non-empty descriptions are preserved.

---

## Structural losses

These cannot be recovered from the shimmed per-set DTCG JSON alone:

| Loss | Details |
|---|---|
| Math expression strings | Evaluated to floats; `round({base} * pow({multiplier}, N))` → `32` |
| Multi-set cascade semantics | `tokenSetOrder` last-wins context not in any single DTCG set |
| Cross-set alias chains | `color.background.default → {palette.default.50}` resolved; ref discarded in flat import |
| TS legacy type names | `spacing → dimension` etc. — one-way rename, original name gone |

**Partial recovery via `ShimResult`**: `ShimResult.Themes` + `ShimResult.Metadata` are
present in the shim output even though they don't appear in the per-set DTCG JSON.

- `toResolverDocument(shimResult, parsedSets)` → DTCG `ResolverDocument` with Color mode /
  Brand / Breakpoint / Text zoom modifier groups.
- `exportToTokensStudio(shimResult, parsedSets)` → Tokens Studio JSON with `$themes` and
  `$metadata` reconstructed. Math expressions and resolved fontSizes are emitted as literals.

---

## Theme structure

12 themes across 5 groups (confirmed):

| Group | Themes |
|---|---|
| Global | Always-on (pseudo-theme for always-active foundation sets) |
| Color mode | Light, Dark |
| Brand | Core, NeonBooks, Eco Tools |
| Breakpoint | Mobile, Tablet, Desktop |
| Text zoom | 100%, 150%, 200% |

`Always-on` lists `Foundations/*`, `Color/Palettes and Scales`, `Typography`, and
`Components/Button` as `"enabled"`. This is Laura's workaround for encoding globally-active
sets as a named theme. When calling `importTokensStudioThemed` without `"Always-on"` in the
requested list, those sets land in `baseSets` (correct behavior).

---

## Flat vs themed import

**`importTokensStudio` (flat, global index):** 250 resolved tokens. Last-set-wins: Dark
mode colors, Eco Tools fonts, Desktop breakpoint, zoom=2 scale (all collapsed to 32/64).

**`importTokensStudioThemed` (per-theme index):** Correct per-theme values. To get the
intended Desktop + 100% zoom combined scale, the per-theme call's `mathIndexSets` needs
to include BOTH the zoom set (`Text zoom/100%`) AND the breakpoint set
(`Breakpoints/Desktop`), which means combining them under one `resolveNamesWithIndex` call
rather than two separate theme resolutions. The current theme definitions separate zoom
and breakpoint into distinct themes.

---

## Open questions

- **`Breakpoints/Desktop.multiplier` value**: if it is 1.25, combining a Desktop +
  zoom/100% per-theme call should give the expected scale spread (8, 10, 13, 16, 20, 25, 31).
  If it is 1, all scale steps collapse to base regardless.
- **Combined zoom + breakpoint resolution**: `importTokensStudioThemed` resolves each
  requested theme independently. Getting a "Desktop + 100%" combined scale requires either
  a single virtual theme that enables both sets, or a post-resolution merge strategy — the
  current architecture resolves each theme independently.
- **CSS verification** (Phase 4): do the shimmed tokens produce CSS custom properties
  matching Penpot's resolved values for the `laura-light-desktop` token set pushed in Phase 2?
