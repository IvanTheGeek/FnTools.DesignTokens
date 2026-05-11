---
id: 033
title: Dimension token aliasing a number — validate as TypeMismatch, emit as Npx
status: accepted
date: 2026-05-10
---

## Context

DTCG allows any token to alias any other token. When a `dimension` token aliases
a `number` token (a common pattern in design systems with a numeric scale
separate from dimension tokens), the resolver propagates the alias target's
value:

```json
spacing.x1 = { "$type": "dimension", "$value": "{scale.x1}" }
scale.x1   = { "$type": "number",    "$value": 16 }
```

After `flattenAliases` + `flattenResolvedFile`, the result is:

```
ResolvedToken {
  Type  = DimensionType        // from declared $type on spacing.x1
  Value = ResolvedNumber 16.0  // from alias target scale.x1
}
```

The emitter previously dispatched on `Value` only, ignoring `Type`. Result:
`--spacing-x1: 16;` — invalid as a CSS `<length>`, browsers silently reject it.
Discovered by a downstream consumer (`request_2026-05-10_01_library`) who was
working around it with a post-process regex.

Three responses considered:

1. Make this a hard error at resolution — refuse to produce the mismatched ResolvedToken
2. Silently coerce in the emitter — treat the number as `Npx` when the declared type is dimension
3. Both — surface the mismatch as a validation error AND coerce in the emitter so callers who bypass validation still get valid CSS

## Decision

**Both.** The two-layer response treats the type system as the documentation and
the emitter as the safety net:

- **`Validation.validate`** — new `checkAliasTypes` walk follows alias chains
  and emits a `TypeMismatch` error when the declared type disagrees with the
  ultimate resolved value's type. The author sees the problem before the file
  reaches an emitter.
- **`CssEmitter.tokenToCssDeclsWith`** — when `token.Value = ResolvedNumber n`
  and `token.Type = DimensionType`, treat the bare number as `{Value=n; Unit=Px}`
  and apply the unit policy. Same fix mirrored in `emitCalcPreserving` so the
  calc()-optimization branch also fires for these tokens when they fit the scale.

The emitter coercion uses **px** as the implicit unit. DTCG dimension values in
JSON authoring are written as `Npx` shorthand or `{value, unit}` objects with px
default — choosing px matches both the spec's default unit and CSS's behavior
when a bare number is supplied where a length is expected (rejection).

## Consequences

- Authors who run `Api.import` (which validates before resolving) see the
  mismatch as a `TypeMismatch` ValidationError; the file is rejected before
  flatten. This is the canonical happy path.
- Authors who skip validation (raw `Primitives.flattenResolved` or its callers)
  still get valid CSS — the emitter coerces silently. The behavior is documented
  in the CssEmitter source.
- Calc-preserving emission continues to work for dimension→number aliases —
  `--spacing-x1: calc(var(--base) * var(--multiplier) * 1px);` is emitted when
  the value fits `base × multiplier^n`.
- `TokenValue.inferType` and `TokenType.displayName` were moved from
  `DesignTokens.fs` (private) to `Foundation/Domain.fs` (public module-on-type
  pattern) so the validation layer can use them without duplicating the mapping.
- No silent breakage: any existing files that relied on the unitless emission
  (none in our test corpus) would now produce different CSS — that CSS was
  invalid before, valid now.
- Validation passes do not catch this when called separately from import
  pipelines that skip them. Callers must opt in to validation for the error to
  surface. The emitter fix is the floor; validation is the ceiling.

## Addendum — `flattenResolvedFile` was preventing the emitter coercion (v0.10.1, 2026-05-10)

