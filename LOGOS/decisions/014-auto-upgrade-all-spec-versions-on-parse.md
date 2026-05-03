---
id: 014
title: All four DTCG spec versions are auto-upgraded to 2025.10 on parse
status: accepted
date: 2026-05-03
---

## Context

Four DTCG versions exist: First Editors' Draft (2021), Second Editors' Draft (2022), Third Editors' Draft (2025-08), and 2025.10 (stable). Real-world files use all four — Figma exports Second ED; hand-authored files may be any version. Three approaches were considered:

1. Support only 2025.10; reject older files with a clear error
2. Detect version, expose it to the caller, let the caller decide how to proceed
3. Auto-detect and upgrade losslessly to the 2025.10 domain model on parse

## Decision

Auto-detect the spec version and upgrade losslessly to 2025.10 on parse. Callers always work against one type system regardless of file origin. `TokenFile.Version` records what was detected, but callers never need to branch on it.

## Consequences

- All four upgrade paths must be maintained as real-world tooling continues to produce older formats.
- Upgrades are lossless: hex string colors → `ColorValue { Hex = Some "..." }` (original hex preserved); dimension strings `"12px"` → `DimensionValue { Value = 12.0; Unit = Px }` (no information dropped).
- `serializeAs (target: SpecVersion)` allows writing older formats when a downstream tool requires them. This path may be lossy — OKLCH colors cannot round-trip to Second ED hex without gamut-mapping math that is explicitly out of scope (ADR-013). The `IAcceptDataLoss` parameter makes this explicit at every call site.
- Version detection is structural: `$schema` URL → known version mapping; absence of `$` prefix on `type`/`value` → First ED; etc. Ambiguous files fail with `SchemaVersionContradicts` rather than guessing.
