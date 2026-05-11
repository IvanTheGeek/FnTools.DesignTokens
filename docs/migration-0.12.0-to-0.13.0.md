# Migration: 0.12.0 → 0.13.0

**No source breaks. No binary breaks. No behaviour changes.**

This release introduces a type alias for the universal Resolver-to-Translator handoff:

```fsharp
type ResolvedTokens = (string list * ResolvedToken) seq
```

Defined in `FnTools.DesignTokens.Foundation` alongside `ResolvedToken`. Every public signature that previously spelled out the tuple-seq form now uses the alias. Rationale: see [ADR-039 v0.13.0 addendum](../LOGOS/decisions/039-emitter-contract-and-naming.md).

---

## What changed

### Type alias added

```fsharp
namespace FnTools.DesignTokens

/// The universal handoff between the Resolver stage and any Translator.
/// Each element is (token path segments, resolved value).
type ResolvedTokens = (string list * ResolvedToken) seq
```

### Public signatures updated

Every function previously spelled `(string list * ResolvedToken) seq` now reads `ResolvedTokens`. Examples:

```fsharp
// Before
val Api.import : string -> Result<(string list * ResolvedToken) seq, ImportError list>
val Api.export : (string list * ResolvedToken) seq -> string
val Css.CssEmitter.emit : (string list * ResolvedToken) seq -> string
val FSharp.emit : string -> (string list * ResolvedToken) seq -> string

// After
val Api.import : string -> Result<ResolvedTokens, ImportError list>
val Api.export : ResolvedTokens -> string
val Css.CssEmitter.emit : ResolvedTokens -> string
val FSharp.emit : string -> ResolvedTokens -> string
```

The themed-emission helpers also become more readable:

```fsharp
// Before
val emitThemedWith
    : DimensionUnitPolicy
   -> (string -> string)
   -> (string list * ResolvedToken) seq
   -> (string * (string list * ResolvedToken) seq) seq
   -> string

// After
val emitThemedWith
    : DimensionUnitPolicy
   -> (string -> string)
   -> ResolvedTokens
   -> (string * ResolvedTokens) seq
   -> string
```

---

## What you need to do

### Nothing.

F# type aliases are transparent — the compiler erases them. `(string list * ResolvedToken) seq` and `ResolvedTokens` are interchangeable at every call site:

```fsharp
// All of these compile in 0.13.0 with no changes:

let tokens : ResolvedTokens = Api.import json |> Result.get
let tokens : (string list * ResolvedToken) seq = Api.import json |> Result.get

let css = FnTools.DesignTokens.Css.CssEmitter.emit tokens   // accepts either spelling
let css = FnTools.DesignTokens.Css.CssEmitter.emit (Api.import json |> Result.get)
```

Your existing code keeps working unchanged.

### Optionally, update your own signatures

If you have wrapper functions or local types that pass resolved tokens around, you can switch to the alias for readability:

```diff
- let writeAllOutputs (tokens: (string list * ResolvedToken) seq) =
+ let writeAllOutputs (tokens: ResolvedTokens) =
      File.WriteAllText("tokens.css", CssEmitter.emit tokens)
      File.WriteAllText("Tokens.fs", FSharp.emit "Tokens" tokens)
```

This is purely cosmetic. Both spellings are equivalent.

---

## What did not change

- **Behaviour.** Same input → same output. No emitter, parser, validator, or resolver was touched beyond the signature aliasing.
- **Binary surface.** Type aliases are erased; the IL is identical between 0.12.0 and 0.13.0 modulo version metadata.
- **Tuple semantics.** Destructuring still works: `for (path, token) in tokens do ...` is unchanged.
- **Package layout.** Same eight packages, same dependencies, same module structure.
- **The `(string list * ResolvedToken) list` form.** Record fields that materialise the seq as a list (e.g. `ImportResult.BaseTokens`) keep the list form. Lists are still implicitly assignable to `ResolvedTokens` parameters because `'a list` is an `IEnumerable<'a>`.

---

## Why

Documentation. `ResolvedTokens -> string` reads as intent; `(string list * ResolvedToken) seq -> string` reads as implementation. With Swift, Kotlin, and XAML emitters planned, every signature site will reference this type — naming it once makes every signature clearer.

See [ADR-039 addendum](../LOGOS/decisions/039-emitter-contract-and-naming.md) for the full rationale, and the design discussion that explicitly rejected a `TokenPath = string list` second-level alias as proliferation without payoff.
