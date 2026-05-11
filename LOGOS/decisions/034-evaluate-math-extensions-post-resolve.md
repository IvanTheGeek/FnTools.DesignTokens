---
id: 034
title: tsMathExpression evaluation is a post-resolve pass at the Api layer, not a Resolver change
status: accepted
date: 2026-05-10
---

## Context

Tokens that carry a `tsMathExpression` extension (`$extensions["com.fntools.designtokens"]["tsMathExpression"]`, ADR-031) preserve the original formula authored in Tokens Studio so DTCG ↔ TS round-trips are lossless. The extension already has two readers:

- `TokensStudio.exportToTokensStudio` — restores the formula as `$value` on TS export.
- `CssEmitter.emitCalcPreserving` — detects `base × multⁿ` patterns and emits `calc()` expressions.

A downstream consumer (`request_2026-05-10_02`) surfaced a third use case: **the formula is the source of truth at resolve time**, not just at round-trip or emit time. Their workflow uses a DTCG resolver document as SoT, with axis sets that override `multiplier`. When the resolver runs against a Desktop axis (`multiplier = 1.25`), scale tokens defined as `round(16 * pow({multiplier}, 3))` should re-evaluate to `31`, not return the baked-in `$value: 16` written at a previous (Base, `multiplier = 1`) resolve.

`Resolver.resolveAll` reads `$value` directly and ignores the extension, so the formula's axis-dependent semantics are silently lost in this workflow.

Five options were considered:

1. **Build into `Resolver.resolveAll` directly** — automatic, but the resolver stops being strict-DTCG-compliant: other tools would resolve the same file to different values.
2. **Opt-in `Resolver.resolveAllWithExtensions`** — preserves strictness, but requires layer surgery (`MathEval` lives in `TokensStudio`, not below `Resolver`).
3. **Post-resolve pass at the meta-package `Api` layer** — keeps `Resolver` untouched; extension semantics live where the export and emit readers also live.
4. **Composable function only (no convenience wrapper)** — minimal API surface; user composes manually each time.
5. **Decline; point users at `Api.importTokensStudioThemed`** — forces them back to TS-as-SoT, which they explicitly moved away from.

## Decision

**Option 3.** Math evaluation lives as a post-resolve pass in the meta-package, exposed as two functions:

```fsharp
type ResolveWithExtensionsResult = {
    Tokens   : (string list * ResolvedToken) list
    Warnings : ExtensionEvaluationWarning list
}

let evaluateMathExtensions
    (tokens: (string list * ResolvedToken) seq)
    : ResolveWithExtensionsResult

let importWithResolverEvaluatingExtensions
    (loadFile : string -> Result<string, string>)
    (context  : Map<string, string>)
    (jsonText : string)
    : Result<ResolveWithExtensionsResult, ImportError list>
```

`evaluateMathExtensions` is the composable primitive — given any resolved token sequence, it evaluates `tsMathExpression` extensions against the numeric context built from the sequence itself (Number, Dimension, Duration tokens contribute their scalar value, keyed by full dot-path). Tokens carrying no extension pass through unchanged.

`importWithResolverEvaluatingExtensions` is the one-call convenience that does `importWithResolver` followed by `evaluateMathExtensions`.

`Resolver.resolveAll` is **unchanged**. A file resolved through it produces the same output as any spec-conformant DTCG resolver would — the extension is metadata at that layer. Extension semantics are an explicit, opt-in capability at the `Api` layer.

`MathEval` stays inside `TokensStudio` (where the shim already uses it). A new public wrapper `TokensStudio.tryEvaluateMathExpression : Map<string, float> -> string -> float option` adapts the index from "resolved numeric values" to the string-keyed form the recursive evaluator expects.

## Rationale

- **Mirrors the pattern established by `validateStrictDtcg` (ADR-028 addendum, 2026-05-10).** Opt-in extension-aware behaviour at the `Api` layer; the underlying core (`Format.serialize`, `Resolver.resolveAll`) stays strict-DTCG-compliant.
- **No layer surgery.** `MathEval` stays in `TokensStudio`; the meta-package already depends on `TokensStudio`. The new function is a natural fit there.
- **Composable.** Advanced callers can run `Resolver.resolveAll` then `Api.evaluateMathExtensions` separately, or call the convenience wrapper. The TS-import family (`Api.importTokensStudioThemed`) already evaluates math correctly per-theme for the TS-as-SoT workflow; this closes the same gap for the DTCG-as-SoT workflow.
- **Honest naming.** `importWithResolverEvaluatingExtensions` is verbose on purpose — the function does more than strict DTCG resolution, and the name says so.
- **Non-fatal warnings, not errors.** Per the request's contract, evaluation failures (missing variable, parse error, NaN result) fall back to the stale `$value` and emit a `MathExpressionFailed` warning. The full resolution succeeds; the author sees the warning and fixes the formula.

## Consequences

- **New types** (Foundation): `ExtensionEvaluationWarning = | MathExpressionFailed of path * expression * reason`, with a formatter. Single-case DU for forward extensibility — future extension-aware passes (e.g. unit coercion, derived colors) can add cases without breaking existing pattern matches.
- **New result type** (meta-package): `ResolveWithExtensionsResult { Tokens; Warnings }`. Matches the TS-family pattern (record carrying both result and warnings alongside the outer `Result` for fatal errors).
- **New public functions** in `Api` and `Api.Primitives`: `evaluateMathExtensions`, `importWithResolverEvaluatingExtensions`, `formatExtensionEvaluationWarning` (Primitives only).
- **New public function** in `TokensStudio`: `tryEvaluateMathExpression : Map<string, float> -> string -> float option`. Exposes the evaluator without exposing the internal recursive `MathEval` module shape.
- **Extension contract is now resolver-aware**, but only when callers explicitly opt in. A file containing `tsMathExpression` resolved through plain `Resolver.resolveAll` still produces what other DTCG tools would produce; same file through `Api.importWithResolverEvaluatingExtensions` produces re-evaluated values. The divergence is documented; users opt in by function name.
- **Future extensions in our vendor namespace** that warrant similar post-resolve treatment should add a case to `ExtensionEvaluationWarning` and extend `evaluateMathExtensions` to handle them. The function becomes the single collection point for "things we evaluate after resolve."
- **Value-type preservation:** when the math expression's host token is a `ResolvedDimension` or `ResolvedDuration`, only the scalar is updated — the unit is preserved. `ResolvedNumber` replaces the whole value. Non-numeric token types carrying the extension are passed through unchanged with no warning (the extension is structurally non-applicable; not a failure).
