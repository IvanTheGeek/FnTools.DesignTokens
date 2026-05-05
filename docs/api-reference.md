# API Reference

The entry point is `FnTools.DesignTokens.Api`. One package reference is all you need:

```xml
<PackageReference Include="FnTools.DesignTokens" Version="0.3.0" />
```

---

## Import from DTCG JSON

### `Api.import`

```fsharp
Api.import (jsonText: string)
    : Result<(string list * ResolvedToken) seq, ImportError list>
```

Parse, validate, and resolve a single DTCG `.tokens.json` file. Returns a flat sequence of `(path segments, resolved token)` pairs. Path segments are the dot-path components — `["color"; "text"; "main"]` for a token at `color.text.main`.

### `Api.importWithResolver`

```fsharp
Api.importWithResolver
    (loadFile : string -> Result<string, string>)
    (context  : Map<string, string>)
    (jsonText : string)
    : Result<(string list * ResolvedToken) seq, ImportError list>
```

Parse a `.resolver.json` document, load referenced token files via `loadFile`, apply context variables, merge sets in resolution order, then resolve. File I/O stays with the caller — `loadFile` receives a path string and returns the file content or an error message.

---

## Import from Tokens Studio format

All four functions accept `ShimConfig`. Pass `ShimConfig.Default` in almost all cases. `{ MathPolicy = PreserveMath }` keeps math expression strings unevaluated for inspection.

### `Api.importTokensStudio`

```fsharp
Api.importTokensStudio (config: ShimConfig) (jsonText: string)
    : Result<TokensStudioImportResult, ImportError list>
```

```fsharp
type TokensStudioImportResult = {
    Tokens   : (string list * ResolvedToken) list
    Warnings : TokensStudioImportWarning list  // SetSkipped | TokenUnresolved | ThemeNotFound | MathEvalFailedVariantAlias
}
```

Merges all sets in `$metadata.tokenSetOrder` (first = lowest priority, last wins) and resolves. Partial-success: tokens with unresolvable aliases appear as `TokenUnresolved` warnings rather than failing the whole import.

Use this for a flat, theme-agnostic snapshot.

### `Api.importTokensStudioThemed`

```fsharp
Api.importTokensStudioThemed
    (config     : ShimConfig)
    (themeNames : string list)
    (jsonText   : string)
    : Result<ThemeAwareImportResult, ImportError list>
```

```fsharp
type ThemeAwareImportResult = {
    BaseTokens : (string list * ResolvedToken) list   // sets not owned by any theme
    Themes     : ThemeTokens list                      // one per requested theme
    Warnings   : TokensStudioImportWarning list
}

type ThemeTokens = {
    ThemeName : string
    Tokens    : (string list * ResolvedToken) list
}
```

Resolves base sets and each theme independently. Each theme's token list includes base sets plus that theme's own sets — so `CssEmitter.emitThemedWith` can diff them automatically to produce only the changed variables per theme block.

Use this to produce `:root` + per-theme override CSS, or any multi-mode output.

### `Api.importTokensStudioCombined`

```fsharp
Api.importTokensStudioCombined
    (config     : ShimConfig)
    (themeNames : string list)
    (jsonText   : string)
    : Result<TokensStudioImportResult, ImportError list>
```

Combines themes from different modifier groups into one resolution pass. For example, `["Light"; "Desktop"]` activates both the Light color set and the Desktop breakpoint set simultaneously. Non-requested groups' sets are excluded from the math index — no cross-group bleed.

Use this when you need a single concrete token snapshot for a specific combination of dimensions.

### `Api.importTokensStudioRaw`

```fsharp
Api.importTokensStudioRaw (config: ShimConfig) (jsonText: string)
    : Result<TokensStudioRawImport, ImportError list>
```

```fsharp
type TokensStudioRawImport = {
    Import     : TokensStudioImportResult    // resolved tokens + warnings
    ShimResult : ShimResult                   // for exportTokensStudio / toResolverDocument
    ParsedSets : Map<string, TokenFile>       // for exportTokensStudio / toResolverDocument
}
```

Same resolved tokens as `importTokensStudio`, plus the raw shim artifacts needed for export. Use this for round-trips — you get everything in one call.

```fsharp
// Round-trip example
let raw = Api.importTokensStudioRaw ShimConfig.Default json |> Result.get
let css = CssEmitter.emitBlock raw.Import.Tokens
let (penpotJson, _) = Api.exportTokensStudio raw.ShimResult raw.ParsedSets
```

---

## Export

### `Api.export`

```fsharp
Api.export (tokens: (string list * ResolvedToken) seq) : string
```

Resolved token list back to a DTCG `.tokens.json` string. Reverses `Api.import`. All values are concrete literals — aliases are not reconstructed.

### `Api.exportTokensStudio`

```fsharp
Api.exportTokensStudio
    (shimResult : ShimResult)
    (parsedSets : Map<string, TokenFile>)
    : string * ExportWarning list
```

Rebuild a Tokens Studio multi-set JSON from DTCG token files. Preserves:
- Alias references (`{color.text.main}` stays as a reference, not resolved)
- Original Tokens Studio type names (`fontFamilies`, `spacing`, etc.)
- HSL expressions (`hsla({hue.blue},{saturation},...)`)
- Combined fontWeight strings (`"400 Italic"`)

Wide-gamut OKLCH colours are downsampled to sRGB hex with an `ExportWarning.LossyColorConversion`. Get `shimResult` and `parsedSets` from `importTokensStudioRaw`.

### `Api.toResolverDocument`

