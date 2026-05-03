---
area: Experiments
status: planned — 2026-05-02
---

# Experiments Planned

These are concrete experiments with a specific hypothesis, method, and expected result. Each one resolves an uncertainty that would otherwise require a downstream assumption.

---

## EXP-01: Penpot HTML import round-trip

**Status**: concluded — hypothesis falsified (2026-05-02)

**Hypothesis**: A rendered Fun.Blazor HTML page can be imported into Penpot as a design canvas.

**Result**: HTML import does not exist in Penpot as of May 2026. It is a community feature
request (open since March 2025) with no implementation. The File menu has no import option
beyond Penpot's own `.penpot` format. The Plugins menu offers only a plugin manager.

The workflow direction is reversed: Penpot → code, not code → Penpot. Penpot's Inspect tab
generates CSS/HTML/SVG from designs.

**What does exist**: A Plugin API (since Penpot 2.3, Nov 2024) could theoretically support
an HTML→Penpot plugin, but none exists as an installable plugin at this time.

**Revised design loop**: design components natively in Penpot using DTCG tokens as variables
(EXP-04), then use the Inspect tab to validate CSS output matches what the F# component
would produce. SVG export from Penpot remains available as a reference.

**Component test artifact**: `/tmp/machine-chips-component.html` — static HTML rendering
of `MachineTypeChips` in all four states (none, washer, dryer, supplies selected) using
DTCG token CSS vars. Renders correctly in browser; Penpot import not possible.

---

## EXP-02: CSS ingestion round-trip

**Status**: planned

**Hypothesis**: The CSS custom-property declarations in the LaundryLog HTML design system handoff file can be reliably converted to valid DTCG 2025.10 token files with zero parse errors.

**Why it matters**: The CSS ingestion tool will be used to bootstrap `cb.tokens.json` + `ll.tokens.json`. If ingestion is lossy or produces invalid DTCG, the bootstrap fails and tokens must be authored from scratch.

**Method**:
1. Run the CSS ingestion tool against `LaundryLog Design System.html`
2. Parse the output files with `FnTools.DesignTokens.Format.parse`
3. Measure: how many tokens ingested, how many type-inference failures, how many validation errors

**Acceptance criteria**:
- Zero `Format.parse` errors on output files
- All OKLCH colors captured with correct component values
- All dimension values captured with correct unit
- No tokens silently dropped

**Notes**: Some CSS properties may not map to DTCG types (e.g., `box-shadow` shorthand → `shadow` composite). Type inference for composite types may need special casing.

---

## EXP-03: Fun.Css CssVar token reference in Fun.Blazor component

**Status**: planned

**Hypothesis**: A Fun.Blazor component can reference emitted `Tokens.*` bindings via Fun.Css `CssVar` and produce correct CSS output, replacing hardcoded class names.

**Why it matters**: This validates the full pipeline end-to-end: DTCG files → emitter → typed bindings → Fun.Blazor component → rendered CSS.

**Method**:
1. Manually write a small `Tokens.fs` module with two or three `CssVar` bindings (before the emitter exists)
2. Update `MachineTypeChips.fs` to reference `Tokens.Color.Machine.washer` instead of `"ll-machine-chip--washer"`
3. Verify: component renders with correct CSS custom property values

**Acceptance criteria**:
- Component compiles with no string-typed token references
- Rendered HTML includes correct `var(--color-machine-washer)` output
- Dark mode: token value changes when `[data-theme="dark"]` is set

---

## EXP-04: Penpot DTCG token variable import

**Status**: priority — run next (2026-05-02)

**Hypothesis**: Penpot can import a DTCG `.tokens.json` file and expose the token values as
Penpot variables, which can then be applied to component fills, strokes, and text styles.

**Why it matters**: Confirmed by a 2026 practitioner article: Penpot supports the W3C DTCG
spec natively — import/export as JSON, multiple themed token sets, light/dark mode. This is
the actual code-design bridge. The TOKENS tab is already visible in the TokenExperiments
workspace.

**Method**:
1. Create a Penpot API token (Penpot UI → profile → Account settings → Access tokens)
   Store at `~/.config/penpot-claude.token`. Configure `PENPOT_TOKEN` env var in
   `.claude/settings.json` — never echo the value in commands.
2. Import `tokens/ll.tokens.json` into Penpot via the TOKENS tab
3. Apply a token variable to a shape fill (e.g., washer teal)
4. Verify the token value is referenced, not hardcoded
5. Export back to DTCG JSON, run through `Format.parse` — verify zero errors
6. Check: does Penpot export in DTCG 2025.10 format or an older variant?

**Known limitations (from research)**:
- Stroke color token application is unreliable in Penpot's current token UI
- Tokens break when changing component variants
- No quick token application from color pickers — manual assignment required
- Can't preview themes side by side (light vs dark)

**Prerequisite**: API token must be created by user in Penpot UI before proceeding.
