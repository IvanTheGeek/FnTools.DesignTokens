## Active / Next

> Phases 1–5 complete — see `tasks-completed.md` and `work-completed.md`. Detailed plan for items below: `work-planned.md`.

### CSS ingestion tool

- [ ] Parse CSS custom properties from an HTML/CSS file → DTCG `.tokens.json`
- [ ] Primary test: LaundryLog design system HTML → `cb.tokens.json` + `ll.tokens.json`
- [ ] Round-trip: `Format.parse` the output with zero errors

### CSS emitter

- [ ] Resolved `ResolvedToken seq` → CSS `:root { --var: value; }` block
- [ ] Dark mode: `[data-theme="dark"] { --var: value; }` override block
- [ ] All 13 token types mapped to correct CSS representation

### Typed F# bindings emitter

- [ ] Resolved tree → `Tokens.*` module with `CssVar` bindings
- [ ] Numeric scale: `500` → `N500` in F# identifier
- [ ] Statically typed — no strings at call site

### Penpot round-trip experiment

- [ ] Import a Fun.Blazor rendered HTML page into Penpot
- [ ] Make a change in Penpot, export SVG, reconstruct in Fun.Blazor
- [ ] Document the workflow and limitations

### FnHCI non-visual targets

- [ ] ConsoleTokens / TuiTokens / ThermalTokens / BrailleTokens domain design
- [ ] TOML authoring format + parser

### NuGet

- [ ] CI publish on tag (deferred until first stable release)