```fsharp
Api.toResolverDocument
    (shimResult : ShimResult)
    (parsedSets : Map<string, TokenFile>)
    : ResolverDocument
```

Map Tokens Studio `$themes` + `$metadata` to a DTCG `ResolverDocument`. Theme groups (Color mode, Breakpoint, Brand, etc.) become modifier groups; their themes become modifier variants; global sets become base `SetRef`s in resolution order.

---

## CSS emission

From `FnTools.DesignTokens.Css` — included via the meta-package.

### Basic block

```fsharp
CssEmitter.emitBlock (tokens: (string list * ResolvedToken) seq) : string
// :root { --color-text-main: #1a1a1a; --spacing-md: 16px; ... }

CssEmitter.emitBlockWith (policy: DimensionUnitPolicy) (tokens) : string
// same, with per-path unit conversion
```

### Themed (multi-mode)

```fsharp
CssEmitter.emitThemed
    (selectorFn  : string -> string)
    (baseTokens  : (string list * ResolvedToken) seq)
    (themeTokens : ThemeTokens seq)
    : string
// :root { base vars }
// [data-theme="dark"] { only the vars that differ }

CssEmitter.emitThemedWith (policy: DimensionUnitPolicy) (selectorFn) (baseTokens) (themeTokens)
    : string
```

### Two-block (base + override)

```fsharp
CssEmitter.emitMultiMode
    (baseTokens     : (string list * ResolvedToken) seq)
    (overrideTokens : (string list * ResolvedToken) seq)
    (overrideSelector : string)
    : string

CssEmitter.emitMultiModeWith (policy) (baseTokens) (overrideTokens) (overrideSelector)
    : string
```

### Calc-preserving (design workbench)

```fsharp
CssEmitter.emitCalcPreserving
    (baseTokenPath       : string list)
    (multiplierTokenPath : string list)
    (tokens              : (string list * ResolvedToken) seq)
    : string
```

Tokens whose value fits `base × multiplier^N` emit `calc(var(--base) * pow(var(--multiplier), N))`. Others emit concrete values. Enables live slider control in a design workbench.

### `DimensionUnitPolicy`

```fsharp
type DimensionUnitPolicy = string list -> DimensionUnit -> DimensionUnit
```

Called per token with the token's path and its current unit. Return `Rem` to convert `px` → `rem` (divides value by 16). Example:

```fsharp
let remPolicy : DimensionUnitPolicy =
    fun path unit ->
        match path with
        | "font-size" :: _ -> Rem
        | _ -> unit
```

---

## CSS ingestion and audit

### `CssIngest.ingest`

```fsharp
CssIngest.ingest (prefix: string) (cssOrHtml: string) : IngestResult
```

Extract `--prefix-*` custom properties from CSS or an inline `<style>` block. Infers token type from value shape (OKLCH → color, `px`/`rem` → dimension, `#rrggbb` → color, etc.). Pass `""` to accept all `--*` variables regardless of prefix.

### `CssAudit.audit` / `CssAudit.auditAgainst`

```fsharp
CssAudit.audit (cssText: string) : AuditResult
CssAudit.auditAgainst (cssText: string) (tokenFile: TokenFile) : AuditResult
```

Scan CSS rules (not just `:root`) for hardcoded values. Groups by inferred type (colors, dimensions, font families, shadows, durations). `auditAgainst` marks values that already match an existing token as duplicates. Each entry includes the selectors that use it and a frequency count.

---

## F# bindings

### `BindingsEmitter.emit`

```fsharp
BindingsEmitter.emit (tokens: (string list * ResolvedToken) seq) : string
```

Resolved tokens → a generated F# source file with a `Tokens` module of `string` constants:

```fsharp
module Tokens =
    module Color =
        module Text =
            let Main = "var(--color-text-main)"
```

Numeric path segments are prefixed with `N` (e.g. `scale.400` → `Scale.N400`). Typography tokens expand to five sub-constants (`FontFamily`, `FontSize`, `FontWeight`, `LetterSpacing`, `LineHeight`). No runtime dependencies — values work directly in [Fun.Css](https://github.com/slaveOftime/Fun.Css) property builders.

---

## Error formatting

```fsharp
Api.formatImportError (e: ImportError) : string
```

Human-readable description of any `ImportError`. Useful for logging or surfacing errors to end users.

---

## `Primitives` submodule

`Api.Primitives` exposes the raw building blocks for composing custom pipelines:

| Name | What it wraps |
|---|---|
| `parse` / `parseAs` / `parseAuto` | `Format.parse*` |
| `serialize` / `serializeAs` / `serializePenpot` | `Format.serialize*` |
| `validate` | `Validation.validate` |
| `load` | parse + validate in one call |
| `flatten` / `tryFind` / `tryResolveAlias` | token tree traversal |
| `parseResolver` / `resolve` / `resolveAll` | `Resolver.*` |
| `flattenResolved` | full resolution pipeline |
| `shimWithConfig` | `TokensStudio.shimSingleFile` |
| `shimTokensStudio` | `TokensStudio.shim` (legacy single-set) |
| `importTokensStudioRaw` / `importTokensStudioThemed` / `importTokensStudioCombined` | aliased from top-level |
| `toResolverDocument` / `exportTokensStudio` | aliased from top-level |
| `formatParseError` / `formatValidationError` / `formatResolveError` / `formatLoadError` / `formatSerializeError` / `formatShimWarning` / `formatImportWarning` / `formatExportWarning` | all error formatters |

Normal usage does not require `Primitives`. Use it when you need a specific intermediate step — e.g. parse without validation, or shim without resolving.
