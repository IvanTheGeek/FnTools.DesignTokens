---
area: Experiment
status: active — 2026-05-03
---

# Laura Experiment — Open Questions

Questions the experiment needs to answer, grouped by the channel they belong to.

---

## Penpot → reading

- What does a Penpot component (frame + variants) look like via MCP/REST? Can we derive
  Fun.Blazor component parameter types from variant structure?
- Do `appliedTokens` on shapes give us enough information to know which CSS custom properties
  a component needs to reference?
- How are Penpot component instances vs masters represented in the shape tree?
- Can we read prototype connections (frame A → frame B on click) and what information do they
  carry? Is there enough to inform routing/navigation structure?

## Token flow — outward (model → Penpot)

- Can we push a DTCG 2025.10 token set (via `serializePenpot` adapter + REST `set-token`/
  `set-token-set`) and have Penpot immediately reflect the change on shapes with `appliedTokens`
  referencing those token paths?
- What happens to shapes whose `appliedTokens` reference a token path that no longer exists
  after a push?
- Can we push a complete multi-set library (multiple sets + themes) via REST, or only one set
  at a time?

## Token flow — inward (Penpot → model)

- When a design decision changes in Penpot (token value, component structure), what does the
  diff look like via `get-file`? Is it detectable without a full file comparison?
- Can we reconstruct a DTCG 2025.10 token file from a Penpot `tokens.json` export for the
  DTCG-compatible subset (colors, dimensions, fontFamily — excluding math expressions)?
- What is lost in the inward direction that cannot be expressed in DTCG 2025.10?
  (Math expressions, HSL aliases, multi-set `$themes` structure.)

## CSS emitter — token → Fun.Blazor

- What does the `:root` CSS block look like for the Dashboard screen's tokens under a given
  theme combination (e.g. Light + Desktop)?
- How do breakpoint theme overrides work in CSS — `:root` + `@media` query override blocks,
  or something else?
- How does a Fun.Blazor component reference CSS custom properties? Is it inline `style`
  attributes, a CSS class, or a generated stylesheet?
- What is the ergonomic pattern for typed token bindings in Fun.Blazor — does the component
  know token names, or only resolved CSS var names?

## Codec boundary

- Which parts of the flow require FnTools.DesignTokens logic (parse, resolve, serialize)?
- Which parts are purely application-layer glue (reading Penpot shapes, generating Fun.Blazor
  component scaffolding)?
- What does FnTools.DesignTokens need to add to support this flow that it does not have today?

## FnHCI boundary

- Where exactly does the codec stop and FnHCI begin in the CSS emission path?
- Is the CSS emitter (`:root` + theme overrides) a codec concern or FnHCI concern?
- What does FnHCI need to own that is not token-format logic?

## NEXUS implications

- What concepts does the model need to be able to express before Penpot becomes a useful
  interface for it? (Component variants, state transitions, navigation intent, data bindings?)
- What information is in Penpot that has no representation in a token format at all?
  (Layout constraints, component composition, interaction triggers.)
- What is the minimal stub of a NEXUS model sufficient to drive one Penpot screen?
