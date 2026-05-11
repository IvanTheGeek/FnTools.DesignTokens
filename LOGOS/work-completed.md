## ValidateOptions + deprecate resolveAll + flattenAliases public (ADR-035 / 036 / 037, v0.10.0, 2026-05-10)

Closes the friction chain that bit `request_2026-05-10_04`: ADR-033
TypeMismatch forces TS-as-SoT consumers off the convenience wrappers →
they fall back to the Primitives path → they hit the `resolveAll` trap
(`resolveAll = resolve >>= flattenAliases` eats the alias graph that
`evaluateMathExtensionsInFile` needs to propagate) → they file a wrong
library request thinking the library is broken.

Two paired fixes plus a deferred-direction ADR for traceability:

- [x] **ADR-035 — `ValidateOptions` opt-in laxness**:
  `ValidateOptions { AllowDimensionAliasingNumber }` + `strict`/`permissive`
  presets in Foundation. `Validation.validateWith` accepts options; legacy
  `validate` is `validateWith ValidateOptions.strict`. New `*With` variants
  for every convenience wrapper: `Api.importWith`, `Api.importWithResolverWith`,
  `Api.importWithResolverEvaluatingExtensionsWith`. Permissive opts out of
  *only* the dimension→number TypeMismatch — other cross-type aliases still
  fail. Strict default protects accidental mismatches.
- [x] **ADR-036 — deprecate `Resolver.resolveAll` + expose `Resolver.flattenAliases`
  publicly**: `resolveAll` is now `[<Obsolete>]` with a deprecation message
  naming both replacement paths (common: `resolve` + `flattenResolved`;
  narrow: `resolve` + `flattenAliases`). `flattenAliases` promoted from
  `let private` to public, with full XML docs explaining the narrow use case.
  Same shared-impl pattern as ADR-034 addendum to avoid FS44 on the Primitives
  re-export.
- [x] **ADR-037 — validation warning channel (deferred)**: option 3 from the
  outside-conversations exchange. Not built; documented as future possible
  route for advisory issues that aren't footguns (unused tokens, scale
  outliers, etc.). Filed for traceability so future agents reach for it
  before adding a new `ValidationError` for something that's really advisory.
- [x] 10 new tests: 5 in ValidationTests (validateWith strict/permissive +
  narrow scope + legacy equivalence + Primitives parity), 3 in ResolverTests
  (flattenAliases public + cycle detection + Primitives parity + deprecated
  resolveAll regression under `#nowarn 44`), 2 in ExtensionEvaluationTests
  (STRICT default friction + PERMISSIVE convenience-wrapper propagation —
  the FRICTION test from 0.9.0 split into STRICT + PERMISSIVE cases).
  328/328 pass (was 318).
- [x] `docs/migration-0.9-to-0.10.md` written: 4 upgrade scenarios + reference.
- [x] `docs/api-reference.md` updated: version + migration list bumped; all
  new functions documented; `resolveAll` marked deprecated in Primitives table.
- [x] `LOGOS/decisions/README.md` ADR index updated: 3 new ADRs cross-referenced,
  ADR-037's `deferred` status called out.
- [x] `LOGOS/insights.md` updated: two new entries — "Per-call opt-in beats
  global laxness for footgun-prevention" + "Deprecation as a discoverability
  fix when renaming would replace one trap with another".

Outside conversation: `LOGOS/outside-conversations/outside-conversations_2026-05-10_01.md`
captures the 4-option comparison that produced this ADR trio. Response to the
requester drafted at `outside-conversations_2026-05-10_02.md`.

## Extension-aware resolve — alias propagation fix (ADR-034 addendum, v0.9.0, 2026-05-10)

Closes `request_2026-05-10_03`. The 0.8.0 `Api.evaluateMathExtensions`
(post-flatten, ResolvedToken seq) had a structural hole: updating a
formula token's value did not propagate to tokens that aliased it, because
`flattenResolved` bakes alias values into concrete numbers before the
function ever runs. By the time the seq exists, the alias→target
relationship is gone from the data.

