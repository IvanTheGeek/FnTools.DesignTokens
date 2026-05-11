---
id: 037
title: Validation warning channel — deferred; documented as future possible route
status: deferred
date: 2026-05-10
---

## Context

When choosing how to address the ADR-033 friction (dimension→number alias
hard-failing validation in TS-as-SoT files), four options were considered
(see `outside-conversations_2026-05-10_01.md` for the full exchange). ADR-035
shipped option 2 (`ValidateOptions` per-call opt-in laxness). This ADR
documents option 3 (typed warning channel) as a **future possible route**
that was not built today but has a clear use case for *other* kinds of
validation issues.

## What option 3 looks like

Introduce a parallel warning channel alongside the existing
`ValidationError` list:

```fsharp
type ValidationWarning =
    | TokenUnused          of path: string
    | ScaleStepOutsideTypicalRange of path: string * factor: float
    | ...

type ValidationResult = {
    Errors   : ValidationError list      // fatal — same shape as today
    Warnings : ValidationWarning list    // advisory; caller decides surfacing
}

let validate (file: TokenFile) : ValidationResult
```

Existing `Result<unit, ValidationError list>` callers would migrate to
inspect both `.Errors` and `.Warnings`, or use a wrapper that returns
`Result<ValidationWarning list, ValidationError list>` to preserve the
binary success/failure split while exposing the warning channel on success.

## Why option 3 was not chosen for the ADR-033 case

The dimension→number alias is a real footgun for accidental misuse:
pre-ADR-033 such files produced silently-wrong CSS. Demoting it to a
warning globally (which is what option 3 would do for this specific case)
trades the hard-error signal for ergonomics. Anyone with an accidental
mistype loses the loud feedback. ADR-035's per-call opt-in is the right
shape for *this* case because the strict default still protects accidents
while the permissive variant unblocks intentional uses at the call site.

So option 3 lost the *immediate* decision. But option 3 remains the right
architectural answer for a different class of validation issues that have
nothing to do with type safety:

## When option 3 would be the right call

A warning channel is appropriate when an issue is:

- **Advisory, not a footgun.** No data corruption risk; the file is still
  structurally valid DTCG.
- **Common enough that a hard error would be noisy.** Half the consumers
  legitimately don't care.
- **Useful enough that silent acceptance loses information.** Authors
  want to know.

Candidate issues that fit this profile:

- **Unused tokens.** A token defined in `core` but never referenced
  anywhere (not by another alias, not in any axis selection). Probably
  dead code, but might be a planned future reference.
- **Scale steps outside typical range.** A `font.size` token at 0.001rem
  or 50rem is probably a typo, but it's not technically invalid.
- **Duplicate values across sibling tokens.** `color.brand.primary` and
  `color.action.default` resolving to the same hex — probably one should
  alias the other, but might be intentional.
- **Description-less tokens in a tier that conventionally documents them.**
  Semantic-tier tokens with no `$description` lose the
  "what does this slot mean?" signal that semantic tier exists to provide.
- **Deep dot-path-style names exceeding some threshold** (e.g.
  `color.feedback.semantic.success.subtle.hover.dark`) — possibly worth
  flattening.

None of these would be hard errors. All would be useful as warnings if
the consumer opts to surface them. None map cleanly to the existing
`ValidationError` types.

## Cost of implementing option 3

When (if) we build it:

1. **New result type**: `ValidationResult` (or extend the existing
   `Result` shape — see "shape" notes below).
2. **Migration**: every existing `Validation.validate` caller updates to
   handle the new shape. The change can be soft if a `validateLegacy`
   alias keeps the old shape, but ideally the new API is the canonical
   one.
3. **Warning catalog**: each warning type needs definition, formatter,
   doc explaining when it fires and what to do about it.
4. **Caller guidance**: consumer docs need a "should I log warnings?
   abort? surface to user?" section. Real conceptual surface, not just
   API surface.
5. **Test infrastructure**: warning enumeration tests, "this warning
   fires only when X" tests for each case.

This is real work. It's also reusable: once the channel exists, adding
new warnings is just one DU case + one formatter line + one test each.

## Shape considerations

Two viable shapes:

**A. Discrete fields on a record:**
```fsharp
type ValidationResult = {
    Errors   : ValidationError list
    Warnings : ValidationWarning list
}
let validate (file: TokenFile) : ValidationResult
```
Pro: explicit, no nesting. Con: `result.Errors` empty + `result.Warnings`
non-empty is "success with warnings" — caller has to inspect both.

**B. Result wrapping warnings on the Ok side:**
```fsharp
let validate (file: TokenFile) : Result<ValidationWarning list, ValidationError list>
```
Pro: keeps the binary success/failure pattern; idiomatic F#. Con: the Ok
case carries advisory information that callers might ignore by pattern
matching on just `Ok _`.

Probably shape B is closer to F# idiom, but C-style consumers might prefer
shape A. Decide when there's a concrete use case to optimize for.

## Trigger

This ADR moves from `deferred` to `accepted` when:

- A concrete advisory issue lands that genuinely doesn't fit the existing
  `ValidationError` types (unused tokens, scale outlier, etc.), AND
- A real consumer wants the signal as a warning (not as an error and not
  as silent acceptance), AND
- Implementation cost is justified by the catalog of warnings that would
  fit the channel (not just one).

Until then, this ADR sits as a documented future direction. If the trigger
never arrives, this ADR stays in `deferred` indefinitely — that's fine.

## Status today (v0.10.0)

Not built. ADR-035 covers the ADR-033 friction via the per-call opt-in
pattern. No warning channel exists. Validation is binary
`Result<unit, ValidationError list>` as before.

Filed for traceability after the option comparison in
`outside-conversations_2026-05-10_01.md`. Future agents and human
contributors should reach for this ADR when they're tempted to add a new
`ValidationError` case for something that's really advisory.
