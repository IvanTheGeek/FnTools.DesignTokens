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

- [ ] Read the Dashboard page shape tree via MCP — list all components, their `appliedTokens`,
      and their variant structure
- [ ] Identify which Penpot components map naturally to Fun.Blazor components
      (strong 1:1) vs which are layout/composition only
- [ ] Document what a Penpot component's variant set tells us about Fun.Blazor parameter types
- [ ] Read the token library via REST `get-file` — confirm the Tokens Studio multi-set
      structure matches what we extracted from the archive
- [ ] Read prototype connections on the Dashboard page — document what navigation intent is
      expressed

Findings → `phase1-findings.md`

---

## Phase 2 — Token flow outward (our tokens → Penpot)

**Goal**: Push a DTCG 2025.10 token set into Penpot and verify it applies to shapes.

- [ ] Define a minimal DTCG 2025.10 token file mirroring Laura's semantic structure for the
      Dashboard screen — concrete values, no math expressions, Light + Desktop theme only
- [ ] Use `serializePenpot` adapter to produce the Penpot-compatible format
- [ ] Push via REST `set-token-set` + `set-token` change ops
- [ ] Verify via MCP that shapes on the Dashboard page have `appliedTokens` resolving to the
      new values
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

- [ ] Implement theme-aware CSS emitter (open task in `tasks-open.md`): given active theme
      names, resolve multi-set merge and emit `:root` + `@media` override blocks
- [ ] Target: Light mode + Desktop breakpoint as the baseline `:root`; Dark mode as an
      override; Mobile as a `@media (max-width: 768px)` override
- [ ] Verify emitted CSS matches Penpot's resolved values for those themes (read via REST)

Findings → `phase4-findings.md`

---

## Phase 5 — Fun.Blazor components (tokens → working UI)

**Goal**: Build Fun.Blazor components for the Dashboard screen that reference the emitted
CSS custom properties. Verify they visually match the Penpot design.

- [ ] Stand up a minimal Fun.Blazor project (separate repo)
- [ ] Scaffold one component from the Dashboard — start with the simplest card
- [ ] Wire the CSS emission from Phase 4 into the project (static file or generated)
- [ ] Work through the component list, documenting where the token binding pattern holds
      and where it breaks

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

## Phase 7 — Prototype path (forward-looking)

**Goal**: Assess whether Penpot prototype connections can inform Fun.Blazor routing.

- [ ] Read all prototype connections on the Dashboard and other mock pages
- [ ] Map to Blazor router structure — what pages, what navigation events
- [ ] Document what prototype information is missing or insufficient for routing decisions

Findings → `phase7-findings.md`

---

## Synthesis

After Phase 6 (or incrementally):

- [ ] `codec-gaps.md` — what FnTools.DesignTokens needs to add
- [ ] `fnhci-gaps.md` — what FnHCI needs to own that no codec addresses
- [ ] `nexus-implications.md` — what the model needs to express before Penpot is a useful
      interface for it