The 0.6.0 ADR-033 fix had a latent bug discovered by
`request_2026-05-10_04` follow-up (`outside-conversations_2026-05-10_03.md`):
`DesignTokens.fs` `flattenResolvedFile` was using `target.Type |> Option.orElse t.Type`
when following an alias — meaning the alias *target's* declared type won
over the aliasing token's declared type. For a `spacing.x1 (DimensionType)`
aliasing `scale.x1 (NumberType)`, the resolved token came out with
`Type = NumberType`, and the ADR-033 emitter coercion guard
(`when token.Type = DimensionType`) never fired. The output was unitless
`--spacing-x1: 16` — the exact behavior ADR-033 was designed to prevent.

The bug was masked through 0.9.0 because every code path that produced
visible CSS output went through `Resolver.flattenAliases` first (either
via `resolveAll` internally, or via the TS-import-specific
`partialFlattenResolvedFile` which already had this fix applied
2026-05-04). When 0.9.0's `evaluateMathExtensionsInFile` introduced the
direct `resolve → evaluate → flattenResolvedFile` path (skipping
`flattenAliases` to enable propagation), the bug became visible. 0.10.0's
`ValidateOptions.permissive` then let TS-as-SoT consumers actually reach
this path with files containing dimension→number aliases — at which point
the requester reported unitless CSS in their build output.

The fix in 0.10.1 mirrors the long-standing `partialFlattenResolvedFile`
implementation:

1. Flip the precedence: `t.Type |> Option.orElse target.Type` — the
   aliasing token's declared type wins, restoring ADR-033's intended
   behavior.
2. Add the `Number → Dimension {n, Px}` / `Number → Duration {n, Milliseconds}`
   coercion at the flatten step so the resolved `Value` matches the
   resolved `Type` (rather than relying solely on the emitter to coerce).

After the fix, `partialFlattenResolvedFile` and `flattenResolvedFile` have
identical alias-handling logic. The two functions could be unified in a
future refactor; for the patch release, they're left parallel with a
comment in each pointing at the other.

Test coverage gap closed: the 0.10.0 test asserted the value (20.0) but
not the type or the CSS output, which let the bug slip through. The 0.10.1
tests assert all three.

Removal of this addendum would require either reverting the fix (no — it
breaks real consumer output) or unifying the two flatten functions
(separate refactor, not patch-release scope).

## Addendum — flatten functions unified (v0.10.2, 2026-05-11)

The parallel-functions cleanup flagged in the v0.10.1 addendum was
completed in v0.10.2. `flattenResolvedFile` and `partialFlattenResolvedFile`
now share `flattenOneToken : TokenFile -> string list -> Token -> Result<ResolvedToken, ValidationError list>`,
which encapsulates the alias-following + Number→Dimension/Duration
coercion logic in one place. The two outer functions are now thin
wrappers that differ only in error-collection strategy:

- `flattenResolvedFile` collects errors with `collect` (fail-fast `Result`)
- `partialFlattenResolvedFile` accumulates errors alongside successes via
  ResizeArray + a new `toPartialError` helper that degrades each
  `ValidationError` to the `(path, message)` tuple shape

This removes the "fix one, forget the parallel" failure mode that gave us
the v0.10.1 bug. Any future change to alias handling now lands in one
place and both code paths benefit automatically.

**Tiny behavior change in partial-success error output**: the previous
`partialFlattenResolvedFile` had an inconsistent degradation — for some
error types the path was doubled in the message (`"path: path: msg"`)
because the code applied `ValidationError.format` (which prepends the
embedded path) on top of the outer path tuple. The new `toPartialError`
uses the embedded path and raw message for `UnresolvedReference`,
`ConstraintViolation`, and `TypeMismatch`, and uses the outer path with
the joined-cycle message for `CircularReference`. This is a slight
cleanup of `TokenUnresolved` warning messages produced by the
`Api.importTokensStudio*` family — no functional change, just less
verbose. No test changes needed (329/329 still pass).

`flattenAliases` (in `Resolver.fs`) is unchanged — it's a different
operation (TokenFile → TokenFile, replaces aliases in the file
representation) and doesn't share logic with the resolved-token-producing
flatten variants. ADR-036 promoted it to public; this refactor doesn't
touch it.
