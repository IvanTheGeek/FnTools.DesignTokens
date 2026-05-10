---
id: 013
title: Library scope ends at the DTCG interchange boundary
status: accepted
date: 2026-05-03
---

## Context

The library could grow beyond parsing and serializing DTCG files. Genuinely useful additions include: value derivation (compute a tint from a base color), type coercion (convert all OKLCH colors to sRGB), semantic token operations beyond what the DTCG resolver covers, or component token management.

Each of these is useful to *someone*. The question is whether they belong in this library.

## Decision

This library's responsibility ends at the DTCG file format boundary. It reads and writes DTCG-compliant JSON; it has no opinion about design systems, component structure, states, or non-DTCG representations. Operations that require understanding what tokens *mean* belong to higher layers.

## Consequences

- The library is strictly testable against the DTCG spec. Any parse or serialization decision can be verified against the published JSON schemas at `/home/ivan/nexus/VARIOUS/community-group/`.
- Lax parsing is never introduced. Non-conformant input is always an error, not a best-effort interpretation.
- ATLAS/NEXUS internal token representations maintain their own translation logic when crossing the DTCG boundary — the library is not extended to accommodate them.
- The library can be published as a general-purpose .NET DTCG codec for any consumer outside NEXUS. Its API surface stays small and its semantics stay predictable.
- Value derivation (tint/shade computation, gamut mapping) belongs to a separate utility — not here. The `colorToHexString` fallback in `serializeAs` for OKLCH colors is a documented gap, not a design goal.

## Addendum — JSON Schema validation against `$schema` URL (2026-05-10)

ADR-003 left a forward reference to "a future schema validator that fetches `$schema` URLs."
After review, this is explicitly **out of scope for this library**, for the following reasons:

- Our domain validation (`Validation.validate`) is **stricter than the DTCG JSON Schema** at every type:
  it enforces alpha range, hex/components consistency, cubic-bezier coordinate ranges,
  fontWeight numeric range, alias-cycle absence, and cross-type alias type-mismatches (ADR-033).
  None of those are expressible in JSON Schema.
- The parser itself acts as a hand-written schema validator that produces typed domain values
  with DTCG-meaningful error messages (`MissingRequiredField`, `InvalidValue`, `UnknownTokenType`).
  A JSON Schema check before parse would duplicate this work with weaker error messages.
- The DTCG 2025.10 schema explicitly notes that `$schema` is not part of the specification
  (see `format.json` `$comment` on the `$schema` property) — it is a courtesy convention for
  editor tooling, not a compliance requirement.
- `$schema` is already used for the one purpose where it adds real value here: spec-version
  detection on parse, with `SchemaVersionContradicts` surfaced when the URL disagrees with
  the structural version.

**If a consumer needs schema-compliance verification as a separate concern**, the right home
is a companion package, not an addition to this library. A plausible shape:

- Package name: `FnTools.DesignTokens.SchemaCheck`
- Depends on this library's `Foundation` only — operates on raw JSON strings before parse
- Takes a `loadSchema : string -> Result<string, string>` parameter (the ADR-003 pattern —
  any external-resource layer uses caller-supplied I/O)
- Resolves the schema's `$ref` chain (`format.json` → `format/group.json`, `format/token.json`,
  `format/tokenType.json`, `format/values/*.json`) the same way
- Pulls in a JSON Schema validator dep (e.g. `JsonSchema.Net`) — kept out of this library
  per ADR-005's selective-dependency posture
- Returns its own `SchemaCheckError` type — distinct from `ParseError` / `ValidationError`
  so consumers can tell the two layers of checking apart

This is a sketch, not a commitment. The package is filed in `tasks-open.md` under
**Candidate companion packages** for someone to pick up when there is a concrete consumer
who needs it. Until then, the gap is intentional and documented here.
