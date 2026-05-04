---
id: 020
title: Resolve number→dimension aliases in typography at shim time
status: accepted
date: 2026-05-04
---

## Context

Tokens Studio allows composite `typography` tokens whose `fontSizes` field aliases a token
of type `"number"`:

```json
"typography": {
  "default": {
    "$type": "typography",
    "$value": {
      "fontSize":   "{scale.base}",
      "fontFamily": "{font-family.default}",
      ...
    }
  }
}
```

`scale.base` is type `"number"` — a dimensionless float produced by a math expression in
`Foundations/Base`. DTCG specifies that `typography.fontSize` must be a dimension value
(`{value: N, unit: "px"}`). The resolver enforces this strictly: when it resolves the alias
and gets a `ResolvedNumber`, it fails with a type mismatch and the whole composite token is
recorded as `TokenUnresolved`.

This caused 18 typography tokens to remain unresolved even after `EvaluateMath` made
`scale.*` tokens available in the merged map.

## Options considered

**Option A — Coerce in the resolver.** When resolving a dimension alias that lands on a
`ResolvedNumber`, wrap as `ResolvedDimension { Value = n; Unit = "px" }`. Minimal change,
but introduces a spec deviation: DTCG requires alias tokens to reference the same type.

**Option B — Resolve at shim time.** In `transformTypographyValue`, when `fontSizes` is an
alias, evaluate the alias target via `MathEval.tryEval` and emit a concrete dimension node
`{value: N, unit: "px"}` directly. The alias never reaches the resolver; the resolver sees a
correctly-typed dimension value. The resolver stays spec-pure.

**Option C — Accept 18 as permanently unresolved.** The values are reachable via `scale.*`
number tokens. Downstream code could work around it. But unresolved tokens in a typography
composite mean the whole composite is absent from the output — a real gap for any consumer
that needs font sizes.

## Decision

Option B: resolve `typography.fontSizes` aliases at shim time via `MathEval.tryEval`.

Rationale:

1. The shim is already the place where Tokens Studio–specific quirks are absorbed. Resolving
   a cross-type alias here is consistent with its existing role (HSL evaluation, math
   expression evaluation, dimension unit injection).

2. The resolver stays spec-compliant. No coercion exception needed; no type-system widening.

3. The resolution uses the same flat index and cycle-detection already in place for number
   math evaluation. `MathEval.tryEval` handles pure aliases (`{scale.base}`), math
   expressions (`round({base} * pow(...))`), and plain numbers — all cases that can appear
   as a `fontSizes` value via an alias chain.

4. The limitation is clearly scoped: only `fontSizes` inside typography composites gets this
   treatment. Other dimension tokens that alias number tokens (if any) still go through the
   resolver normally.

## Consequences

- Laura's file: 0 unresolved tokens (was 18), 250 resolved tokens (was 232).
- The shim produces `typography.*.fontSize` as a concrete `{value, unit}` dimension node.
  Alias chains that cannot be resolved (unknown alias path, cycle) fall back to keeping the
  alias string — the token remains an unresolvable alias, which the resolver handles as it
  always has.
- The "unit is always px" assumption is baked in. If a `fontSizes` alias resolves to a value
  that was stored as `rem`, the unit information is lost in the flat index (raw values are
  strings — the unit isn't preserved separately). This is acceptable: all scale tokens in
  the Tokens Studio pattern are dimensionless numbers intended to represent `px` values.
