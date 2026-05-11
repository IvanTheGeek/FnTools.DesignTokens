# Migrating from 0.7 to 0.8

Released: 2026-05-10. Tracking ADR: [`034-evaluate-math-extensions-post-resolve.md`](../LOGOS/decisions/034-evaluate-math-extensions-post-resolve.md). Source request: [`request_2026-05-10_02.md`](../LOGOS/requests/request_2026-05-10_02.md).

---

## TL;DR

0.8.0 is **purely additive**. No API signatures, types, or behaviors changed
for existing code paths. `Resolver.resolveAll` remains strict-DTCG-compliant.
One new opt-in capability: post-resolve evaluation of `tsMathExpression`
extensions, exposed as `Api.evaluateMathExtensions` and the convenience
wrapper `Api.importWithResolverEvaluatingExtensions`. If you don't author
files with `tsMathExpression` extensions or you're happy with the snapshot
behavior, do nothing beyond bumping the version.

```diff
- <PackageReference Include="FnTools.DesignTokens" Version="0.7.0" />
+ <PackageReference Include="FnTools.DesignTokens" Version="0.8.0" />
```

---

## What's new

### The problem this solves

A `tsMathExpression` extension carries the original formula a token was
authored from. As of 0.7.0 it had two readers — `exportToTokensStudio` (TS
round-trip) and `emitCalcPreserving` (CSS calc emission). The `$value` itself
was a snapshot, written at the time of the last shim.

In a DTCG-as-SoT workflow (using a `.resolver.json` document to compose
axis sets), `Resolver.resolveAll` reads `$value` directly and ignores the
extension. If an axis set overrides a variable that feeds the formula —
e.g. `Breakpoints/Desktop` overrides `multiplier = 1.25` — the dependent
scale tokens stay at the snapshot value (`16`), instead of re-evaluating
to `round(16 × 1.25³) = 31`.

The TS-import family (`Api.importTokensStudioThemed` etc.) re-runs the math
per-theme correctly. The DTCG-as-SoT workflow had no equivalent — until now.

### What 0.8.0 adds

```fsharp
type ResolveWithExtensionsResult = {
    Tokens   : (string list * ResolvedToken) list
    Warnings : ExtensionEvaluationWarning list
}

type ExtensionEvaluationWarning =
    | MathExpressionFailed of path: string * expression: string * reason: string

Api.evaluateMathExtensions
    (tokens: (string list * ResolvedToken) seq)
    : ResolveWithExtensionsResult

Api.importWithResolverEvaluatingExtensions
    (loadFile : string -> Result<string, string>)
    (context  : Map<string, string>)
    (jsonText : string)
    : Result<ResolveWithExtensionsResult, ImportError list>

TokensStudio.tryEvaluateMathExpression
    (resolvedValues : Map<string, float>)
    (expression     : string)
    : float option

ExtensionEvaluationWarning.format
    (w: ExtensionEvaluationWarning)
    : string
```

`evaluateMathExtensions` walks any resolved token sequence; for each token
carrying `tsMathExpression`, evaluates against the resolved numeric context
(built from the same sequence — every `ResolvedNumber`, `ResolvedDimension`,
`ResolvedDuration` keyed by full dot-path) and replaces the value with the
result. `ResolvedDimension` and `ResolvedDuration` hosts preserve their unit;
only the scalar changes. `ResolvedNumber` is replaced wholesale. Non-numeric
hosts (Color, FontFamily, etc.) pass through unchanged — the extension is
structurally non-applicable, not a failure.

`importWithResolverEvaluatingExtensions` is the one-call convenience for
the DTCG-as-SoT workflow.

### Why it's not in `Resolver.resolveAll`

DTCG `$extensions` are spec-defined as vendor metadata, not semantic. If
`Resolver.resolveAll` interpreted our own extension namespace, files would
resolve to **different values** in our resolver vs. any other DTCG resolver
— a portability hazard. ADR-034 chose the opt-in-by-function-name shape
instead: the underlying core stays strict; extension-aware behavior is an
explicit, deliberate call. Mirrors the `Api.validateStrictDtcg` pattern from
0.7.0.

---

## Migration scenarios

