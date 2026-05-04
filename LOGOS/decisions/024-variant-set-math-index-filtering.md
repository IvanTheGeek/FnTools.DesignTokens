---
id: ADR-024
status: accepted — 2026-05-04
area: TokensStudio shim
---

# ADR-024 — Variant-set math index filtering with `MathEvalFailedVariantAlias`

## Context

`shimSingleFile` builds a single flat alias-resolution index from ALL token sets
(last-set-wins). When a file contains Tokens Studio `$themes` data, some sets are
"variant" — enabled in exactly one theme within a modifier group (e.g. `Text zoom/200%`
is enabled only in the 200% theme). Including these in the global flat index caused
theme-bleed: `zoom = 2` from `Text zoom/200%` (last in `tokenSetOrder`) contaminated
math expressions in all sets, making `round({base} * pow({multiplier}, N))` evaluate to
the same value for every theme.

ADR-020 (dimension alias resolution at shim time) and the prior math-evaluator work
(2026-05-04) fixed the bleed for `importTokensStudioThemed` by passing a per-theme set
list. But `shimSingleFile` still used the global index, and calling code had no indication
that the result was contaminated — no warning was emitted.

## Decision

**Auto-detect theme-variant sets** in `shimSingleFile` and use a separate filtered index
for math expression evaluation while keeping the global index for HSL and typography alias
resolution.

### What counts as a variant set

A set is variant if it is `"enabled"` in some themes of a modifier group (non-empty
`group` field) but not all themes in that group. Detection is per-group (matching
`toResolverDocument`'s `varyingPerGroup` logic) to avoid false positives from pseudo-themes
that appear as the only member of their group (e.g. an "Always-on" group with one theme).

### Two-index architecture

`shimCore` now builds two indexes:

| Index | Contents | Used for |
|---|---|---|
| `globalIndex` | All sets (unchanged) | HSL alias resolution, typography fontSizes |
| `mathIndex` | Non-variant sets only (auto-detected) | `MathEval.tryEval` only |

HSL palette colors that reference brand hue aliases (e.g. `{hue.primary}` from
`Brand/Core`) still resolve via `globalIndex`, preserving the existing behavior.
Only the math evaluator is restricted to the filtered index.

### New warning: `MathEvalFailedVariantAlias`

When math evaluation fails AND the restriction is active (`isVariantFiltered = true`),
`shimCore` emits `MathEvalFailedVariantAlias (path, expr)` instead of `MathEvalFailed`.
`formatWarning` appends the hint:

```
EVAL  path — could not evaluate: expr (references a theme-variant alias;
             use Api.importTokensStudioThemed for correct per-theme resolution)
```

Files without `$themes` (no variant sets detected) still emit `MathEvalFailed`.
`shimSingleFileWithMathIndex` (explicit filter provided by caller) also keeps existing
behavior and always emits `MathEvalFailed`.

### Behaviour change for `importTokensStudio` on Laura's file

With the filtered math index, `Foundations/Base` scale tokens that reference `{zoom}`
(a Text-zoom-group variant alias) fail evaluation and are dropped from the shimmed
output. Their dependents — spacing, radius, and sizing tokens that reference `{scale.*}`
— become `TokenUnresolved` in the flat import.

| Metric | Before (global index) | After (filtered math index) |
|---|---|---|
| Resolved tokens | 250 (wrong scale values) | 204 (correct, or informatively absent) |
| Sets skipped | 0 | 0 |
| Tokens unresolved | 0 | 36 (spacing/radius/sizing → {scale.*}) |
| Color tokens | 143 (correct) | 143 (unchanged — HSL uses globalIndex) |
| `MathEvalFailedVariantAlias` warnings | 0 | 10 (Foundations/Base scale math) |

The 250→204 delta represents tokens that previously had wrong values (zoom-contaminated).
Callers who need correct scale values should use `Api.importTokensStudioThemed`.

## Alternatives considered

### A — Emit `MathEvalFailedVariantAlias` on any math failure when variant sets exist
Keep the global index unchanged; change only the warning type based on whether variant
sets were detected. Simpler, but the hint fires even when the math failure is unrelated
to variant aliases.

### B — Single index (no split), filter variant sets from everything
Excluded: breaks HSL resolution because palette colors reference brand hue aliases which
are in variant sets. Color token count drops from 143 to ~1.

### C — Two indexes (chosen)
Filters only the math evaluator; HSL and typography resolution use the global index.
Preserves color token behavior while correctly flagging variant-dependent math.

## Consequences

- `shimSingleFile` on a multi-theme file: math expressions that reference variant aliases
  fail with a clear hint; all other tokens resolve as before.
- `importTokensStudio` (flat): returns fewer tokens for files where foundation tokens
  depend on theme-variant aliases, plus `TokenUnresolved` warnings for their dependents.
  This is the correct signal: callers should migrate to `importTokensStudioThemed`.
- `shimSingleFileWithMathIndex` (explicit caller-provided filter): unchanged.
- `importTokensStudioThemed`: unchanged — already uses per-theme filtered index.
- `ShimWarning` gains `MathEvalFailedVariantAlias of path * expr`. Code matching on
  `ShimWarning` without a wildcard must add a case (F# exhaustiveness check).
