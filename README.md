# FnTools.DesignTokens

F# library for working with [DTCG 2025.10](https://tr.designtokens.org/format/) design tokens — the W3C Community Group standard for sharing design decisions between tools.

If your design system lives in Penpot or Tokens Studio, this library is how you get those tokens into your F# codebase as typed values, resolved CSS custom properties, or typed binding constants — and back again.

---

## Why this library

Design tokens are the single source of truth for a design system: colours, spacing, typography, shadows, transitions. The DTCG spec defines a portable JSON format for them. The problem is the gap between that format and actual use:

- Tokens Studio (VS Code plugin) and Penpot both use a variant format — different type names, HSL expressions, math formulas, multi-set structures — none of which parses cleanly as DTCG.
- CSS output requires alias resolution across multiple token sets, unit normalisation, and multi-theme override logic.
- Round-trips (read → modify → write back to Penpot) need to preserve the original structure, aliases, and expressions, not flatten everything to concrete values.

This library handles all of that.

---

## What it does

**Parse and validate** DTCG 2025.10 token files. All 13 token types. Alias chains, composite values, group inheritance. Errors accumulate rather than short-circuit — you get all problems at once.

**Resolve** multi-set token files using a resolver document — merge sets in resolution order, apply modifier contexts (themes, breakpoints, brands), flatten to a concrete token list.

**Shim Tokens Studio format** — convert Tokens Studio multi-set JSON to DTCG-compatible form:
- Type renames (`fontFamilies` → `fontFamily`, `spacing` → `dimension`, etc.)
- Math expression evaluation (`round({base} * pow({multiplier}, 2))` → a concrete float)
- HSL expression evaluation (`hsla({hue.blue},{saturation},{lightness.600},1)` → `#3d7ab5`)
- Typography composite field normalisation
- `$themes` / `$metadata` extraction for theme-aware workflows

**Emit CSS** from resolved tokens — `:root {}` blocks, per-theme override blocks (`[data-theme="dark"] {}`), responsive `@media` blocks, `calc()` expressions that preserve mathematical relationships, per-path unit policy (e.g. `font-size.*` tokens in `rem`).

**Emit typed F# bindings** — a `Tokens` module of `string` constants with `var(--token-name)` values, usable directly in [Fun.Css](https://github.com/slaveOftime/Fun.Css).

**Export back to Tokens Studio** — preserve aliases, TS type names, HSL expressions, and combined fontWeight strings. The round-trip is lossless for everything the format can represent.

**Ingest CSS** — extract custom properties from existing CSS files, infer token types, produce DTCG token files. The migration path from a hand-authored CSS design system.

**Audit CSS** — scan stylesheets for hardcoded values not covered by existing tokens.

---

## Install

The package is hosted on a self-managed Forgejo feed, not NuGet.org. Add the source first:

**`nuget.config`** (place alongside your `.sln` / `.fsproj` / `.fsx`):

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="FnTools" value="https://forgejo.ivanthegeek.com/api/packages/FnTools/nuget/index.json" />
  </packageSources>
</configuration>
```

**.NET CLI**
```bash
dotnet add package FnTools.DesignTokens --version 0.3.0
```

**PackageReference**
```xml
<PackageReference Include="FnTools.DesignTokens" Version="0.3.0" />
```

**F# script (`#r`)**
```fsharp
#r "nuget: FnTools.DesignTokens, 0.3.0"
```

One reference gets everything. The sub-packages (`Foundation`, `Format`, `Validation`, `Resolver`, `Css`, `Bindings`, `TokensStudio`) are published separately if you need only specific layers.

---

## Quick start

```fsharp
open FnTools.DesignTokens

// Tokens Studio JSON → themed CSS
let tsJson = System.IO.File.ReadAllText "tokens.json"
let result = Api.importTokensStudioThemed ShimConfig.Default ["Light"; "Dark"] tsJson |> Result.get

let css =
    CssEmitter.emitThemedWith
        (fun path unit -> match path with "font-size" :: _ -> Rem | _ -> unit)
        (fun theme -> $"[data-theme=\"{theme}\"]")
        result.BaseTokens
        result.Themes
```

```fsharp
// Round-trip: read Tokens Studio → modify → export back to Penpot
let raw = Api.importTokensStudioRaw ShimConfig.Default tsJson |> Result.get
// ... inspect or modify raw.Import.Tokens ...
let (penpotJson, warnings) = Api.exportTokensStudio raw.ShimResult raw.ParsedSets
```

```fsharp
// Plain DTCG JSON → F# binding constants
let tokens = Api.import (System.IO.File.ReadAllText "tokens.dtcg.json") |> Result.get
let bindings = BindingsEmitter.emit tokens
// generates: module Tokens = let ColorTextMain = "var(--color-text-main)"
```

---

## Docs

- [API Reference](docs/api-reference.md) — all public functions with signatures, types, and usage patterns
- [Building & contributing](CONTRIBUTING.md) — prerequisites, build, test, publish
- [AI/LLM index](llms.txt) — structured index for AI-assisted development

Architecture decisions are recorded in [LOGOS/decisions/](LOGOS/decisions/).

---

## License

[AGPL-3.0](LICENSE)