- [x] `Api.evaluateMathExtensionsInFile : TokenFile -> EvaluateMathInFileResult` —
  pre-flatten evaluation. Walks the TokenFile (where aliases are still
  `TokenValue.Alias` literals), builds an alias-aware string index, evaluates
  each `tsMathExpression`-bearing token against it, replaces `$value`.
  Subsequent `flattenResolved` follows aliases and picks up the new values
  automatically — propagation correct by construction.
- [x] `TokensStudio.tryEvaluateMathExpressionWithIndex : Map<string,string> -> string -> float option` —
  public wrapper exposing MathEval's raw string-index mode for use with the
  alias-aware index.
- [x] `Api.importWithResolverEvaluatingExtensions` rewired internally to use
  `evaluateMathExtensionsInFile` between `resolve` and `flattenResolvedFile`.
  Public signature unchanged; propagation works on upgrade.
- [x] `Api.evaluateMathExtensions` marked `[<System.Obsolete(...)>]` pointing
  at the new function. Still works for the single-formula-token case (no
  aliases). Removal target: v1.0.0.
- [x] 12 new tests in `ExtensionEvaluationTests.fs` `inFileTests`, including
  three explicit propagation tests (single-hop, multi-hop chain,
  formula-references-formula). Old tests preserved under `#nowarn "44"`
  in `deprecatedFunctionTests` for regression coverage. 315/315 pass.
- [x] ADR-034 addendum (2026-05-10, v0.9.0) — full diagnosis + the lesson:
  "post-flatten + alias-following are incompatible by design; do the work
  in the representation that still has the structure."
- [x] `docs/migration-0.8-to-0.9.md` written.
- [x] `docs/api-reference.md` updated: new function documented, deprecated
  function explicitly marked, Primitives table updated.

## Extension-aware resolve (ADR-034, completed 2026-05-10)

Reported by downstream consumer (`LOGOS/requests/request_2026-05-10_02.md`).

The DTCG-as-SoT workflow with axis sets lost the per-axis math evaluation that
the TS-import family already does correctly. `Resolver.resolveAll` reads
`$value` directly and ignores `tsMathExpression` — so scale tokens defined as
`round({base} * pow({multiplier}, 3))` returned the stale snapshot value
written at the last save, not the freshly-evaluated value under the active
axis combination.

- [x] `Api.evaluateMathExtensions : seq -> ResolveWithExtensionsResult` — post-resolve
  pass that walks tokens, evaluates `tsMathExpression` against the current
  resolved numeric context, replaces the value (preserving unit for Dimension/Duration),
  collects failures as `ExtensionEvaluationWarning.MathExpressionFailed`.
- [x] `Api.importWithResolverEvaluatingExtensions` — one-call convenience that
  composes `importWithResolver` + `evaluateMathExtensions`.
- [x] `TokensStudio.tryEvaluateMathExpression : Map<string, float> -> string -> float option` —
  public wrapper over the internal `MathEval` evaluator so the meta-package
  can drive it without re-exporting the recursive module shape.
- [x] `ExtensionEvaluationWarning` DU + formatter in Foundation.
- [x] `Resolver.resolveAll` unchanged — strict DTCG compliance preserved.
- [x] 12 new tests in new `ExtensionEvaluationTests.fs` covering: pass-through,
  literal expression, {variable} resolution from Number / Dimension context,
  missing variable warning, parse error warning, multi-failure collection,
  unit preservation for Dimension, non-numeric host pass-through, interleaved
  tokens, formatter, Primitives parity.
- [x] ADR-034 written.
- [x] 303/303 tests pass.

## Strict DTCG 2025.10 compliance validator (ADR-028 addendum, completed 2026-05-10)

Closes ADR-028's "future strict-mode serialiser" forward reference. Built as a
validator rather than a serializer so `Format.serialize` stays infallible (ADR-012)
and strictness becomes a separate, opt-in concern (mirrors ADR-033 — validation
surfaces, leaf layer accommodates).

- [x] `Validation.validateStrictDtcg : TokenFile -> Result<unit, ValidationError list>` — walks
  the file, reports `ConstraintViolation` for any literal `DimensionValue` with `Unit = Em`
  (direct or inside Border/Shadow/Typography/StrokeStyle composites). References pass.
