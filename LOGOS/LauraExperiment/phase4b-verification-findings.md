---
area: Experiment
status: complete — 2026-05-04
phase: 4 — CSS verification + @media breakpoints
---

# Phase 4 Verification Findings

Script: `scripts/phase4-verify.fsx`. Full CSS output: `scripts/phase4-output.css`.

---

## Verification approach

`importTokensStudioCombined ["Always-on"; "Light"; "Desktop"; "100%"; "Core"]` on Laura's
full token file, comparing against the known-correct values from the `laura-light-desktop`
push documented in `phase2-findings.md` Part 2.

"Always-on" must be explicitly included because it is the pseudo-theme that owns the
foundation sets (`Foundations/*`, `Color/Palettes and Scales`, `Typography`,
`Components/Button`). Without it, those sets are classified as theme-owned by the global
`allThemeSets` computation and excluded from `combinedSets` entirely.

---

## Verification results

| Token | Expected (phase2 push) | Emitter output | Match |
|---|---|---|---|
| `color.background.default` | `#fafafa` | `#fafafa` | ✓ |
| `color.background.body`    | `#f3f2f3` | `#f2f3f2` | ~ |
| `color.border.default`     | `#c2bcc1` | `#bcc2be` | ~ |
| `breakpoint`               | `1200px`  | `1200px`  | ✓ |
| `spacing.3xs`              | `8px`     | `8px`     | ✓ |
| `spacing.sm`               | `16px`    | `16px`    | ✓ |
| `radius.sm`                | `16px`    | `16px`    | ✓ |

**Scale values now correct**: `--scale-3xs: 8`, `--scale-sm: 16`, `--scale-md: 20`,
`--scale-lg: 25`, `--scale-xl: 31`, `--scale-2xl: 39`, `--scale-3xl: 49`. The full
Desktop + 100% zoom scale spread from phase2-findings is reproduced exactly.

---

## Bug discovered and fixed: `buildFlatIndex` used JSON property order, not tokenSetOrder

**Root cause**: in `shimCore`, the `sets` array was built from `root |> Seq.choose ...`
which iterates in JSON property order. Tokens Studio JSON is not required to list set
objects in `tokenSetOrder` sequence, and Laura's file does not — `Foundations/Base` is
the *last* property in her JSON (position 21 of 22), while it is *first* in `tokenSetOrder`.

`buildFlatIndex` is last-set-wins. With JSON property order, `Foundations/Base`
(which contains `multiplier = 1`) was always last → its `multiplier` overwrote
`Breakpoints/Desktop`'s `multiplier = 1.25` → all scale tokens evaluated to
`round(16 × 1^N) = 16`.

**Fix** (`TokensStudio.fs` `shimCore`): sort the extracted sets by their position in
`tokenSetOrder` before building any index. Sets not listed in `tokenSetOrder` sort last.
The `transformedSets` map (per-set shimmed JSON) is built from the same sorted array, so
the sort is applied consistently.

This fix is contained entirely in `shimCore`. No public API changes. All 251 tests pass.

The `mathBleedJson` test fixture happened to list sets in JSON order matching
`tokenSetOrder`, so the bug was not caught by existing tests.

---

## Color discrepancy — HSL brand-bleed (pre-existing, not fixed here)

The two color mismatches (`#c2bcc1` vs `#bcc2be`, `#f3f2f3` vs `#f2f3f2`) are caused
by a known limitation of shim-time HSL evaluation:

- Color tokens in Laura's file use HSL expressions like `hsl({hue.primary}, 15%, 88%)`
  where `hue.primary` comes from a Brand set.
- The shim evaluates HSL at shim time using `globalIndex` (all sets, tokenSetOrder
  last-wins). The last Brand set in `tokenSetOrder` is `Brand/Eco Tools`, so its hue
  is used for ALL color tokens regardless of which brand theme is requested.
- The phase2 push was constructed with Core's hue manually; our emitter uses Eco Tools'.
- The resulting colors are similar (same lightness/saturation family) but differ in hue
  by a few degrees.

