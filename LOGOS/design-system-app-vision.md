---
area: Vision
status: draft
date: 2026-05-02
---

# Design System App Vision

A GUI tool — the design system app — that makes the token/component/decision graph navigable, editable, and connected to the broader ATLAS product model.

## Core Capabilities

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

## Status

Vision only. No implementation started. Capture here as direction for future ATLAS/FnHCI work.

## Open Questions

- Should this be a standalone app in its own repo, or a module inside the ATLAS toolset?
- Is the PATH model format already defined in ATLAS, or does it need to be specified?
- How does git write-back work in a Blazor app — shell-out, libgit2sharp, or Forgejo API?
