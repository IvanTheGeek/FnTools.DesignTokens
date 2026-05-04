---
area: Experiment
status: complete — 2026-05-04
phase: 4 — CSS emission (tokens → Fun.Blazor surface)
---

# Phase 4 Findings — CSS Emission

Implementation: `FnTools.DesignTokens.Css` — `CssEmitter.emitThemed` + `Api.importTokensStudioThemed`.

---

## What was built

### `importTokensStudioThemed`

```fsharp
Api.importTokensStudioThemed
    (config     : ShimConfig)
    (themeNames : string list)
    (jsonText   : string)
    : Result<ThemeAwareImportResult, ImportError list>
```

Returns:
```fsharp
type ThemeAwareImportResult = {
    BaseTokens : (string list * ResolvedToken) list
    Themes     : ThemeSet list
    Warnings   : TokensStudioImportWarning list
}
type ThemeSet = { ThemeName: string; Tokens: (string list * ResolvedToken) list }
```

`Warnings` includes `SetSkipped`, `TokenUnresolved`, and `ThemeNotFound` entries.

### `CssEmitter.emitThemed`

```fsharp
CssEmitter.emitThemed
    (selectorForTheme : string -> string)
    (baseTokens       : (string list * ResolvedToken) seq)
    (themes           : (string * (string list * ResolvedToken) seq) seq)
    : string
```

Emits one `:root {}` block from `baseTokens`, then for each theme only the tokens whose CSS
value differs from the base (or tokens not present in base at all). Themes with no diffs
produce no override block.

---

## Base / theme partition

The correct partition for `:root` vs `[data-theme="X"]` is **set membership in any active
theme's `selectedTokenSets`**:

- Sets NOT listed in any active theme's `selectedTokenSets` → **global** → `:root`
- Sets listed in at least one theme's `selectedTokenSets` → **theme-specific** → override blocks only

This partition is computed from the active theme names passed to `importTokensStudioThemed`.
If themeNames is `["Light"; "Dark"]`, the union of their `selectedTokenSets` defines which
sets are theme-specific. Everything else goes into the base.

**Key distinction from "source" status**: Laura's file uses only `"enabled"` status across
all theme sets. The original assumption (that "source" sets are global and "enabled" sets are
theme-specific) was wrong for this file. The correct criterion is purely structural:
global = not mentioned in any active theme.

---

## Laura file results (Light + Dark themes)

| Tier | Set sources | Token count |
|---|---|---|
| Base (`:root`) | Palettes and Scales, Breakpoints/Desktop, Text zoom/100%, Brand/Eco Tools, Typography, Components/Button | 179 resolved |
| Light override | Color mode/Light, Color/Light Component, Color/Light Core | ~20 semantic color diffs |
| Dark override | Color mode/Dark, Color/Dark Component, Color/Dark Core | ~20 semantic color diffs |

The base contains the full palette (139 colors), all dimensions, font families, and component
tokens. The Light and Dark overrides contain only the semantic color tokens that differ
between modes — exactly the `[data-theme="X"]` content a real stylesheet needs.

All 179 base tokens parse and emit. The 57 `TokenUnresolved` warnings (spacing/sizing/
typography referencing skipped math-expression set) carry through to `ThemeAwareImportResult.Warnings`.

---

## CSS output shape

```css
:root {
  --color-background-default: oklch(…);
  --color-text-main: oklch(…);
  /* 179 properties — palette, dimensions, typography, components */
}

[data-theme="Light"] {
  --color-background-default: oklch(…);  /* only the ~20 semantic diffs */
  --color-text-main: oklch(…);
}

[data-theme="Dark"] {
  --color-background-default: oklch(…);
  --color-text-main: oklch(…);
}
```

The override blocks contain only the delta — unchanged tokens are not repeated. This
keeps overrides minimal and makes the diff auditable.

---

## `emitThemed` design

`selectorForTheme` is a caller-supplied function rather than a fixed prefix. This
preserves flexibility:
- `fun n -> sprintf "[data-theme=\"%s\"]" n` — data-attribute selector (tests use this)
- `fun _ -> "@media (prefers-color-scheme: dark)"` — media query
- `fun n -> sprintf ".theme-%s" n` — class-based theming

The function itself has no opinion on selector syntax. See ADR 019.

---

## Verified: theme with no diffs produces no block

If a theme's resolved tokens are identical to the base for every path, `emitThemed` emits
nothing for that theme. The 4 unit tests for `emitThemed` cover this case explicitly.

---

## Test coverage

12 new tests in `TokensStudioTests.fs` and `CssEmitterTests.fs`:

| Suite | Count | Focus |
|---|---|---|
| `importTokensStudioThemed` unit | 4 | empty themes, unknown theme, single theme, two themes |
| Laura Light+Dark integration | 4 | theme count, base colors, per-theme token counts, diff assertion |
| `emitThemed` unit | 4 | no-theme, two-theme selectors, diff-only override, no-diff no-block |

203/203 tests pass.

---

## Gaps not addressed in Phase 4

- **`@media` breakpoint overrides** — breakpoint tokens (`Breakpoints/Desktop`, `Mobile`,
  `Tablet`) are in the base. They are not expressed as `@media` queries — Penpot has no
  concept of media queries. The plan called for `@media (max-width: 360px)` blocks for
  Mobile and Tablet breakpoints. This would require caller-side logic: resolve Mobile and
  Tablet theme sets and pass them as additional themes with media-query selectors.
  Not implemented — Phase 4 delivers the architecture; breakpoint media queries are a
  Phase 5 component concern.

- **Math expressions** — `Foundations/Base` is still skipped due to `PreserveMath` policy.
  57 tokens referencing `scale.*` remain unresolved. Phase 5 will need concrete fallback
  values for spacing/sizing/typography at the targeted breakpoint + zoom combination.
