---
id: ADR-029
status: accepted — 2026-05-04
area: public API
---

# ADR-029 — `importTokensStudioRaw` for discoverable round-trip workflow

## Context

`importTokensStudio`, `importTokensStudioThemed`, and `importTokensStudioCombined` all call
`TokensStudio.shimSingleFile` internally but return only the resolved-token result.
`ShimResult` and `ParsedSets` — the two artifacts required by `exportTokensStudio` and
`toResolverDocument` — are discarded.

A caller wanting the full round-trip (read TS → modify → write back to TS format) had no
way to get those artifacts from the top-level `Api` module. The only path was
`Primitives.shimWithConfig`, which requires knowing that an internal sub-module exists and
calling the shim manually, then reproducing the tokenSetOrder-ordered parse step.

This was the source of the "shimTokensStudio is in the binary but undocumented" confusion
reported by downstream AI consumers: they found the function via DLL inspection, found no
top-level `Api` entry for it, and concluded there was a missing one-liner. The actual gap
was larger — there was no single top-level call that returns both the resolved tokens and the
shim state needed to export.

## Decision

Add `importTokensStudioRaw` to `Api`:

```fsharp
type TokensStudioRawImport = {
    Import     : TokensStudioImportResult       // resolved tokens + warnings
    ShimResult : ShimResult                      // for exportTokensStudio / toResolverDocument
    ParsedSets : Map<string, TokenFile>          // for exportTokensStudio / toResolverDocument
}

let importTokensStudioRaw
    (config   : ShimConfig)
    (jsonText : string)
    : Result<TokensStudioRawImport, ImportError list>
```

`importTokensStudio` is refactored to delegate:

```fsharp
let importTokensStudio config jsonText =
    importTokensStudioRaw config jsonText |> Result.map (fun r -> r.Import)
```

The round-trip workflow is now a three-line sequence without reaching into `Primitives`:

```fsharp
let raw = Api.importTokensStudioRaw ShimConfig.Default jsonText |> Result.get
// ... modify raw.Import.Tokens if needed ...
let css = CssEmitter.emitBlock raw.Import.Tokens
let (penpotJson, _) = Api.exportTokensStudio raw.ShimResult raw.ParsedSets
```

## Rationale

`Primitives` is a power-user escape hatch — it is not the correct place to document the
round-trip workflow. Callers reasoning about what API to use start at `Api`; if the right
function is not there, they either give up or reach into internals.

Returning a record rather than a tuple separates the three orthogonal concerns by name:
- `Import` — the consumer-facing result (same shape as before)
- `ShimResult` — the shim metadata (themes, metadata, annotated sets) needed for export
- `ParsedSets` — all successfully-parsed DTCG TokenFile instances needed for export

`importTokensStudio` keeps its existing signature and behavior unchanged. The delegation
introduces no observable difference; it is an internal cleanup.

`importTokensStudioThemed` and `importTokensStudioCombined` are not given raw variants
because their use cases (base + per-theme diff, combined multi-group) are export-oriented
themselves — callers needing the full round-trip path on themed imports use
`importTokensStudioRaw` for the initial read, then drive theme resolution separately.

## Consequences

- `TokensStudioRawImport` type added to `FnTools.DesignTokens.Api` module (in `DesignTokens.fs`).
- `importTokensStudioRaw` added to top-level `Api` and aliased in `Primitives`.
- `importTokensStudio` body reduced to one delegation line; behavior unchanged.
- No test changes — existing `importTokensStudio` tests cover the delegation path.