- [x] `Api.validateStrictDtcg` and `Primitives.validateStrictDtcg` surface to consumers.
- [x] 10 new tests: plain Em, Px/Rem pass, Em in shadow.blur / typography.letterSpacing /
  border.width / strokeStyle.dashArray, alias-not-followed, multi-violation collection
  (ADR-002), regular validate still accepts Em (separation of concerns), deep-path reporting.
- [x] ADR-028 addendum: documents why-validator-not-serializer, the rejected
  Em→Px coercion option, and the "extensions gather here" pattern for future
  spec deviations.
- [x] `docs/api-reference.md`: new section + Primitives table entry.
- [x] 291/291 tests pass.

## Dimension→number alias emission + validation (ADR-033, completed 2026-05-10)

Reported by downstream consumer (`LOGOS/requests/request_2026-05-10_01_library...md`).

- [x] `tokenToCssDeclsWith` (CssEmitter): when `Value=ResolvedNumber n` and `Type=DimensionType`, treat as `{Value=n; Unit=Px}` and apply unit policy. Fixes `--spacing-x1: 16;` → `1rem`/`16px`.
- [x] `emitCalcPreserving` mirror: same coercion in the calc-optimization branch so dimension→number aliases still fit the scale and produce `calc()` expressions.
- [x] `Validation.checkAliasTypes`: follow alias chains, emit `TypeMismatch` when declared `$type` ≠ ultimate resolved value's type.
- [x] `TokenType.displayName` + `TokenValue.inferType` promoted from private in `DesignTokens.fs` to public module-on-type pattern in `Foundation/Domain.fs` (used by both validation and resolver).
- [x] 12 new tests: 6 CssEmitter (tokenToCssDecls Npx, identity policy, Rem policy, themed path policy, regression no-unitless, calc() optimization fires); 5 Validation (mismatch flagged, same-type passes, chain mismatch, cycle not flagged as mismatch, unresolved not flagged as mismatch); 1 emitCalcPreserving extension. 281/281 total.
- [x] ADR-033 written.
- [x] Version bumped 0.5.1 → 0.6.0 (new validation rule + new emitter behavior, both consumer-visible).

## serializeResolver (ADR-032, completed 2026-05-08)

- [x] `Resolver.serializeResolver (doc: ResolverDocument) : string` added to `Resolver.fs`
- [x] Inline sources via `Format.serialize` embedded as `JsonNode`; FileRef as `{"path":"..."}`
- [x] SetRef → `{"set":"..."}`, ModifierRef → `{"modifier":"...","context":"..."}`
- [x] Optional fields omitted when None; empty sets/modifiers omit the key; extensions written when non-empty
- [x] Exposed as `Api.serializeResolver` (top-level) and `Primitives.serializeResolver`
- [x] 5 new tests covering round-trip, FileRef, Inline, optional fields, extensions; 265/265 pass
- [x] ADR-032 written

## Math expression round-trip (ADR-031, completed 2026-05-08)

- [x] `extMathExpressionKey = "tsMathExpression"` added to vendor namespace constants
- [x] Added to `shimAllInternalKeys` (stripped on TS re-import) and `shimExportStripKeys` (stripped after recovery on TS export)
- [x] Annotation (5) in `walkObj`: written when `tsType = "number"` and `isMathExpression origRawValue` and `EvaluateMath` succeeds
- [x] Recovery in `addTokensToObj`: `tsMathExpression` checked first in the `recoveredValue` chain; raw string used as `$value` when present
- [x] 2 new tests: `"8 * 2"` round-trip (expression restored, float not emitted, key stripped); plain `"16"` has no annotation
- [x] 260/260 tests pass; ADR-031 written

## Dark-mode CSS emitter (completed 2026-05-03)

- [x] `CssEmitter.emitBlock (selector: string) (tokens: ...) : string` — emits any selector block
- [x] `CssEmitter.emit` refactored as `emitBlock ":root"` wrapper; existing callers unchanged
- [x] `CssEmitter.emitMultiMode (baseTokens) (overrideTokens) (overrideSelector) : string` — diff-only override block
  - Override block omitted entirely when all tokens are identical
  - New tokens present only in override set are included in the override block
- [x] Functions wrapped in `[<AutoOpen>] module CssEmitter` for qualified access without breaking `emit`
- [x] 6 new tests (180 total): emitBlock selector, emit ≡ emitBlock :root, no-diff suppression, color diff, partial diff, new-token inclusion

