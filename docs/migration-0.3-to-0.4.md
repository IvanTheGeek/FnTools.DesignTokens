# Migrating from 0.3 to 0.4

Released: 2026-05-06. Tracking ADR: [`030-import-tokens-studio-combined-with-dtcg-base.md`](../LOGOS/decisions/030-import-tokens-studio-combined-with-dtcg-base.md).

---

## TL;DR

Purely additive. One new public function: `Api.importTokensStudioCombinedWith`,
plus user-facing repo docs (README, CONTRIBUTING, API reference, llms.txt).
Bump the version and rebuild — nothing existing changed.

```diff
- <PackageReference Include="FnTools.DesignTokens" Version="0.3.0" />
+ <PackageReference Include="FnTools.DesignTokens" Version="0.4.0" />
```

---

## What's new

### `Api.importTokensStudioCombinedWith`

```fsharp
Api.importTokensStudioCombinedWith
    (config     : ShimConfig)
    (themeNames : string list)
    (tsJson     : string)
    (sets       : (string * string) list)
    (_          : DtcgSetRole)             // pass AsBasePrimitives
    : Result<TokensStudioImportResult, ImportError list>

type DtcgSetRole = | AsBasePrimitives
```

Same as `importTokensStudioCombined` (added in 0.2.0), plus extra DTCG sets
injected as the lowest-priority base layer. The `DtcgSetRole` argument is
required at every call site — a single-case DU rather than a boolean —
to make the role of those extra sets unambiguous: they are always
included regardless of `themeNames`, and TS theme sets always override
them. Resolution order: DTCG base sets → TS base sets → TS active-theme
sets.

Use this when you have stable primitive tokens authored in DTCG and want
to layer Tokens-Studio-driven theming on top without converting the
primitives to TS format first.

## Repo documentation added in this release

- `README.md`, `CONTRIBUTING.md` — user-facing repo entry points.
- `docs/api-reference.md` — first hand-written API reference.
- `llms.txt` — LLM-friendly project summary.

These don't affect library behavior; they help discovery and adoption.

## Migration

Nothing required. Existing calls to `importTokensStudioCombined` and
other `Api.*` functions keep working identically.
