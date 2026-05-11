# FnTools.DesignTokens

F# library implementing the **DTCG 2025.10** specification — the W3C Community Group standard for sharing design tokens between tools. Parses, resolves, and translates design-token files into any target format your codebase needs: CSS custom properties, F# source code, Tokens Studio JSON, and more.

> **New here?** Read [`docs/concept.md`](docs/concept.md) first — one page on what this library is, where it sits in the design-token toolchain, and what it deliberately does *not* do.

---

## Where this library sits in the pipeline

The DTCG community describes the design-token toolchain as four stages:

```
DTCG source → Resolver → Translator → Target
```

**This library fills the Resolver and Translator stages.** It does not author tokens (a design tool does that) and it does not run a UI (your application does that).

- **Resolver stage** — accepts a DTCG token file, Tokens Studio JSON, or a `.resolver.json`; merges multi-set inheritance chains; evaluates math expressions; resolves aliases; produces a flat sequence of `(path, resolved-value)` pairs.
- **Translator stage** — turns that flat sequence into a target-native string: CSS custom properties, F# constants, Penpot-compatible JSON, and (planned) Swift, Kotlin, XAML for native platforms.

The boundary between these two stages is a single type:

```fsharp
(string list * ResolvedToken) seq
```

Every emitter consumes this. Add a new platform target by adding a new emitter package — the core does not change. See [ADR-039: Emitter contract and naming](LOGOS/decisions/039-emitter-contract-and-naming.md).

Strings in, strings out. The library never touches the filesystem; the caller owns I/O (see [ADR-003](LOGOS/decisions/003-io-belongs-to-caller.md)).

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

**Audit CSS** — scan stylesheets for hardcoded values not covered by existing tokens; flag duplicates of values already covered by tokens.

---

## Package layout

Eight packages, all in one meta-package:

| Package | Stage | What it does |
|---|---|---|
| `FnTools.DesignTokens.Foundation` | core | Domain types. No external dependencies. |
| `FnTools.DesignTokens.Format` | resolver | JSON parsing and serialization. |
| `FnTools.DesignTokens.Validation` | resolver | Strict-by-default constraint checks; permissive mode opt-in. |
| `FnTools.DesignTokens.Resolver` | resolver | Multi-set merging, alias resolution, math expression evaluation. |
| `FnTools.DesignTokens.Css` | translator | CSS custom-property emitter, calc-preserving emission, themed emission, CSS ingest + audit. |
| `FnTools.DesignTokens.FSharp` | translator | F# source emitter; produces typed token modules. *(Renamed from `Bindings` in v0.12.0.)* |
| `FnTools.DesignTokens.TokensStudio` | translator | Tokens Studio import/export with alias-preserving round-trip. |
| `FnTools.DesignTokens` | meta | Re-exports all of the above; one reference gets everything. |

Each translator package depends only on `Foundation`. A consumer that only emits Swift will eventually reference only `Foundation` + `Swift`. See [ADR-001](LOGOS/decisions/001-layer-split-architecture.md).

---

## Install

Hosted on a self-managed Forgejo feed, not NuGet.org. Add the source first:

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
dotnet add package FnTools.DesignTokens --version 0.12.0
```

**PackageReference**
```xml
<PackageReference Include="FnTools.DesignTokens" Version="0.12.0" />
```

**F# script (`#r`)**
```fsharp
#r "nuget: FnTools.DesignTokens, 0.12.0"
```

One reference gets everything. The sub-packages (`Foundation`, `Format`, `Validation`, `Resolver`, `Css`, `FSharp`, `TokensStudio`) are published separately if you need only specific layers.

---

## Quick start

```fsharp
open FnTools.DesignTokens

// Plain DTCG JSON → resolved tokens
let json    = System.IO.File.ReadAllText "tokens.json"
let tokens  = Api.import json |> Result.get   // (string list * ResolvedToken) seq

// Resolved tokens → CSS
let css = FnTools.DesignTokens.Css.CssEmitter.emit tokens
System.IO.File.WriteAllText("tokens.css", css)

// Resolved tokens → F# bindings module
let source = FnTools.DesignTokens.FSharp.emit "Tokens" tokens
System.IO.File.WriteAllText("Tokens.fs", source)
```

```fsharp
// Tokens Studio JSON → themed CSS (math expressions evaluated, aliases preserved)
let tsJson = System.IO.File.ReadAllText "tokens.json"
let result = Api.importTokensStudioThemed ShimConfig.Default ["Light"; "Dark"] tsJson |> Result.get
let css =
    FnTools.DesignTokens.Css.CssEmitter.emitThemedWith
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

The five-minute walkthrough lives in [`docs/getting-started.md`](docs/getting-started.md).

---

## Docs

- [**Concept**](docs/concept.md) — pipeline, library role, what is and is not in scope. Read first.
- [**Getting started**](docs/getting-started.md) — five-minute walkthrough from `.tokens.json` to CSS + F# bindings.
- [**API reference**](docs/api-reference.md) — every public function with signatures, types, and usage patterns.
- [**Architecture decisions**](LOGOS/decisions/) — 39 ADRs, indexed by topic in [`LOGOS/decisions/README.md`](LOGOS/decisions/README.md).
- [**Migration guides**](docs/) — `migration-X-to-Y.md` for every minor version bump.
- [**AI/LLM index**](llms.txt) — structured index for AI-assisted development.
- [**Building & contributing**](CONTRIBUTING.md) — prerequisites, build, test, publish.

---

## License

[AGPL-3.0](LICENSE)
