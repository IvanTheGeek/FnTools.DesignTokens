## Active / Next

> Phases 1–5 complete — see `tasks-completed.md` and `work-completed.md`. Detailed plan for items below: `work-planned.md`.

### Penpot × Tokens Studio learning experiments

Analysis file: `penpot-tokens-studio-format.md`. The Design mocks.penpot file from Laura's
demo is in `/home/ivan/ARCHIVE/Penpot-DesignTokens/`. Primary interaction is via the
**MCP server + browser extension** — not the REST API (broken for tokens) and not file
import/export. See `penpot-api.md` for the three-surface comparison.

- [x] **Tokens Studio parser shim** — `FnTools.DesignTokens.TokensStudio` project; 9 transforms
  (type rename, fontFamily unwrap, typography field rename, dimension unit injection,
  math expression policy, HSL evaluation, transparent normalisation, $themes/$metadata
  extraction). Verified against Laura's 305-token file: 148 tokens parse clean in
  single-set mode; remaining sets fail only on expected cross-set refs or preserved math
  expressions. 179/179 tests pass. Merged 2026-05-03.
- [x] **Multi-set resolution** — `Api.importTokensStudio (config: ShimConfig) (jsonText: string)` merges all
  sets in `tokenSetOrder` via `Resolver.resolve` with `Inline` sources; partial-success: returns
  `TokensStudioImportResult` with resolved tokens + `SetSkipped`/`TokenUnresolved` warnings.
  Laura's file: 179 resolved tokens, 1 skipped set (`Foundations/Base` — math expressions),
  57 unresolved (spacing/sizing/typography/stroke refs into skipped set). 191 tests pass. 2026-05-03.
- [x] **Math expression evaluator** — `MathPolicy.EvaluateMath` (new default): recursive-descent
  evaluator inside the shim resolves `round({base} * pow({multiplier}, N))` and `16 * {zoom}`
  forms to concrete floats at shim time. Supports `round/pow/ceil/floor/abs/sqrt/min/max/sin/cos/tan/
  asin/acos/atan/atan2/log/log2/log10/exp` and `+/-/*/^` operators; alias resolution with cycle
  detection via flat index. `isAlias` tightened to match pure `{path}` only (not compound
  expressions). `TUnknown` token for explicit failure on unrecognised characters (no silent skips).
  Laura results after ADR-020 shim fix: 250 resolved tokens (was 232), 0 skipped sets, 0 unresolved
  (was 18). Penpot TokenScript operator audit confirmed `^` = `Math.pow` and trig/log functions
  are all natively supported — our shim is a superset. 208 tests pass. 2026-05-04.
- [x] **Token round-trip** — push `ivanthegeek.tokens.json` into Penpot and read it back.
  REST path (b) complete: 27/27 tokens pushed via `set-token-set` + `set-token` change ops;
  read back via both `get-file` and MCP Plugin API — exact match. Type name corrections
  documented in `penpot-api.md`. MCP Plugin API is read-only (no write surface in 2.14.4).
  Findings → `phase2-findings.md`. 2026-05-04.
- [x] **MCP coverage query** — MCP Plugin API coverage query verified: `findShapesUsingToken`
  and full `coverageMap` pattern documented. Key finding: `applyToShapes`/`applyToSelected`
  are silent no-ops; REST `mod-obj` with `~:applied-tokens` set op is the correct write path.
  Shapes created + bound via REST; read back via `shape.tokens` confirmed. 2026-05-04.
- [x] **Theme-aware CSS emitter** — `Api.importTokensStudioThemed (config) (themeNames) (json)`
  resolves base (global) sets and per-theme sets separately; `CssEmitter.emitThemed`
  emits `:root` + `[data-theme="X"]` override blocks for each named theme's diffs.
  12 new tests (8 unit + 4 Laura Light/Dark integration). 203/203 pass. 2026-05-04.
