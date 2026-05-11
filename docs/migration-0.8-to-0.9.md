# Migrating from 0.8 to 0.9

Released: 2026-05-10. Tracking ADR: [`034-evaluate-math-extensions-post-resolve.md`](../LOGOS/decisions/034-evaluate-math-extensions-post-resolve.md) (addendum). Source request: [`request_2026-05-10_03.md`](../LOGOS/requests/request_2026-05-10_03.md).

---

## TL;DR

**0.8.0 had a structural bug.** `Api.evaluateMathExtensions` could not
propagate updated values through alias chains. If `scale.x1` was a formula
token and `spacing.x1` aliased `{scale.x1}`, updating `scale.x1` from 16 to
20 did not touch `spacing.x1`. Silent.

**Fixed in 0.9.0** by adding `Api.evaluateMathExtensionsInFile`, which
operates on the pre-flatten `TokenFile` (where aliases are still aliases)
instead of the post-flatten `ResolvedToken seq` (where aliases have already
been baked into concrete values). The convenience wrapper
`Api.importWithResolverEvaluatingExtensions` is rewired internally to use
the new path — its public signature is unchanged, but propagation now works.

The original `Api.evaluateMathExtensions` is marked `[<Obsolete>]` and emits
a build warning when called. It still works (for the single-formula-token
case with no aliases); it just can't propagate.

```diff
- <PackageReference Include="FnTools.DesignTokens" Version="0.8.0" />
+ <PackageReference Include="FnTools.DesignTokens" Version="0.9.0" />
```

---

## What changed

### Deprecated — `Api.evaluateMathExtensions`

```fsharp
[<System.Obsolete("...")>]
Api.evaluateMathExtensions
    (tokens: (string list * ResolvedToken) seq)
    : ResolveWithExtensionsResult
```

The function still ships in 0.9.0 with identical behavior to 0.8.0 — it
just emits a deprecation warning. Removal target: v1.0.0 or earlier if no
consumer is identified.

Why the function can't be fixed in place: `Resolver.resolveAll` /
`flattenResolvedFile` follows aliases and bakes their values into concrete
`ResolvedToken`s before producing the flat list. By the time the seq
exists, the alias→target relationship is gone from the data — only the
target's value remains. There's no way to know that `spacing.x1`'s
`ResolvedNumber 16.0` originated from following `{scale.x1}`, so updating
`scale.x1` cannot reach back to `spacing.x1`.

### New — `Api.evaluateMathExtensionsInFile`

```fsharp
type EvaluateMathInFileResult = {
    File     : TokenFile
    Warnings : ExtensionEvaluationWarning list
}

Api.evaluateMathExtensionsInFile (file: TokenFile) : EvaluateMathInFileResult
```

Operates on a `TokenFile` (the type returned by `Resolver.resolve` before
`flattenAliases` runs). In this representation, aliases are still literal
`TokenValue.Alias (CurlyBrace path)` references — the structural
relationship is intact.

The function:

1. Builds an alias-aware index from the file. Each path maps to one of:
   - the raw `tsMathExpression` string (preferred when present — the
     formula is the source of truth, `$value` may be stale)
   - the numeric scalar (`Number` / `Dimension.Value` / `Duration.Value`)
   - an alias-reference string `"{target.path}"`
2. For each token carrying `tsMathExpression`, evaluates the expression
   against the index. `MathEval` recurses through aliases and nested
   expressions with cycle detection.
3. Replaces the token's `$value` with the evaluated number.
   `Dimension` and `Duration` preserve their unit.
4. Returns the updated `TokenFile` plus any `ExtensionEvaluationWarning`s.

The caller then runs `Primitives.flattenResolved` on the updated file.
Aliases naturally pick up the new values because `tryResolveAliasIn` reads
the freshly-evaluated `$value`. **Propagation is correct by construction.**

### Rewired — `Api.importWithResolverEvaluatingExtensions`

Same public signature as 0.8.0. Internal pipeline changed:

```diff
- parseResolver → validate → resolve → validate → flattenResolvedFile → evaluateMathExtensions
+ parseResolver → validate → resolve → validate → evaluateMathExtensionsInFile → flattenResolvedFile
```

If you were calling this in 0.8.0, you get the propagation fix automatically
on upgrade. No code change needed.

### New helper — `TokensStudio.tryEvaluateMathExpressionWithIndex`

```fsharp
TokensStudio.tryEvaluateMathExpressionWithIndex
    (rawIndex   : Map<string, string>)
    (expression : string)
    : float option
```

Lower-level evaluator companion to `tryEvaluateMathExpression`. Takes a
string-keyed index where each value can be a plain number (`"16"`), a math
expression (`"round({base} * pow({mult}, 3))"`), or an alias reference
(`"{target.path}"`). The evaluator recurses with cycle detection.

