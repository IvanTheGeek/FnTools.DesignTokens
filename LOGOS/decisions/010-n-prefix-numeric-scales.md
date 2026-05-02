---
id: 010
title: N-prefix for numeric token names in generated F# bindings
status: accepted
date: 2026-05-02
---

## Context

Industry-standard token naming uses numeric scales: `color.blue.500`, `spacing.4`, `fontWeight.700`. These are valid DTCG token names (JSON object keys). They are not valid F# identifiers — identifiers cannot start with a digit.

Options considered:
1. Require authors to use non-numeric names (`blue-mid`, `space-sm`)
2. Emit backtick-escaped identifiers (`` ``500`` ``)
3. Prefix with `N` in generated code only (`N500`)

## Decision

DTCG token files use numeric scales as-is (industry standard; Penpot and other tools expect them). The bindings emitter adds an `N` prefix to produce valid F# identifiers: `color.blue.500` → `Tokens.Color.Blue.N500`.

The `N` prefix is applied in the **emitter only** — a single transformation point. Token files, CSS var names, and resolver paths use the numeric form unchanged.

## Consequences

- Token file authors use the same naming they would in any other DTCG toolchain.
- F# consumers get compile-time-checked references: `Tokens.Color.Blue.N500`.
- CSS vars use the numeric form: `--color-blue-500` (no N prefix in CSS output).
- The emitter must apply the N prefix consistently to any path segment that starts with a digit.
- If a token path segment is a reserved F# keyword (e.g., `default`, `type`), the emitter also backtick-escapes it: `` ``default`` ``.
