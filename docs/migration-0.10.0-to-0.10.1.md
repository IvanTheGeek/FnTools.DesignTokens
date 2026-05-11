# Migrating from 0.10.0 to 0.10.1

Released: 2026-05-10. Tracking ADR: [`033-dimension-number-alias-handling.md`](../LOGOS/decisions/033-dimension-number-alias-handling.md) (addendum). Source: [`outside-conversations_2026-05-10_03.md`](../LOGOS/outside-conversations/outside-conversations_2026-05-10_03.md).

---

## TL;DR

Pure bug fix. **No API changes; no behavior changes for working code.** Fixes a latent bug in `flattenResolvedFile` that was clobbering the aliasing token's declared `$type` with the alias target's `$type`, preventing ADR-033's emitter coercion from firing. Visible symptom: unitless CSS like `--spacing-x1: 20` instead of `--spacing-x1: 20px`.

```diff
- <PackageReference Include="FnTools.DesignTokens" Version="0.10.0" />
+ <PackageReference Include="FnTools.DesignTokens" Version="0.10.1" />
```

If you were on the four-step Primitives workaround that v0.10.0 consumers used to work around the bug (`resolve` + `evaluateMathExtensionsInFile` + `flattenAliases` + `flattenResolved`), you can drop back to the convenience wrapper `Api.importWithResolverEvaluatingExtensionsWith ValidateOptions.permissive` after upgrading — it now produces correct CSS without the manual `flattenAliases` step. The four-step still works; it's just no longer necessary.

---

## What was broken

`DesignTokens.fs` `flattenResolvedFile` (used by `Api.import`, `Api.importWithResolver`, and `Api.importWithResolverEvaluatingExtensions`) handled alias-following with this line:

```fsharp
// BUG (0.10.0 and earlier)
| Some target -> Ok { target with Type = target.Type |> Option.orElse t.Type }
```

For a `spacing.x1 (DimensionType)` aliasing `scale.x1 (NumberType)`, the alias target's `NumberType` won over the aliasing token's declared `DimensionType`. Result:

- `spacing.x1` resolved with `Type = NumberType`, `Value = ResolvedNumber 20`
- CSS emitter dispatched on `Value = ResolvedNumber` → bare `--spacing-x1: 20;`
- ADR-033's emitter coercion guard (`when token.Type = DimensionType`) never fired because the type had been stripped before the emitter saw it

