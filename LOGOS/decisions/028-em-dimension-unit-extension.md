---
id: ADR-028
status: accepted — 2026-05-04
area: domain types
---

# ADR-028 — Em dimension unit as deliberate spec extension

## Context

DTCG 2025.10 §7.4.6 defines exactly two valid values for `$value.unit` in
dimension tokens: `"px"` and `"rem"`. The spec authors chose these units
deliberately for platform portability — `px` maps to Android `dp`/iOS `pt`; `rem`
maps to a system-relative font-size multiplier. `em` (relative to the current
element's font-size) is CSS-specific and has no cross-platform equivalent.

Tokens Studio and Penpot both carry `em`-unit values, particularly for
`letter-spacing` tokens. A lossless TS ↔ DTCG round-trip through this library
requires preserving the original unit. Without `DimensionUnit.Em` in the domain,
the shim must either:

- (a) strip the unit and store a bare number — lossy and semantically wrong, or
- (b) emit an error and skip the token — prevents any use of TS letter-spacing in
  the pipeline.

Both alternatives degrade the round-trip for a common real-world pattern.

## Decision

Add `Em` to `DimensionUnit`:

```fsharp
type DimensionUnit = Px | Rem | Em
```

`Em` is a deliberate extension beyond the DTCG 2025.10 spec. It is documented as
such and must not appear in strict DTCG 2025.10 output unless the caller has opted
into extension tokens.

### Where `Em` is valid

- **Domain**: `DimensionValue` with `Unit = Em` is a legal value; the resolver,
  CSS emitter, and TokensStudio shim all handle it.
- **TS/Penpot export**: `exportToTokensStudio` emits `"unit": "em"` — Tokens
  Studio and Penpot accept it.
- **CSS emission**: `dimensionToCss` produces `"0.22em"` — valid CSS.

### Where `Em` is rejected

- **CssIngest**: `em` values from CSS source are classified as `CssNative` and
  emitted as `Skipped` warnings. The authoring direction produces portable tokens,
  not component-layer CSS. `em` has no cross-platform meaning, so it belongs in
  component code, not token files.
- **Strict DTCG 2025.10 serialisation**: a future strict-mode serialiser should
  treat `Em` as an error or map it to `px` with a data-loss acknowledgment (same
  pattern as `IAcceptDataLoss` for older spec versions).

## Rationale

The existing `IAcceptDataLoss` pattern (ADR for lossy export) already establishes
that the library can support non-spec values with explicit, documented opt-in.
Adding `Em` to the DU is lower friction than any alternative: the type system
ensures exhaustive handling everywhere a `DimensionUnit` is matched; the compiler
surfaces any new pattern-match site immediately.

The single-library approach (one domain covering spec + extensions) is preferred
over a separate "extensions" type because:

- Token files from TS/Penpot are the primary real-world input.
- The shim already operates as a controlled, documented deviation from strict
  DTCG.
- Extensions added through the DU are checked at compile time; extensions added
  through string escape hatches are not.

## Consequences

- `Format.parseDimensionUnit` now accepts `"em"` → `Ok Em`.
- `Format.dimensionUnitToString` now emits `"em"` for `Em`.
- `CssEmitter.dimensionToCss` (and `dimensionWithPolicy`) emit `"em"` for `Em`.
- `CssAudit.tokenValueToCssStrings` emits `"em"` for `Em` (for token-value
  matching against audit entries).
- `TokensStudio.fs`: `dimStr` emits `"em"` for `Em`.
- All files that match on `DimensionUnit` updated to handle `Em` at compile time —
  0 incomplete-pattern warnings after the change.
- `insights.md` updated: entry "DTCG dimension units are `px` and `rem` only"
  corrected to reflect `Em` support and accurate `CssIngest` behavior.

## Addendum — strict-mode validator built (2026-05-10)

The "future strict-mode serialiser" mentioned in the Rejected section is now
built, taking the alternate form discussed in the original ADR's parenthetical
("a future strict-mode serialiser should treat `Em` as an error"):
`Validation.validateStrictDtcg : TokenFile -> Result<unit, ValidationError list>`.

Walks the file, reports any literal `DimensionValue` with `Unit = Em` (direct
or inside Border/Shadow/Typography/StrokeStyle composites) as a
`ConstraintViolation`. References (aliases) are not followed — only literal
positions are checked, matching the rest of the validation layer's convention.

Design choice (vs. a fallible `serializeStrict`):

- `Format.serialize` stays infallible, preserving the ADR-012 principle that
  serialisation of a structurally valid file cannot fail.
- Strictness becomes a separate concern: validation is where "is this
  acceptable?" lives; serialise is where "render it" lives. This mirrors how
  ADR-033 placed the `dimension`→`number` cross-type check in validation
  rather than in the emitter.
- The "map `Em` to `px` with `IAcceptDataLoss`" coercion option from the
  original wording was rejected: `em` is element-relative; no general
  numeric conversion exists.

Surfaced via `Api.validateStrictDtcg` and `Api.Primitives.validateStrictDtcg`;
documented in `docs/api-reference.md`. 10 new tests cover the common
extension positions plus error-collection-not-short-circuited (ADR-002 still
applies) and the separation-of-concerns guarantee that regular `validate`
still accepts `Em` (extension is allowed in the domain; only opt-in strictness
rejects it).

When future extensions are added to the domain (e.g., a new dimension unit or
color space), add the corresponding rejection in `Validation.fs`
`nonSpecTokenValue` and a test asserting the new case. The "extensions
gather here" pattern keeps the strict checker the one place to find the
catalogue of library-level deviations from the spec.
