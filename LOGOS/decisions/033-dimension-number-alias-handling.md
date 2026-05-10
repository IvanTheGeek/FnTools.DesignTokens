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
