---
id: 039
title: Emitter contract `ResolvedTokens -> string` and target-named packages
status: accepted
date: 2026-05-11
---

## Context

The library has three working emitter packages today: `Css`, `Bindings` (F# code generation), and `TokensStudio`. Planned future targets include Swift, Kotlin, and XAML for native expansion (see `LauraExperiment/LOGOS/DesignSystemTool/targets.md`).

Two questions were left implicit until now:

1. **What is the contract a new emitter must satisfy?** The shape was consistent in practice — every emitter consumed the resolved token sequence and returned a string — but no ADR named it as a contract, so a future emitter author had to reverse-engineer it from the three existing examples.
2. **How are emitter packages named?** Two of the three followed an obvious pattern (named by what they output: `Css`, `TokensStudio`), but `Bindings` was named by its *role* ("F# code that binds to tokens"). With Swift/Kotlin/XAML on the horizon, the inconsistency stops being cosmetic — `Bindings` becomes ambiguous once "F# bindings" is one of several language outputs.

## Decision

### Contract

Every emitter package in `FnTools.DesignTokens.*` exposes at least one function with this shape:

```fsharp
val emit : ResolvedTokens -> string

// where ResolvedTokens is defined in Foundation as:
type ResolvedTokens = (string list * ResolvedToken) seq
```

The input is the universal handoff point from the Resolver stage: a flat sequence of `(token path segments, resolved value)` pairs. The output is a single string containing the target-native artifact. The caller writes the string to disk; no emitter performs file I/O (per ADR-003).

The alias was introduced in v0.13.0 (see addendum below); prior to that, every signature spelled the tuple-seq form explicitly. The contract type itself is unchanged — the alias is purely a documentation improvement.

Emitter packages may expose additional functions for variants (`emitThemed`, `emitChecked`, `emitWith`, etc.) but they all share the same input type. Resolution happens once upstream; multiple emitters consume the same resolved sequence in parallel.

### Naming convention

Emitter packages are named by the target they produce, not by the role they play.

| Package suffix | Target |
|---|---|
| `.Css` | CSS custom properties |
| `.FSharp` | F# source code (modules of `string` constants) |
| `.TokensStudio` | Tokens Studio / Penpot JSON |
| `.Swift` *(future)* | Swift constants / SwiftUI Color extensions |
| `.Kotlin` *(future)* | Kotlin constants / Compose ThemeData |
| `.Xaml` *(future)* | XAML resource dictionaries (Avalonia, UNO, MAUI) |

Two principles fall out of this:

- **Languages and tools are first-class.** `Css`, `FSharp`, `Swift`, `Kotlin` name an output language; `TokensStudio` names an output tool. Both are concrete things the consumer can see and verify.
- **Role-based names are rejected.** `Bindings` was role-based ("bind tokens to code"). Once two language targets exist, role-based names become ambiguous — every emitter is a "binding" in some sense. The previous name is preserved historically in ADR-010 and ADR-038, both of which now refer to the F# emitter under its new name.

### Breaking rename

The existing `FnTools.DesignTokens.Bindings` package is renamed to `FnTools.DesignTokens.FSharp` as a breaking change in the next minor version. The current sole consumer (`LauraApp`, an experiment by definition) confirmed the breaking change is acceptable. No deprecation alias is published; the old package id stops being published.

Module and type names follow:

- `BindingsEmitter` → `FSharpEmitter`
- `Bindings.emit` / `Bindings.emitChecked` → `FSharp.emit` / `FSharp.emitChecked`
- `BindingsIdentifierIssue` → `FSharpIdentifierIssue`

## Consequences

- **Adding a new emitter is mechanical.** A new package is a `.fsproj` referencing `FnTools.DesignTokens.Foundation` only (the resolved token types live in Foundation), an `emit` function with the contract signature, and an entry in the meta-package. No core code changes; no schema discussions; no API negotiations.
- **The meta-package surface is symmetric.** `FnTools.DesignTokens.Css.emit`, `FnTools.DesignTokens.FSharp.emit`, `FnTools.DesignTokens.TokensStudio.emit` — a consumer reading the meta-package sees a clear pattern.
- **The library is written in F# and F# remains the primary surface for advanced use.** The `FSharp` emitter is one target among several, not a privileged one. The renaming is exactly what makes that statement true.
- **Identifier-safety rules per language live in each language's emitter.** ADR-038's pattern (checked emission catching collisions and conflicts at the language boundary) generalises to every language. A future `.Swift` package owns its own identifier-safety rules; they do not creep into Foundation or Validation.
- **`TokensStudio` does not need renaming.** It targets a specific tool with a specific JSON shape — naming by tool is consistent with naming by language. A future `.Penpot` or `.Figma` would follow the same convention.
- **The contract type is stable across minor versions.** Adding a new field to `ResolvedToken` is a breaking change for every emitter; this is intentional. Emitters are the place where structural domain changes get noticed.

## Relationships

- Cites [ADR-003: I/O belongs to the caller](003-io-belongs-to-caller.md) — emitter functions return strings, never write files.
- Cites [ADR-012: Structural enforcement over documentation](012-structural-enforcement-over-documentation.md) — `ResolvedToken` is alias-free and type-non-optional by construction; emitters consume it without re-validating.
- Cites [ADR-013: Library scope ends at the DTCG interchange boundary](013-library-scope-dtcg-interchange-boundary.md) — emitters cross the boundary outward; nothing emitter-specific leaks back into the core.
- Renames the package referenced by [ADR-010](010-n-prefix-numeric-scales.md) and [ADR-038](038-bindings-identifier-safety.md). Both ADRs receive an addendum noting the rename.
- Caller-side examples and traps live in `LauraExperiment/LOGOS/library/api-patterns.md` (`emitChecked` vs `emit`; the `resolveAll` trap closed by ADR-036).

## Addendum — `ResolvedTokens` type alias (2026-05-11, v0.13.0)

The contract type defined above was originally spelled out everywhere as `(string list * ResolvedToken) seq`. In v0.13.0 the type is given a name in Foundation:

```fsharp
type ResolvedTokens = (string list * ResolvedToken) seq
```

This is a non-breaking change in F# semantics — type aliases are erased by the compiler, so existing source compiles unchanged and the binary surface is identical. The benefit is documentation: every public signature now reads as intent rather than implementation (`val emit : ResolvedTokens -> string` instead of `val emit : (string list * ResolvedToken) seq -> string`).

The alias lives in `Foundation/Domain.fs`, near `ResolvedToken`. No new types, no behavioural changes.

A `TokenPath = string list` alias was considered alongside and explicitly rejected for now — `string list` is idiomatic and ubiquitous; adding a second-level alias would proliferate without clear payoff. Reconsider if the path becomes a sore spot.
