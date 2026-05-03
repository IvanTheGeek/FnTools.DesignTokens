## Active / Next

> Phases 1–5 complete — see `tasks-completed.md` and `work-completed.md`. Detailed plan for items below: `work-planned.md`.

### Penpot × Token Studio learning experiments

Analysis file: `penpot-token-studio-format.md`. The Design mocks.penpot file from Laura's
demo is in `/home/ivan/ARCHIVE/Penpot-DesignTokens/`. Primary interaction is via **Penpot
APIs** (REST + Plugins API + MCP server), not file import/export.

- [ ] **Token Studio parser shim** — read a Token Studio `tokens.json`, strip non-DTCG types
  (`fontFamilies`, `fontSizes`, `spacing`, `borderWidth`) and math/HSL expressions, return
  parseable DTCG JSON per set. Use Laura's file as test input. This is a separate function
  (not `Format.parse`) — it lives in a `TokenStudio` module.
- [ ] **API round-trip test** — push `ivanthegeek.tokens.json` via Penpot REST API, then read
  it back and verify the token values round-trip correctly through Penpot's internal storage.
- [ ] **MCP verification** — after pushing tokens via API, use the Penpot MCP server to query
  which shapes use a given token; validate design system coverage.
- [ ] **Theme-aware CSS emitter** — given a list of active theme names, resolve the multi-set
  merge in our resolver and emit `:root` + override blocks. Maps Token Studio `$themes`
  activation to our resolver's `resolutionOrder`.

### CSS ingestion — non-standard unit handling

### CSS ingestion — non-standard unit handling

- [ ] `CssIngest`: when a CSS custom-property value uses a non-DTCG unit (`em`, `%`, `vw`, `vh`, etc.), emit an explicit `Skipped` warning instead of silently stripping the unit and emitting a bare `number` token. Callers need to know the value was degraded.
- [ ] `CssAudit`: add a future `CssNative` value type for values that are valid CSS but not DTCG-tokenisable (`em`/`%`/`vw`/`vh`/`ch`/`fr` dimensions, CSS `clamp()`/`calc()` expressions). Currently they are `Unknown` and excluded; surfacing them as a named category lets the bootstrap workflow direct them to the component layer explicitly.

### FnHCI non-visual targets

- [ ] ConsoleTokens / TuiTokens / ThermalTokens / BrailleTokens domain design
- [ ] TOML authoring format + parser
- [ ] ADR-001 (FnHCI): Fun.Css vs FSS — record CSS binding choice for Fun.Blazor consumers once the FnHCI project has a LOGOS directory

### NuGet

- [ ] CI publish on tag (deferred until first stable release)
