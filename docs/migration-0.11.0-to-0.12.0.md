# Migration: 0.11.0 → 0.12.0

**Breaking change.** The F# emitter package is renamed from `Bindings` to `FSharp`.

This release renames one package and one type. No behaviour changes; no other API surface changes. The rename is mechanical — find-and-replace at the call site is sufficient.

The rationale is recorded in [ADR-039: Emitter contract and target-named packages](../LOGOS/decisions/039-emitter-contract-and-naming.md): every emitter package is now named by the target it produces (`Css`, `FSharp`, `TokensStudio`, and future `Swift`, `Kotlin`, `Xaml`), not by its role. `Bindings` was the only role-named emitter and was becoming ambiguous as language targets multiply.

---

## What changed

### Package id

| Before | After |
|---|---|
| `FnTools.DesignTokens.Bindings` | `FnTools.DesignTokens.FSharp` |

The previous package id stops being published. The meta-package `FnTools.DesignTokens` pulls in the new package id transitively.

### Module path

| Before | After |
|---|---|
| `module FnTools.DesignTokens.Bindings` | `module FnTools.DesignTokens.FSharp` |
| `open FnTools.DesignTokens.Bindings` | `open FnTools.DesignTokens.FSharp` |
| `Bindings.emit` | `FSharp.emit` |
| `Bindings.emitChecked` | `FSharp.emitChecked` |
| `Bindings.checkIdentifierSafety` | `FSharp.checkIdentifierSafety` |
| `Bindings.toFsharpIdent` | `FSharp.toFsharpIdent` *(name preserved)* |

### Type names

| Before | After |
|---|---|
| `BindingsIdentifierIssue` | `IdentifierIssue` |
| `BindingsIdentifierIssue.IdentifierCollision` | `IdentifierIssue.IdentifierCollision` |
| `BindingsIdentifierIssue.LeafBranchConflict` | `IdentifierIssue.LeafBranchConflict` |
| `module BindingsIdentifierIssue` (helpers) | `module IdentifierIssue` |
| `BindingsIdentifierIssue.format` | `IdentifierIssue.format` |

The redundant `Bindings` prefix is dropped because the type already lives inside the `FSharp` module — `FSharp.IdentifierIssue` is unambiguous and reads cleanly.

### File and directory names (only relevant if you build from source)

| Before | After |
|---|---|
| `src/FnTools.DesignTokens.Bindings/` | `src/FnTools.DesignTokens.FSharp/` |
| `src/FnTools.DesignTokens.Bindings/BindingsEmitter.fs` | `src/FnTools.DesignTokens.FSharp/FSharpEmitter.fs` |
| `tests/.../BindingsEmitterTests.fs` | `tests/.../FSharpEmitterTests.fs` |

---

## Migration

### If you use the meta-package

```diff
- <PackageReference Include="FnTools.DesignTokens" Version="0.11.0" />
+ <PackageReference Include="FnTools.DesignTokens" Version="0.12.0" />
```

```diff
- open FnTools.DesignTokens.Bindings
+ open FnTools.DesignTokens.FSharp

- let source = BindingsEmitter.emit "Tokens" tokens
+ let source = FSharp.emit "Tokens" tokens
```

### If you reference the emitter package directly

```diff
- <PackageReference Include="FnTools.DesignTokens.Bindings" Version="0.11.0" />
+ <PackageReference Include="FnTools.DesignTokens.FSharp" Version="0.12.0" />
```

```diff
- open FnTools.DesignTokens.Bindings
+ open FnTools.DesignTokens.FSharp
```

### If you matched on `BindingsIdentifierIssue`

```diff
- match issue with
- | BindingsIdentifierIssue.IdentifierCollision (fsPath, tokenPaths) -> ...
- | BindingsIdentifierIssue.LeafBranchConflict (...) -> ...
+ match issue with
+ | IdentifierIssue.IdentifierCollision (fsPath, tokenPaths) -> ...
+ | IdentifierIssue.LeafBranchConflict (...) -> ...
```

The case constructor names (`IdentifierCollision`, `LeafBranchConflict`) are unchanged — only the DU type name itself changed.

---

## What did not change

- **Behaviour.** `emit` and `emitChecked` produce the same output as in 0.11.0. `checkIdentifierSafety` returns the same issues. The N-prefix rule (ADR-010), keyword backtick-escaping, typography expansion, and identifier-safety checks (ADR-038) are unchanged.
- **Generated file content.** A `Tokens.fs` emitted by 0.11.0 and 0.12.0 from the same input is byte-identical.
- **Other packages.** `Foundation`, `Format`, `Validation`, `Resolver`, `Css`, `TokensStudio` are unchanged in 0.12.0 except for the version number.
- **`toFsharpIdent`.** Function name preserved — it converts to an F#-valid identifier, which is what its name describes.

---

## Why

[ADR-039](../LOGOS/decisions/039-emitter-contract-and-naming.md) records both halves of the decision: the universal emitter contract (`(string list * ResolvedToken) seq -> string`, the type every Translator consumes) and the package naming rule (target language or tool, never role). The current consumer of this library (`LauraApp`, an experiment by definition) confirmed the breaking rename. Future emitter packages — Swift, Kotlin, XAML — will follow the same naming convention.
