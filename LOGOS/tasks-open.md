## Active / Next

> Completed work has been moved to `tasks-completed.md`; rich narrative for shipped
> features lives in `work-completed.md`; architectural decisions in `LOGOS/decisions/`
> (index: `LOGOS/decisions/README.md`).

### FnHCI non-visual targets

- [ ] `ConsoleTokens` / `TuiTokens` / `ThermalTokens` / `BrailleTokens` domain design
- [ ] TOML authoring format + parser
- [ ] ADR-001 (FnHCI): Fun.Css vs FSS — record CSS binding choice for Fun.Blazor consumers once the FnHCI project has a LOGOS directory

### Candidate companion packages (deferred — pick up when a consumer needs it)

- [ ] **`FnTools.DesignTokens.SchemaCheck`** — JSON Schema validation against the DTCG `$schema`
  URL. Out of scope for this library (see ADR-013 addendum, 2026-05-10): our domain validation
  is strictly stronger at every type, and the DTCG spec itself notes `$schema` is a courtesy
  convention rather than a compliance requirement. If someone needs schema-compliance
  verification as a separate check, it belongs in a companion package depending on
  `Foundation` only, using the ADR-003 `loadSchema: string -> Result<string, string>` pattern
  for `$ref` resolution, and pulling in a JSON Schema validator dep
  (`JsonSchema.Net` or similar) so this library's selective-dependency posture (ADR-005)
  stays intact.
