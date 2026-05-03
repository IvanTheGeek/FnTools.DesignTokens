---
area: Experiments
status: planned — 2026-05-02
---

# Experiments Planned

These are concrete experiments with a specific hypothesis, method, and expected result. Each one resolves an uncertainty that would otherwise require a downstream assumption.

---

## EXP-01: Penpot HTML import round-trip

**Status**: concluded — hypothesis falsified (2026-05-02)

**Hypothesis**: A rendered Fun.Blazor HTML page can be imported into Penpot as a design canvas.

**Result**: HTML import does not exist in Penpot as of May 2026. It is a community feature
request (open since March 2025) with no implementation. The File menu has no import option
beyond Penpot's own `.penpot` format. The Plugins menu offers only a plugin manager.

The workflow direction is reversed: Penpot → code, not code → Penpot. Penpot's Inspect tab
generates CSS/HTML/SVG from designs.

**What does exist**: A Plugin API (since Penpot 2.3, Nov 2024) could theoretically support
an HTML→Penpot plugin, but none exists as an installable plugin at this time.

**Revised design loop**: design components natively in Penpot using DTCG tokens as variables
(EXP-04), then use the Inspect tab to validate CSS output matches what the F# component
would produce. SVG export from Penpot remains available as a reference.

**Component test artifact**: `/tmp/machine-chips-component.html` — static HTML rendering
of `MachineTypeChips` in all four states (none, washer, dryer, supplies selected) using
DTCG token CSS vars. Renders correctly in browser; Penpot import not possible.

---

## EXP-02: CSS ingestion round-trip

**Status**: concluded — hypothesis confirmed

**Hypothesis**: The CSS custom-property declarations in the LaundryLog HTML design system handoff file can be reliably converted to valid DTCG 2025.10 token files with zero parse errors.

**Why it matters**: The CSS ingestion tool will be used to bootstrap `cb.tokens.json` + `ll.tokens.json`. If ingestion is lossy or produces invalid DTCG, the bootstrap fails and tokens must be authored from scratch.

**Method**:
1. Run the CSS ingestion tool against `LaundryLog Design System.html`
2. Parse the output files with `FnTools.DesignTokens.Format.parse`
3. Measure: how many tokens ingested, how many type-inference failures, how many validation errors

**Acceptance criteria**:
- Zero `Format.parse` errors on output files ✓
- All OKLCH colors captured with correct component values ✓
- All dimension values captured with correct unit ✓
- No tokens silently dropped ✓ (skipped values are explicitly warned)

**Results** (2026-05-03):
- CB: 102 tokens ingested, 0 skipped, 0 `Format.parse` errors
- LL: 16 tokens ingested, 22 skipped, 0 `Format.parse` errors
  - Skipped: all cross-prefix `var(--cb-*)` references (expected — need resolver doc to express), 1 `linear-gradient` value (no DTCG type)

**Notes**: Three bugs found and fixed during implementation:
1. CSS block comments containing `--` declarations were being parsed as tokens (fixed: `stripBlockComments` pre-pass)
2. Shadow dim-count mismatch crashed on unexpected token shapes (fixed: `None` fallback instead of crash)
3. `JsonNode` extracted from a `tokenLeaf` retains its parent pointer, causing "node already has a parent" on shadow color assignment (fixed: `DeepClone()` on extraction)

---

## EXP-03: Fun.Css CssVar token reference in Fun.Blazor component

**Status**: planned

**Hypothesis**: A Fun.Blazor component can reference emitted `Tokens.*` bindings via Fun.Css `CssVar` and produce correct CSS output, replacing hardcoded class names.

**Why it matters**: This validates the full pipeline end-to-end: DTCG files → emitter → typed bindings → Fun.Blazor component → rendered CSS.

**Method**:
1. Manually write a small `Tokens.fs` module with two or three `CssVar` bindings (before the emitter exists)
2. Update `MachineTypeChips.fs` to reference `Tokens.Color.Machine.washer` instead of `"ll-machine-chip--washer"`
3. Verify: component renders with correct CSS custom property values

**Acceptance criteria**:
- Component compiles with no string-typed token references
- Rendered HTML includes correct `var(--color-machine-washer)` output
- Dark mode: token value changes when `[data-theme="dark"]` is set

---

## EXP-04: Penpot DTCG token variable import

**Status**: concluded — hypothesis partially confirmed (2026-05-03)

**Hypothesis**: Penpot can import a DTCG `.tokens.json` file and expose the token values as
Penpot variables, which can then be applied to component fills, strokes, and text styles.

**Result**: Import works, but with a critical format constraint: Penpot's `design-tokens/v1`
feature does NOT support the DTCG 2025.10 color format (`{ "colorSpace": "oklch", "components":
[...] }`). It only accepts hex strings (`#RRGGBB`). Our `ll.tokens.json` uses DTCG 2025.10
OKLCH throughout, so it imports as 1 error token instead of 9. A hex-adapted version imports
all 9 tokens correctly across the full `machine.*` hierarchy.

**Detailed findings** (see `LOGOS/penpot-api.md` for full technical reference):

**Format Penpot actually accepts**:
```json
{
  "machine": {
    "washer": {
      "default": { "$type": "color", "$value": "#0d9488" }
    }
  }
}
```
- File name → set name (e.g., `lltokens-hex.json` → set "lltokens-hex")
- Top-level JSON keys → token group names
- `$type` must be on each **leaf** token, NOT on a group
- Color values must be hex (`#RRGGBB`) — oklch objects are silently ignored (produce error token)
- No `$schema` key required

**Export format** (Tokens Studio variant, not DTCG 2025.10):
```json
{
  "lltokens-hex": {
    "machine": {
      "washer": {
        "default": { "$value": "#0d9488", "$type": "color", "$description": "" }
      }
    }
  }
}
```
- Set name wraps the entire structure (extra nesting level vs. import)
- `$description` always present (empty string)
- No `$schema` declaration

**What this means for the pipeline**: `ll.tokens.json` (DTCG 2025.10 OKLCH) cannot be
imported into Penpot as-is. A Penpot adapter is needed that:
1. Converts OKLCH `{ colorSpace, components }` → hex (gamut-map to sRGB if needed)
2. Moves `$type` from group level to each leaf token
3. Optionally strips `$schema` (harmless to include, just ignored)

This adapter is a future task. The format gap is fundamental — Penpot is behind the 2025.10
spec on color space support.

**API auth discovery**: Penpot access tokens use `Authorization: Token <token>` (NOT Bearer).
Self-hosted instances require `enable-access-tokens` in `PENPOT_FLAGS` to show the token
management UI, but the `/settings/access-tokens` direct URL works even without the flag.

**Prerequisite**: API token must be created by user in Penpot UI before proceeding.
