## Active / Next

> Phases 1–5 complete — see `tasks-completed.md` and `work-completed.md`. Detailed plan for items below: `work-planned.md`.

### CSS ingestion tool

- [x] Parse CSS custom properties from an HTML/CSS file → DTCG `.tokens.json`
- [x] Primary test: LaundryLog design system HTML → `cb.tokens.json` + `ll.tokens.json`
- [x] Round-trip: `Format.parse` the output with zero errors

### FnHCI non-visual targets

- [ ] ConsoleTokens / TuiTokens / ThermalTokens / BrailleTokens domain design
- [ ] TOML authoring format + parser
- [ ] ADR-001 (FnHCI): Fun.Css vs FSS — record CSS binding choice for Fun.Blazor consumers once the FnHCI project has a LOGOS directory

### `#RRGGBBAA` alpha hex — silent failure in upgrade path

- [x] `upgradeStringValues` (Format.fs) only handles 7-char `#RRGGBB` (`s.Length = 7`)
- [x] 9-char `#RRGGBBAA` falls through silently — no error, no upgrade, token silently dropped
- [x] Fix: parse alpha from last 2 hex digits and set `Alpha`
- [x] Add test: parse Second Editor's Draft file with `#RRGGBBAA` color → correct alpha and hex preserved

### Spec-version export / downgrade

- [x] Add a single-case DU `type ExportLossAcknowledged = | IAcceptDataLoss` — required parameter on any lossy export path
- [x] Signature: `serializeAs : SpecVersion -> ExportLossAcknowledged -> TokenFile -> string`
      vs. lossless: `serialize : TokenFile -> string` — the type difference is the contract
- [x] Caller must literally write `IAcceptDataLoss` at the call site; cannot be a variable, config flag, or condition
- [x] Second Editor's Draft emitter: write color `$value` as hex string (stored Hex field first, then computed from SRGB; OKLCH falls back to V2025_10 object form)
- [ ] Penpot adapter = thin shim on Second Editor's Draft emitter: strip `$schema` ✓ (already done), add set-name wrapper — deferred
- [ ] Style Dictionary: consumer of DTCG, not a format target — no adapter needed
- [ ] Figma Variables API: completely different schema (REST CRUD, not DTCG) — separate integration if ever needed

### NuGet

- [ ] CI publish on tag (deferred until first stable release)
