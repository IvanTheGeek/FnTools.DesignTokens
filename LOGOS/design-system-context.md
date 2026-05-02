---
area: Design System Context
status: working notes — 2026-05-02
---

# Design System Context

Working notes for continuing sessions. Covers token architecture decisions, stack choices, naming conventions, Penpot workflow, and FnHCI model.

---

## Projects in scope

| Project | Path | Notes |
|---|---|---|
| FnTools.DesignTokens | `/home/ivan/DEVELOPMENT/FnTools/FnTools.DesignTokens` | DTCG 2025.10 library — this repo |
| LaundryLog | `/home/ivan/nexus/LaundryLog` | Primary consumer; has existing components + design system |
| FnHCI | (planned) | Broader token framework including non-visual targets |
| ATLAS | (planned) | Design system runtime; uses FnTools.DesignTokens at interchange boundary |

The LaundryLog design system HTML handoff is at:
`/home/ivan/nexus/LaundryLog/artifacts/LaundryLog_Design_System_Handoff/LaundryLog Design System.html`

---

## Token file structure — LaundryLog

Two DTCG token files, two tiers:

```
tokens/
  cb.tokens.json    — CheddarBooks primitives  (raw named values, no aliases)
  ll.tokens.json    — LaundryLog semantic       (aliases into cb.tokens.json)
```

A `.resolver.json` merges them in order: `cb.tokens.json` first, `ll.tokens.json` second. Later entries win.

CheddarBooks is the base brand layer. LaundryLog extends it. If a CheddarBooks-only product existed, it would have its own semantic file referencing the same `cb.tokens.json` primitives.

No component token files. See insights.md § "Three-tier token model".

---

## Naming convention

### In token files

Token path segments are lowercase with dots as separators. The path IS the CSS var name (minus the `--` prefix).

```
color.blue.N500          → --color-blue-N500      (primitive)
color.action.default     → --color-action-default  (semantic)
color.machine.washer     → --color-machine-washer  (semantic, machine-type specific)
```

