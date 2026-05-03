---
area: Experiment
status: active — 2026-05-03
---

# Laura Experiment — Vision

## Source material

Laura Calbach's "Design Systems at Scale | Penpot × Tokens Studio" demo files, imported to
the local Penpot 2.14.4 instance. Two Penpot files in the archive:

- **System library** — shared design system: typography, color palette, spacing, components.
  1286 shapes, 1019 with `appliedTokens`.
- **Design mocks** — four screens using the system: Dashboard, Landing page, Email, Thumbnail.
  318 shapes, 258 with `appliedTokens`.

Archive path: `/home/ivan/ARCHIVE/Penpot-DesignTokens/Design mocks.penpot`

---

## What this experiment is

A learning lab. Not a product. Not LaundryLog.

The goal is to establish — empirically, through working code — what it takes to use Penpot
as a bidirectional interface between a semantic model and a rendered UI. Laura's files provide
a complete, working design system and set of screens to work against without having to design
anything first.

---

## The model of the world

Penpot is not the source of truth. It is an **interaction surface** for the truth. The truth
lives in the model — eventually NEXUS, which does not exist yet. For this experiment, the
model is stubbed: we define a minimal semantic model for one screen (the Dashboard) and
express it as tokens and components.

```
NEXUS model
    ↕  (channel: tokens + components)
  Penpot
    ↕  (channel: CSS custom properties + component parameters)
 Fun.Blazor
```

Each arrow is bidirectional. The experiment learns what each channel looks like and where it
breaks down.

---

## Why this matters beyond the experiment

- Defines what the codec layer (FnTools.DesignTokens) can and cannot express
- Surfaces the FnHCI boundary — where token resolution ends and UI binding begins
- Establishes a working pattern that can later be applied to LaundryLog
- Probes what NEXUS needs to be able to express before Penpot becomes a useful interface for it
- Tests Penpot as a prototype/interaction surface without having to build one from scratch

---

## Target screen

**Dashboard page** from "Design mocks". Most variety — cards, layout, typography, spacing,
color, stroke — so it exercises the widest token and component surface.

---

## What the code lives

Planning and findings: `LOGOS/LauraExperiment/` in this repo (codec questions belong here).

Application code: separate repo when we get there. Fun.Blazor application, not a library.
