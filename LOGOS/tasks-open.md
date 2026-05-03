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

### `#RRGGBBAA` alpha hex — silent failure in upgrade path

- [ ] `upgradeStringValues` (Format.fs) only handles 7-char `#RRGGBB` (`s.Length = 7`)
- [ ] 9-char `#RRGGBBAA` falls through silently — no error, no upgrade, token silently dropped
- [ ] Fix: either return `Error (InvalidValue ...)`, or parse alpha from last 2 hex digits and set `Alpha`
- [ ] Add test: parse Second Editor's Draft file with `#RRGGBBAA` color → expect error or correct alpha

### Spec-version export / downgrade

- [ ] Add a single-case DU `type ExportLossAcknowledged = | IAcceptDataLoss` — required parameter on any lossy export path
- [ ] Signature: `serializeAs : SpecVersion -> ExportLossAcknowledged -> TokenDocument -> JsonNode`
      vs. lossless: `serialize : TokenDocument -> JsonNode` — the type difference is the contract
- [ ] Caller must literally write `IAcceptDataLoss` at the call site; cannot be a variable, config flag, or condition
- [ ] Second Editor's Draft emitter: write color `$value` as hex string (`$value.hex` fallback, or gamut-map if missing)
- [ ] Penpot adapter = thin shim on Second Editor's Draft emitter: strip `$schema`, add set-name wrapper
- [ ] Style Dictionary: consumer of DTCG, not a format target — no adapter needed
- [ ] Figma Variables API: completely different schema (REST CRUD, not DTCG) — separate integration if ever needed

### NuGet

- [ ] CI publish on tag (deferred until first stable release)