## Penpot export shim (completed 2026-05-03)

- [x] `Format.serializePenpot (setName: string) (_ : ExportLossAcknowledged) (file: TokenFile) : string`
- [x] Wraps Second ED output in `{ "sets": { "<name>": { ...tokens } } }` — Penpot's token import format
- [x] No `$schema`; colors as hex strings (inherits Second ED rules)
- [x] Re-exported as `Primitives.serializePenpot` in meta-package
- [x] 4 new tests (174 total): sets wrapper, no $schema, hex colors, inner content round-trips

## CssAudit duplication detector (completed 2026-05-03)

- [x] `AuditEntry.MatchedToken : string option` — populated by `auditAgainst`, always `None` from `audit`
- [x] `auditAgainst (cssText: string) (file: TokenFile) : AuditResult` — annotates each entry with the path of a matching token
- [x] Token lookup: canonical CSS strings computed from `ColorValue` (hex / sRGB components→rgba), `DimensionValue`, `FontFamilyValue`
- [x] Input normalisation: short hex expanded (`#fff`→`#ffffff`), rgba spacing canonicalised
- [x] 7 new tests (170 total); validated against `ivanthegeek.tokens.json`
- [x] Closes the last open item in the CSS Bootstrap / Migration workflow step 2

## CssAudit duplication detector + DimensionUnit spec alignment (completed 2026-05-03)

- [x] `AuditEntry.MatchedToken : string option` — populated by new `auditAgainst` function
- [x] `CssAudit.auditAgainst` — walks TokenFile, builds canonical-CSS lookup, annotates entries
- [x] Matches: hex (with short-hex expansion), rgba (normalised spacing), dimension, font-family; Shadow/Alias return None
- [x] 7 new tests (170 → 179 pass total)
- [x] Reverted `DimensionUnit.Em` — spec allows only `px` and `rem`; `em` is CSS-specific and component-layer
- [x] `isDimensionValue` in CssAudit narrowed to `px|rem` only — non-DTCG units classified as Unknown/excluded
- [x] Removed `font.tracking.wide` from `ivanthegeek.tokens.json` (0.22em letter-spacing → component code)
- [x] `insights.md`: DTCG dimension unit scope, em/component-layer rule of thumb
- [x] `tasks-open.md`: CssIngest warning on non-standard units; future CssNative audit category

## ivanthegeek.com bootstrap (completed 2026-05-03)

- [x] `CssIngest.ingest` against live site CSS (prefix-less, `""`)
- [x] `CssAudit.audit` — 44 distinct hardcoded values inventoried
- [x] Named and grouped: 13 color tokens, 6 font tokens, 4 spacing tokens, 5 shadow tokens = 28 total
- [x] `samples/ivanthegeek.tokens.json` authored — parses and round-trips clean
- [x] Gap found and fixed: `DimensionUnit` added `Em` case; parser + both emitters updated

## Phase 6 — CSS emitter + typed F# bindings emitter (completed 2026-05-02)

### CSS emitter — `FnTools.DesignTokens.Css`

- [x] `CssEmitter.fs` — all 13 DTCG token types → CSS custom properties; typography expands to 5 sub-vars
- [x] OKLCH, OKLab, LCH, Lab, HSL, HWB → dedicated CSS functions; other spaces → `color()` generic syntax
- [x] `emit` function: resolved token seq → `:root { }` CSS block
- [x] 26 tests covering color, scalar, shadow, varName, emit (including LaundryLog integration)
- [x] LaundryLog `ll.css` emitted: 124 custom properties, 5148 bytes
- [x] Added to slnx and meta-package

### Typed F# bindings emitter — `FnTools.DesignTokens.Bindings`

- [x] `BindingsEmitter.fs` — resolved token seq → nested `module` F# source with `var()` string constants
- [x] `toFsharpIdent`: N-prefix for numeric segments, PascalCase via hyphen-splitting, no backtick needed (keywords capitalise cleanly)
- [x] Typography tokens expand to 5 sub-property bindings matching CSS emitter suffixes
- [x] Generated file has zero runtime dependencies
- [x] 22 tests: ident conversion, module structure, typography expansion, LaundryLog integration
- [x] LaundryLog `Tokens.fs` emitted: 161 lines covering all cb + ll tokens
- [x] Added to slnx and meta-package

