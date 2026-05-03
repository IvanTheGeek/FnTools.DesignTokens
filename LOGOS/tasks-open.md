## Active / Next

> Phases 1–5 complete — see `tasks-completed.md` and `work-completed.md`. Detailed plan for items below: `work-planned.md`.

### CSS ingestion — non-standard unit handling

- [ ] `CssIngest`: when a CSS custom-property value uses a non-DTCG unit (`em`, `%`, `vw`, `vh`, etc.), emit an explicit `Skipped` warning instead of silently stripping the unit and emitting a bare `number` token. Callers need to know the value was degraded.
- [ ] `CssAudit`: add a future `CssNative` value type for values that are valid CSS but not DTCG-tokenisable (`em`/`%`/`vw`/`vh`/`ch`/`fr` dimensions, CSS `clamp()`/`calc()` expressions). Currently they are `Unknown` and excluded; surfacing them as a named category lets the bootstrap workflow direct them to the component layer explicitly.

### FnHCI non-visual targets

- [ ] ConsoleTokens / TuiTokens / ThermalTokens / BrailleTokens domain design
- [ ] TOML authoring format + parser
- [ ] ADR-001 (FnHCI): Fun.Css vs FSS — record CSS binding choice for Fun.Blazor consumers once the FnHCI project has a LOGOS directory

### NuGet

- [ ] CI publish on tag (deferred until first stable release)
