# Migrating from 0.2 to 0.3

Released: 2026-05-04. Tracking ADR: [`029-import-tokens-studio-raw-roundtrip-api.md`](../LOGOS/decisions/029-import-tokens-studio-raw-roundtrip-api.md).

---

## TL;DR

Purely additive. One new public function: `Api.importTokensStudioRaw`. Bump
the version and rebuild — nothing existing changed.

```diff
- <PackageReference Include="FnTools.DesignTokens" Version="0.2.0" />
+ <PackageReference Include="FnTools.DesignTokens" Version="0.3.0" />
```

---

## What's new

### `Api.importTokensStudioRaw`

```fsharp
Api.importTokensStudioRaw (config: ShimConfig) (jsonText: string)
    : Result<TokensStudioRawImport, ImportError list>

type TokensStudioRawImport = {
    Import     : TokensStudioImportResult    // resolved tokens + warnings
    ShimResult : ShimResult                   // for exportTokensStudio / toResolverDocument
    ParsedSets : Map<string, TokenFile>       // for exportTokensStudio / toResolverDocument
}
```

Same resolved tokens as `importTokensStudio`, plus the raw shim artifacts
needed for export. Before 0.3.0, the round-trip workflow required dropping
down to `Primitives.shimWithConfig` to get `ShimResult` and `ParsedSets`
separately. With `importTokensStudioRaw`, the whole round-trip is three
lines from `Api`:

```fsharp
let raw = Api.importTokensStudioRaw ShimConfig.Default json |> Result.get
let css = CssEmitter.emit raw.Import.Tokens
let (penpotJson, _) = Api.exportTokensStudio raw.ShimResult raw.ParsedSets
```

## Migration

Nothing required. `Api.importTokensStudio` (the simpler variant that
discards the round-trip artifacts) continues to work identically. Use the
new function only when you need to round-trip back to Tokens Studio /
Penpot.
