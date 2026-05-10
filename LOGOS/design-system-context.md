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

Token path segments are lowercase with dots as separators. Dots → hyphens in emitted CSS.
Numeric scales use N-prefix (`N500`, `N1`). See `LOGOS/naming.md` for full analysis and
known gaps.

**Current LaundryLog token structure (as built):**

```
cb.tokens.json — CheddarBooks primitives
  color / neutral, amber, green, red, blue, white, black
          text.{primary,secondary,muted,inverse,accent,success,danger}
          surface.{base,raised,sunken,overlay}
          border.{default,strong,accent}
          accent.{default,hover,subtle,on}
          feedback.{success,danger,info}.{default,subtle}
  font / family.{display,body,mono}
         weight.{light,regular,medium,semibold,bold}
         size.{xs,sm,base,md,lg,xl,2xl,3xl,4xl,5xl}
         line-height.{tight,snug,normal,relaxed}
         letter-spacing.{tight,normal,wide,wider}
  spacing / N0 … N24  (4px base unit)
  radius / xs, sm, md, lg, xl, 2xl, pill
  shadow / xs, sm, md, lg, xl, focus-ring
  duration / instant, fast, normal, slow
  easing / standard, spring, out

ll.tokens.json — LaundryLog extension
  color / machine / washer, dryer, supplies (each: default, subtle, border)
```

**Generated F# bindings** (`tokens/Tokens.fs`, 161 lines):

Rules: PascalCase all segments; N-prefix for digit-starting segments (`2xl` → `N2xl`);
hyphens join as camelCase (`focus-ring` → `FocusRing`). F# keywords capitalise cleanly.
Binding type is `string` — values are `"var(--name)"` ready for Fun.Css/inline styles.

```fsharp
Tokens.Color.Text.Primary              = "var(--color-text-primary)"
Tokens.Color.Machine.Washer.Default    = "var(--color-machine-washer-default)"
Tokens.Color.Feedback.Success.Subtle   = "var(--color-feedback-success-subtle)"
Tokens.Font.LineHeight.Normal          = "var(--font-line-height-normal)"
Tokens.Shadow.FocusRing                = "var(--shadow-focus-ring)"
Tokens.Spacing.N4                      = "var(--spacing-N4)"
```

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
| CSS binding in F# | `string` `"var(--name)"` | Fun.Css property builders accept strings directly; no wrapper type needed |
| UI framework | Fun.Blazor | F# DSL for Blazor; SSR/WASM/PWA/MAUI Hybrid |
| Design tool | Penpot | Open-source; DTCG import/export; HTML import does not exist (EXP-01 falsified 2026-05-02 — open community request, no implementation) |
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
2. ~~Import HTML into Penpot~~ — EXP-01 (2026-05-02) confirmed Penpot has no HTML import (community request open since March 2025, no implementation). This direction is not currently feasible.
3. Alternative: re-create the structure manually in Penpot from the rendered HTML as visual reference, or pivot to a token-only round trip via the DTCG format.

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

Completed as of 2026-05-02:
- CSS emitter (`FnTools.DesignTokens.Css`) ✓
- Typed F# bindings emitter (`FnTools.DesignTokens.Bindings`) ✓
- LaundryLog `tokens/ll.css` (124 CSS vars) ✓
- LaundryLog `tokens/Tokens.fs` (161-line F# bindings) ✓

Next up (see `tasks-open.md`):
1. Penpot round-trip experiment — see `experiments-planned.md`
2. Or: migrate LaundryLog components to use `Tokens.*` bindings instead of hardcoded class names
3. Or: CSS ingestion tool (parse legacy CSS → DTCG JSON) — lower priority since token files are already authored
