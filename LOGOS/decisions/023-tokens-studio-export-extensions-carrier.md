---
id: 023
title: Tokens Studio export uses $extensions as the primary lossy-metadata carrier
status: accepted
supersedes: 022 (lossy-metadata carrier choice only; preserve-aliases stands)
date: 2026-05-04
---

## Context

ADR-022 chose `$description` as the carrier for wide-gamut color metadata that
cannot survive the conversion to sRGB hex required by the Tokens Studio /
Penpot import surface. The annotation form
`"source: color(oklch 0.7 0.2 120) — converted to sRGB hex …"` is human readable
and round-trips through Penpot, but it is inherently brittle:

1. Mechanical recovery requires regex-parsing free-text English.
2. It collides with author-supplied descriptions (the exporter has to append
   to existing prose).
3. It is lossy — channel-specific markers like `none`, alpha precision,
   colorSpace identifiers with hyphens (`display-p3`, `prophoto-rgb`) all have
   to be re-derived from the formatted string.

DTCG §3.5 reserves `$extensions` for vendor metadata explicitly: tools "should
preserve" extensions they do not understand. That is a structurally cleaner
carrier — typed JSON, namespace-isolated, no human-language parsing required.

The empirical Penpot test in
[penpot-extensions-preservation-test.md](../penpot-extensions-preservation-test.md)
established that Penpot strips `$extensions` (Plugin API has no slot, internal
transit storage has no field, exports omit them). So `$extensions` alone is not
sufficient for a Penpot-stage round-trip. But it is the right carrier for any
pipeline that does not pass through Penpot, and we lose nothing by emitting it
alongside the `$description` annotation.

## Decision

**Emit both carriers; favour `$extensions` on import.**

The exporter writes wide-gamut color data to **both**:

1. `$extensions["com.fntools.designtokens"]["originalColor"]` — a structured
   DTCG color JsonObject with `colorSpace`, `components`, optional `alpha`,
   optional `hex`. This is the primary carrier.
2. `$description` — the existing human-readable annotation from ADR-022,
   appended to any author-supplied description. This is the Penpot-survival
   companion.

The importer reconstructs the wide-gamut color in priority order:

1. **`$extensions` payload present** → use it directly. Exact round-trip.
2. **Only `$description` annotation present** → parse the annotation string
   and rebuild the DTCG color. Lower fidelity (no `hex` recovery; component
   parsing by regex) but functional.
3. **Neither present** → keep the lossy sRGB hex from `$value`. Documented
   degradation; the `$type: color` token still parses.

The importer also preserves any author-supplied extensions verbatim through
the round-trip; only the `originalColor` marker under our vendor namespace is
stripped on the way out (it is a transport artifact, not canonical DTCG data).

**Vendor namespace:** `com.fntools.designtokens` (reverse-DNS, project-level).
Only one key under it today (`originalColor`), but the namespace gives us room
to add other carriers without further design work.

**Public API:** unchanged. `Api.exportTokensStudio` and the shim consume the
same types as before — the dual-carrier emission is internal.

## Rationale

| Scenario                                | ADR-022 only        | ADR-023            |
|-----------------------------------------|---------------------|--------------------|
| Pure DTCG round-trip (no Penpot)        | regex-parsing       | exact match        |
| Tokens Studio web app round-trip        | regex-parsing       | exact match        |
| Penpot-as-stage round-trip              | regex-parsing       | regex-parsing¹     |
| Authoring tools that strip extensions   | regex-parsing       | regex-parsing      |
| Authoring tools that strip both         | sRGB hex (degraded) | sRGB hex (degraded)|

¹ Penpot strips extensions but preserves `$description`, so the description
fallback path activates.

The dual-carrier choice is therefore strictly an upgrade: every scenario
ADR-022 handled is preserved or improved.

## Consequences

- **Three-tier graceful degradation** is observable in the codebase:
  `tryRecoverWideGamutColor` checks `$extensions` first, falls back to
  `$description` parsing, falls back to nothing (in which case the literal
  sRGB hex from `$value` is used).
- **`ExportWarning.LossyColorConversion`** still fires once per wide-gamut
  color even when `$extensions` is emitted — the warning is a contract
  signal that an out-of-gamut conversion happened, independent of whether a
  recovery carrier was emitted. Consumers that want to know whether
  round-trip is possible should rely on the carrier presence, not the warning.
- **Author-supplied `$extensions`** are merged: a non-`com.fntools.designtokens`
  vendor namespace passes through unchanged on both export and import sides.
  Tests cover this passthrough explicitly.
- **Penpot stage** still loses the structured payload; the description
  annotation is the only thing that survives. ADR-022's reasoning for keeping
  the description carrier is therefore still correct — the change is to add
  `$extensions` alongside, not to replace.
- **Walker fix (incidental):** the shim now inherits `$type` from group level
  per DTCG §7.4, so the export → shim → import round-trip works for tokens
  whose type is declared on a parent group rather than the leaf. This was a
  latent bug exposed by the round-trip tests added for this ADR.

## Test coverage

`TokensStudioTests.fs` adds five round-trip tests covering:

- `oklch wide-gamut DTCG → TS export → TS shim → DTCG parse preserves ColorValue`
  — exact match through the structured carrier.
- `extension-stripped TS JSON falls back to $description annotation` —
  Penpot-equivalent: only the description survives.
- `both extensions and description stripped → falls back to lossy sRGB hex`
  — worst case: degraded but parses cleanly.
- `import priority: $extensions wins over $description when both are present`
  — proves the ordering when the two carriers conflict.
- `extensions passthrough: user-authored vendor extensions survive round-trip`
  — DTCG §3.5 conformance for unrelated vendor namespaces.

## References

- `LOGOS/penpot-extensions-preservation-test.md` — empirical Penpot behaviour.
- `LOGOS/decisions/022-tokens-studio-export-preserve-aliases.md` — the ADR
  this one supersedes (lossy-metadata carrier only; preserve-aliases stands).
- DTCG 2025.10 §3.5 (`$extensions`), §7.4 (type inheritance).
- Penpot tracker issue: <https://github.com/penpot/penpot/issues/9307>.