- [x] **Penpot export comparison (Phase 4b)** — compare three paths: (a) MCP `export_shape`
  → SVG, (b) Inspect tab HTML/CSS (Code view), (c) raw API shapes → our CSS emitter.
  Key finding: all three paths lose token names; only Path C preserves them as CSS custom
  properties. Missing step identified: shape-to-component CSS generator using `shape.tokens`.
  Findings → `LauraExperiment/phase4b-findings.md`. 2026-05-04.
- [x] **`$ref` JSON Pointer support in Resolver** — RFC 6901 same-document pointer resolution
  in `parseTokenSource`; `resolveJsonPointer` walks `#/path/to/node` with `~0`/`~1` escaping
  and array index support; `$ref → $ref` chains resolve transitively. Root `JsonObject` threaded
  through 5 private parse functions so `$defs` is available as a pointer target. 5 new tests.
  213 tests pass. 2026-05-04.
- [x] **Tokens Studio export** (`toResolverDocument` + `exportToTokensStudio`) — ADR-021 and
  ADR-022. `toResolverDocument` maps `$themes`/`$metadata` to a DTCG `ResolverDocument`:
  modifier groups from `group` fields, varying-set detection per modifier group, global sets
  (not mentioned in any theme) as base `SetRef`s. `exportToTokensStudio` serialises DTCG token
  files back to Tokens Studio JSON (preserve-aliases path): alias refs kept as `{path}` strings,
  sRGB colors → `#rrggbb` hex (lossless), wide-gamut → hex + `$description` annotation +
  `ExportWarning.LossyColorConversion`. `$themes` and `$metadata` reconstructed from `ShimResult`.
  Public API: `Api.exportTokensStudio`, `Api.toResolverDocument`, `formatExportWarning`.
  9 new tests. 222/222 pass. 2026-05-04.
- [x] **Penpot `$extensions` preservation test** — pre-ADR-023 empirical check.
  Result: Penpot strips `$extensions` (Plugin API Token has only `id/name/type/value/description`;
  `.extensions` assignment doesn't persist; internal transit storage has no extensions slot;
  export format omits them). Findings → `penpot-extensions-preservation-test.md`;
  upstream tracking issue: <https://github.com/penpot/penpot/issues/9307>. 2026-05-04.
- [x] **`$extensions` carrier for lossy color metadata (ADR-023)** — supersedes ADR-022
  carrier choice. Exporter emits both `$extensions[com.fntools.designtokens][originalColor]`
  (structured DTCG payload, vendor-namespaced) **and** the existing `$description`
  annotation (Penpot-survival companion). Importer recovers in priority order: extension →
  description regex → lossy sRGB hex. User-authored vendor extensions pass through verbatim.
  Incidental fix: shim now inherits `$type` from group level per DTCG §7.4. 5 new round-trip
  tests covering all four scenarios + extensions passthrough. 227/227 tests pass. 2026-05-04.
- [x] **Laura Dashboard semantic token push (Phase 2 Part 2)** — `laura-light-desktop` set
  (35 tokens: 15 colors, 13 dimensions, 6 typography + 1 stroke) pushed to Design mocks.
  All 120 bound shapes verified. Local set last-in-order wins over System Library. 2026-05-04.
  Key findings: new REST API format (`set-id`/`token-id` UUIDs required); math-evaluator
  theme-bleed bug; dimension unit stripping. See `LauraExperiment/phase2-findings.md` Part 2.
- [x] **Math-evaluator theme-bleed fix** — `EvaluateMath` evaluated math at shim time using
  the full multi-set token index; last set in `tokenSetOrder` won for each alias path, making
  `Text zoom/200%` (zoom=2) override all themes. Fix: `shimSingleFileWithMathIndex` builds the
  alias-resolution index from only the active-set list; `importTokensStudioThemed` calls it once
  per theme (and once for base sets) with the per-theme set list. `shimCore` private helper
  factors the shared logic. 3 new tests (math-bleed fixture + Small=10/Large=20 assertions).
  230/230 tests pass. 2026-05-04.
