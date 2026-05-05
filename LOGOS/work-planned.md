## CSS hardcoded value scanner (bootstrap step 2)

Companion to `CssIngest` — covers the values that are NOT in CSS custom properties.
Feeds the human/AI naming step (step 3) in the CSS Bootstrap / Migration workflow.
See `design-system-app-vision.md § CSS Bootstrap / Migration` for full workflow context.

- [x] Parse all CSS rules (not just `:root`) from a CSS or HTML file
- [x] Extract unique values grouped by inferred type: colors, dimensions (px/rem), font families, font weights, durations, shadows
- [x] Record selector context and frequency for each unique value (which rules use it, how many times)
- [x] Output a structured audit report — `AuditResult` with typed `AuditEntry` list sorted by count
- [x] Flag values that already match an existing token's `$value` (duplication detector) — `auditAgainst (cssText: string) (file: TokenFile) : AuditResult`; `AuditEntry.MatchedToken` field; validated against ivanthegeek.com
- [x] Integrate into `FnTools.DesignTokens.Css` as `CssAudit` module alongside `CssIngest`

## Design system app (future)

Unified GUI for token management, component gallery, ADR review/creation, and ATLAS PATH prototyping.
See `design-system-app-vision.md` for full scope.

- [ ] Define the PATH model format (ATLAS coordination needed)
- [ ] Spike: read + display token tree in a Fun.Blazor Blazor app
- [ ] Spike: render a single component with live token switching
- [ ] ADR list + create form backed by git write-back
- [ ] PATH walkthrough prototype mode

## NuGet packaging

- [ ] CI publish on tag (deferred until first stable release)

## ~~CSS ingestion tool~~ ✓ done 2026-05-02

Convert an existing CSS custom-property file (or inline `<style>` block) to DTCG `.tokens.json` files.
Primary test case: LaundryLog HTML design system + ivanthegeek.com.

- [x] CSS custom-property parser (regex-based)
- [x] Type inference heuristics (OKLCH → color, px/rem → dimension, rgba → color with alpha, shadow, etc.)
- [x] Group/hierarchy inference from naming convention (e.g., `--cb-color-action-default` → `color / action / default`)
- [x] Prefix-less mode: pass `""` to accept all `--*` vars regardless of prefix
- [x] Output writer: DTCG JSON with correct `$type` + `$value` structure
- [x] Round-trip validation: parse output back through `Format.parse` with zero errors

## ~~CSS emitter~~ ✓ done 2026-05-02

`FnTools.DesignTokens.Css` — resolved token tree → CSS custom properties in `:root {}` block.
All 13 token types supported. Path segments → hyphen-delimited CSS var names.
Multi-mode output: `emitThemed`/`emitThemedWith` emit `:root` + per-theme diff blocks;
`emitMultiMode`/`emitMultiModeWith` emit two-block base + override output.
`DimensionUnitPolicy` allows per-path unit selection (e.g. rem for font-size tokens).
`emitCalcPreserving` for design-tool workbench slider (ADR-027).

## ~~Typed F# bindings emitter~~ ✓ done 2026-05-02

`FnTools.DesignTokens.Bindings` — resolved token tree → `Tokens.*` module with `string` var() constants.
N-prefix for numeric segments, PascalCase identifiers, typography expands to 5 sub-props.
Generated file has zero runtime dependencies; values work directly in Fun.Css property builders.

## Penpot round-trip workflow

See `experiments-planned.md` for experiment details.

- [ ] Test Penpot HTML import with a Fun.Blazor rendered component
- [ ] Validate that Penpot SVG export → Fun.Blazor reconstruction is workable
- [ ] Define the standard Penpot→Fun.Blazor translation pattern

## FnHCI non-visual token support

Extend beyond DTCG to cover console, TUI, thermal printer, and braille targets.

- [ ] `ConsoleTokens` — ANSI escape codes, 256-color palette, bold/italic/underline modifiers
- [ ] `TuiTokens` — TUI layout tokens (border chars, box drawing, spacing units in character cells)
- [ ] `ThermalTokens` — print density, font size codes, barcode/QR spec
- [ ] `BrailleTokens` — braille cell dot patterns, grade 1/2 encoding hints
- [ ] TOML authoring format for non-DTCG tokens (shallow/config-like; no nested group semantics needed)
- [ ] Shared resolver concept: `FnHCI.Resolver` can merge DTCG tokens + non-DTCG tokens into a unified output per target
