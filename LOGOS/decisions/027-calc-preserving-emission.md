---
id: ADR-027
status: accepted — 2026-05-04
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

## Upgrade path to option (a)

When the design system app needs to support arbitrary expressions (not just
power-of-multiplier), add `AnnotatedToken`:

```fsharp
type AnnotatedToken = {
    Resolved   : ResolvedToken
    SourceExpr : string option   // original TS math expression, e.g. "{base} * {multiplier}"
}
```

The TS shim stores the raw expression string before evaluation. A new
`Api.importTokensStudioAnnotated` returns `AnnotatedToken list`. A new
`emitCalcPreservingAnnotated` consumes this and substitutes `{x}` → `var(--x)`
in the expression string.

This change is non-breaking: `emitCalcPreserving` (mathematical) and
`emitCalcPreservingAnnotated` (expression-based) can coexist.

## Consequences

- `emitCalcPreserving` is in `CssEmitter` and produces a single `:root` block
  (no theme overrides — the workbench manages axis switching separately).
- Non-scale dimension tokens always emit their concrete values — correct.
- If `--base` or `--multiplier` tokens are absent from the token list, all
  dimension tokens emit as concrete values (safe fallback).
- The mathematical approach does not survive changes to the scale structure
  (e.g., if a future density axis uses a different formula). That signals the
  right time to implement option (a).