The bug was latent — it had been present since 0.6.0 — but masked by every common code path going through `Resolver.flattenAliases` first (which preserved the aliasing token's declared type correctly). 0.9.0's `evaluateMathExtensionsInFile` introduced the `resolve → evaluate → flattenResolvedFile` path that skipped `flattenAliases` to enable propagation. 0.10.0's `ValidateOptions.permissive` then let TS-as-SoT consumers actually reach this path with files containing dimension→number aliases, exposing the bug.

The parallel `partialFlattenResolvedFile` (used by all `Api.importTokensStudio*` paths) had the correct logic since 2026-05-04 — that's why the TS-import path always produced correct output. Only the DTCG-import paths had the bug.

## What changed

`flattenResolvedFile` now matches `partialFlattenResolvedFile`:

```fsharp
// FIX (0.10.1)
| Some target -> Ok { target with Type = t.Type |> Option.orElse target.Type }
```

Plus the same `Number → Dimension { n, Px }` / `Number → Duration { n, Milliseconds }` coercion at the flatten step that `partialFlattenResolvedFile` has had for months. The resolved `Value` now matches the resolved `Type` instead of relying on the emitter to coerce.

After this fix, every code path that handles a dimension→number alias produces:

- `Type = DimensionType` (the aliasing token's declared intent wins)
- `Value = ResolvedDimension { Value = n; Unit = Px }` (coerced from the alias target's `Number n`)

And the CSS emitter produces valid output: `--spacing-x1: 20px;` with identity policy, `--spacing-x1: 1.25rem;` with a Rem policy.

---

## Migration scenarios

### Scenario A — you're on v0.10.0 with the four-step workaround

The requester from `outside-conversations_2026-05-10_03.md` did this:

```fsharp
// 0.10.0 — manual four-step workaround for the type-loss bug
match Api.Primitives.parseResolver json with
| Ok doc ->
    match Resolver.resolve loadFile input doc with
    | Ok mergedFile ->
        let r = Api.evaluateMathExtensionsInFile mergedFile
        // WORKAROUND: flattenAliases preserves declared types correctly,
        // so inserting it here repairs what flattenResolvedFile was breaking
        match Resolver.flattenAliases r.File with
        | Ok aliasFlattened ->
            match Api.Primitives.flattenResolved aliasFlattened with
            | Ok tokens -> ...
        | Error es -> ...
```

After 0.10.1, you can drop back to the convenience wrapper or the three-step composition — both now produce correct types and units:

```fsharp
// 0.10.1 — one-call convenience (preferred)
Api.importWithResolverEvaluatingExtensionsWith
    ValidateOptions.permissive loadFile input json
```

Or:

```fsharp
// 0.10.1 — three-step Primitives (the explicit alternative)
match Api.Primitives.parseResolver json with
| Ok doc ->
    match Resolver.resolve loadFile input doc with
    | Ok mergedFile ->
        let r = Api.evaluateMathExtensionsInFile mergedFile
        match Api.Primitives.flattenResolved r.File with
        | Ok tokens -> ...
```

The four-step still works — `flattenAliases` is now harmless rather than necessary. Keep it if you prefer the explicit chain; remove it if you want fewer lines.

### Scenario B — you're on v0.10.0 without the workaround

Your CSS output for dimension→number aliases was unitless. Browsers silently ignored these declarations. **You probably have broken styling somewhere you didn't notice.** Upgrade to 0.10.1 and verify your output — `--spacing-*`, `--size-*`, `--radius-*`, etc. should now have units.

### Scenario C — you're not on a path that uses alias-following through `flattenResolvedFile`

The single-file `Api.import` and `Api.importWithResolver` paths theoretically had the same bug, but for the bug to manifest you needed a file with a dimension→number alias *and* you needed to bypass validation (otherwise ADR-033's `TypeMismatch` rejected the file before flatten). In practice, this was rare without the convenience wrapper. If your tests pass and your CSS output looks right, nothing changed for you.

### Scenario D — you use the `Api.importTokensStudio*` family

No change. Those paths used `partialFlattenResolvedFile` which has had the correct behavior since 2026-05-04. The bug never affected the TS-import family.

---

## What did NOT change

- No API changes. No new functions, no deprecations, no signature changes.
- `Resolver.resolve`, `Resolver.flattenAliases`, `Resolver.resolveAll`, the emitter family, the validation family — all unchanged.
- ADR-033's emitter coercion (`when token.Type = DimensionType` in `CssEmitter.tokenToCssDeclsWith`) is unchanged. It's still the safety net; this fix means it now actually receives the right inputs to fire.
- `partialFlattenResolvedFile` is unchanged — `flattenResolvedFile` now matches it. The two functions could be unified in a future refactor; for this patch release they're left parallel with cross-reference comments.

---

## Upgrade steps

1. Update your `PackageReference`:
   ```xml
   <PackageReference Include="FnTools.DesignTokens" Version="0.10.1" />
   ```

2. Build. No compile-time changes expected.

3. If you're emitting CSS that contains `--spacing-*` / `--size-*` / `--radius-*` tokens that resolve through a dimension→number alias chain, check your output — values that were previously bare numbers now have units.

4. If you wrote a four-step Primitives workaround on 0.10.0, you can simplify it back to three steps (or use the convenience wrapper).

---

## Reference

- **ADR-033 addendum** — full diagnosis of the latent bug and the parallel-functions cleanup direction.
- **Source conversation** — `LOGOS/outside-conversations/outside-conversations_2026-05-10_03.md` (the requester's discovery + their four-step workaround). Response in `_04.md`.
- **Tests** — 329/329 pass. The 0.10.0 `PERMISSIVE` test in `ExtensionEvaluationTests.fs` was strengthened to assert `spacing.x1.Type = DimensionType` and `spacing.x1.Value = ResolvedDimension { 20.0, Px }`. New `PERMISSIVE → CSS emit` test exercises the full convenience-wrapper-to-CSS pipeline and asserts `--spacing-x1: 20px;` (identity policy) and `--spacing-x1: 1.25rem;` (Rem policy).
