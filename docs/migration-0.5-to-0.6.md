# Migrating from 0.5 to 0.6

Released: 2026-05-10. Tracking ADR: [`033-dimension-number-alias-handling.md`](../LOGOS/decisions/033-dimension-number-alias-handling.md).

---

## TL;DR

0.6.0 fixes one bug class — `dimension` tokens that alias `number` tokens used to
emit unitless CSS (`--spacing-x1: 16;`), which browsers silently rejected.
The fix lands on **two layers at once**: validation now flags the cross-type
alias as a `TypeMismatch` error, and the CSS emitter coerces the bare number
to `Npx` so output is valid even when validation is skipped. There are **no
API signature changes** and **no removed types** — but consumers in three
specific situations will see new behavior. See the scenarios below.

```diff
- // 0.5.1 (silently broken)
- --spacing-x1: 16;

+ // 0.6.0 with default emitter — valid CSS
+ --spacing-x1: 16px;

+ // 0.6.0 with Rem unit policy — same source, 16 → 1rem
+ --spacing-x1: 1rem;
```

---

## What changed

### 1. Bug fix — `CssEmitter` (and friends) emit valid CSS for dimension→number aliases

When a token declared `$type: dimension` aliases another token declared
`$type: number`, the resolver propagates the number's value. The resulting
`ResolvedToken` has `Type = DimensionType` but `Value = ResolvedNumber n`.

Previously, every emitter that touched dimension tokens (`emit`, `emitWith`,
`emitThemed`, `emitThemedWith`, `emitMultiMode`, `emitMultiModeWith`,
`emitCalcPreserving`) dispatched on `Value` only and produced unitless
output. As of 0.6.0, the emitter treats `(Type = DimensionType, Value =
ResolvedNumber n)` as `{Value = n; Unit = Px}` and runs it through the unit
policy normally.

| Emitter input | 0.5.1 output | 0.6.0 output |
|---|---|---|
| `emit` (no policy) | `--x: 16;` | `--x: 16px;` |
| `emitWith` Rem policy | `--x: 16;` | `--x: 1rem;` |
| `emitWith` identity policy | `--x: 16;` | `--x: 16px;` |
| `emitCalcPreserving` (value fits scale) | `--x: 16;` | `--x: calc(var(--base) * ... * 1px);` |
| `emitCalcPreserving` (value doesn't fit) | `--x: 16;` | `--x: 16px;` |

### 2. New validation rule — `TypeMismatch` for cross-type alias chains

`Validation.validate` now follows alias chains and emits a `TypeMismatch`
error when the declared `$type` disagrees with the ultimate resolved value's
type. The error reports the origin path, declared type name, and actual type
name.

```fsharp
// Token file with the same cross-type alias
{ "spacing": { "x1": { "$type": "dimension", "$value": "{scale.x1}" } },
  "scale":   { "x1": { "$type": "number",    "$value": 16          } } }

// 0.5.1: Validation.validate returns Ok ()
// 0.6.0: Validation.validate returns
//   Error [ TypeMismatch ("spacing.x1", "dimension", "number") ]
```

The check follows transitive aliases (`a → b → c` flags both `a` and `b` if
`c` is the type that disagrees). Circular aliases and unresolved references
are not flagged as `TypeMismatch` — they are already covered by
`CircularReference` and `UnresolvedReference`, respectively.

### 3. `emitCalcPreserving` now matches dimension→number aliases for the calc() optimization

The calc-preserving branch (ADR-027) previously matched `ResolvedDimension`
only. As of 0.6.0 it also matches `ResolvedNumber n when Type = DimensionType`
with `{Value = n; Unit = Px}` — so dimension→number alias chains that fit
`value = base × multiplier^n` produce `calc()` expressions instead of
falling through to a concrete `Npx` literal.

### 4. Internal — `TokenValue.inferType` and `TokenType.displayName` promoted to Foundation

These two helpers were private in `DesignTokens.fs`. They are now public on
the `Foundation` package as `TokenValue.inferType` and `TokenType.displayName`,
because the new validation rule (above) needs them and Validation depends
only on Foundation.

If you previously copy-pasted either function into your own code, you can
delete the copy and use the shared one.

---

## Migration scenarios

### Scenario A — you use `Api.import` (the recommended path)

`Api.import` runs validation. **If your token files contain `dimension` aliases
to `number` tokens, the import will now fail with `TypeMismatch` instead of
succeeding silently.**

Two options:

1. **Fix the alias** (recommended). Author the target as `$type: dimension`
   with a `px` unit so the alias is type-consistent:
   ```diff
   - "scale": { "x1": { "$type": "number", "$value": 16 } }
   + "scale": { "x1": { "$type": "dimension", "$value": { "value": 16, "unit": "px" } } }
   ```
2. **Filter the error** if you intentionally use the number-scale pattern
   and want the emitter to coerce silently:
   ```fsharp
   match Api.import jsonText with
   | Error errs ->
       let nonMismatch =
           errs |> List.filter (function
               | ImportError.ValidationFailed vs ->
                   vs |> List.exists (function TypeMismatch _ -> false | _ -> true)
               | _ -> true)
       if List.isEmpty nonMismatch then
           // re-run skipping validation (Primitives tier) — see Scenario B
           ...
       else
           Error nonMismatch
   ```
   The cleaner version is to use `Primitives.*` directly and skip validation
   for that file.

### Scenario B — you use `Primitives.*` without validation

The validation rule does not fire (you skipped it). **Your CSS output changes
from invalid unitless to valid `Npx` (or `Nrem` under a unit policy)** — this
is a behavioral change but in the bug-fix direction. Browsers that silently
ignored your old CSS will now apply the new value.

Action: rebuild any expected-output snapshots / golden files for tokens
where the input had a dimension→number alias. The diff will be `16` → `16px`
(or `1rem`, etc.).

### Scenario C — you had a post-process regex workaround

If your build script post-processes the emitted CSS to append `px` to bare
numeric values (the original report that triggered ADR-033 described exactly
this), **remove the workaround**. The emitter now produces valid CSS without
help. The workaround was fragile (path-specific regex) and didn't apply unit
policies; the built-in fix is correct for every path and respects the policy.

### Scenario D — you import Tokens Studio files

No impact unless your TS file deliberately uses the dimension→number cross-type
pattern, which is unusual in Tokens Studio's data model (TS dimension/number
distinction is at the field level, not normally aliased across types). If
you do hit a mismatch, the same two options as Scenario A apply.

