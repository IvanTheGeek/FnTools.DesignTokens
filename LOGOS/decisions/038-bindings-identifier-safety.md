---
id: 038
title: Bindings identifier safety lives in the Bindings layer, not in Validation
status: accepted
date: 2026-05-11
---

## Context

The §C gap identified in the 2026-05-10 LOGOS audit: nothing in the
library catches DTCG token paths that would collide when transformed to
F# identifiers by `BindingsEmitter.emit`. Two failure patterns produce
**silent data loss** in the generated F# (not compile errors — the
generated file compiles fine but is missing tokens):

1. **Identifier collision** — two DTCG paths transform to the same F#
   identifier path. The underlying `Map.add` in `insertAt` silently keeps
   the last-encountered token and drops the rest.
   - `color.dark` and `color.Dark` both → `["Color"; "Dark"]`
   - `font.line-height` and `font.lineHeight` both → `["Font"; "LineHeight"]`
   - `scale.400` and `scale.N400` both → `["Scale"; "N400"]`
   - Typography expansion: `font.heading` (typography → 5 sub-paths
     including `Font.Heading.FontSize`) vs. an explicit dimension token
     at `font.heading.font-size` → same `Font.Heading.FontSize` path
2. **Leaf/branch conflict** — one DTCG path produces a Leaf at an F#
   path that another path extends as a Branch.
   - `font` (Leaf at `["Font"]`) + `font.size.sm` (extends Font as Branch)
   - `insertAt` sees the existing Leaf, falls through to `_ -> Map.empty`,
     and overwrites the Leaf with the Branch — the Leaf token is lost

Both are silent data loss. The consumer's downstream code references a
binding that doesn't exist in the generated file, fails to compile
(reasonable signal but disconnected from the cause) or silently misses a
token they expected (worse — uses an undefined-but-string-typed value).

## Decision

Build the safety check **in the Bindings layer**, not in `Validation` or
`Foundation`. The check is exposed as two functions:

```fsharp
type BindingsIdentifierIssue =
    | IdentifierCollision of fsharpPath: string list * tokenPaths: string list list
    | LeafBranchConflict
        of leafFsharpPath: string list
         * leafTokenPath: string list
         * extendingTokenPaths: string list list

module BindingsIdentifierIssue =
    let format : BindingsIdentifierIssue -> string

let checkIdentifierSafety
    (tokens: (string list * ResolvedToken) seq)
    : BindingsIdentifierIssue list

let emitChecked
    (moduleName: string)
    (tokens: (string list * ResolvedToken) seq)
    : Result<string, BindingsIdentifierIssue list>
```

`emit` keeps its existing infallible signature — no breaking change.
Consumers who want pre-flight safety call `checkIdentifierSafety`
explicitly, or use `emitChecked` for the one-call composed version.

### Scope decisions

| Concern | v0.11.0 | Reasoning |
|---|---|---|
| Identifier collisions | ✅ Catch | Silent data loss; clear footgun |
| Leaf/branch conflicts | ✅ Catch | Same silent-data-loss class |
| Non-ASCII identifiers | ⏭️ Skip | F# accepts Unicode identifiers fine in practice; no real footgun |
| Module nesting depth | ⏭️ Skip | Real-world max is 5 segments; F# practical limit is far higher |

Non-ASCII and nesting depth can be added later if a consumer reports a
real toolchain incompatibility. The two concerns shipped today cover
every silent-data-loss path the emitter has.

## Rationale — why the Bindings layer, not Validation

A `Foundation`/`Validation` placement would put F# naming rules in a
layer that's supposed to be language-agnostic. ADR-013 (library scope
ends at the DTCG interchange boundary) is the relevant counter-argument:
a future TypeScript token-emitter consumer of this same library
shouldn't get F#-naming-rule errors from `Validation.validate`. The
domain layer knows about colors and dimensions; it shouldn't know that
`Color.Dark` and `Color.dark` collide in F# (or anywhere else).

The Bindings layer already encodes the F# transform (`toFsharpIdent` +
typography expansion). It's the natural home for the safety check on
that transform. Every emitter package owns its own naming concerns
independently of the others:

- `Css` emitter — owns CSS custom-property naming
- `Bindings` emitter — owns F# identifier naming
- (future) `TypeScript` emitter — would own JS/TS identifier naming
- (future) Other targets — own their own conventions

Each emitter's safety checks belong with the emitter, surfaced as
opt-in pre-flight functions that don't disturb the language-agnostic
core. Same shape established by `Api.validateStrictDtcg` (ADR-028
addendum) — opt-in compliance check at the layer where the check is
meaningful.

## Implementation — DRY via shared helper

Followed the pattern established by ADR-033 v0.10.2 addendum (the
flatten-functions unification): `buildTree` and `checkIdentifierSafety`
share `expandedFsPaths : string list -> ResolvedToken -> (string list * string) list`,
which materialises the F# identifier paths a single token will occupy
(one entry for most types, five for typography). This eliminates the
"fix one, forget the parallel" failure mode that gave us the v0.10.1
flatten bug — both the build-the-tree path and the check-the-tree path
walk the same path expansion logic.

## Consequences

- `BindingsEmitter.emit` is unchanged — existing consumers keep working.
  Their generated F# may be silently missing tokens; this ADR adds the
  opt-in detection but doesn't change emission behavior.
- `BindingsEmitter.checkIdentifierSafety` is the explicit pre-flight
  check. Returns `[]` if `emit` would produce one binding per token.
  Returns a list of `BindingsIdentifierIssue` values otherwise.
- `BindingsEmitter.emitChecked` composes check + emit in one call.
  Returns `Result<string, BindingsIdentifierIssue list>`.
- `BindingsIdentifierIssue` is a public type in the Bindings package;
  consumers can pattern-match on its cases or format with
  `BindingsIdentifierIssue.format` for log output.
- No changes to `Foundation`, `Validation`, `Resolver`, `Format`, `Css`,
  or `TokensStudio`. The whole feature lives in one package.
- 10 new tests in `BindingsEmitterTests.safetyTests`: clean baseline,
  case collision, hyphen-vs-camel collision, numeric N-prefix collision,
  typography expansion collision, leaf/branch conflict, `emitChecked`
  happy path, `emitChecked` failure path, formatter output, real-world
  sample (`samples/ivanthegeek.tokens.json`) clean baseline. 339/339 pass.

### Pattern for future emitters

When the future TypeScript / Swift / Kotlin / etc. emitters land, each
should add its own equivalent: `EmitterName.checkIdentifierSafety` +
`EmitterName.emitChecked`. Language-specific concerns stay in
language-specific layers; `Foundation` / `Validation` stay agnostic.

This pattern is also why a single generic "identifier collision" check
in `Validation` would be wrong: collision rules depend on the
*destination language*, not the source DTCG. JS allows mixed-case
identifiers that differ in case; F# (via our PascalCase normalization)
collapses them. The check is meaningful only at the target language's
emitter boundary.

Shipped in v0.11.0 (2026-05-11).
