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
