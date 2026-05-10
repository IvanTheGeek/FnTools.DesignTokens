# API Reference

`FnTools.DesignTokens` — DTCG 2025.10 codec, validator, resolver, and emitters (CSS, F# bindings, Tokens Studio). The entry point is `FnTools.DesignTokens.Api`. One package reference is all you need:

```xml
<PackageReference Include="FnTools.DesignTokens" Version="0.6.0" />
```

The meta-package transitively pulls in seven layered libraries: `Foundation`, `Format`, `Validation`, `Resolver`, `Css`, `Bindings`, `TokensStudio`. Reference an individual layer if you want a smaller dependency surface.

See [`migration-0.5-to-0.6.md`](./migration-0.5-to-0.6.md) for the changes in 0.6.0 (new `TypeMismatch` validation, dimension→number alias emission fix).

---

## Import from DTCG JSON

### `Api.import`

```fsharp
Api.import (jsonText: string)
    : Result<(string list * ResolvedToken) seq, ImportError list>
```

Parse, validate, and resolve a single DTCG `.tokens.json` file. Returns a flat sequence of `(path segments, resolved token)` pairs — `["color"; "text"; "main"]` for a token at `color.text.main`.

Validation includes (since 0.6.0) a cross-type alias check: a `dimension` token aliasing a `number` token returns `ImportError.ValidationFailed [TypeMismatch (path, "dimension", "number")]`. See the migration guide for how to handle.

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

All five functions accept `ShimConfig`. Pass `ShimConfig.Default` in almost all cases. `{ MathPolicy = PreserveMath }` keeps math-expression strings unevaluated for inspection.

### `Api.importTokensStudio`

```fsharp
Api.importTokensStudio (config: ShimConfig) (jsonText: string)
    : Result<TokensStudioImportResult, ImportError list>
```

```fsharp
type TokensStudioImportResult = {
    Tokens   : (string list * ResolvedToken) list
    Warnings : TokensStudioImportWarning list  // SetSkipped | DtcgSetSkipped | TokenUnresolved | ThemeNotFound | MathEvalFailedVariantAlias
}
```

Merges all sets in `$metadata.tokenSetOrder` (first = lowest priority, last wins) and resolves. Partial-success: tokens with unresolvable aliases appear as `TokenUnresolved` warnings rather than failing the whole import. Use for a flat, theme-agnostic snapshot.

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

Resolves base sets and each theme independently. Each theme's token list includes base sets plus that theme's own sets — feed the result to `CssEmitter.emitThemed` to produce `:root` + per-theme override CSS.

### `Api.importTokensStudioCombined`

```fsharp
Api.importTokensStudioCombined
    (config     : ShimConfig)
    (themeNames : string list)
    (jsonText   : string)
    : Result<TokensStudioImportResult, ImportError list>
```

Combines themes from different modifier groups into one resolution pass. For example, `["Light"; "Desktop"]` activates both the Light color set and the Desktop breakpoint set simultaneously. Non-requested groups' sets are excluded from the math index — no cross-group bleed. Use this when you need a single concrete token snapshot for a specific combination of dimensions.

### `Api.importTokensStudioCombinedWith`

```fsharp
Api.importTokensStudioCombinedWith
    (config     : ShimConfig)
    (themeNames : string list)
    (tsJson     : string)
    (sets       : (string * string) list)
    (_          : DtcgSetRole)             // pass AsBasePrimitives
    : Result<TokensStudioImportResult, ImportError list>
```

Same as `importTokensStudioCombined`, plus extra DTCG sets injected as the lowest-priority base layer. The `DtcgSetRole` argument is required at every call site to make the role of those sets explicit: they are always included regardless of `themeNames`, and TS theme sets always override them. Resolution order: DTCG base sets → TS base sets → TS active-theme sets.

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
let raw = Api.importTokensStudioRaw ShimConfig.Default json |> Result.get
let css = CssEmitter.emit raw.Import.Tokens
let (penpotJson, _) = Api.exportTokensStudio raw.ShimResult raw.ParsedSets
```

---

## Export

### `Api.export`

```fsharp
Api.export (tokens: (string list * ResolvedToken) seq) : string
```

Resolved token list back to a DTCG 2025.10 `.tokens.json` string. Reverses `Api.import`. All values are concrete literals — aliases are not reconstructed.

### `Api.exportTokensStudio`

```fsharp
Api.exportTokensStudio
    (shimResult : ShimResult)
    (parsedSets : Map<string, TokenFile>)
    : string * ExportWarning list
```

Rebuild a Tokens Studio multi-set JSON from DTCG token files. Preserves:

- Alias references (`{color.text.main}` stays a reference, not resolved)
- Original Tokens Studio type names (`fontFamilies`, `spacing`, etc.)
- HSL expressions (`hsla({hue.blue},{saturation},...)`)
- Combined fontWeight strings (`"400 Italic"`)
- Math expressions (`{base} * pow({mult}, 3)`) — see ADR-031

Wide-gamut OKLCH colours are downsampled to sRGB hex with an `ExportWarning.LossyColorConversion`. Get `shimResult` and `parsedSets` from `importTokensStudioRaw`.

### `Api.toResolverDocument`

```fsharp
Api.toResolverDocument
    (shimResult : ShimResult)
    (parsedSets : Map<string, TokenFile>)
    : ResolverDocument
```

Map Tokens Studio `$themes` + `$metadata` to a DTCG `ResolverDocument`. Theme groups (Color mode, Breakpoint, Brand, etc.) become modifier groups; their themes become modifier variants; global sets become base `SetRef`s in resolution order.

### `Api.serializeResolver`

```fsharp
Api.serializeResolver (doc: ResolverDocument) : string
```

Serialize a `ResolverDocument` to JSON. Output round-trips through `parseResolver` (and therefore `importWithResolver`) to a structurally equivalent document. `$ref` pointers are never emitted — all sources are written as concrete `inline` or `path` objects.

---

## Strict DTCG 2025.10 compliance check

### `Api.validateStrictDtcg`

```fsharp
Api.validateStrictDtcg (file: TokenFile) : Result<unit, ValidationError list>
```

Reports `ConstraintViolation` errors for any feature that is valid in this library's domain but **not** in the published DTCG 2025.10 spec — today, only `DimensionUnit.Em` (added per ADR-028 for Tokens Studio / Penpot round-trip fidelity, but not in DTCG §7.4.6's list of valid dimension units).

Use this when you need to guarantee that a `TokenFile` contains no library extensions before exporting it to a strict downstream consumer. Detects `Em` in any literal position:

- `TokenValue.Dimension` directly
- `Border.Width`, `StrokeStyle.dashArray`
- `Shadow.OffsetX / OffsetY / Blur / Spread`
- `Typography.FontSize / LetterSpacing`

References (aliases) are not followed — only literal positions are checked. Errors are collected across the whole file, not short-circuited on the first violation. Regular `Validation.validate` (and `Api.import`'s built-in validation) continue to accept `Em` — strict compliance is an opt-in additional check.

```fsharp
match Api.import jsonText with
| Error es -> handleImportErrors es
| Ok tokens ->
    // Optional: refuse to ship anything containing library extensions
    match Format.parse jsonText |> Result.bind (Api.validateStrictDtcg >> Result.mapError ValidationFailed >> Result.map (fun () -> ())) with
    | Error _ -> warnUserAboutExtensions ()
    | Ok _    -> ()
    proceed tokens
```

---

## CSS emission

From `FnTools.DesignTokens.Css` — included via the meta-package, `[<AutoOpen>]` so functions are unqualified after `open FnTools.DesignTokens.Css`.

### `emit` — defaults to `:root`

```fsharp
emit (tokens: (string list * ResolvedToken) seq) : string
// :root { --color-text-main: #1a1a1a; --spacing-md: 16px; ... }
```

### `emitBlock` — any selector or at-rule

```fsharp
emitBlock (selector: string) (tokens: (string list * ResolvedToken) seq) : string
```

When `selector` starts with `@` (e.g. `@media (max-width: 600px)`), declarations are wrapped in an inner `:root { }` block so the output is valid CSS.

### `emitWith` — `:root` with a unit policy

```fsharp
emitWith (policy: DimensionUnitPolicy) (tokens: (string list * ResolvedToken) seq) : string
```

### Themed (multi-mode)

```fsharp
emitThemed
    (selectorForTheme : string -> string)
    (baseTokens       : (string list * ResolvedToken) seq)
    (themes           : (string * (string list * ResolvedToken) seq) seq)
    : string
// :root { base vars }
// [data-theme="dark"] { only the vars that differ from :root }
```

`themes` is a sequence of `(themeName, tokens)` tuples. From `Api.importTokensStudioThemed`, build it as:

```fsharp
let themes =
    result.Themes |> List.map (fun t -> t.ThemeName, t.Tokens :> _ seq)
emitThemed (fun n -> sprintf "[data-theme=\"%s\"]" n) result.BaseTokens themes
```

```fsharp
emitThemedWith (policy: DimensionUnitPolicy) (selectorForTheme) (baseTokens) (themes)
    : string
```

### Two-block (base + override)

```fsharp
emitMultiMode
    (baseTokens       : (string list * ResolvedToken) seq)
    (overrideTokens   : (string list * ResolvedToken) seq)
    (overrideSelector : string)
    : string

emitMultiModeWith (policy) (baseTokens) (overrideTokens) (overrideSelector)
    : string
```

### Calc-preserving (design workbench)

```fsharp
CssEmitter.emitCalcPreserving
    (baseVarName       : string)    // e.g. "--base"
    (multiplierVarName : string)    // e.g. "--multiplier"
    (tokens            : (string list * ResolvedToken) seq)
    : string
```

Dimension tokens whose value fits `base × multiplier^N` emit `calc(var(--base) * var(--multiplier) * ... * 1px)`. Others emit concrete values. Annotation via the Tokens Studio `tsMathExpression` vendor extension is consulted first (ADR-031), then mathematical inference. Enables live slider control in a design workbench.

As of 0.6.0 the calc match also fires for dimension tokens aliasing number tokens (ADR-033) — the bare number is treated as `Npx` before the scale check.

### `DimensionUnitPolicy`

```fsharp
type DimensionUnitPolicy = string list -> DimensionUnit -> DimensionUnit
```

Called per token with its path and declared unit. Return `Rem` to convert `px` → `rem` (divides value by 16); return `Px` to convert the other direction. Example:

```fsharp
let remForTypography : DimensionUnitPolicy =
    fun path unit ->
        match path with
        | "font-size" :: _ | "line-height" :: _ -> Rem
        | _ -> unit
```

Identity is `DimensionUnitPolicy.identity` (passes through unchanged).

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

Scan CSS rules (not just `:root`) for hardcoded values. Groups by inferred type (colors, dimensions, font families, shadows, durations). `auditAgainst` populates each entry's `MatchedToken : string option` with the path of a matching token when one exists. Each entry includes the selectors that use it and a frequency count.

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

`Api.Primitives` exposes the raw building blocks for composing custom pipelines.

| Name | What it wraps |
|---|---|
| `parse` / `parseAs` / `parseAuto` | `Format.parse*` |
| `serialize` / `serializeAs` / `serializePenpot` | `Format.serialize*` — `serializeAs` requires an `IAcceptDataLoss` parameter at the call site (ADR-028, marks a lossy spec downgrade) |
| `validate` | `Validation.validate` — includes the cross-type alias check from ADR-033 |
| `validateStrictDtcg` | `Validation.validateStrictDtcg` — opt-in spec-extension check (ADR-028 addendum) |
| `flatten` / `tryFind` / `tryResolveAlias` | token tree traversal |
| `parseResolver` / `serializeResolver` / `resolve` / `resolveAll` | `Resolver.*` |
| `flattenResolved` | full resolution pipeline |
| `shimWithConfig` | `TokensStudio.shimSingleFile` |
| `shimTokensStudio` | `TokensStudio.shim` (legacy single-set) |
| `importTokensStudioRaw` / `importTokensStudioThemed` / `importTokensStudioCombined` / `importTokensStudioCombinedWith` | aliased from top-level |
| `toResolverDocument` / `exportTokensStudio` | aliased from top-level |
| `formatShimWarning` / `formatImportWarning` / `formatExportWarning` | error formatters |

Normal usage does not require `Primitives`. Use it when you need a specific intermediate step — e.g. parse without validation, or shim without resolving.

---

## See also

- [`migration-0.5-to-0.6.md`](./migration-0.5-to-0.6.md) — what changed in the current version
- [`spec-context.md`](./spec-context.md) — DTCG 2025.10 spec references and version history
- [`LOGOS/decisions/`](../LOGOS/decisions/) — Architecture Decision Records (33 ADRs)
- [`samples/ivanthegeek.tokens.json`](../samples/ivanthegeek.tokens.json) — real-world sample bootstrapped from a live site
