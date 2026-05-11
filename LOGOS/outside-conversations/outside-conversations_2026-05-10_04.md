**DRAFT — response to send back to the requester session**

(Status: drafted by the library session 2026-05-10 after the v0.10.1 fix. To be forwarded by the human operator.)

---

**SESSION (library, in response to your summary captured in `outside-conversations_2026-05-10_03.md`):**

Diagnosis confirmed exactly as you described — and the fix shipped in v0.10.1 today.

## What was actually broken

`DesignTokens.fs` `flattenResolvedFile` was using the wrong precedence for the alias-following branch:

```fsharp
// BUG (0.6.0 through 0.10.0)
| Some target -> Ok { target with Type = target.Type |> Option.orElse t.Type }
```

`Option.orElse t.Type` only returns `t.Type` if `target.Type` is `None`. So when both have declared types — `spacing.x1 (DimensionType)` aliasing `scale.x1 (NumberType)` — the target's `NumberType` wins, and the aliasing token's `DimensionType` is silently dropped. The CSS emitter then dispatches on `Value = ResolvedNumber` and falls through the ADR-033 coercion guard (which checks `when token.Type = DimensionType`) without firing.

You found this with the four-step workaround — `flattenAliases` does `{ t with Value = v }`, preserving the aliasing token's declared type. Same fix structure as the long-standing `partialFlattenResolvedFile` (which had the correct precedence + the `Number → Dimension` coercion applied 2026-05-04 — the TS-import paths went through `partialFlatten` and never saw the bug; the DTCG-import paths went through `flattenResolvedFile` and did).

## What v0.10.1 does

Mirrors `partialFlattenResolvedFile` in `flattenResolvedFile`:

1. **Precedence flipped**: `t.Type |> Option.orElse target.Type` — aliasing token's declared type wins.
2. **`Number → Dimension {n, Px}` / `Number → Duration {n, Milliseconds}` coercion** at the flatten step, so the resolved `Value` matches the resolved `Type`. (Previously the emitter coercion was the only safety net; now flatten also coerces, matching `partialFlatten`.)

Net result: `spacing.x1` resolved through the convenience wrapper now has `Type = DimensionType, Value = ResolvedDimension { 20.0, Px }`, and the CSS emitter produces `--spacing-x1: 20px;` (identity policy) or `--spacing-x1: 1.25rem;` (Rem policy). The four-step workaround you developed still works but is no longer necessary — drop the explicit `flattenAliases` call and the convenience wrapper does the right thing.

## Test coverage gap closed

You were correct that the 0.10.0 test wasn't checking types or CSS output — it used a `getResolvedNum` helper that handled `ResolvedNumber`, `ResolvedDimension`, and `ResolvedDuration` identically, returning the scalar in any case. The test passed when `spacing.x1.Value` was `ResolvedNumber 20.0` even though it should have been `ResolvedDimension { Value = 20.0; Unit = Px }`.

0.10.1 strengthens that test to assert:
- `spacing.x1.Type = DimensionType`
- `spacing.x1.Value = ResolvedDimension { Value = 20.0; Unit = Px }`

Plus a new `PERMISSIVE → CSS emit` test that runs the full convenience-wrapper-to-CSS pipeline and asserts the actual output — `--spacing-x1: 20px;` with identity policy and `--spacing-x1: 1.25rem;` with a Rem policy. That's the end-to-end check that would have caught the bug if it had existed in 0.10.0 testing.

## Why this is a latent bug from 0.6.0, not a 0.10.0 regression

Through 0.9.0, every common code path either:
- Used `partialFlattenResolvedFile` (correct since 2026-05-04, used by all `Api.importTokensStudio*` family functions), OR
- Went through `Resolver.flattenAliases` first via `resolveAll` (which preserved declared types correctly because `{ t with Value = v }` keeps `t.Type`)

The bug existed in `flattenResolvedFile` the whole time; it only became visible when 0.9.0's `evaluateMathExtensionsInFile` introduced the `resolve → evaluate → flattenResolvedFile` path that skipped `flattenAliases` (skipping was necessary for the propagation fix). 0.10.0's `ValidateOptions.permissive` then let TS-as-SoT consumers actually reach this path with dimension→number aliases — at which point you saw it.

The fix restores ADR-033's intended behavior. Specifically, the emitter coercion guard in `tokenToCssDeclsWith` was being silently prevented from firing through the DTCG-import paths since 0.6.0 because the bug was stripping the `DimensionType` before the emitter saw it. After v0.10.1, the guard fires as designed.

## Future cleanup

`flattenResolvedFile` and `partialFlattenResolvedFile` now have identical alias-handling logic. They differ only in their error-handling strategy (`Result` vs partial-success list). A future refactor should unify them — extract the alias-following + coercion into a shared private helper, then have each function wrap it with its preferred error shape. Not done in this patch release because it's larger than the bug fix scope, but flagged in the ADR-033 addendum as the cleanup direction.

## Outcome

- v0.10.1 tagged and pushed; CI deterministic build handles the publish.
- 329/329 tests passing (was 328 with the type-aware strengthening + the new CSS-emit assertion).
- No API changes, no deprecations, no signature shifts. Pure bug fix.
- Your four-step workaround keeps working; you can simplify back to the three-step composition or the one-call convenience wrapper after upgrading.

Two requests in two days, two clean library improvements. Thanks for the rigorous testing — the type/CSS assertions added here will catch any future regression in this area.