Numeric scales use N prefix in paths too (consistent with generated F#, avoids any parser ambiguity).

Group structure for `cb.tokens.json`:
```
color /
  blue / N100 … N900
  teal / N100 … N900      (washer machine color family)
  orange / N100 … N900    (dryer machine color family)
  purple / N100 … N900    (supplies machine color family)
  neutral / N100 … N900   (slate grays)
  white, black            (absolute primitives)
spacing / N1 … N16        (4px base: N1=4px, N2=8px, … N16=64px)
font /
  family / sans, mono
  weight / regular(400), medium(500), semibold(600), bold(700)
  size / N1 … N12         (12px–96px scale)
radius / N1 … N6          (4px–24px)
duration / fast(100ms), normal(200ms), slow(400ms)
```

Group structure for `ll.tokens.json`:
```
color /
  action /
    default, hover, active, disabled
  surface /
    base, subtle, inverse
  text /
    primary, secondary, muted, inverse, disabled
  border /
    default, focus, error
  machine /
    washer, dryer, supplies             (active selection color per machine type)
    washer.border, dryer.border, supplies.border
  status /
    error, warning, success, info
spacing /
  touch.min                             (48px — minimum touch target)
  component /
    padding.x, padding.y
    gap
```

### In CSS output

`--color-action-default`, `--spacing-touch-min`, etc. Dots in path → hyphens in CSS var name.

### In generated F# bindings

```fsharp
module Tokens =
  module Color =
    module Action =
      let ``default`` = CssVar "--color-action-default"
      let hover       = CssVar "--color-action-hover"
    module Machine =
      let washer  = CssVar "--color-machine-washer"
      let dryer   = CssVar "--color-machine-dryer"
  module Spacing =
    module Touch =
      let min = CssVar "--spacing-touch-min"
```

Backtick escaping for reserved words (`default`). Numeric scales: `N500`.

---

## Existing LaundryLog CSS design system — key values

From the HTML handoff file. All colors are OKLCH.

**Machine palette**:
- Washer: teal `oklch(56% 0.14 200)`
- Dryer: orange `oklch(60% 0.17 48)`
- Supplies: purple `oklch(56% 0.14 290)`

**Spacing**: 4px base unit, `space-1` (4px) through `space-16` (64px)

**Typography**:
- DM Sans (UI / sans body)
- JetBrains Mono (code / monospace)

**Theme**: dark mode via `[data-theme="dark"]` attribute

**Prefix convention** (old, being replaced):
- `--cb-*` = CheddarBooks foundation (primitive layer)
- `--ll-*` = LaundryLog extension (semantic/component layer)

These old names will NOT be carried into the new token files. Clean break. New CSS vars derived from token paths only.

---

## Fun.Blazor components — current state

Located at `/home/ivan/nexus/LaundryLog/src/LaundryLog.UI/Components/`:
- `MachineTypeChips.fs` — machine selector chips (Washer/Dryer/Supplies)
- `PaymentChips.fs` — payment method selector
- `MoneyInput.fs` — currency amount input
- `LocationInput.fs` — location text input
- `LineTotalDisplay.fs` — running total display

These components currently reference CSS class names from the old system (e.g., `ll-machine-chip`, `ll-machine-chip--washer`). Migration path: swap inline class strings for `Tokens.*` bindings once the CSS emitter and typed bindings emitter are ready.

The components are the **primary validation target** for the token pipeline. When `MachineTypeChips.fs` can reference `Tokens.Color.Machine.washer` instead of `"ll-machine-chip--washer"`, the pipeline is working.

---

## Stack decisions (locked)

| Concern | Tool | Notes |
|---|---|---|
| Token format | DTCG 2025.10 `.tokens.json` | Community standard; JSONC comments work today |
| Non-DTCG tokens | TOML | Console/TUI/thermal/braille; shallow config-like structure |
| TOML parser | `Tomlyn` (`xoofx/Tomlyn`, NuGet) | F#-friendly TOML 1.0 library; use for FnHCI non-visual token files |
| CSS authoring | DTCG files → CSS emitter | No handwritten CSS vars; emitter is the source |
| CSS binding in F# | Fun.Css `CssVar` | Same author as Fun.Blazor; composable |
| UI framework | Fun.Blazor | F# DSL for Blazor; SSR/WASM/PWA/MAUI Hybrid |
| Design tool | Penpot | Open-source; DTCG import/export; HTML import (untested) |
| Token tooling (Node) | None | Not using Style Dictionary or Terrazzo; emitters are F# |
| Color space | OKLCH | Native DTCG 2025.10 support; all LaundryLog colors |

---

## Namespace architecture decision (2026-05-02)

`FnTools.DesignTokens` stays as a standalone library — independent NuGet, no FnHCI dependency.
`FnTools.FnHCI.Tokens` will be a future aggregator layer that depends on `FnTools.DesignTokens`
alongside ConsoleTokens, TuiTokens, etc. when that work starts.

Do NOT rename `FnTools.DesignTokens.*` to `FnTools.FnHCI.Tokens.Design.*` — the current name
is better for standalone publishing and keeps the DTCG library useful outside the FnHCI context.

## FnHCI token model

FnHCI extends the token concept beyond visual/DTCG to cover all rendering targets:

```
FnHCI.Tokens
├── DesignTokens    (DTCG 2025.10 — web/UI visual)
├── ConsoleTokens   (ANSI escape, 256-color, bold/italic/underline)
├── TuiTokens       (border chars, box drawing, character-cell spacing)
├── ThermalTokens   (print density, font codes, barcode/QR spec)
└── BrailleTokens   (dot patterns, grade 1/2 encoding hints)
```

Each target has its own token domain and authoring format. DTCG handles the visual tier. TOML handles the others (they are shallow and config-like; no nested group semantics needed).

A shared `FnHCI.Resolver` concept can merge tokens across targets into a unified per-target output. This is future work — no implementation yet.

---

## Penpot workflow (planned)

See `experiments-planned.md` for the specific experiments. The intended workflow:

**Design direction** (Penpot → Fun.Blazor):
1. Design component variants in Penpot using imported DTCG tokens as variables
2. Export SVG or HTML
3. Reconstruct structure in Fun.Blazor; SVG is reference for shapes, layout, tokens

**Refinement direction** (Fun.Blazor → Penpot):
1. Render Fun.Blazor component to HTML
2. Import HTML into Penpot (feature is new as of ~early 2026; untested)
3. Refine in Penpot, export, update Fun.Blazor

Penpot is visual exploration; Fun.Blazor is ground truth. The two sync at the token layer (shared CSS vars from emitted DTCG tokens).

---

## Penpot local instance

Penpot is running locally at `http://localhost:9001`.
REST API: `http://localhost:9001/api/rpc/command/<endpoint>`
Auth header: `Authorization: Token <token>` (not Bearer — "Token" prefix)

Token storage convention (same pattern as Forgejo): `~/.config/penpot-claude.token`

Key endpoints:
- `get-profile` — verify token
- `list-projects` — list all projects
- `get-file` — file content including design token/variable data
- SVG export path to be confirmed from `http://localhost:9001/api/_doc`

Claude Code env in `.claude/settings.json` (project-level):
```json
{ "env": { "PENPOT_TOKEN": "$(cat ~/.config/penpot-claude.token)" } }
```

To create a token: Penpot UI → Profile menu → API Access Tokens → New token. Self-hosted instances need `enable-access-tokens = true` in the Penpot config (check if already enabled).

Fallback options if direct REST API is insufficient: Penpot MCP server, Penpot Plugins API. Both are more setup overhead — try REST first.

## Git remotes

```
FnTools.DesignTokens:
  primary   https://forgejo.ivanthegeek.com/FnTools/FnTools.DesignTokens
  mirror    https://github.com/IvanTheGeek/FnTools.DesignTokens (auto-push on commit)

LaundryLog:
  (check CLAUDE.md in that repo for current remotes)
```

Token reading: `~/.config/forgejo-claude.token` — Bearer token for Forgejo API calls.

---

## Next session starting point

1. Read this file + `work-planned.md`
2. First task: CSS ingestion tool — parse LaundryLog HTML design system → DTCG JSON
3. Test file: `/home/ivan/nexus/LaundryLog/artifacts/LaundryLog_Design_System_Handoff/LaundryLog Design System.html`
4. Output location: `tokens/` directory in LaundryLog repo (TBD — check with user)
