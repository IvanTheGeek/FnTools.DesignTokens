---
area: Vision
status: draft
date: 2026-05-02
---

# Design System App Vision

A GUI tool — the design system app — that makes the token/component/decision graph navigable, editable, and connected to the broader ATLAS product model.

## Core Capabilities

### CSS Bootstrap / Migration

The workflow for adopting the design system from an existing CSS codebase. Runs once at adoption time; re-runs during maintenance when new hardcoded values drift in.

**Step 1 — Custom property ingestion** (available today via `CssIngest`):
Extracts `--prefix-*` or all `--*` declarations from any CSS or HTML file and produces valid DTCG 2025.10 JSON. Zero-config for sites already using CSS custom properties.

**Step 2 — Hardcoded value audit** (planned tool):
Scans every CSS rule (not just `:root`) for unique values by type: colors, font sizes, line heights, spacing, radii, durations. Groups by type and frequency of use. Output is an inventory the developer reviews — "these 5 hex values appear in your rules; here is where each one is used." This is the gap between a site that uses CSS vars and one that has a design system.

**Step 3 — Token naming** (human + AI):
Human or AI reviews the rendered page alongside the inventory and assigns token names. "This green appears in all primary buttons and active states → `accent.default`." AI can draft names from selector context and value semantics; human approves or edits. Output is additional entries in `.tokens.json`.

**Step 4 — CSS refactoring** (tool-assisted):
Replaces hardcoded values in rules with the emitted `var(--token-name)` references. Given the token file, the tool proposes find-and-replace operations across the CSS. After this step the tokens are the single source of truth and the hardcoded values are gone.

Steps 1 and 4 are fully automated. Step 2 is automatable (scanner). Step 3 requires judgment — it is inherently a design decision, not a parsing problem. AI accelerates it; a human ratifies it.

---

### Token Management
- View the full resolved token tree: primitive → semantic layers, per token set
- Edit token values and see the CSS and F# output update live
- Create new tokens and token sets via UI (persists to `.tokens.json` files in the repo)
- Visualise alias chains: click a semantic token and trace it back to its primitive

### Layer (Resolution Set) Editor
- See which token sets compose the current resolved document
- Drag to reorder resolution priority
- Toggle sets on/off to preview what changes
- Create a new set (produces a new `.tokens.json` + resolver entry)

### Component Gallery
- Render each Fun.Blazor component in isolation
- Show all states: default, hover, active, disabled, error, etc.
- Render states side-by-side or in a matrix view
- Show which tokens each component references (token → component usage map)
- Toggle dark/light mode and custom themes

### ADR Integration
- List all decision records from `LOGOS/decisions/` across loaded repos
- Read full ADR in-app (rendered Markdown)
- Create new ADR via a form: pre-fills id, date, frontmatter; saves to `LOGOS/decisions/`
- Link from a token or component to the ADR that explains why it is the way it is
- History view: timeline of decisions

### ATLAS Integration — PATH Modeling

**The design mocks file is the input surface for PATH state definitions.**

Each screen in the mocks represents a specific application state — what the user
sees at a particular moment with particular data after a particular sequence of
actions. Prototype connections between screens express transitions. The mocks file
is not a picture of the application; it is a specification of the application's
state space, expressed visually.

The flow is:

```
System Library          Design Mocks              PATHS + Fun.Blazor
──────────────          ────────────              ─────────────────
Tokens (values)    →    Screens (states)      →   State graph (nodes + edges)
Components         →    Component layout      →   Component tree per state
Themes/Sets        →    Token resolution      →   CSS custom properties
                        Prototype connections  →   Router / navigation events
```

The mocks file bridges the design system and the implementation. It consumes
the library's primitives and composes them into specific states. The implementation
consumes the mocks to know what states to build and how to build them.

**What the mocks file informs for PATHS and Fun.Blazor:**
- Which states need to exist (one per distinct screen)
- Which components appear in each state and how they compose
- Which component variants are actually used (limits what needs to be built)
- What transitions connect states (prototype connections → router events)
- Which token combination is active per state (breakpoint + mode + brand)

**What the mocks file does not carry (requires separate authoring):**
- Guard conditions on transitions ("only navigable if logged in")
- Data bindings and state parameters ("which user ID to show")
- Error states and loading states (usually implicit in the design, not explicit)
- Back-navigation intent beyond what prototype connections express

PATH modeling tools:
- Import ATLAS PATH definitions (user journeys across screens)
- Each PATH step maps to a component or screen
- Walk a PATH as a prototype: renders the relevant components in sequence
- State transitions between steps are interactive (click through like a real app)
- Paths can include decision branch points (if user does X, go to Y vs. Z)

### Prototype Mode
- A sequential walkthrough of a defined PATH
- Each step renders the actual Fun.Blazor component with production tokens
- Interaction is real (not a static mockup) — components respond to clicks/input
- Purpose: stakeholder review, user testing, and design validation before full app build

## Relationship to Penpot

Penpot handles visual composition and asset storage. The design system app is not Penpot — it is the live token/component layer that runs _under_ the visual designs.

- Penpot exports → component reference implementations
- Design system app holds the authoritative token values that Penpot syncs from
- ADRs explain choices that Penpot's design history cannot capture

## What This Is Not

- Not a replacement for Penpot for visual design
- Not a CMS or content management tool
- Not a general-purpose documentation site
- Not something that replaces actual app code — components are real F# code, not prototypes

## Implementation Approach

- Built with Fun.Blazor (same stack as the apps it models)
- Reads token files, LOGOS directories, and ATLAS PATH files from local filesystem (or git)
- Primarily a local-first developer tool; may be deployable as an internal team app
- ADR creation writes back to git via a small server-side handler
- PATH walkthrough uses the real component library — no static snapshots

## UX Reference — 72F Design System Generator

The [72F Design System Generator](https://github.com/72F-Studio/72f-design-system-generator)
Penpot plugin (by Parth Kulkarni / 72F Studio) is worth studying as a UX reference for the
Token Management section. It offers a structured, form-driven workflow for:

- Creating token sets from preset templates
- Defining colors, typography, spacing, radius, and shadow tokens through guided forms
- Managing themes (create, delete, switch active)
- Exporting tokens to JSON

The interaction model is more accessible than raw JSON editing, and the generator +
manager + export tab structure maps naturally onto what the design system app needs.

The gap between 72F and what we want: it outputs Tokens Studio format (not DTCG), has
no CSS ingestion or conversion workflow, no alias chain visualization, and no ADR/PATHS
integration. A DTCG-native version of this UX — built on `FnTools.DesignTokens` — with
the CSS bootstrap workflow (ingest → audit → name → refactor) layered in would be the
target. The 72F plugin is the rough proof that the form-driven model works in practice.

Note: 72F has a license mismatch (README says Apache 2.0, `LICENSE` file is AGPL-3.0).
Issue filed: https://github.com/72F-Studio/72f-design-system-generator/issues/1

## Status

Vision only. No implementation started. Capture here as direction for future ATLAS/FnHCI work.

## Open Questions

- Should this be a standalone app in its own repo, or a module inside the ATLAS toolset?
- Is the PATH model format already defined in ATLAS, or does it need to be specified?
- How does git write-back work in a Blazor app — shell-out, libgit2sharp, or Forgejo API?