### Scenario E — you only call serializers / `serializeResolver` / Bindings emitter

No impact. None of these paths changed.

---

## Upgrade steps

1. Update your `PackageReference`:
   ```xml
   <PackageReference Include="FnTools.DesignTokens" Version="0.6.0" />
   ```
   (Or individual layers — `FnTools.DesignTokens.Foundation`, `.Format`,
   `.Validation`, `.Resolver`, `.Css`, `.Bindings`, `.TokensStudio` — all at 0.6.0.)

2. Build. The library API surface is unchanged, so the only compile-time
   surprise is if you had a local copy of `inferType` shadowing the now-public
   one in Foundation.

3. Run your tests / build pipeline:
   - If you use `Api.import`, watch for new `TypeMismatch` errors and decide
     between fix-the-alias and filter-the-error (Scenario A).
   - If you have snapshots of emitted CSS, expect new `Npx`/`Nrem` values
     where you previously saw bare numbers (Scenario B).
   - If you had a post-process regex, remove it (Scenario C).

4. Verify a sample emitter run for any file containing dimension→number
   aliases. The fix is transparent — there is no opt-out flag — but eyeballing
   one diff confirms the upgrade landed.

---

## Reference

- **ADR-033** — `LOGOS/decisions/033-dimension-number-alias-handling.md` — full rationale and alternatives.
- **Original report** — `LOGOS/requests/request_2026-05-10_01_library_*.md` — the downstream consumer who surfaced the bug and what their workaround looked like.
- **Tests** — 12 new tests cover the change:
  - `CssEmitterTests.dimNumberAliasTests` — six emitter-side cases (identity/Rem/themed policy, regression-no-unitless, calc() optimization).
  - `ValidationTests` — five validation cases (mismatch flagged, same-type passes, chain mismatch flags each step, cycle not flagged as mismatch, unresolved not flagged as mismatch).
  - `CssEmitterTests.calcPreservingTests` — one extra case for the dimension→number calc branch.
