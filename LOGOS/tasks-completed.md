## Completed

### Research & planning
- [x] Read DTCG 2025.10 spec in full — Format, Color, and Resolver modules
- [x] Cloned community-group repo; JSON schemas at `VARIOUS/community-group/www/public/schemas/2025.10/`
- [x] Identified all 13 token types and their field constraints
- [x] Documented 14 color spaces and component value ranges
- [x] Understood two reference syntaxes: curly-brace and JSON Pointer (RFC 6901)
- [x] Understood four-stage resolver algorithm
- [x] Documented what is explicitly out of scope (file I/O, gamut mapping, JSON5, custom type plugins)
- [x] Decided on F# type design patterns: `ValueOrRef<'T>`, `ColorComponent`, mutual recursion via `and`, private `TokenName` constructor
- [x] Wrote full implementation plan: `~/.claude/plans/how-might-we-turn-toasty-deer.md`
- [x] Wrote spec research context: `docs/spec-context.md`
- [x] Initialized repo with plan context commit

### Phase 1 — domain + validation
- [x] `Errors.fs` — ParseError, ValidationError, ResolveError, LoadError, ImportError DUs + format functions
- [x] `Domain.fs` — SpecVersion, full type system (records, DUs, ValueOrRef, ColorComponent)
- [x] `Validation.fs` — all numeric range checks, alias cycle detection, hex/components consistency
- [x] `Json.fs` — `System.Text.Json.Nodes` read helpers (tryGet*, require*, readRef, readExtensions)

### Phase 2 — format
- [x] `Format.fs` — `parse` / `serialize` / `parseAuto` / `parseAs` / `serializeAs`
- [x] Type dispatch with `$type` inheritance down the group tree
- [x] All 13 token value parsers + writers
- [x] Error collection (not short-circuit)
- [x] Round-trip lossless serialization including `$extensions`
- [x] Version detection (FirstED, SecondED, ThirdED, V2025_10) and upgrade-on-parse
- [x] Bug fix: version detector no longer descends into `$value` content (avoided false First-ED detection from inner `value`/`unit` keys)

### Phase 3 — resolver
- [x] `Resolver.fs` — `parseResolver` / `resolve` / `resolveAll`
- [x] Four-stage algorithm with caller-supplied `loadFile`
- [x] Deep merge (group level) vs. replace (token level)
- [x] Alias chain flattening with cycle detection
- [x] Bug fix: `validateResolver` accepts `{"modifier":"name"}` shorthand (empty context = use default)

### Phase 4 — public façade + tests
- [x] `DesignTokens.fs` — `Api` module with `import`, `importWithResolver`, `export`, `Primitives` nested module
- [x] `Fixtures.fs`, `Generators.fs` (Hedgehog 0.x)
- [x] `ValidationTests.fs`, `ParseTests.fs`, `VersionTests.fs`, `FlattenTests.fs`, `ResolverTests.fs`, `SimpleApiTests.fs`, `PropertyTests.fs`
- [x] `Program.fs` — Expecto entry point
- [x] All 50 tests pass; library + tests build with 0 warnings, 0 errors

### Workspace move (2026-05-01)
- [x] Repo moved from `/home/ivan/nexus/NEXUS-Tokens` to `/home/ivan/DEVELOPMENT/FnTools/FnTools.DesignTokens`
- [x] Created `/home/ivan/DEVELOPMENT/FnTools/AGENTS.md` (shared FnTools agent guide)
- [x] Updated `CLAUDE.md` to reflect new path, layered architecture plan, and git workflow
- [x] Replaced project Stop hook with safety-net + push-current-branch (no more timestamp-only commits as the primary record)
- [x] Removed GitKraken hooks from global settings
- [x] Removed parent `nexus` Stop hook (duplicate auto-commit)
- [x] Created public GitHub repo `IvanTheGeek/FnTools.DesignTokens`, wired as `origin`, pushed `main`
- [x] Recorded the move in `/home/ivan/nexus` history (deletion commit)
- [x] Added `LICENSE` (AGPL-3.0)

