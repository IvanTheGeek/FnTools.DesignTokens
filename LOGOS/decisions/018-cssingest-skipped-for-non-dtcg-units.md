---
id: 018
title: CssIngest emits Skipped for non-DTCG units — never degrades silently
status: accepted
date: 2026-05-04
---

## Context

CSS custom properties use units that are valid CSS but have no DTCG 2025.10 counterpart:
`em`, `%`, `vw`, `vh`, `ch`, `fr`, `lh`. Similarly, `calc()` and `clamp()` expressions
are valid CSS values but not DTCG token values.

When `CssIngest` encounters `--cb-tracking-tight: -0.02em`, it cannot produce a valid DTCG
dimension token (DTCG allows only `px` and `rem` for dimension values). Four options:

1. **Strip the unit and emit a bare number** — e.g. `-0.02em` → `number -0.02`. Silent data
   corruption: the unit is discarded without any record.
2. **Emit a string token** — `"$type": "string", "$value": "-0.02em"`. Technically valid DTCG
   but loses type semantics and breaks the CSS emitter's type dispatch.
3. **Emit `Skipped` warning, no token** — the property is recorded in `IngestResult.Warnings`
   with the property name and reason. Nothing is emitted into the token file.
4. **Future: emit `CssNative` token type** — a named category for "valid CSS but not
   DTCG-tokenisable", surfaced in audit workflows.

The previous implementation stripped `em` silently (option 1). The `%`, `vw`, `vh`, etc.
units already produced `Skipped` because they failed `tryParseF`; only `em` had explicit
stripping logic.

## Decision

All non-DTCG units produce an explicit `Skipped (propertyName, reason)` warning and no
token. No value is silently degraded or truncated. `tryParseNumber` returns `None` for any
value with a unit suffix; the `em`-stripping code is removed.

Option 4 (`CssNative`) remains open as a future enhancement to the audit workflow but is
not part of `CssIngest` itself.

## Consequences

- `IngestResult.Warnings` is a complete record of every skipped property and why. Callers
  know exactly what was not tokenised.
- No silent data corruption. A value that cannot be expressed as a DTCG token is absent
  from the output, not present as a wrong type.
- `CssAudit` — the planned value scanner for non-`:root` CSS — will eventually surface
  `em`/`%` values under a `CssNative` category. `CssIngest` and `CssAudit` handle
  different concerns: ingest produces tokens from custom properties; audit catalogs all
  values (including those that cannot be tokenised) to inform the migration workflow.
- Components that use `em`-based spacing or `%`-based sizing must continue using those
  values directly — they are not expressible as DTCG tokens and the component layer
  (ADR 017) is the right owner.

## Addendum — `TokenValue.CssNative` is closed, not deferred (2026-05-10)

The original ADR left option 4 ("emit `CssNative` token type") "open as a future
enhancement." On review (session 2026-05-10), this is now explicitly **closed**.
The audit-time classification (`CssAudit.AuditValueType.CssNative`) is the
complete answer; there will be no first-class `TokenValue.CssNative` domain case.

Three reasons:

1. **No clean JSON shape exists.** DTCG 2025.10 has no `cssNative` or `string`
   token type. Every implementation route breaks something:
   - `$type: "cssNative"` produces files other DTCG consumers reject (violates ADR-014 and ADR-013).
   - `$type: "string"` has the same problem — `string` isn't in the spec.
   - `$type: "number"` + a vendor `$extensions.cssNative` value carries the
     unit only in the extension, lying about the token's actual type. This is
     option 2 in the original ADR ("lose type semantics") wearing a disguise.

2. **The component layer already owns these values (ADR-017).** Values like
   `clamp(2rem, 5vw, 3.6rem)` for hero typography or `0.22em` letter-spacing
   are element-relative by definition — they belong in the component's CSS,
   not in the portable design-system vocabulary. The whole point of `em` is
   "relative to the current element's font-size," which has no meaning at the
   token layer.

3. **No companion-package middle path.** Unlike the schema-validator close
   (ADR-013 addendum, 2026-05-10) where a downstream `FnTools.DesignTokens.SchemaCheck`
   could layer on top of `Foundation`, `TokenValue` is a closed DU in
   `Foundation`. A separate package cannot add a case to it. The choice is
   binary: either we add it here, or we close the question. We close it.

The user-visible behavior is unchanged from the original decision: `CssIngest`
emits `Skipped` warnings; `CssAudit` surfaces these values under the
`CssNative` classification so the bootstrap workflow routes them to the
component layer; the codec itself never produces or consumes a `CssNative`
token. The "future" wording in option 4 of the original decision is hereby
withdrawn.
