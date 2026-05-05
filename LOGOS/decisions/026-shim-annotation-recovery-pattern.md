---
id: 026
title: Shim-annotation recovery pattern for non-color data losses
status: accepted
extends: 023 (same vendor namespace; adds three new keys)
date: 2026-05-04
---

## Context

ADR-023 established a pattern for preserving wide-gamut color data across the
Tokens Studio (TS) ↔ DTCG round-trip: the shim annotates losses in
`$extensions["com.fntools.designtokens"]` so the exporter can restore them.

ADR-023 only covered one loss category (wide-gamut color → sRGB hex). The shim
also discards three other categories of information with no recovery path:

1. **TS type renames** — `spacing`, `borderRadius`, `fontSizes`, `borderWidth`
   are mapped to the DTCG type `dimension`. After the round-trip the original
   TS type name is gone; a file authored with `"spacing"` re-exports as
   `"dimension"`, which Tokens Studio and Penpot accept but which changes the
   semantic grouping in the original tool.

2. **HSL color expressions** — `hsla({hue.primary},{saturation},{lightness},1)`
   are evaluated at shim time to an sRGB hex string. The expression is lost.
   Any per-brand or per-theme alias variance in the original expression is
   collapsed to the resolved value for whichever brand was active when the shim
   ran.

3. **FontWeight combined values** — Tokens Studio stores composite values like
   `"400 Italic"` in `fontWeights` fields. The shim extracts the numeric part
   (`400`) and discards the style suffix. Note: this loss occurs inside
   typography composite `$value` objects (the `fontWeights` field within a
   `typography` token); standalone `$type: "fontWeights"` tokens pass through
   unchanged. Full recovery of typography-embedded combined values is deferred
   (see Consequences).

## Decision

Extend the ADR-023 vendor namespace with three new keys, following the same
annotate-on-shim / recover-on-export pattern.

### New extension keys

Under `$extensions["com.fntools.designtokens"]` on DTCG token leaves:

| Key | Written by | Consumed by | Purpose |
|---|---|---|---|
| `tsType` | shim `walkObj` | export `addTokensToObj` | Original TS type name before rename |
| `originalHsl` | shim `walkObj` | export `addTokensToObj` | Original HSL expression before hex conversion |
| `originalFontWeight` | shim `walkObj` | export `addTokensToObj` | Original combined fontWeight string (standalone tokens only) |
| `originalTypographyFontWeight` | shim `walkObj` | export `addTokensToObj` | Original combined fontWeight from typography composite `$value.fontWeights` |

`originalColor` (ADR-023) is unchanged and remains in the namespace.

### Annotation rules (shim side)

- **`tsType`**: written when `typeRenames` maps the TS type to a different DTCG
  type (e.g. `spacing` → `dimension`). Not written for types that pass through
  unchanged (`color`, `number`, `fontWeight`, etc.).

- **`originalHsl`**: written when `dtcgType = "color"`, the raw value is not an
  alias ref, and `hslRx.IsMatch(rawValue)` is true (i.e. the value is a
  `hsl(...)` / `hsla(...)` expression that was converted to hex). Note: `hslRx`
  matches bare-number arguments and alias refs but NOT `%`-suffixed values; the
  convention in Tokens Studio files is to omit `%` (e.g.
  `hsla({hue.primary},{saturation},{lightness},1)`).

- **`originalFontWeight`**: written when `tsType = "fontWeights"`, the raw
  value is not an alias ref, and the value contains a space (e.g.
  `"400 Italic"`). This handles standalone `$type: "fontWeights"` tokens only.

- **`originalTypographyFontWeight`** *(added 2026-05-04)*: written when
  `tsType = "typography"` and the composite `$value.fontWeights` field is a
  non-alias string containing a space (e.g. `"400 Italic"`). Stored on the
  token leaf's vendor namespace. Recovery patches the `fontWeight` field inside
  the exported composite object rather than replacing the whole `$value`.

### Recovery rules (export side)

