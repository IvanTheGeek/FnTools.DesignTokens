# Migrating from 0.10.1 to 0.10.2

Released: 2026-05-11. Tracking ADR: [`033-dimension-number-alias-handling.md`](../LOGOS/decisions/033-dimension-number-alias-handling.md) (second addendum).

---

## TL;DR

Pure internal refactor + a tiny cleanup of one error-message format. **No public API changes. No behavior changes for typical consumers.** Bump the version and rebuild.

```diff
- <PackageReference Include="FnTools.DesignTokens" Version="0.10.1" />
+ <PackageReference Include="FnTools.DesignTokens" Version="0.10.2" />
```

---

## What changed

### Internal — flatten functions unified

`flattenResolvedFile` and `partialFlattenResolvedFile` (both in `DesignTokens.fs`) used to each carry their own copy of the alias-following + Number→Dimension/Duration coercion logic. They drifted out of sync, which is exactly how the v0.10.1 bug happened — the fix landed in one function 2026-05-04 and was forgotten in the other until 0.10.1.

0.10.2 extracts a shared private helper `flattenOneToken : TokenFile -> string list -> Token -> Result<ResolvedToken, ValidationError list>` and has both outer functions delegate to it. They now differ only in error-collection strategy (fail-fast `Result` vs. partial-success accumulation). Any future change to alias handling lands in one place and both code paths benefit automatically.

Pure refactor — no observable behavior change for the resolved-token output.

### Tiny cleanup — partial-success warning messages

The `Api.importTokensStudio*` family produces `TokensStudioImportWarning.TokenUnresolved (path, ref)` warnings via `partialFlattenResolvedFile`. Some warning messages had the path doubled in the `ref` field — `"spacing.x1: spacing.x1: cannot determine $type"` — because the old code applied `ValidationError.format` (which prepends the embedded path) on top of the outer path tuple. The new `toPartialError` helper uses the embedded path and raw message for `UnresolvedReference`, `ConstraintViolation`, and `TypeMismatch`, and uses the outer path with the joined-cycle message for `CircularReference`.

Net effect: `TokenUnresolved` warning `ref` field is slightly cleaner — no more `"path: path: msg"` doubling. The `path` field of the warning is unchanged.

---

## Migration scenarios

### Scenario A — you don't read `TokenUnresolved.Ref` strings programmatically

Do nothing. The change is purely cosmetic in log output.

### Scenario B — you parse `TokenUnresolved.Ref` strings programmatically

The `Ref` field for `ConstraintViolation`/`TypeMismatch`-derived warnings no longer has the doubled path prefix. If you were relying on the `"path: ..."` prefix to extract the path, you can now read `TokenUnresolved.Path` directly — that's what the field was always for; the prefix was an accidental side effect of error formatting.

### Scenario C — you only use `Api.import`, `Api.importWithResolver`, `Api.importWithResolverEvaluatingExtensions` (the DTCG-import family)

No change. These use `flattenResolvedFile` directly and never see `TokenUnresolved` warnings.

---

## What did NOT change

- No public API additions, deprecations, signature changes
- No behavior changes for any successful import
- `Resolver.flattenAliases` (ADR-036, public since v0.10.0) — different operation, not touched
- All 329 tests still pass; no test relied on the doubled-path format

---

## Upgrade steps

1. Update your `PackageReference`:
   ```xml
   <PackageReference Include="FnTools.DesignTokens" Version="0.10.2" />
   ```
2. Build. No expected compile-time or runtime impact.
3. If you log `TokenUnresolved` warnings, output may be slightly tidier. No code changes needed.

---

## Reference

- **ADR-033 v0.10.2 addendum** — the cleanup direction flagged in the v0.10.1 addendum, completed.
- **Tests** — 329/329 still pass. No test changes were needed; the refactor preserves all observable behavior except the warning-format cleanup, which no test exercised.