`tryEvaluateMathExpression` (`Map<string, float>` → expression) remains for
the simpler post-resolve numeric context case. Use
`tryEvaluateMathExpressionWithIndex` when your context still has aliases
or nested expressions.

---

## Migration scenarios

### Scenario A — you only call `Api.importWithResolverEvaluatingExtensions`

Do nothing. The public signature is unchanged; propagation works now.

```fsharp
// Same call, fixed behavior:
match Api.importWithResolverEvaluatingExtensions loadFile context resolverJson with
| Ok result -> /* result.Tokens now has propagated values */
| Error es -> /* handle */
```

### Scenario B — you call `Api.evaluateMathExtensions` directly on a `ResolvedToken seq`

You'll see a `FS0044` deprecation warning. Two options:

**Recommended — switch to the pre-flatten pipeline:**

```diff
+ // Manually compose, evaluating before flattening
- match Api.import json with
- | Ok tokens ->
-     let r = Api.evaluateMathExtensions tokens
-     processTokens r.Tokens

+ match Format.parse json with
+ | Ok file ->
+     let r = Api.evaluateMathExtensionsInFile file
+     match Api.Primitives.flattenResolved r.File with
+     | Ok tokens -> processTokens (List.ofSeq tokens) (List.ofSeq r.Warnings)
+     | Error es -> ...
```

**Quick suppression (if you know your file has no formula-aliased tokens
and you don't need propagation):**

```fsharp
#nowarn "44"
// existing Api.evaluateMathExtensions calls keep working
```

This is a stopgap. The deprecated function will be removed at v1.0.0.

### Scenario C — you use the Primitives path manually (skips validation)

The user who reported the bug uses this pattern because their SoT contains
`dimension` tokens aliasing `number` tokens (the canonical TS scale pattern),
which `Validation.validate` flags as `TypeMismatch` per ADR-033.

Updated pattern:

```fsharp
match Api.Primitives.parseResolver resolverJson with
| Error es -> Error es
| Ok doc ->
    match Resolver.resolve loadFile input doc with
    | Error es -> Error es
    | Ok mergedFile ->
        // NEW: insert evaluation between resolve and flatten
        let r = Api.evaluateMathExtensionsInFile mergedFile
        match Api.Primitives.flattenResolved r.File with
        | Error es -> Error es
        | Ok tokens ->
            // tokens has propagated values; r.Warnings has any eval failures
            Ok (List.ofSeq tokens, r.Warnings)
```

The validation friction (the reason you skip `validate` between `resolve`
and the new evaluation step) is unchanged in 0.9.0. It's tracked as a
separate follow-up.

---

## What did NOT change

- `Format.parse` / `Format.serialize` — no API or behavior changes.
- `Resolver.resolveAll` — still strict-DTCG-compliant, reads `$value`
  directly, no extension semantics. The extension-aware behavior is in
  the meta-package, not the resolver, per ADR-034 original.
- `Validation.validate` — no API or behavior changes (notably, the
  `TypeMismatch` for dimension→number aliases from ADR-033 still fires;
  see Scenario C).
- `CssEmitter.emitCalcPreserving` — still reads `tsMathExpression` for
  scale detection (ADR-027). Independent of the resolve-time evaluation.
- `Api.validateStrictDtcg` — unchanged (ADR-028 addendum / 0.7.0).
- The `TokensStudioImportResult` shape, `ResolveWithExtensionsResult`
  shape, and `ExtensionEvaluationWarning` shape are all unchanged.

---

## Upgrade steps

1. Update your `PackageReference`:
   ```xml
   <PackageReference Include="FnTools.DesignTokens" Version="0.9.0" />
   ```

2. Build. If you call `Api.evaluateMathExtensions` (or
   `Api.Primitives.evaluateMathExtensions`), you'll get FS0044 warnings —
   migrate per Scenario B or temporarily `#nowarn "44"` while you migrate.

3. If you use `Api.importWithResolverEvaluatingExtensions`, no code change;
   verify your output now has correctly-propagated values for any
   alias-of-formula tokens.

4. Tests: confirm the bug case is fixed. The new `inFileTests` testList in
   `ExtensionEvaluationTests.fs` contains a `PROPAGATION` smoke test based
   on the exact scenario from the bug report.

---

## Reference

- **ADR-034 addendum** — full diagnosis of the design hole and the
  pre-flatten fix.
- **Original bug report** — `LOGOS/requests/request_2026-05-10_03.md`.
- **315 tests pass** — including 11 new tests for
  `evaluateMathExtensionsInFile`, of which 3 specifically exercise alias
  propagation (single-hop, multi-hop chain, formula-references-formula).
- **One deprecation-regression test** preserved under `#nowarn "44"` so
  the deprecated function keeps working until removal.