## Phase 5 — Namespace rename + layer split (completed 2026-05-02)

Phases 1–4 (single-assembly NEXUS.DesignTokens) are complete and tracked in `tasks-completed.md`. This phase finished the relocation: renamed the namespace and split the assembly into layers under the FnTools brand.

### Namespace rename — NEXUS.DesignTokens → FnTools.DesignTokens

- [x] Rename namespace in every `.fs` file under `src/` and `tests/`
- [x] Update every `open NEXUS.DesignTokens*` in tests and any future consumers
- [x] Rename `src/NEXUS.DesignTokens/` directory + `NEXUS.DesignTokens.fsproj` → `FnTools.DesignTokens.fsproj`
- [x] Rename `tests/NEXUS.DesignTokens.Tests/` directory + `NEXUS.DesignTokens.Tests.fsproj` → `FnTools.DesignTokens.Tests.fsproj`
- [x] Update `AssemblyName` / `RootNamespace` in both `.fsproj` files
- [x] Update test project's `<ProjectReference>` to the renamed source project
- [x] Update `CLAUDE.md` — move the "Namespace _(current)_" line to "Namespace: FnTools.DesignTokens" once the rename lands; remove the "Planned rename" note
- [x] Verify: `dotnet build` (0 warnings, 0 errors) + `dotnet test` (50/50 pass)

### Solution file

- [x] Decide: `.sln` or no `.sln` — added `FnTools.DesignTokens.slnx` (SDK default format) covering all 5 source projects + 1 test project

### Layer split

- [x] `FnTools.DesignTokens.Foundation` — pure types, smart constructors, zero non-BCL deps
  - `Errors.fs`, `Domain.fs` (no Json/Validation; pure model only)
- [x] `FnTools.DesignTokens.Format` — JSON parse/serialize via `System.Text.Json`
  - depends on `Foundation`; contains `Json.fs`, `Format.fs`
- [x] `FnTools.DesignTokens.Validation` — invariants (alpha/component ranges, fontWeight, alias cycles, hex/components consistency)
  - depends on `Foundation`; `FsToolkit.ErrorHandling` dependency lives here
- [x] `FnTools.DesignTokens.Resolver` — multi-set / modifier resolver document semantics
  - depends on `Foundation` + `Format` (to parse resolver JSON)
- [x] `FnTools.DesignTokens` — meta-package re-exporting the four above as `FnTools.DesignTokens.Api`
  - depends on all four
- [x] Test project `FnTools.DesignTokens.Tests` references the meta-package and stays as one assembly

## Reference smoke tests (completed 2026-05-02)

- [x] Parse spec's own example token files from `community-group` repo with zero errors and serialize back round-trip
  - 26 tests in `SmokeTests.fs`, drawn from `design-token.md`, `groups.md`, `composite-types.md`, `aliases.md`, and `types.md`
  - Covers all 13 token types, `$root`, `$extends`, curly-brace and JSON Pointer aliases, composite sub-value aliases
  - All 76 tests pass

## NuGet packaging metadata + publish script (completed 2026-05-02)

- [x] `PackageId`, `Version`, `Authors`, `Description`, `PackageLicenseExpression`, `RepositoryUrl` added to all 5 `.fsproj` files
- [x] `<IsPackable>false</IsPackable>` added to test project to prevent accidental packing
- [x] `publish.sh` — builds, packs, and pushes all 5 packages to Forgejo NuGet feed
  - Feed: `https://forgejo.ivanthegeek.com/api/packages/FnTools/nuget/index.json`
  - Token from `FORGEJO_TOKEN` env or `~/.config/forgejo-claude.token`
  - `--skip-duplicate` prevents re-push failures

## Remote migration to Forgejo (completed 2026-05-02)

- [x] Primary remote moved from GitHub to `https://forgejo.ivanthegeek.com/FnTools/FnTools.DesignTokens`
- [x] Forgejo push mirror configured: auto-syncs to `https://github.com/IvanTheGeek/FnTools.DesignTokens` on every commit (`sync_on_commit: true`)
- [x] `CLAUDE.md` updated: Forgejo is primary; GitHub is mirror
