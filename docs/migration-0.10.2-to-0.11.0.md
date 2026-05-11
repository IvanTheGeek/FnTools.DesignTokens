# Migrating from 0.10.2 to 0.11.0

Released: 2026-05-11. Tracking ADR: [`038-bindings-identifier-safety.md`](../LOGOS/decisions/038-bindings-identifier-safety.md).

---

## TL;DR

Purely additive. Closes the §C bindings-safety gap from the 2026-05-10 LOGOS audit. Two new functions in the `Bindings` package (`checkIdentifierSafety` and `emitChecked`) catch a silent-data-loss class of bug in `BindingsEmitter.emit` — when two DTCG tokens transform to the same F# identifier path, the existing emitter silently keeps only one of them.

No breaking changes. Existing `emit` calls keep working unchanged.

```diff
- <PackageReference Include="FnTools.DesignTokens" Version="0.10.2" />
+ <PackageReference Include="FnTools.DesignTokens" Version="0.11.0" />
```

---

## What's new

### The problem this solves

`BindingsEmitter.emit` transforms each DTCG token path to a PascalCase F# identifier path (e.g. `color.text.main` → `Color.Text.Main`). Two patterns cause **silent data loss** in the generated F#:

1. **Identifier collision** — two DTCG paths transform to the same F# path:
   - `color.dark` and `color.Dark` both → `Color.Dark`
   - `font.line-height` and `font.lineHeight` both → `Font.LineHeight`
   - `scale.400` and `scale.N400` both → `Scale.N400`
   - Typography expansion: `font.heading` (typography token → expands to 5 sub-paths including `Font.Heading.FontSize`) collides with an explicit dimension token at `font.heading.font-size`

2. **Leaf/branch conflict** — one F# path is a strict prefix of another:
   - Token at `font` (Leaf at `Font`) + token at `font.size.sm` (extends `Font` as Branch)
   - The Leaf gets overwritten by the Branch

In both cases, the underlying `Map.add` silently keeps the last-encountered token; the others are missing from the generated bindings module. The consumer's downstream code either compiles fine but is missing tokens they expected, or fails to compile against references to bindings that don't exist.

### The new API

```fsharp
open FnTools.DesignTokens.Bindings

// Issue type — both variants represent silent data loss
type BindingsIdentifierIssue =
    | IdentifierCollision of fsharpPath: string list * tokenPaths: string list list
    | LeafBranchConflict
        of leafFsharpPath: string list
         * leafTokenPath: string list
         * extendingTokenPaths: string list list

module BindingsIdentifierIssue =
    val format : BindingsIdentifierIssue -> string

// Pre-flight check — returns [] if emit is safe
let checkIdentifierSafety
    (tokens: (string list * ResolvedToken) seq)
    : BindingsIdentifierIssue list

// emit + check in one call
let emitChecked
    (moduleName: string)
    (tokens: (string list * ResolvedToken) seq)
    : Result<string, BindingsIdentifierIssue list>
```

### Why the check lives in the Bindings layer, not Validation

F# naming rules are F#-specific. A future TypeScript or Swift emitter would have its own rules (e.g., JS allows identifiers that differ only in case where F# collapses them via PascalCase). Putting "no identifier collisions" in `Validation.validate` would force every consumer to inherit every potential emitter's naming opinions, regardless of which emitter they actually use — and would violate ADR-013's "library scope ends at the DTCG interchange boundary." Per-emitter `*.checkIdentifierSafety` + `*.emitChecked` pairs keep the domain layer agnostic. See ADR-038 for the full rationale.

---

## Migration scenarios

### Scenario A — you don't use `BindingsEmitter` at all

Do nothing. The change is isolated to the `Bindings` package.

### Scenario B — you call `BindingsEmitter.emit` and your token file has no collisions

Do nothing. Your existing emit call keeps working unchanged. If you want pre-flight safety in your build:

```fsharp
open FnTools.DesignTokens.Bindings

// One-call replacement that fails fast on issues
match emitChecked "Tokens" tokens with
| Ok source -> File.WriteAllText("Tokens.fs", source)
| Error issues ->
    for i in issues do eprintfn "%s" (BindingsIdentifierIssue.format i)
    exit 1
```

### Scenario C — you call `BindingsEmitter.emit` and your token file MIGHT have collisions

Run `checkIdentifierSafety` once to find out:

```fsharp
let issues = checkIdentifierSafety tokens
if issues |> List.isEmpty then
    printfn "Bindings are safe to generate"
else
    for i in issues do printfn "%s" (BindingsIdentifierIssue.format i)
```

If issues are reported, you have a real bug in your generated bindings — some tokens are silently missing. Fix the DTCG file (rename the colliding tokens), then proceed.

### Scenario D — you're authoring a CI/build script

Switch from `emit` to `emitChecked` so the build fails fast on collision issues instead of silently producing broken bindings:

```diff
- let source = emit "Tokens" tokens
- File.WriteAllText("Tokens.fs", source)
+ match emitChecked "Tokens" tokens with
+ | Ok source -> File.WriteAllText("Tokens.fs", source)
+ | Error issues ->
+     for i in issues do eprintfn "%s" (BindingsIdentifierIssue.format i)
+     failwith "BindingsEmitter detected identifier collisions — bindings cannot be safely generated"
```

---

## What did NOT change

- `BindingsEmitter.emit` keeps its signature `emit : string -> (string list * ResolvedToken) seq -> string`. Same output for any input that produces no collisions.
- The PascalCase identifier transform (`toFsharpIdent`) is unchanged.
- Typography expansion to 5 sub-properties is unchanged.
- No other package (`Foundation`, `Validation`, `Resolver`, `Format`, `Css`, `TokensStudio`) is touched.
- `Api`, `Primitives` exports unchanged.

---

## Scope of the check (v0.11.0)

| Concern | v0.11.0 | Reasoning |
|---|---|---|
| Identifier collisions (case, hyphen-vs-camel, N-prefix, typography expansion) | ✅ Catch | Silent data loss; clear footgun |
| Leaf/branch conflicts | ✅ Catch | Same silent-data-loss class |
| Non-ASCII identifiers | ⏭️ Skip | F# accepts Unicode identifiers fine in practice |
| Module nesting depth | ⏭️ Skip | Real-world max is 5 segments; F# limit is far higher |

If Unicode or nesting depth bite a real consumer later, add then.

---

## Upgrade steps

1. Update your `PackageReference`:
   ```xml
   <PackageReference Include="FnTools.DesignTokens" Version="0.11.0" />
   ```

2. Build. No expected compile-time impact unless you were referencing `BindingsEmitter.emit` via the (incorrect) qualified name shown in earlier api-reference docs — the actual module name is `FnTools.DesignTokens.Bindings`, accessed as `emit` unqualified after `open`. The doc was fixed in this release.

3. Decide whether to switch `emit` → `emitChecked` per Scenario D above. Recommended for build pipelines.

4. Tests: 339/339 pass. New `BindingsEmitterTests.safetyTests` testList covers all collision/conflict patterns plus a real-world sample baseline (`samples/ivanthegeek.tokens.json`).

---

## Reference

- **ADR-038** — full rationale (layer placement, scope decisions, pattern for future emitter packages).
- **Insights entry** — "Language-specific safety checks belong in language-specific emitter layers" in `LOGOS/insights.md`.
- **Tests** — 10 new in `BindingsEmitterTests.safetyTests`. 339/339 total pass.
