# Getting Started with FnTools.DesignTokens

`FnTools.DesignTokens` is a .NET library for working with [DTCG 2025.10](https://www.designtokens.org/) design-token files in F#. It parses, validates, resolves, and emits — `.tokens.json` in, CSS custom properties or typed F# bindings out, with full round-trip support for Tokens Studio and Penpot along the way.

This guide takes you from "I have a `.tokens.json` file" to "I have a CSS stylesheet and an F# `Tokens` module" in five minutes.

---

## Install

```xml
<PackageReference Include="FnTools.DesignTokens" Version="0.12.0" />
```

That's the meta-package; it pulls in seven layered libraries transitively (`Foundation`, `Format`, `Validation`, `Resolver`, `Css`, `FSharp`, `TokensStudio`). Reference an individual layer if you want a smaller dependency surface — see [`api-reference.md`](./api-reference.md).

NuGet feed: `https://forgejo.ivanthegeek.com/api/packages/FnTools/nuget/index.json`

```xml
<configuration>
  <packageSources>
    <add key="forgejo" value="https://forgejo.ivanthegeek.com/api/packages/FnTools/nuget/index.json" />
  </packageSources>
</configuration>
```

---

## A minimal token file

```json
{
  "$schema": "https://design-tokens.org/schemas/2025.10/format.schema.json",
  "color": {
    "accent": {
      "$type": "color",
      "$value": {
        "colorSpace": "srgb",
        "components": [0.10, 0.43, 0.10],
        "hex": "#1a6e1a"
      }
    },
    "white": {
      "$type": "color",
      "$value": { "colorSpace": "srgb", "components": [1, 1, 1], "hex": "#ffffff" }
    }
  },
  "spacing": {
    "md": { "$type": "dimension", "$value": { "value": 1, "unit": "rem" } },
    "lg": { "$type": "dimension", "$value": { "value": 1.5, "unit": "rem" } }
  }
}
```

Save as `tokens.json`.

---

## Five-minute walkthrough

### 1. Import (parse + validate + resolve)

```fsharp
open FnTools.DesignTokens

let json = System.IO.File.ReadAllText "tokens.json"

match Api.import json with
| Error errors ->
    errors
    |> List.iter (fun e -> eprintfn "%s" (Api.formatImportError e))
| Ok tokens ->
    for (path, token) in tokens do
        printfn "%s = %A" (String.concat "." path) token.Value
```

`Api.import` does parse → validate → resolve in one call. Returns a flat sequence of `(path segments, resolved token)` pairs. Validation includes structural invariants (hex/components consistency, alpha range, cubic-bezier coordinates, alias-cycle absence, cross-type alias mismatches — see ADRs 002, 033).

### 2. Emit CSS

```fsharp
open FnTools.DesignTokens.Css

match Api.import json with
| Error _ -> ()
| Ok tokens ->
    let css = emit tokens
    System.IO.File.WriteAllText("tokens.css", css)
```

Output:

```css
:root {
  --color-accent: #1a6e1a;
  --color-white: #ffffff;
  --spacing-md: 1rem;
  --spacing-lg: 1.5rem;
}
```

The CSS emitter is `[<AutoOpen>]` after `open FnTools.DesignTokens.Css` — `emit`, `emitBlock`, `emitWith`, `emitThemed`, etc. are all top-level functions. See [`api-reference.md`](./api-reference.md#css-emission) for the full menu.

### 3. Generate a typed F# module

```fsharp
open FnTools.DesignTokens

match Api.import json with
| Error _ -> ()
| Ok tokens ->
    let source = FnTools.DesignTokens.FSharp.emit "Tokens" tokens
    System.IO.File.WriteAllText("Tokens.fs", source)
```

Output (`Tokens.fs`):

```fsharp
module Tokens =
    module Color =
        let Accent = "var(--color-accent)"
        let White  = "var(--color-white)"
    module Spacing =
        let Md = "var(--spacing-md)"
        let Lg = "var(--spacing-lg)"
```

Drop this into your Fun.Blazor project. Reference `Tokens.Color.Accent` in your component code; the F# compiler now checks that every token reference exists. No runtime dependency on this library — the generated file is just `string` constants.

### 4. Round-trip back to JSON

```fsharp
match Api.import json with
| Error _ -> ()
| Ok tokens ->
    let regenerated = Api.export tokens
    System.IO.File.WriteAllText("tokens-roundtrip.json", regenerated)
```

`Api.export` writes valid DTCG 2025.10 JSON. Round-trip is lossless for everything the parser accepts, including `$extensions` (ADR-011).

---

## Going further

### Multiple sets with axes — `.resolver.json`

When you have orthogonal axes (Light/Dark, Desktop/Tablet/Mobile, Brand A/B), the DTCG resolver merges sets in priority order:

```json
{
  "$schema": "https://design-tokens.org/schemas/2025.10/resolver.schema.json",
  "sets": {
    "core":         { "path": "./core.tokens.json" },
    "light":        { "path": "./light.tokens.json" },
    "dark":         { "path": "./dark.tokens.json" }
  },
  "modifiers": {
    "theme": {
      "default": "light",
      "contexts": [
        { "name": "light", "sets": [{ "set": "light" }] },
        { "name": "dark",  "sets": [{ "set": "dark"  }] }
      ]
    }
  },
  "resolutionOrder": [
    { "set": "core" },
    { "modifier": "theme" }
  ]
}
```

```fsharp
let loadFile path = Ok (System.IO.File.ReadAllText path)
let context = Map.ofList [ "theme", "dark" ]

match Api.importWithResolver loadFile context (System.IO.File.ReadAllText "design.resolver.json") with
| Error _ -> ()
| Ok tokens ->
    let css = emit tokens
    System.IO.File.WriteAllText("dark.css", css)
```

`loadFile` is caller-supplied (ADR-003) — works the same in CLI tools, Figma plugins, WASM, or unit tests.

### Per-theme CSS in one pass

```fsharp
match Api.importTokensStudioThemed ShimConfig.Default ["Light"; "Dark"] tsJson with
| Error _ -> ()
| Ok result ->
    let themes = result.Themes |> List.map (fun t -> t.ThemeName, t.Tokens :> _ seq)
    let css =
        emitThemed
            (fun n -> sprintf "[data-theme=\"%s\"]" n)
            result.BaseTokens
            themes
    System.IO.File.WriteAllText("tokens.css", css)
```

Emits `:root { /* base */ }` plus one `[data-theme="dark"] { /* diffs only */ }` block per theme. Tokens identical to base are not repeated.

### Math expressions tied to axes (Tokens Studio workflow)

If your tokens use math expressions like `round({base} * pow({multiplier}, 3))` and your axes override `multiplier`, the `Resolver.resolveAll` path reads the stale `$value` snapshot. To re-evaluate per-axis, use:

```fsharp
match Api.importWithResolverEvaluatingExtensions loadFile context resolverJson with
| Error _ -> ()
| Ok result ->
    result.Warnings
    |> List.iter (fun w -> eprintfn "%s" (ExtensionEvaluationWarning.format w))
    let css = emit result.Tokens
    System.IO.File.WriteAllText("tokens.css", css)
```

This is opt-in (ADR-034) — `Resolver.resolveAll` stays strict-DTCG-compliant.

### Strict DTCG 2025.10 compliance check

The library accepts `DimensionUnit.Em` as a deliberate extension (ADR-028) for Tokens Studio / Penpot round-trip fidelity. To guarantee a file contains no library extensions before exporting to a strict downstream consumer:

```fsharp
let file = Format.parse json |> Result.defaultWith (fun _ -> failwith "parse")
match Api.validateStrictDtcg file with
| Ok () -> proceed file
| Error errors ->
    errors
    |> List.iter (fun e -> eprintfn "%s" (ValidationError.format e))
```

### Migrating from existing CSS

If you're starting from a CSS file with custom properties:

```fsharp
open FnTools.DesignTokens.Css

let css = System.IO.File.ReadAllText "existing.css"

// 1. Extract :root custom properties as tokens
let ingestResult = CssIngest.ingest "" css
System.IO.File.WriteAllText("tokens.json", ingestResult.Json)

// 2. Inventory hardcoded values in rules (not in :root)
let audit = CssAudit.audit css
for entry in audit.Entries do
    printfn "[%d] %A %s" entry.Count entry.ValueType entry.RawValue
```

`CssIngest` handles `:root { --x: ... }` declarations; `CssAudit` surfaces hardcoded values in other rules so you can decide which to tokenize. Together they're the "bootstrap from a real site" workflow.

---

## Common pitfalls

- **Validation is opt-in for strict spec compliance.** `Api.import` runs the structural validator. To also check that no library extensions (`Em` dimensions today) are present, add `Api.validateStrictDtcg` (ADR-028 addendum). Plain `Validation.validate` accepts `Em`.
- **`tsMathExpression` is metadata at resolve time by default.** `Resolver.resolveAll` reads `$value` directly. Use `Api.importWithResolverEvaluatingExtensions` (ADR-034) to re-evaluate expressions against the active axis combination.
- **Aliasing a `dimension` token to a `number` token** is flagged as `TypeMismatch` (ADR-033) — if intentional, run the `Primitives.*` pipeline to skip validation; the emitter coerces the bare number to `Npx` for valid CSS regardless.
- **`em` is excluded from `CssIngest` output** (ADR-018). Relative units like `em`/`%`/`vw` are element-relative; they belong in component CSS, not portable design tokens. The audit surfaces them under `CssNative` so you can route them to the component layer.
- **OKLCH colors don't downsample to hex automatically.** Set the `Hex` field on the token before calling `serializePenpot` or `serializeAs SecondEditorsDraft` — gamut-mapping is the caller's responsibility (ADR-013, ADR-016).

---

## What to read next

- [`api-reference.md`](./api-reference.md) — full function signatures, every public API
- [`spec-context.md`](./spec-context.md) — DTCG 2025.10 spec references, version history, what the library implements
- [`../LOGOS/decisions/README.md`](../LOGOS/decisions/README.md) — index of all 39 ADRs by topic
- [`../samples/ivanthegeek.tokens.json`](../samples/ivanthegeek.tokens.json) — real-world sample bootstrapped from a live site, useful as a copy-and-modify starting point
- [Migration guides](./api-reference.md) — see the top of the API reference for the full list

If you hit something unclear or unexpected, the ADR index is the fastest path to the design rationale — most behavior decisions have one.
