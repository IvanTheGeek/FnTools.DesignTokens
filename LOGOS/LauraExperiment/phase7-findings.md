---
area: Experiment
status: complete — 2026-05-04
phase: 7 — Prototype path → PATHS states
source: Penpot MCP Plugin API (live file) + archive scan
---

# Phase 7 Findings — Prototype path → PATHS states

---

## Verification method

Two independent sources confirmed zero prototype connections:

1. **MCP Plugin API** — `penpotUtils.findShapes(s => s.interactions && s.interactions.length > 0, page.root)` across all 4 pages returned empty arrays. `page.flows` was `[]` on every page. Total shapes scanned: Landing page 124, Dashboard 146, Email 35, Thumbnail 9 = 314 shapes.

2. **Archive scan** — `grep -rl '"interactions"'` and `grep -rl '"flows"'` across 2071 JSON files in the extracted `.penpot` archive returned 0 matches.

**Finding: the Design mocks file contains no prototype connections.**

---

## Screen inventory

Each Penpot page holds one primary screen board. These are the application surfaces the mocks represent:

| Penpot page | Primary board | Dimensions | Description |
|---|---|---|---|
| Landing page | `Landing page` | 1200×572 | Marketing page: header (nav + button), hero (text + graphic), email signup form, footer |
| Dashboard | `Dashboard` | 1200×774 | App dashboard: 6 `pattern/card` instances — image card, duo cards, icon+link list, info card, graph card |
| Email | `Email` → `content` | 1200×1093 | Email template: graphic header, content body, graphic footer |
| Thumbnail | `Thumbnail` | 1020×680 | OG/social thumbnail: text overlay on background |

Each page also contains a `Swatches` demo board (colour palette grid) except Email and Thumbnail. The swatches are documentation artefacts, not application screens.

---

## PATHS state candidates

Taking each screen at face value as a PATHS state:

| PATHS state (proposed) | Source board | Penpot page | Evidence |
|---|---|---|---|
| `Landing` | `Landing page` | Landing page | Marketing/acquisition surface; email signup form present |
| `Dashboard` | `Dashboard` | Dashboard | Authenticated app view; informational cards |
| `EmailConfirmation` | `Email` | Email | Transactional email layout (post-signup or notification) |
| `Thumbnail` | `Thumbnail` | Thumbnail | Not an app state — OG image asset, not a screen |

`Thumbnail` is a static asset (social/OG card), not an interactive application state. Three real screen states: `Landing`, `Dashboard`, `EmailConfirmation`.

---

## Transitions: what can be inferred vs what was authored

**Zero transitions were authored.** Nothing can be read from prototype connections because none exist.

What can be *inferred* from structure alone:

| Implied trigger | Source state | Target state | Basis |
|---|---|---|---|
| Email signup form submit | `Landing` | `EmailConfirmation` | Email signup component on Landing page; Email page is the only other non-dashboard surface |
| Authentication / sign in | `Landing` | `Dashboard` | Landing has a menu button; Dashboard is the app interior |
| (back-navigation) | `Dashboard` | `Landing` | Implied by app structure, not modelled |

These are hypotheses. No destination, animation, or trigger type is encoded in the file.

---

## What a Penpot prototype connection carries vs what PATHS needs

| Field | Penpot `Interaction` | PATHS transition needs |
|---|---|---|
| Trigger type | `click` / `mouse-enter` / `mouse-leave` / `after-delay` | Trigger type (compatible subset) |
| Source shape | The shape the interaction is on | Source element / component |
| Destination | Target `Board` (navigate-to action) | Target state |
| Animation | Type + duration | Presentation concern only |
| Guard condition | **absent** | Mandatory for non-trivial flows (auth checks, validation) |
| Data carried | **absent** | Needed for form values, IDs, context |
| Error target | **absent** | Every real transition has failure paths |
| Back-navigation | **absent** (no "previous screen" wiring in file) | Essential for mobile/accessible UX |
| Scroll position | `preserveScrollPosition` | Presentational; PATHS doesn't own this |

Penpot covers: happy-path trigger + destination. It does not carry: guards, data binding, error states, or context propagation. A PATHS transition is richer than what Penpot's prototype authoring can express.

---

## Could Penpot prototype authoring be a PATHS input surface?

**Partial yes, with structural changes.** The current file is not structured for prototyping:

- One screen per page, not multiple frames on a single page. Penpot's prototype model expects all screens in a flow to live as sibling top-level frames on one page, connected by interactions. The current layout (one board buried in a Demo wrapper) is presentation-mode layout, not prototype-mode layout.
- No flows defined at page level.
- Component variants are present in the System library but no variant-switching interactions are wired on instances.

**If the mocks were restructured for prototyping**, Penpot could supply:
- State names (board names → PATHS state IDs)
- Happy-path trigger → destination edges (the graph skeleton)
- Trigger type (click, hover, delay) as a hint for UX classification

**What Penpot cannot supply regardless of structuring:**
- Guard conditions (conditional logic)
- Data carried across transitions
- Error / fallback states
- Multi-step flows within a single screen (form validation, inline errors)

**Verdict**: Penpot prototype authoring is a *documentation layer* for PATHS — useful for communicating happy-path navigation intent to stakeholders, but not a machine-readable input surface for PATHS definitions. The PATHS model must be authored separately in code; the Penpot prototype can serve as a human-readable illustration of a subset of it.

---

## Structural gap: single-screen-per-page vs multi-frame-per-page

The Design mocks file uses one page per application section. This is a reasonable organisation for static documentation (designers scan by section) but it is incompatible with Penpot's prototype player, which navigates between top-level frames on a single page.

To use Penpot prototyping for PATHS:

1. Each PATHS state needs to be a top-level frame (Board) on a single page
2. Interactions must be added to the trigger elements (buttons, links, form submits)
3. `navigate-to` actions must point to destination frames
4. Flows must be defined (page-level entry points)

This is a design authoring convention change, not a tooling limitation. The Penpot Plugin API fully supports reading this structure — the issue is that it was not used in Laura's demo file.

---

## Summary

| Question | Answer |
|---|---|
| Are there prototype connections in the file? | No — zero on all 4 pages |
| Why not? | File is a design token demonstration, not an interactive prototype |
| What screens exist? | Landing, Dashboard, Email (3 application screens + 1 asset) |
| Can PATHS states be derived? | Manually, from screen names — not programmatically from connections |
| Can PATHS transitions be derived? | No — nothing to read |
| Is Penpot a viable PATHS input surface? | Partial — could supply happy-path graph skeleton if restructured; cannot supply guards, data, or error paths |
| What would make it viable? | Restructure to multi-frame-per-page, add interactions to UI elements, define flows |
