---
id: ADR-025
status: accepted — 2026-05-04
area: TokensStudio shim
---

# ADR-025 — Combined theme resolution with `importTokensStudioCombined`

## Context

`importTokensStudioThemed` resolves each theme independently: each theme's set list is
built by collecting sets from the theme's own `selectedTokenSets`, then adding any sets
that are not listed in any *active* theme's `selectedTokenSets` as "base" sets.

This approach has a subtle cross-group bleed bug. When themes from different modifier
groups are relevant to a resolution — for example, a Color-mode group (Light/Dark) and a
Breakpoint group (Mobile/Desktop) — requesting only one theme (e.g. "Light") causes the
other group's non-selected sets (e.g. "Mobile" and "Tablet") to be treated as base sets
because they are not listed in "Light"'s `selectedTokenSets`.

**Concrete example using `mathBleedJson`:**

```
importTokensStudioThemed ["Small"] mathBleedJson
  allThemeSets = Small's sets only = {zoom/small, base}
  "zoom/large" not in allThemeSets → treated as base → included in math index
  math index = [zoom/small, zoom/large, base]
  {zoom} resolves to 2 (zoom/large last in tokenSetOrder)
  size = round(10 * 2) = 20  ← WRONG (should be 10 for Small)
```

The existing per-theme math index in `importTokensStudioThemed` (ADR-020 theme-bleed fix)
only prevents bleed *between* themes that are all requested in one call. It does not help
when themes from different groups exist in the file but only one group is being resolved.

## Decision

Add `importTokensStudioCombined`, a new API function that:

1. Computes `allThemeSets` from **ALL** themes in the file (not just the active ones).
2. Builds `combinedOwn` as the union of all requested themes' enabled/source sets.
3. Builds `combinedSets` = sets in tokenSetOrder that are either not in any theme OR
   are in a requested theme's own sets.
4. Calls `shimSingleFileWithMathIndex config combinedSets` — one shim pass with the
   combined set list as the math index.
5. Resolves a single flat `TokensStudioImportResult` (not a `ThemeAwareImportResult`).

```
importTokensStudioCombined ["Small"] mathBleedJson
  allThemeSets = ALL themes' sets = {zoom/small, zoom/large, base}
  combinedOwn = Small's sets = {zoom/small, base}
  combinedSets = [zoom/small, base]  (zoom/large excluded)
  math index = [zoom/small, base]
  {zoom} resolves to 1 (zoom/small only)
  size = round(10 * 1) = 10  ← correct
```

## When to use each function

| Function | Use case |
|---|---|
| `importTokensStudio` | Flat import; no theme structure needed |
| `importTokensStudioThemed` | CSS `:root` + theme override blocks (Light/Dark emitter) |
| `importTokensStudioCombined` | One snapshot combining themes from different groups (e.g. Light + Desktop + 100%) |

`importTokensStudioCombined` is appropriate when a single concrete context is required
(one brand, one color mode, one breakpoint, one zoom level) and the output is a flat
token snapshot rather than a set of overrides.

## Alternatives considered

### A — Fix `importTokensStudioThemed` directly

Change `allThemeSets` computation in `importTokensStudioThemed` to use all themes.
Rejected: would break callers relying on the current base-vs-theme split semantics,
and the change in return type (flat vs themed) is a separate concern.

### B — Virtual theme parameter

Add a "combined" flag to `importTokensStudioThemed` that switches to flat output.
Rejected: overloads the function signature; cleaner to keep two separate functions
with distinct return types.

### C — New function (chosen)

`importTokensStudioCombined` with a flat `TokensStudioImportResult` return type makes
the intent explicit at the call site, leaves `importTokensStudioThemed` unchanged, and
has a simple implementation that reuses `shimSingleFileWithMathIndex` and the existing
`Resolver.resolve` path.

## Consequences

- `importTokensStudioThemed` is unchanged; all existing callers unaffected.
- `importTokensStudioCombined` is added to `Api.*` and `Api.Primitives.*`.
- The cross-group bleed described above is eliminated for combined-context use cases.
- Math expressions that reference sets from non-requested themes correctly fail or
  evaluate only against the combined set list — no silent contamination.
- Return type is `TokensStudioImportResult` (same as `importTokensStudio`); callers
  wanting per-theme override diffs should still use `importTokensStudioThemed`.
