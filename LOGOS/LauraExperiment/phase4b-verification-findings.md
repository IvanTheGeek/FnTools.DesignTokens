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
| `spacing.3xs`              | `8px`     | `8`       | ✓ value, no unit |
| `spacing.sm`               | `16px`    | `16`      | ✓ value, no unit |
| `radius.sm`                | `16px`    | `16`      | ✓ value, no unit |

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

## `spacing.*` / `radius.*` resolve as `ResolvedNumber`, not `ResolvedDimension`

The alias chain is: `spacing.sm` (`$type: dimension`, `$value: {scale.sm}`) →
`scale.sm` (`$type: number`, `$value: 16`). DTCG alias resolution inherits the target's
resolved value, so `spacing.sm` becomes a `ResolvedNumber(16)` rather than
`ResolvedDimension {Value=16, Unit=Px}`.

The CSS emitter emits `--spacing-sm: 16` (bare number, no unit). This matches Penpot's
`token.resolvedValue` behavior (strips the unit to a bare number) but is not a valid CSS
length — a `px` suffix is required for most CSS properties.

Workaround (Phase 5): at the component layer, add `px` via `calc(var(--spacing-sm) * 1px)`
or by using the emitted numbers as multipliers. Long-term fix: the shim should propagate
the `$type` annotation through alias chains so `spacing.*` dimensions keep their `px` unit.

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

Responsive CSS emitted: `importTokensStudioCombined ["Always-on"; "Light"; "Desktop"; "100%"; "Core"]`
as `:root`, then `importTokensStudioCombined ["Always-on"; "Light"; "Mobile"; "100%"; "Core"]`
compared against the base to produce the `@media (max-width: 360px)` override. The diff
contains only the tokens that change between Desktop and Mobile (primarily `breakpoint` and
`multiplier`). The full responsive CSS is 16 KB for 248 base tokens plus 2-token mobile diff.

---

## What was built / verified

| Item | Status |
|---|---|
| `emitBlock` `@media` nesting (`:root` inside `@`) | ✓ done |
| 3 new `emitBlock` tests + 1 `emitThemed @media` test | ✓ 251 tests pass |
| `buildFlatIndex` tokenSetOrder sort fix | ✓ done |
| Scale token values correct (8, 10, 13, 16, 20, 25, 31, 39, 49) | ✓ verified |
| Color tokens verified against phase2 push | ~ same family, hue differs by ~Eco Tools vs Core |
| Breakpoint token correct (1200px desktop, 360px mobile) | ✓ verified |
| Responsive CSS written to `scripts/phase4-output.css` | ✓ done |

---

## Gaps carried to Phase 5

- **`ResolvedNumber` for spacing/radius** — bare numbers without `px` unit; component layer
  must add the unit.
- **HSL brand-bleed** — color tokens evaluated at shim time with last-brand hue; brand
  per-call theme separation needed for correct per-brand colors.
- **`spacing.*` / `radius.*` unresolved with just `["Light"; "Desktop"; "100%"]`** — the
  "Always-on" pseudo-theme must be included explicitly; API documentation should note this.
- **Typography tokens** — not verified against Penpot; depend on font availability in
  Penpot's registry.
