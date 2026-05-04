## Active / Next

> Phases 1–5 complete — see `tasks-completed.md` and `work-completed.md`. Detailed plan for items below: `work-planned.md`.

### Penpot × Tokens Studio learning experiments

Analysis file: `penpot-tokens-studio-format.md`. The Design mocks.penpot file from Laura's
demo is in `/home/ivan/ARCHIVE/Penpot-DesignTokens/`. Primary interaction is via the
**MCP server + browser extension** — not the REST API (broken for tokens) and not file
import/export. See `penpot-api.md` for the three-surface comparison.

- [ ] **Tokens Studio parser shim** — read a Tokens Studio `tokens.json`, strip non-DTCG types
  (`fontFamilies`, `fontSizes`, `spacing`, `borderWidth`) and math/HSL expressions, return
  parseable DTCG JSON per set. Use Laura's file as test input. This is a separate function
  (not `Format.parse`) — it lives in a `TokensStudio` module.
- [ ] **Token round-trip** — push `ivanthegeek.tokens.json` into Penpot and read it back.
  Two paths to compare: (a) MCP `execute_code` with `penpot.tokens` Plugin API, (b) REST
  `update-file` with `set-token-set` + `set-token` change ops. Both verified working in
  2.14.4 — REST uses transit+json, MCP uses JS. See `penpot-api.md` REST token change types.
- [ ] **MCP coverage query** — after pushing tokens, use the MCP to query which shapes in a
  test frame have `appliedTokens` referencing a given token path; validate coverage.
- [ ] **Theme-aware CSS emitter** — given a list of active theme names, resolve the multi-set
  merge in our resolver and emit `:root` + override blocks. Maps Tokens Studio `$themes`
  activation to our resolver's `resolutionOrder`.
- [ ] **Penpot export comparison (Phase 4b)** — compare three paths for getting component
  code out of Penpot: (a) MCP `export_shape` → SVG, (b) Inspect tab HTML/CSS, (c) raw API
  shapes → our CSS emitter. Document tradeoffs. See `LauraExperiment/plan.md` Phase 4b.
- [ ] **PATHS state mapping (Phase 7)** — read prototype connections on all mock pages;
  map each screen to a PATHS state and each connection to a transition; document what
  information Penpot carries vs what PATHS needs.

### CSS ingestion — non-standard unit handling

- [ ] `CssIngest`: when a CSS custom-property value uses a non-DTCG unit (`em`, `%`, `vw`, `vh`, etc.), emit an explicit `Skipped` warning instead of silently stripping the unit and emitting a bare `number` token. Callers need to know the value was degraded.
- [ ] `CssAudit`: add a future `CssNative` value type for values that are valid CSS but not DTCG-tokenisable (`em`/`%`/`vw`/`vh`/`ch`/`fr` dimensions, CSS `clamp()`/`calc()` expressions). Currently they are `Unknown` and excluded; surfacing them as a named category lets the bootstrap workflow direct them to the component layer explicitly.

### FnHCI non-visual targets

- [ ] ConsoleTokens / TuiTokens / ThermalTokens / BrailleTokens domain design
- [ ] TOML authoring format + parser
- [ ] ADR-001 (FnHCI): Fun.Css vs FSS — record CSS binding choice for Fun.Blazor consumers once the FnHCI project has a LOGOS directory

### NuGet

- [ ] CI publish on tag (deferred until first stable release)