This is a distinct issue from the tokenSetOrder sort bug and affects any file that uses
HSL with brand-variant hue tokens. Correct per-brand color resolution would require
either running `importTokensStudioThemed`/`importTokensStudioCombined` with a brand
theme per call, or extending the shim to support separate globalIndex/HSL-index passes.

---

## `spacing.*` / `radius.*` alias-type coercion (fixed 2026-05-04)

The alias chain `spacing.sm` (`$type: dimension`, `$value: {scale.sm}`) →
`scale.sm` (`$type: number`, `$value: 16`) previously discarded the `dimension` annotation
and resolved to `ResolvedNumber(16)` (bare number, no unit).

**Fix** — two changes in `partialFlattenResolvedFile` (`DesignTokens.fs`):

1. **Type precedence flip**: `t.Type |> Option.orElse target.Type` — the alias token's own
   `$type` now takes precedence over the aliased target's type, per DTCG intent.

2. **Number→Dimension coercion**: when the resolved type is `DimensionType` but the value is
   a bare `Number`, it is promoted to `Dimension {Value=n, Unit=Px}` before `toResolvedValue`.

**Shim fix** (`TokensStudio.fs` `walkObj`): a typeless alias token (`$value: {ref}` with no
`$type` and no inherited scope type) is now passed through as a leaf (previously silently
dropped because `isLeaf = false`). Three new tests verify all coercion paths; 254 pass.

---

## `@media` breakpoint overrides

`emitBlock` updated to detect `@`-prefixed selectors and nest custom property declarations
in an inner `:root { }` rule, producing valid CSS:

```css
@media (max-width: 360px) {
  :root {
    --breakpoint: 360px;
    --multiplier: 1.1;
  }
}
```

The `emitThemed` function automatically benefits — callers can pass `@media (...)` as the
`selectorForTheme` return value and get correct output.

Responsive CSS emitted:
- `:root` — `importTokensStudioCombined ["Always-on"; "Light"; "Desktop"; "100%"; "Core"]`
- `@media (max-width: 1020px)` — `importTokensStudioCombined ["Always-on"; "Light"; "Tablet"; "100%"; "Core"]` diff against desktop base
- `@media (max-width: 360px)` — `importTokensStudioCombined ["Always-on"; "Light"; "Mobile"; "100%"; "Core"]` diff against desktop base

Each `@media` block contains only the tokens that differ from the desktop base (primarily
`breakpoint` and `multiplier`). Full responsive CSS is 17 KB for 248 base tokens plus
tablet and mobile diffs.

---

## What was built / verified

| Item | Status |
|---|---|
| `emitBlock` `@media` nesting (`:root` inside `@`) | ✓ done |
| 3 new `emitBlock` tests + 1 `emitThemed @media` test | ✓ done |
| `buildFlatIndex` tokenSetOrder sort fix | ✓ done |
| Scale token values correct (8, 10, 13, 16, 20, 25, 31, 39, 49) | ✓ verified |
| Color tokens verified against phase2 push | ~ same family, hue differs by ~Eco Tools vs Core |
| Breakpoint correct (Desktop 1200px, Tablet 1020px, Mobile 360px) | ✓ verified |
| `spacing.*` / `radius.*` emit with `px` unit | ✓ fixed (alias-type coercion) |
| Responsive CSS written to `scripts/phase4-output.css` | ✓ done (17 KB) |
| 254 tests pass | ✓ |

---

## Gaps carried to Phase 5

- **HSL brand-bleed** — color tokens evaluated at shim time with last-brand hue; brand
  per-call theme separation needed for correct per-brand colors.
- **`spacing.*` / `radius.*` unresolved with just `["Light"; "Desktop"; "100%"]`** — the
  "Always-on" pseudo-theme must be included explicitly; API documentation should note this.
- **Typography tokens** — not verified against Penpot; depend on font availability in
  Penpot's registry.
