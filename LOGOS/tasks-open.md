## Active / Next

> Phases 1–5 complete — see `tasks-completed.md` and `work-completed.md`. Detailed plan for items below: `work-planned.md`.

### CSS ingestion tool

- [ ] Parse CSS custom properties from an HTML/CSS file → DTCG `.tokens.json`
- [ ] Primary test: LaundryLog design system HTML → `cb.tokens.json` + `ll.tokens.json`
- [ ] Round-trip: `Format.parse` the output with zero errors

### Penpot round-trip experiment

- [ ] Import a Fun.Blazor rendered HTML page into Penpot
- [ ] Make a change in Penpot, export SVG, reconstruct in Fun.Blazor
- [ ] Document the workflow and limitations

### FnHCI non-visual targets

- [ ] ConsoleTokens / TuiTokens / ThermalTokens / BrailleTokens domain design
- [ ] TOML authoring format + parser

### NuGet

- [ ] CI publish on tag (deferred until first stable release)
