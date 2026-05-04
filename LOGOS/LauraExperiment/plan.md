---
area: Experiment
status: active — 2026-05-03
---

# Laura Experiment — Plan

Phased. Each phase produces findings that inform the next. No phase requires the previous
to be fully complete before starting — some can run in parallel once the reading phase
establishes a baseline.

---

## Phase 1 — Read (Penpot → understanding)

**Goal**: Establish what the MCP/REST gives us for components and token bindings. No code
written — just reading and documenting.

- [x] Read the Dashboard page shape tree via MCP — list all components, their `appliedTokens`,
      and their variant structure
- [x] Identify which Penpot components map naturally to Fun.Blazor components
      (strong 1:1) vs which are layout/composition only
- [x] Document what a Penpot component's variant set tells us about Fun.Blazor parameter types
- [x] Read the token library via REST `get-file` — confirm the Tokens Studio multi-set
      structure matches what we extracted from the archive
- [x] Read prototype connections on the Dashboard page — document what navigation intent is
      expressed (none found — static mockups only)

Findings → `phase1-findings.md`

---

## Phase 2 — Token flow outward (our tokens → Penpot)

**Goal**: Push a DTCG 2025.10 token set into Penpot and verify it applies to shapes.

- [x] Push `ivanthegeek.tokens.json` (DTCG 2025.10) via REST `set-token-set` + `set-token`.
      27/27 tokens pushed and read back (REST + MCP Plugin API). Type name map documented.
      Plugin API is read-only — no write surface in 2.14.4. 2026-05-04.
- [ ] Define a DTCG token file mirroring Laura's semantic structure for the Dashboard screen
      and push it; verify via MCP that shapes have `appliedTokens` resolving to the new values
- [ ] Document what broke: missing paths, format mismatches, schema gaps

Findings → `phase2-findings.md`

---

## Phase 3 — Token flow inward (Penpot → our tokens)

**Goal**: Read Penpot's token library and reconstruct a DTCG-compatible subset.

- [ ] Use the Tokens Studio parser shim (or a stub) to extract DTCG-compatible tokens from
      Laura's `tokens.json` — colors, dimensions, fontFamily only; skip math/HSL expressions
- [ ] Verify extracted tokens parse cleanly through `Format.parse`
- [ ] Document what is lost: math expressions, HSL aliases, multi-set `$themes`

Findings → `phase3-findings.md`

---

## Phase 4 — CSS emission (tokens → Fun.Blazor surface)

**Goal**: Produce the CSS custom property declarations a Fun.Blazor component needs for the
Dashboard screen.

- [x] Implement theme-aware CSS emitter: `importTokensStudioThemed` + `CssEmitter.emitThemed`.
      Base/theme partition by set membership in any active theme's `selectedTokenSets`.
      Emits `:root` + caller-supplied selector override blocks (diff-only). 2026-05-04.
- [ ] Target: breakpoint variants as `@media` overrides (Mobile 360px, Tablet 1020px) —
      Phase 5 component concern; the emitter architecture supports it via `selectorForTheme`.
- [ ] Verify emitted CSS matches Penpot's resolved values for those themes (read via REST)

Findings → `phase4-findings.md`

---

## Phase 4b — Penpot export comparison

**Goal**: Compare Penpot's own export paths against our API-derived approach to understand
what each gives us and where gaps are.

- [x] **SVG export** — MCP `export_shape` on `pattern / card`; all tokens resolved to
      RGB/px; font/image URLs internal; layout absent; zero token traceability. 2026-05-04.
- [x] **Inspect tab HTML/CSS** — Code view: all tokens resolved to hex/px, UUID class names,
      localhost URLs. Styles view: hybrid — radius/strokeWidth token names preserved,
      colors/spacing resolved. 2026-05-04.
- [x] **Raw API → our emitter** — produces CSS custom properties with full token names;
      theme-switchable at runtime; missing step: shape-to-component CSS generator that
      combines `shape.tokens` bindings with layout geometry. 2026-05-04.
- [x] Document the tradeoffs: when would you use each path?

Findings → `phase4b-findings.md`

---

## Phase 5 — Fun.Blazor components (tokens → working UI)

**Goal**: Build Fun.Blazor components for the Dashboard screen that reference the emitted
CSS custom properties. Each screen in the mocks represents a specific application state —
the collection of screens maps to PATHS states, and prototype connections map to PATHS
transitions. The components built here are the implementation of those states.

- [ ] Stand up a minimal Fun.Blazor project (separate repo)
- [ ] Scaffold one component from the Dashboard — start with the simplest card
- [ ] Wire the CSS emission from Phase 4 into the project (static file or generated)
- [ ] Work through the component list, documenting where the token binding pattern holds
      and where it breaks
- [ ] Identify which screens map to distinct PATHS states and which are variants of the
      same state (e.g. breakpoint versions of the same page)

Findings → `phase5-findings.md`

---

## Phase 6 — Bidirectional validation

**Goal**: Make a change in one direction and verify it flows through.

- [ ] Change a token value in our DTCG file → push via REST → verify Penpot updates
- [ ] Change a token value in Penpot → read via REST → verify our DTCG file can be updated
- [ ] Change a component variant in Penpot → read via MCP → assess what would need to change
      in the Fun.Blazor component

Findings → `phase6-findings.md`

---

## Phase 7 — Prototype path → PATHS states

**Goal**: Assess whether Penpot prototype connections can inform PATHS state definitions
and Fun.Blazor routing.

Each screen in the Design Mocks file is a specific application state. Prototype connections
between screens express transitions — what happens when a user clicks a button or navigates.
This maps directly to the PATHS concept: states + transitions form the navigable graph of
the application.

- [ ] Read all prototype connections on all mock pages (Dashboard, Landing, Email, Thumbnail)
- [ ] Map each connection to a PATHS transition: source state, trigger, target state
- [ ] Identify what information is present in a Penpot prototype connection vs what a PATHS
      transition needs (trigger type, guard conditions, data carried)
- [ ] Document what is missing: conditions, data bindings, back-navigation, error states
- [ ] Assess whether Penpot prototype authoring could be a PATHS input surface or only
      a documentation layer

Findings → `phase7-findings.md`

---

## Synthesis

After Phase 6 (or incrementally):

- [ ] `codec-gaps.md` — what FnTools.DesignTokens needs to add
- [ ] `fnhci-gaps.md` — what FnHCI needs to own that no codec addresses
- [ ] `nexus-implications.md` — what the model needs to express before Penpot is a useful
      interface for it
