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
- [ ] **Penpot export comparison (Phase 4b)** — compare three paths for getting component
  code out of Penpot: (a) MCP `export_shape` → SVG, (b) Inspect tab HTML/CSS, (c) raw API
  shapes → our CSS emitter. Document tradeoffs. See `LauraExperiment/plan.md` Phase 4b.
- [ ] **PATHS state mapping (Phase 7)** — read prototype connections on all mock pages;
  map each screen to a PATHS state and each connection to a transition; document what
  information Penpot carries vs what PATHS needs.

### CSS ingestion — non-standard unit handling

- [x] `CssIngest`: values with non-DTCG units (`em`, `%`, `vw`, `vh`, etc.) now emit an explicit `Skipped` warning instead of silently stripping the unit and emitting a bare `number` token. Fix: remove `em`-stripping from `tryParseNumber`; updated test. 2026-05-04.
- [ ] `CssAudit`: add a future `CssNative` value type for values that are valid CSS but not DTCG-tokenisable (`em`/`%`/`vw`/`vh`/`ch`/`fr` dimensions, CSS `clamp()`/`calc()` expressions). Currently they are `Unknown` and excluded; surfacing them as a named category lets the bootstrap workflow direct them to the component layer explicitly.

### FnHCI non-visual targets

- [ ] ConsoleTokens / TuiTokens / ThermalTokens / BrailleTokens domain design
- [ ] TOML authoring format + parser
- [ ] ADR-001 (FnHCI): Fun.Css vs FSS — record CSS binding choice for Fun.Blazor consumers once the FnHCI project has a LOGOS directory

### NuGet

- [ ] CI publish on tag (deferred until first stable release)
