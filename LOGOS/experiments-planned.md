---
area: Experiments
status: planned — 2026-05-02
---

# Experiments Planned

These are concrete experiments with a specific hypothesis, method, and expected result. Each one resolves an uncertainty that would otherwise require a downstream assumption.

---

## EXP-01: Penpot HTML import round-trip

**Status**: planned

**Hypothesis**: A rendered Fun.Blazor HTML page can be imported into Penpot as a design canvas, allowing visual refinement in Penpot followed by structured extraction back to Fun.Blazor.

**Why it matters**: If Penpot HTML import works, the design loop is: write F# → render → Penpot refinement → Fun.Blazor update. If it does not work, the loop goes: Penpot SVG design → Fun.Blazor reconstruction from SVG reference.

**Method**:
1. Render `LaundryLog.UI` entry form page to a static HTML file (e.g., `dotnet run` + screenshot, or prerender)
2. Import the HTML into Penpot via File > Import
3. Verify: are layout, typography, and colors reflected accurately?
4. Make a change in Penpot (e.g., adjust a color or move a component)
5. Export to SVG; attempt to extract the delta back to Fun.Blazor

**Expected result**: Either a usable round-trip workflow with documented limitations, or a clear statement of what Penpot HTML import cannot do.

**Notes**: Penpot announced HTML import in early 2026. Not tested. SVG export is reliable and is the fallback.

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

**Status**: planned

**Hypothesis**: Penpot can import a DTCG `.tokens.json` file and expose the token values as Penpot variables, which can then be applied to component fills, strokes, and text styles.

**Why it matters**: If Penpot variables can be driven by DTCG files, design changes in Penpot token values propagate to the exported CSS/SVG. The token file is the shared source of truth for both tools.

**Method**:
1. Export `ll.tokens.json` from the LaundryLog token set
2. Import into Penpot via the tokens panel
3. Apply a token variable to a component fill
4. Change the token value in Penpot, re-export to DTCG, verify the change is in the exported file
5. Round-trip: re-import the exported file through `Format.parse`

**Expected result**: Token values are editable in Penpot and exportable back to valid DTCG JSON.