### Phase 5 — Namespace rename + layer split (2026-05-02)
- [x] Renamed namespace `NEXUS.DesignTokens` → `FnTools.DesignTokens` across all `.fs`, `.fsproj`, docs
- [x] Split single assembly into Foundation / Format / Validation / Resolver / meta-package
- [x] Added `FnTools.DesignTokens.Css` emitter project (DTCG → CSS custom properties)
- [x] Added `FnTools.DesignTokens.Bindings` emitter project (DTCG → typed F# var() constants)
- [x] Added `FnTools.DesignTokens.slnx` covering all 8 projects (6 src + 1 meta + 1 tests)
- [x] Build: 0 warnings, 0 errors across solution; tests: 124/124 pass
- [x] LaundryLog: migrated all CSS from `--cb-*`/`--ll-*` to DTCG-emitted names
- [x] LaundryLog: generated `tokens/Tokens.fs` F# bindings; wired into `LaundryLog.UI.fsproj`
- [x] Token rename batch (2026-05-02): fixed fused camelCase segments, casing inconsistencies
  - `feedback.{success,danger,info}` → promoted to groups with `.default` + `.subtle`
  - `shadow.focusRing` → `shadow.focus-ring`
  - `font.lineHeight` → `font.line-height`; `font.letterSpacing` → `font.letter-spacing`

### Penpot × Tokens Studio integration (2026-05-03 → 2026-05-10)

> Narrative entries with full context live in `work-completed.md`. The list below
> is the original-task-ticked form preserved for traceability. ADR references
> point at the canonical decision record.

- [x] **Tokens Studio parser shim** (2026-05-03) — `FnTools.DesignTokens.TokensStudio` project; 9 transforms (type rename, fontFamily unwrap, typography field rename, dimension unit injection, math expression policy, HSL evaluation, transparent normalisation, `$themes`/`$metadata` extraction). 179/179 tests pass.
- [x] **Multi-set resolution** (2026-05-03) — `Api.importTokensStudio` merges sets in `tokenSetOrder` via `Resolver.resolve` with `Inline` sources; partial-success warnings. 191 tests pass.
- [x] **Math expression evaluator** (2026-05-04) — `MathPolicy.EvaluateMath`; recursive-descent evaluator handles `round/pow/ceil/floor/abs/sqrt/min/max/sin/cos/tan/asin/acos/atan/atan2/log/log2/log10/exp` + `+ - * / ^`. 208 tests pass.
- [x] **Token round-trip** (2026-05-04) — REST push + read-back exact match for `ivanthegeek.tokens.json`.
- [x] **MCP coverage query** (2026-05-04) — `findShapesUsingToken` / `coverageMap` patterns documented; `applyToShapes`/`applyToSelected` confirmed silent no-ops.
- [x] **Theme-aware CSS emitter** (2026-05-04) — `Api.importTokensStudioThemed` + `CssEmitter.emitThemed`. 203/203 pass.
- [x] **Penpot export comparison (Phase 4b)** (2026-05-04) — SVG / Inspect Code / our emitter comparison. Only our path preserves token names. Findings → `LauraExperiment/phase4b-findings.md`.
- [x] **`$ref` JSON Pointer support in Resolver** (2026-05-04) — RFC 6901 same-document pointer resolution with `~0`/`~1` escaping. 213 tests pass.
- [x] **Tokens Studio export** (ADRs 021, 022) (2026-05-04) — `toResolverDocument` + `exportToTokensStudio`. 222/222 pass.
- [x] **Penpot `$extensions` preservation test** (2026-05-04) — pre-ADR-023 empirical check; Penpot strips `$extensions`. Findings → `penpot-extensions-preservation-test.md`.
- [x] **`$extensions` carrier for lossy color metadata** (ADR-023, 2026-05-04) — supersedes ADR-022 carrier choice. 227/227 pass.
- [x] **Laura Dashboard semantic token push (Phase 2 Part 2)** (2026-05-04) — `laura-light-desktop` set (35 tokens) pushed to Design mocks. All 120 bound shapes verified.
- [x] **Math-evaluator theme-bleed fix** (2026-05-04) — `shimSingleFileWithMathIndex` builds the alias index from active-set list only. 230/230 pass.
- [x] **Variant-set math index filtering + `MathEvalFailedVariantAlias`** (ADR-024, 2026-05-04) — two-index architecture (globalIndex / mathIndex). 233/233 pass.
- [x] **Token flow inward (Phase 3)** (2026-05-04) — 22/22 sets of Laura's file parse cleanly. Structural losses documented. See `LauraExperiment/phase3-findings.md`.
- [x] **Phase 4 CSS verification + `@media` breakpoints** (2026-05-04) — `emitBlock` detects `@`-prefixed selectors and nests in inner `:root { }`. `buildFlatIndex` ordering bug fixed. 254/254 pass.
- [x] **Alias `$type` coercion + typeless alias shim fix** (2026-05-04) — `spacing.*`/`radius.*` tokens that alias `number` targets now resolve to `ResolvedDimension` with `px` unit. 254/254 pass.
- [x] **`importTokensStudioCombined`** (ADR-025, 2026-05-04) — cross-group math bleed fix. 254/254 pass.
- [x] **Shim-annotation recovery** (ADR-026, 2026-05-04) — `tsType`, `originalHsl`, `originalFontWeight` keys + `originalTypographyFontWeight`. 258/258 pass.
- [x] **PATHS state mapping (Phase 7)** (2026-05-04) — zero prototype connections in Laura's file (confirmed via Plugin API + archive scan); Penpot prototyping is partial documentation layer. Findings → `LauraExperiment/phase7-findings.md`.
- [x] **Em dimension unit** (ADR-028, 2026-05-04) — `DimensionUnit.Em` added as deliberate spec extension for TS/Penpot round-trip fidelity.
- [x] **`DimensionUnitPolicy` + policy-aware emitters** (2026-05-04) — `emitWith`, `emitThemedWith`, `emitMultiModeWith` added to `CssEmitter`.
- [x] **`emitCalcPreserving`** (ADR-027, 2026-05-04) — mathematical reverse-engineering emitter for design-tool workbench slider.

### CSS ingestion — non-standard unit handling (2026-05-04)

- [x] `CssIngest`: non-DTCG units (`em`, `%`, `vw`, `vh`, etc.) now emit explicit `Skipped` warnings instead of silently stripping the unit.
- [x] `CssAudit`: `CssNative` value type for values that are valid CSS but not DTCG-tokenisable (relative units + `clamp()`/`calc()`). 10 new tests, 243/243 pass.

### API additions post-0.5 (2026-05-04 → 2026-05-10)

- [x] **`importTokensStudioRaw` round-trip API** (ADR-029, 2026-05-04, v0.3.0) — `TokensStudioRawImport` record with `Import + ShimResult + ParsedSets`. Round-trip is three lines from `Api` alone. 258/258 pass.
- [x] **`importTokensStudioCombinedWith`** (ADR-030, 2026-05-06, v0.4.0) — DTCG base sets alongside TS theme resolution. `DtcgSetRole = AsBasePrimitives` required marker.
- [x] **Math expression round-trip** (ADR-031, 2026-05-08) — `extMathExpressionKey` (`tsMathExpression`) annotation in `$extensions["com.fntools.designtokens"]`. 260/260 pass.
- [x] **`serializeResolver`** (ADR-032, 2026-05-08) — `ResolverDocument` serialization. 265/265 pass.
- [x] **Dimension→number alias emission + validation** (ADR-033, 2026-05-10, v0.6.0) — dual-layer fix: CssEmitter coerces, Validation flags as `TypeMismatch`. 281/281 pass.
- [x] **`Api.validateStrictDtcg`** (ADR-028 addendum, 2026-05-10, v0.7.0) — opt-in DTCG 2025.10 spec-compliance check. 291/291 pass.
- [x] **`Api.evaluateMathExtensions`** (ADR-034, 2026-05-10, v0.8.0) — post-resolve evaluation of `tsMathExpression` extensions. Closes `request_2026-05-10_02`. 303/303 pass.

### NuGet packaging

- [x] **CI publish on tag** (2026-05-02, v0.5.0 first stable release) — `.forgejo/workflows/publish-stable.yml` triggers on `v*` tags; deterministic builds. Manual `./publish.sh` retained for `--dev` pre-releases.

### LOGOS housekeeping (2026-05-10)

- [x] ADRs 003 / 013 / 018 / 028 forward references all closed (addenda + ADR-034 builds).
- [x] §A staleness sweep — `naming.md`, `design-system-context.md`, `insights.md`, `penpot-api.md` cross-references corrected.
- [x] ADR index: `LOGOS/decisions/README.md` (by-topic groups, cross-references diagram, chronological master list).
- [x] Migration guides for v0.5→v0.6, v0.6→v0.7, v0.7→v0.8 in `docs/`.
- [x] Retroactive tags v0.6.0 / v0.7.0 pushed for traceability.
- [x] `publish.sh` flagged tag-triggered CI as the preferred stable path.