In `addTokensToObj`:

- **`$type`**: if `tsType` is present in extensions, use it directly as the TS
  type string instead of `typeNameStr tt` (the DTCG-to-TS name map).
- **`$value`**: if `originalHsl` is present, use the stored expression as
  `$value`, overriding the lossy hex from `exportValue`. If `originalFontWeight`
  is present, use the stored string, overriding the numeric output.

### Stripping rules

`shimAllInternalKeys = { originalColor, originalHsl, tsType, originalFontWeight, originalTypographyFontWeight }`  
`shimExportStripKeys = { originalHsl, tsType, originalFontWeight, originalTypographyFontWeight }`

- **On TS import (`cloneExtensionsForOutput`)**: all four keys are stripped from
  the `com.fntools.designtokens` vendor object. These keys are DTCG-only
  transport metadata; they must not persist into a TS file.

- **On DTCG export (`buildExtensionsObject`)**: `shimExportStripKeys` are
  stripped from the vendor namespace in user-supplied extensions before writing
  to TS output. `originalColor` is intentionally kept (ADR-023: it is a
  TS-side recovery carrier, not a DTCG-only artifact).

User-authored extensions under any other vendor namespace pass through both
sides verbatim.

## Rationale

The same annotate/recover pattern from ADR-023 is the lowest-friction extension
point: no new public API, no format changes, backward-compatible (old exporters
produce DTCG tokens with no annotations, new exporters recover them when
present). The vendor namespace already exists; adding keys is free.

## Consequences

- **`exportToTokensStudio` now restores original TS type names**: a file
  authored with `$type: "spacing"` round-trips back to `"spacing"` instead of
  `"dimension"`. The existing test assertion was updated to reflect this.

- **Typography `fontWeights` combined values** *(resolved 2026-05-04)*: the
  `originalTypographyFontWeight` key (see table above) annotates the composite
  token leaf when `$value.fontWeights` contains a combined value like
  `"400 Italic"`. On export, `addTokensToObj` patches the composite's
  `fontWeight` field. The annotation lives on the leaf, not inside the composite
  value object, which keeps recovery consistent with the other three keys.

- **hslRx `%`-suffix limitation** *(resolved 2026-05-04)*: `hslRx` updated to
  accept optional `%` after S and L arguments (groups 2 and 3 changed from
  `[\d.]+` to `[\d.]+%?`). The `resolve` lambda strips trailing `%` before
  passing to `resolveToFloat`. Round-trips correctly for both bare-number and
  `%`-suffixed forms.

- **No public API changes**: `Api.exportTokensStudio`, `Api.importTokensStudio`,
  and all combinator variants are unchanged. The extension keys are internal.

## Test coverage

`TokensStudioTests.fs` adds four tests:

- `ADR-026: tsType round-trip` — `spacing`/`fontSizes` → `dimension` on import;
  `spacing`/`fontSizes` restored on export.
- `ADR-026: tsType not added for non-renamed types` — `dimension` stays
  `dimension`; no `tsType` extension emitted.
- `ADR-026: originalHsl round-trip` — `hsl(240, 50, 60)` → hex on import;
  original expression restored on export.
- `ADR-026: shim-internal keys stripped from TS export` — `originalHsl`,
  `originalFontWeight`, `tsType` absent from TS output; `originalColor` kept.

Existing test `"type names are DTCG names, not TS legacy names"` updated to
`"original TS type names are recovered on export (ADR-026)"` with inverted
assertions.

## References

- `LOGOS/decisions/023-tokens-studio-export-extensions-carrier.md` — the
  ADR this one extends (same namespace, same annotate/recover pattern).
- `src/FnTools.DesignTokens.TokensStudio/TokensStudio.fs` — `extTsTypeKey`,
  `extOriginalHslKey`, `extOriginalFontWeightKey`, `shimAllInternalKeys`,
  `shimExportStripKeys`, `addVendorExtension`, `tryReadVendorString`.
