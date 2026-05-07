---
id: 030
title: importTokensStudioCombinedWith — DTCG base sets alongside TS theme resolution
status: accepted
date: 2026-05-06
---

## Context

A consumer may have token sources in two formats:
1. A Tokens Studio export (the typical designer workflow)
2. One or more native DTCG 2025.10 files authored directly in spec format

`importTokensStudioCombined` hides the `ResolverDocument` assembly entirely. Including a native DTCG source in the resolution required bypassing it and assembling internal types manually — a library gap.

## Decision

Add `importTokensStudioCombinedWith` alongside `importTokensStudioCombined`. It accepts an extra `sets: (string * string) list` parameter (name + DTCG JSON pairs) and a required `DtcgSetRole` argument.

```fsharp
importTokensStudioCombinedWith
    (config     : ShimConfig)
    (themeNames : string list)
    (tsJson     : string)
    (sets       : (string * string) list)
    (_          : DtcgSetRole)   // caller writes: AsBasePrimitives
    : Result<TokensStudioImportResult, ImportError list>
```

Resolution order: DTCG base sets (list order) → TS base sets → TS active-theme sets. Later wins, so TS theme sets always override DTCG sets.

## `DtcgSetRole = | AsBasePrimitives`

The single-case DU follows the `IAcceptDataLoss` pattern (ADR-012 / Errors.fs). The caller must write `AsBasePrimitives` at the call site, which:
- Makes the priority and role visible in code review without consulting docs
- Forces the caller to reason "is this actually a base primitive set, or do I need theme-conditional DTCG sets?"
- Is extensible: `DtcgSetRole` can gain additional cases later without a breaking change to existing callers

## `DtcgSetSkipped` warning

A new `DtcgSetSkipped of setName: string` case is added to `TokensStudioImportWarning`, distinct from `SetSkipped` (which is for TS sets that fail after shimming). Keeping them separate makes warning logs unambiguous: the caller can tell immediately whether a TS set or a DTCG set was excluded.

## Constraints — not in this API

- Extra sets **cannot** be scoped to a specific theme. They are always included regardless of `themeNames`. Theme-conditional DTCG sets require manual `ResolverDocument` assembly.
- `importTokensStudioRaw` has no parallel variant — the shim result it returns is unrelated to DTCG extra sets.
- `importTokensStudioCombined` is unchanged.

## Consequences

- The common case (TS-only) keeps its existing function unchanged.
- The mixed case (TS + DTCG primitives) has a high-level entry point at the same abstraction level as the TS-only path.
- Parse failures in extra sets emit `DtcgSetSkipped` warnings and are skipped; other sets continue — consistent with the degraded-not-failed pattern throughout the import API.
