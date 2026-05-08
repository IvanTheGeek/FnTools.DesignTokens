---
id: 031
title: Math expression round-trip via tsMathExpression annotation
status: accepted
extends: 026 (same vendor namespace; adds one new key)
date: 2026-05-08
---

## Context

The shim evaluates Tokens Studio math expressions (e.g. `"round({base} * pow({multiplier}, 2))"`)
to concrete floats via `MathEval.tryEval` at import time (`EvaluateMath` policy). This is the
correct behaviour for DTCG consumers — the spec has no math syntax. However, the raw expression
string was discarded and never stored anywhere, so `exportToTokensStudio` could not restore it.
Penpot and Tokens Studio would receive the evaluated float (e.g. `25.6`) instead of the formula.

This breaks the TS → DTCG → TS round-trip for scale tokens, which are the primary use of math
expressions in real Tokens Studio files (e.g. Laura's type scale: `round({base} * pow({multiplier}, N))`).

## Decision

Extend the ADR-023/026 vendor namespace with one new key:

| Key | Written by | Consumed by | Purpose |
|---|---|---|---|
| `tsMathExpression` | shim `walkObj` | export `addTokensToObj` | Raw math expression before float evaluation |

### Annotation rule (shim side, `walkObj` annotation block)

Written when: `tsType = "number"` **and** `isMathExpression origRawValue` **and** `EvaluateMath`
succeeds (i.e. `transformToken` returns `Some`). If evaluation fails the token is omitted entirely —
no annotation is written, no recovery is needed.

### Recovery rule (export side, `addTokensToObj`)

`tsMathExpression` is checked first in the `recoveredValue` chain (before `originalHsl`,
`originalFontWeight`, etc.). When present, the stored string is used directly as `$value`,
replacing the evaluated float that `exportValue` produced.

### Stripping rules

- Added to `shimAllInternalKeys` — stripped from TS input on re-import (prevents re-processing
  our own annotations when a shimmed DTCG file is re-imported).
- Added to `shimExportStripKeys` — stripped from the `com.fntools.designtokens` vendor namespace
  in the TS output after recovery (the expression is the `$value`; the extension key is redundant).

## Rationale

Same annotate-on-shim / recover-on-export pattern as ADR-023 and ADR-026. No public API changes.
The annotation is free — `addVendorExtension` creates the vendor namespace lazily. Recovery is a
single `tryReadVendorString` call added to the front of the existing `recoveredValue` chain.

## Consequences

- **`exportToTokensStudio` now restores math expressions**: a scale token authored as
  `"round({base} * pow({multiplier}, 2))"` round-trips back to that exact string, not `25.6`.
- **Only `EvaluateMath` + success path is annotated**: `SkipMath` omits the token; `PreserveMath`
  passes the string through without evaluation (and without annotation — the string is already the
  `$value`). Failed evaluations omit the token entirely with a `MathEvalFailed` warning.
- **Alias-based math expressions**: expressions like `"round({base} * pow({multiplier}, N))"` that
  contain alias refs are evaluated against the flat token index. The raw expression string (including
  `{alias}` refs) is stored verbatim so Tokens Studio can re-resolve them against its own sets.

## Test coverage

`TokensStudioTests.fs` adds two tests:

- `ADR-031: tsMathExpression round-trip` — `"8 * 2"` evaluates to `16.0` on import; original
  expression `"8 * 2"` restored as `$value` on export; `tsMathExpression` key absent from output.
- `ADR-031: plain number has no tsMathExpression extension` — `"16"` (no math operators) does not
  trigger the annotation; value emitted as JSON number `16`.

## References

- `LOGOS/decisions/023-tokens-studio-export-extensions-carrier.md` — ADR establishing the vendor namespace.
- `LOGOS/decisions/026-shim-annotation-recovery-pattern.md` — ADR this one extends.
- `src/FnTools.DesignTokens.TokensStudio/TokensStudio.fs` — `extMathExpressionKey`,
  `shimAllInternalKeys`, `shimExportStripKeys`, annotation block (5), `addTokensToObj` recovery chain.
