---
id: ADR-027
status: accepted — 2026-05-04; upgraded 2026-05-10
area: CSS emission
---

# ADR-027 — Calc-preserving CSS emission for the design-tool workbench

## Context

The design-tool workbench needs a CSS `--base` slider that recomputes the entire
token scale live in the browser without a rebuild. The production emitter
(`emitThemed`) bakes all math at emit time and outputs concrete values like
`--spacing-sm: 16px;`. That is correct for shipping but useless for a slider.

The required output is:

```css
:root {
  --base: 16;
  --multiplier: 1.25;
  --spacing-sm: calc(var(--base) * 1px);
  --spacing-md: calc(var(--base) * var(--multiplier) * 1px);
}
```

## Problem

`ResolvedToken` holds the resolved float value. By the time a token reaches
`CssEmitter`, the symbolic expression (`{base} * {multiplier}`) no longer exists
— the resolver has already evaluated it to `20.0`.

Two approaches to recover the expression:

**(a) Store source expressions in the pipeline** — when the Tokens Studio shim
evaluates a math expression, it also stores the original string in a new
`AnnotatedToken` wrapper. `emitCalcPreserving` consumes `AnnotatedToken` instead
of `ResolvedToken`.

**(b) Mathematical reverse-engineering at emit time** — given that `base` and
`multiplier` are present as explicit `number` tokens in the resolved set, any
dimension token whose value satisfies `d = base × multᴺ` for an integer N can
have its `calc()` expression reconstructed without any pipeline changes.

## Decision

Implement option (b) as `emitCalcPreserving` in `CssEmitter`.

### Why (b) for now

- The Laura token set, and any type-scale–based design system, defines all
  derived dimension tokens as `base × multᴺ`. The pattern is exact, not
  approximate.
- No pipeline changes required — the function is self-contained in `CssEmitter`.
- Dimension tokens that do *not* fit the pattern (fixed radii, border widths,
  breakpoints) fall back to their concrete resolved values. This is correct
  behaviour: those tokens are not scale-dependent and should not be affected
  by a `--base` slider.
- Negative N is supported: sub-scale tokens like `spacing.xs = base / mult`
  emit as `calc(var(--base) / var(--multiplier) * 1px)`.

### Algorithm

```fsharp
n = round(log(dimVal / baseVal) / log(multVal))
verify: |baseVal × multValⁿ − dimVal| / dimVal < 1e-6
```

If verified: emit `calc(var(--base) [× var(--multiplier)]ⁿ * 1px)` (n
multiplications for positive n, n divisions for negative n, plain `1px` for
n = 0).

If not verified: emit the concrete resolved value as normal.

### CSS `round()` — omitted

The design-tool workbench is a preview surface. Sub-pixel rendering is
acceptable and avoids a Values Level 5 dependency. `round()` can be added later
if per-pixel snapping becomes important.

## Upgrade: option (a) implemented via `tsMathExpression` (2026-05-10)

The mathematical-only approach (b) fails when Tokens Studio's `round()` function
changes the resolved value — e.g. `round({base} * pow({multiplier}, 3))` evaluates
to `31` not `31.25`. The 1e-6 relative tolerance check correctly rejects this, but
the `calc()` expression is then lost.

ADR-031 (math-expression round-trip) already stores the original TS expression in
`$extensions["com.fntools.designtokens"]["tsMathExpression"]`. `emitCalcPreserving`
now reads that annotation **before** attempting mathematical reverse-engineering:

1. If `tsMathExpression` is present in `token.Metadata.Extensions`:
   - Strip any outer `round(…)` wrapper (CSS emitter omits it — preview surface, no Values Level 5 dep).
   - Parse integer exponent N from `{base} * pow({multiplier}, N)`.
   - If parsed: emit `calc()` with N multiplications/divisions.
2. If no annotation (or unrecognised expression shape): fall back to `tryInferCalcN`.

This resolves the `round()` fallback without altering the stored annotation
(preserving the TS round-trip defined in ADR-031). Tokens that genuinely do not fit
the scale pattern still emit concrete values.

No new public API — `emitCalcPreserving` signature is unchanged.

## Consequences

- `emitCalcPreserving` is in `CssEmitter` and produces a single `:root` block
  (no theme overrides — the workbench manages axis switching separately).
- Non-scale dimension tokens always emit their concrete values — correct.
- `round()`-wrapped tokens now emit `calc()` correctly; the annotation is read
  but not mutated.
- If `--base` or `--multiplier` tokens are absent from the token list, all
  dimension tokens emit as concrete values (safe fallback).
- Arbitrary TS expressions beyond `pow({mult}, N)` still fall back to mathematical
  inference. A future extension could handle `min()`, `max()`, etc. by parsing
  additional patterns in `tryParsePowN`.
