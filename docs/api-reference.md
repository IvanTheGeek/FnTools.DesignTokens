# API Reference

`FnTools.DesignTokens` — DTCG 2025.10 codec, validator, resolver, and emitters (CSS, F# bindings, Tokens Studio). The entry point is `FnTools.DesignTokens.Api`. One package reference is all you need:

```xml
<PackageReference Include="FnTools.DesignTokens" Version="0.10.1" />
```

The meta-package transitively pulls in seven layered libraries: `Foundation`, `Format`, `Validation`, `Resolver`, `Css`, `Bindings`, `TokensStudio`. Reference an individual layer if you want a smaller dependency surface.

Migration guides (newest first):
- [`migration-0.10.0-to-0.10.1.md`](./migration-0.10.0-to-0.10.1.md) — current release. Bug fix: `flattenResolvedFile` was clobbering the aliasing token's declared type, producing unitless CSS for dimension→number aliases. No API changes (ADR-033 addendum).
- [`migration-0.9-to-0.10.md`](./migration-0.9-to-0.10.md) — `ValidateOptions` opt-in laxness (ADR-035); `Resolver.resolveAll` deprecated, `Resolver.flattenAliases` now public (ADR-036).
- [`migration-0.8-to-0.9.md`](./migration-0.8-to-0.9.md) — `evaluateMathExtensions` deprecated; new `evaluateMathExtensionsInFile` fixes alias propagation (ADR-034 addendum).
- [`migration-0.7-to-0.8.md`](./migration-0.7-to-0.8.md) — `Api.evaluateMathExtensions` and `Api.importWithResolverEvaluatingExtensions` (ADR-034 original).
- [`migration-0.6-to-0.7.md`](./migration-0.6-to-0.7.md) — `Api.validateStrictDtcg` opt-in spec-compliance check (ADR-028 addendum).
- [`migration-0.5-to-0.6.md`](./migration-0.5-to-0.6.md) — `TypeMismatch` validation, dimension→number alias emission fix (ADR-033).
- [`migration-0.4-to-0.5.md`](./migration-0.4-to-0.5.md) — pure version bump; first NuGet-CI release.
- [`migration-0.3-to-0.4.md`](./migration-0.3-to-0.4.md) — `Api.importTokensStudioCombinedWith` (ADR-030).
- [`migration-0.2-to-0.3.md`](./migration-0.2-to-0.3.md) — `Api.importTokensStudioRaw` (ADR-029).

---

## Import from DTCG JSON

### `Api.import` / `Api.importWith`

```fsharp
Api.import     (jsonText: string)
    : Result<(string list * ResolvedToken) seq, ImportError list>

Api.importWith (opts: ValidateOptions) (jsonText: string)
    : Result<(string list * ResolvedToken) seq, ImportError list>
```

Parse, validate, and resolve a single DTCG `.tokens.json` file. Returns a flat sequence of `(path segments, resolved token)` pairs — `["color"; "text"; "main"]` for a token at `color.text.main`.

`import` is `importWith ValidateOptions.strict` — strict validation, which since 0.6.0 includes a cross-type alias check (a `dimension` token aliasing a `number` token returns `ImportError.ValidationFailed [TypeMismatch (path, "dimension", "number")]`, per ADR-033).

Use `importWith ValidateOptions.permissive` when your source contains the canonical Tokens Studio scale pattern (dimension tokens aliasing number tokens). The flag is a narrow whitelist for that one mismatch; other cross-type aliases still produce errors. See ADR-035.

### `Api.importWithResolver` / `Api.importWithResolverWith`

```fsharp
Api.importWithResolver
    (loadFile : string -> Result<string, string>)
    (context  : Map<string, string>)
    (jsonText : string)
    : Result<(string list * ResolvedToken) seq, ImportError list>

Api.importWithResolverWith
    (opts     : ValidateOptions)
    (loadFile : string -> Result<string, string>)
    (context  : Map<string, string>)
    (jsonText : string)
    : Result<(string list * ResolvedToken) seq, ImportError list>
```

Parse a `.resolver.json` document, load referenced token files via `loadFile`, apply context variables, merge sets in resolution order, then resolve. File I/O stays with the caller — `loadFile` receives a path string and returns the file content or an error message.

`importWithResolver` is `importWithResolverWith ValidateOptions.strict`. Use the `*With` variant with `ValidateOptions.permissive` for Tokens Studio-style sources that contain dimension→number aliases.

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

## Extension-aware resolve

### `Api.evaluateMathExtensionsInFile`

```fsharp
Api.evaluateMathExtensionsInFile (file: TokenFile) : EvaluateMathInFileResult

type EvaluateMathInFileResult = {
    File     : TokenFile
    Warnings : ExtensionEvaluationWarning list
}

type ExtensionEvaluationWarning =
    | MathExpressionFailed of path: string * expression: string * reason: string
```

Pre-flatten pass over a `TokenFile`. For any token carrying a `tsMathExpression` extension (`$extensions["com.fntools.designtokens"]["tsMathExpression"]`, ADR-031), evaluates the expression against an alias-aware index of the file and replaces the token's `$value` with the result. **Crucially, this happens before `flattenResolved` follows aliases** — so dependent tokens that alias a formula token pick up the evaluated value automatically when subsequently flattened. This is the correct shape for axis-aware math.

For `Dimension` and `Duration` hosts, only the scalar is updated — the unit is preserved. `Number` is replaced wholesale. Non-numeric hosts (Color, FontFamily, etc.) carrying the extension pass through unchanged with no warning (the extension is structurally non-applicable).

Failures (missing variable, parse error, non-numeric result) emit `MathExpressionFailed (path, expression, reason)` warnings and **keep the stale `$value`** — the resolution proceeds, the author sees the warning and fixes the formula.

Supports the same operators and functions as the import-time evaluator: `+ - * / ^ %`, parentheses, unary minus/plus, and `round / floor / ceil / abs / sqrt / pow / min / max / sin / cos / tan / asin / acos / atan / atan2 / log / log2 / log10 / exp`.

Use between `Resolver.resolve` and `Primitives.flattenResolved` in the manual pipeline:

```fsharp
match Resolver.resolve loadFile context doc with
| Error es -> handle es
| Ok merged ->
    let { File = updatedFile; Warnings = warnings } = Api.evaluateMathExtensionsInFile merged
    match Api.Primitives.flattenResolved updatedFile with
    | Error es -> handle es
    | Ok tokens ->
        // tokens carry the evaluated values; aliases that pointed at formula
        // tokens have automatically propagated the new values
        ...
```

Or use `Api.importWithResolverEvaluatingExtensions` (below) which does this composition automatically.

### `Api.evaluateMathExtensions` (deprecated 0.9.0)

```fsharp
[<System.Obsolete>]
Api.evaluateMathExtensions
    (tokens: (string list * ResolvedToken) seq)
    : ResolveWithExtensionsResult
```

The original 0.8.0 post-flatten variant. **Does not propagate updates through alias chains** — by the time the flat `ResolvedToken seq` exists, alias-target relationships have been baked into concrete values and the function has no way to recover them. Updating `scale.x1` does not update `spacing.x1` that originally aliased it. See ADR-034 addendum (2026-05-10) for the full diagnosis.

Use `evaluateMathExtensionsInFile` instead. The deprecated function is retained for compatibility with 0.8.0 consumers; the only known consumer is the request author who reported the propagation bug. Removal target: v1.0.0.

### `Api.importWithResolverEvaluatingExtensions` / `*With` variant

```fsharp
Api.importWithResolverEvaluatingExtensions
    (loadFile : string -> Result<string, string>)
    (context  : Map<string, string>)
    (jsonText : string)
    : Result<ResolveWithExtensionsResult, ImportError list>

Api.importWithResolverEvaluatingExtensionsWith
    (opts     : ValidateOptions)
    (loadFile : string -> Result<string, string>)
    (context  : Map<string, string>)
    (jsonText : string)
    : Result<ResolveWithExtensionsResult, ImportError list>

type ResolveWithExtensionsResult = {
    Tokens   : (string list * ResolvedToken) list
    Warnings : ExtensionEvaluationWarning list
}
```

Convenience: `parseResolver → validate → resolve → evaluateMathExtensionsInFile → flattenResolvedFile` in one call. Use when your `.resolver.json` composes axis sets whose values feed math expressions on dependent tokens — for example, a Breakpoint set overrides `multiplier` and you want every `round({base} * pow({multiplier}, N))` token AND every token that aliases such a scale to re-evaluate against the active axis combination.

The plain function uses strict validation. **For Tokens Studio scale-pattern files** (dimension tokens aliasing number tokens — the canonical TS pattern), use `importWithResolverEvaluatingExtensionsWith ValidateOptions.permissive ...`. This is the one-call replacement for the manual Primitives composition that 0.9.0 consumers had to use; see migration-0.9-to-0.10 (ADR-035).

As of 0.9.0, this family uses the pre-flatten evaluation path (ADR-034 addendum). Updates propagate through alias chains correctly.

`Resolver.resolveAll` (deprecated in 0.10.0 per ADR-036) is **not** used by this function — internally it composes `Resolver.resolve` + `evaluateMathExtensionsInFile` + `flattenResolvedFile`, which is the correct order for propagation.

### `TokensStudio.tryEvaluateMathExpression`

```fsharp
TokensStudio.tryEvaluateMathExpression
    (resolvedValues : Map<string, float>)
    (expression     : string)
    : float option
```

Public wrapper over the internal evaluator. Variables in the expression syntax `{path}` are looked up in `resolvedValues` by full dot-path. Returns `None` on parse failure, missing variable, or non-numeric result. Useful when you want to evaluate a single expression outside the meta-package's full-tree pass.

---

## Opt-in validation laxness (ADR-035, 0.10.0)

### `ValidateOptions`

```fsharp
type ValidateOptions = {
    /// When true, dimension→number aliases pass validation. Other cross-type
    /// alias mismatches still produce TypeMismatch errors. The flag is a
    /// narrow whitelist for the canonical Tokens Studio scale pattern.
    AllowDimensionAliasingNumber : bool
}

module ValidateOptions =
    val strict     : ValidateOptions   // default — all checks active
    val permissive : ValidateOptions   // dimension→number alias allowed
```

Passed to `Validation.validateWith` and the `*With` variants of every import function. Strict default protects accidental mismatches; permissive opts in to the TS scale pattern at the call site.

### `Validation.validateWith`

```fsharp
Validation.validate     (file: TokenFile)
    : Result<unit, ValidationError list>

Validation.validateWith (opts: ValidateOptions) (file: TokenFile)
    : Result<unit, ValidationError list>
```

`validate` is `validateWith ValidateOptions.strict` (backward-compatible). Use `validateWith ValidateOptions.permissive` to suppress the ADR-033 cross-type alias check for `dimension → number` while keeping every other structural rule active.

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
| `validate` / `validateWith` | `Validation.validate` (strict) / `validateWith` (caller-supplied `ValidateOptions`, ADR-035) — includes the cross-type alias check from ADR-033 |
| `validateStrictDtcg` | `Validation.validateStrictDtcg` — opt-in spec-extension check (ADR-028 addendum) |
| `evaluateMathExtensionsInFile` / `importWithResolverEvaluatingExtensions` / `importWithResolverEvaluatingExtensionsWith` / `importWith` / `importWithResolverWith` / `formatExtensionEvaluationWarning` | pre-flatten evaluation of `tsMathExpression` extensions with alias propagation (ADR-034 addendum); `*With` variants accept `ValidateOptions` (ADR-035) |
| `evaluateMathExtensions` ⚠️ deprecated | post-flatten variant; does not propagate through alias chains. Use `evaluateMathExtensionsInFile` |
| `flattenAliases` | `Resolver.flattenAliases` — newly public in 0.10.0 (ADR-036). Walks a `TokenFile` and replaces every `TokenValue.Alias` with the literal value it points to. Most consumers don't need this — `flattenResolved` and the emitters follow aliases themselves. |
| `resolveAll` ⚠️ deprecated | `Resolver.resolveAll` — consumes the alias graph, blocking post-resolve passes like `evaluateMathExtensionsInFile`. Replace with `resolve` + downstream (common pipeline) or `resolve` + `flattenAliases` (narrow case). See ADR-036. |
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

- [`getting-started.md`](./getting-started.md) — five-minute walkthrough: import → validate → emit CSS → bind in F#
- [Migration guides](#) — `migration-0.2-to-0.3.md` through `migration-0.9-to-0.10.md` (see top of page for full list)
- [`spec-context.md`](./spec-context.md) — DTCG 2025.10 spec references and version history
- [`LOGOS/decisions/`](../LOGOS/decisions/) — Architecture Decision Records (37 ADRs, 1 deferred — `decisions/README.md` indexes them)
- [`samples/ivanthegeek.tokens.json`](../samples/ivanthegeek.tokens.json) — real-world sample bootstrapped from a live site
