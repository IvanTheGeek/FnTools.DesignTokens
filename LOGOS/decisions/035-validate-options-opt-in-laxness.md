---
id: 035
title: ValidateOptions for opt-in laxness on known-safe authoring patterns
status: accepted
date: 2026-05-10
---

## Context

ADR-033 introduced cross-type alias validation: a `dimension` token aliasing
a `number` token produces `ValidationError.TypeMismatch ("path", "dimension",
"number")`. The rule is correct in principle — pre-ADR-033 the CSS emitter
silently emitted unitless values for such tokens — but it hard-fails on **the
canonical Tokens Studio scale pattern**, which is universal in TS exports:

```jsonc
// TS scales are authored as numbers so math expressions can operate on them
"scale":   { "x0": { "$type": "number",    "$value": 16 } }
// Dimension tokens layer on top via aliases
"spacing": { "x1": { "$type": "dimension", "$value": "{scale.x0}" } }
```

Consumers with TS-as-SoT files cannot use the convenience wrappers
(`Api.import`, `Api.importWithResolver`,
`Api.importWithResolverEvaluatingExtensions`) — they hard-fail on every such
file. Their workaround is to drop down to the `Primitives` path and skip
validation entirely, which:

1. Loses the safety net for *other* validation issues (alpha range, alias
   cycles, the rest of the structural checks).
2. Forces a multi-line manual composition where the convenience wrapper would
   be a one-call import.
3. Surfaced a footgun: skipping validation also means losing the alias graph
   trap signal — the requester for `request_2026-05-10_04` was bitten by
   `resolveAll` precisely because they were on the Primitives path due to
   this friction (now also addressed by ADR-036).

Four options were considered (`outside-conversations_2026-05-10_01.md`):

1. **Demote dimension→number alias from `TypeMismatch` to a warning.** Affects
   all callers globally. Loses the hard-error signal for *accidental* mismatches.
2. **Add `ValidateOptions` record + `validateWith` variant + per-call opt-in.**
   Strict default protects existing users; permissive mode unlocks the
   TS pattern at the call site.
3. **Add a typed warning channel (`ValidationWarning`) alongside `ValidationError`.**
   Architecturally cleanest in isolation, but reshapes the result type for
   every consumer and introduces new conceptual surface (warning vs error
   semantics).
4. **Leave as-is; `Primitives` is the documented escape hatch.** Friction stays;
   every new TS-as-SoT consumer hits the same wall.

## Decision

**Option 2.** Add `ValidateOptions { AllowDimensionAliasingNumber: bool }`
in `Foundation/Errors.fs`, with `ValidateOptions.strict` and
`ValidateOptions.permissive` predefined values. Add a `validateWith` variant
of `Validation.validate`. Surface `*With` variants of every public import
function so callers can opt into the laxness at the call site:

```fsharp
Validation.validateWith                                : ValidateOptions -> TokenFile -> Result<unit, ValidationError list>
Api.importWith                                         : ValidateOptions -> string -> Result<...>
Api.importWithResolverWith                             : ValidateOptions -> loadFile -> context -> string -> Result<...>
Api.importWithResolverEvaluatingExtensionsWith         : ValidateOptions -> loadFile -> context -> string -> Result<...>
```

The legacy `validate` / `import` / `importWithResolver` /
`importWithResolverEvaluatingExtensions` keep their existing signatures, now
implemented as `*With ValidateOptions.strict ...`. **No existing call site
changes; no existing behavior changes.**

### Permissive scope is narrow on purpose

`AllowDimensionAliasingNumber = true` opts out **only** the
`dimension → number` mismatch. Other cross-type alias mismatches
(`dimension → color`, `color → string`, etc.) still produce `TypeMismatch`
errors under permissive. The flag is a one-pattern whitelist, not a general
type-coercion gate.

If future Tokens Studio patterns or other authoring conventions need
similar laxness, add narrow fields (`AllowDurationAliasingNumber`, etc.)
rather than a generic "allow cross-type aliases" toggle. Per-pattern fields
keep the intent obvious at the call site and resist scope creep.

## Rationale

Per-call opt-in is the right shape because:

- **Strict default protects accidents.** A consumer who *accidentally* mis-typed
  a token still sees the hard error; only consumers who *deliberately* use the
  TS pattern opt in. Option 1's "demote to warning" loses this distinction
  globally.
- **No reshape of the validation result type.** Option 3's warning channel
  changes `Result<unit, ValidationError list>` to a record with warnings,
  affecting every existing consumer. Option 2 adds a new function signature
  alongside the existing one; nothing existing changes.
- **Mirrors established library patterns.** Same shape as `IAcceptDataLoss`
  (ADR-028 / ADR-031, lossy export acknowledgment) and `DtcgSetRole`
  (ADR-030, mixed-format import marker): a single-purpose record/DU passed
  at the call site to make a deliberate choice visible.
- **Single record, future-extensible.** Adding fields to `ValidateOptions`
  doesn't break call sites that use the `ValidateOptions.strict` /
  `ValidateOptions.permissive` predefined values; only inline record
  constructions would need updating, and the doc steers callers away from
  those.

## Consequences

- `Validation.validate` continues to behave as it did in 0.9.0 — strict by
  default, preserving the safety net for accidental mismatches.
- TS-as-SoT consumers can now use the convenience wrappers by passing
  `ValidateOptions.permissive` — `importWithResolverEvaluatingExtensionsWith
  ValidateOptions.permissive loadFile context json` is one line.
- The "ADR-033 forces consumers to the Primitives path" friction is closed
  for consumers who want the convenience path. Consumers can still use
  Primitives if they want; the door isn't taken away.
- New API surface: 1 type (`ValidateOptions`), 1 module (`ValidateOptions`),
  4 new functions (`Validation.validateWith`, `Api.importWith`,
  `Api.importWithResolverWith`, `Api.importWithResolverEvaluatingExtensionsWith`),
  all surfaced in `Primitives` too.
- The pattern is extensible: future authoring conventions that warrant
  opt-in laxness add narrow fields to `ValidateOptions`. The strict-default
  posture is preserved.
- ADR-037 (warning channel as future possible route) is filed alongside this
  decision — option 3 is not built but is documented as a viable
  architectural direction for *other* kinds of advisory issues that are
  genuinely "notable but not fatal" (e.g. "this token is unused", "this scale
  step is outside typical visual range"). The dimension→number case is too
  important a footgun to demote to a warning, but option 3 might be the
  right answer for other concerns.

Shipped in v0.10.0 (2026-05-10).