### Scenario A — you don't use `tsMathExpression` extensions

Do nothing. Bump the version. None of your code paths change.

### Scenario B — you use `Api.importTokensStudio*` (TS-as-SoT workflow)

Do nothing. The TS-import family already re-evaluates math per-theme correctly
(via the per-theme shim index, work-completed 2026-05-04). 0.8.0 closes the
equivalent gap for the **other** workflow (DTCG-as-SoT); it doesn't change
how TS-as-SoT works.

### Scenario C — you have a `.resolver.json` SoT with axis sets that affect math expressions

This is the scenario the feature was built for. Two ways to use it:

**One-call convenience:**
```fsharp
let loadFile path = Ok (File.ReadAllText path)
let context = Map.ofList [ "breakpoint", "desktop" ]

match Api.importWithResolverEvaluatingExtensions loadFile context resolverJson with
| Error errs ->
    errs |> List.iter (fun e -> eprintfn "%s" (Api.formatImportError e))
| Ok result ->
    // result.Tokens has the re-evaluated values
    // result.Warnings has any MathExpressionFailed entries
    result.Warnings
    |> List.iter (fun w -> eprintfn "%s" (ExtensionEvaluationWarning.format w))
    for (path, token) in result.Tokens do
        printfn "%s = %A" (String.concat "." path) token.Value
```

**Composable:**
```fsharp
match Api.importWithResolver loadFile context resolverJson with
| Error errs -> handle errs
| Ok tokens ->
    let result = Api.evaluateMathExtensions tokens
    // result.Tokens, result.Warnings as above
```

Use the composable form when you want to do other post-resolve work between
`importWithResolver` and the math evaluation, or want to evaluate a
hand-built sequence that didn't come from a resolver.

### Scenario D — you author files containing `tsMathExpression` extensions deliberately

The extension is now executable at resolve time *if you opt in*. Plain
`Resolver.resolveAll` (and `Api.import` / `Api.importWithResolver`) still
read `$value` and ignore the extension — choose between strict-DTCG behavior
and extension-aware behavior by which function you call.

---

## On failure

Per the request's contract, evaluation failures are **non-fatal**:

- Missing variable (e.g. `{multiplier}` not found in the resolved context)
- Parse error in the expression
- Non-numeric result (NaN, ±∞)

→ The token keeps its stale `$value`, the resolution succeeds, and a
`MathExpressionFailed (path, expression, reason)` warning lands in
`result.Warnings`. Format with `ExtensionEvaluationWarning.format` for a
single-line human-readable string.

Hard errors (parse failed, validation failed, circular reference) still
return `Error` from the outer `Result` and stop the import, same as
`Api.importWithResolver`.

---

## Upgrade steps

1. Update your `PackageReference`:
   ```xml
   <PackageReference Include="FnTools.DesignTokens" Version="0.8.0" />
   ```
   Or individual layers — all 8 packages at 0.8.0.

2. Build. No compile-time changes expected.

3. If you have the DTCG-as-SoT-with-axes workflow, swap
   `Api.importWithResolver` → `Api.importWithResolverEvaluatingExtensions`
   at the call sites where you want re-evaluated values. The return type
   changes from `seq` to `ResolveWithExtensionsResult` (record with `Tokens`
   and `Warnings`) — handle the warnings, then proceed with `.Tokens` the
   same way you would the old `seq`.

---

## Reference

- **ADR-034** — `LOGOS/decisions/034-evaluate-math-extensions-post-resolve.md`
  — full rationale (5 options considered, why option 3 won).
- **Original request** — `LOGOS/requests/request_2026-05-10_02.md`.
- **Related ADRs** — ADR-031 (`tsMathExpression` round-trip), ADR-027
  (`emitCalcPreserving` reads the same extension for CSS calc()), ADR-024
  (per-theme math index for the TS-import family).
- **Tests** — 12 new cases in `ExtensionEvaluationTests.fs` covering
  pass-through, literal expression, `{variable}` from numeric context,
  Dimension scalar update with unit preservation, missing-variable warning,
  parse-error warning, multi-failure collection, non-numeric host
  pass-through, interleaved tokens, formatter output, Primitives parity.