- [x] **Variant-set math index filtering + `MathEvalFailedVariantAlias` (ADR-024)** — `shimSingleFile`
  auto-detects theme-variant sets from `$themes` (per-group detection matching `toResolverDocument`).
  Two-index architecture: `globalIndex` (all sets, used for HSL/typography alias resolution) and
  `mathIndex` (excludes variant sets, used for `MathEval.tryEval` only). Math evaluation failures
  referencing variant aliases emit `MathEvalFailedVariantAlias` with hint:
  "use `Api.importTokensStudioThemed` for correct per-theme resolution". Laura flat import:
  204 resolved tokens (was 250, theme-bleed inflated), 36 unresolved (spacing/radius/sizing depend
  on failed scale tokens), 0 sets skipped, 143 color tokens unchanged (HSL still uses globalIndex).
  3 new tests. 233/233 tests pass. 2026-05-04.
- [x] **Token flow inward (Phase 3)** — `shimSingleFile` + `Format.parse` on all 22 sets of
  Laura's file: 22/22 parse cleanly, 0 warnings. 305 tokens (250 after flat last-set-wins import).
  Type distribution: color 174, number 57, dimension 47, typography 18, fontFamily 9. Structural
  losses documented: math expression strings (evaluated to floats, originals discarded), multi-set
  cascade semantics, cross-set alias chains. `$themes` + `$metadata` preserved in ShimResult →
  recoverable via `toResolverDocument` / `exportToTokensStudio`. Key finding: combined zoom +
  breakpoint per-theme call needed for correct scale spread. See `LauraExperiment/phase3-findings.md`.
  2026-05-04.
- [x] **Phase 4 CSS verification + `@media` breakpoints** — `emitBlock` updated to detect
  `@`-prefixed selectors and nest declarations in inner `:root { }` (valid CSS for media
  queries). 4 new tests. Bug found + fixed: `buildFlatIndex` used JSON property order instead
  of `tokenSetOrder` — `Foundations/Base` was last in Laura's JSON (position 21/22), causing
  `multiplier=1` to overwrite Desktop's `1.25` → all scale tokens were 16. Fix: sort `sets`
  by tokenSetOrder in `shimCore` before any index build. Scale tokens now verify correctly
  (8/10/13/16/20/25/31/39/49). Color discrepancy documented: HSL uses globalIndex (Eco Tools
  hue wins), phase2 push used Core hue manually. Responsive CSS written to
  `scripts/phase4-output.css`. Findings → `phase4b-verification-findings.md`. 251/251 pass.
  2026-05-04.
- [x] **`importTokensStudioCombined` (ADR-025)** — new API for combining themes from different
  modifier groups into a single resolution context. Fixes cross-group math bleed: uses ALL
  themes (not just active) for `allThemeSets` computation, so sets from non-requested groups
  are never mistaken for base sets. Returns flat `TokensStudioImportResult`. 5 new tests.
  248/248 pass. 2026-05-04.
- [ ] **PATHS state mapping (Phase 7)** — read prototype connections on all mock pages;
  map each screen to a PATHS state and each connection to a transition; document what
  information Penpot carries vs what PATHS needs.

### CSS ingestion — non-standard unit handling

- [x] `CssIngest`: values with non-DTCG units (`em`, `%`, `vw`, `vh`, etc.) now emit an explicit `Skipped` warning instead of silently stripping the unit and emitting a bare `number` token. Fix: remove `em`-stripping from `tryParseNumber`; updated test. 2026-05-04.
- [x] `CssAudit`: `CssNative` value type for values that are valid CSS but not DTCG-tokenisable
  (`em`/`%`/`vw`/`vh`/`ch` relative units and `clamp()`/`calc()` expressions). Previously `Unknown`
  and silently excluded; now surface as a named category so the bootstrap workflow can route them to
  the component layer explicitly. `fr` noted unreachable via `designProperties` (grid-specific).
  10 new tests. 243/243 pass. 2026-05-04.

### FnHCI non-visual targets

- [ ] ConsoleTokens / TuiTokens / ThermalTokens / BrailleTokens domain design
- [ ] TOML authoring format + parser
- [ ] ADR-001 (FnHCI): Fun.Css vs FSS — record CSS binding choice for Fun.Blazor consumers once the FnHCI project has a LOGOS directory

### NuGet

- [ ] CI publish on tag (deferred until first stable release)
