---
id: 011
title: $extensions values are always round-tripped without inspection
status: accepted
date: 2026-05-03
---

## Context

DTCG files can carry `$extensions` on any token or group — a free-form JSON object for tool-specific metadata. Figma writes `com.figma.*` keys; Style Dictionary writes `com.style-dictionary.*`; others write their own. The library has no schema for these keys and cannot know their semantics.

Three approaches were considered:

1. Drop unrecognised extensions on parse — lossy, non-conformant
2. Fail or warn on unrecognised extension keys — breaks valid files from any tool
3. Store and re-emit verbatim, regardless of content

## Decision

Store all `$extensions` values as `Map<string, JsonElement>`. Always round-trip them unchanged through parse → serialize. A serializer that drops unknown extensions is explicitly non-conformant with the DTCG spec.

## Consequences

- `JsonElement` preserves the raw JSON without further parsing — extension values cannot be inspected without `System.Text.Json` APIs. This is intentional: the library has no business interpreting them.
- Files produced by Figma, Style Dictionary, or any other tool survive a round-trip through this library without data loss.
- If the library later recognises a previously-unknown extension key, existing serialised values still round-trip correctly — the `Map` is additive.
- `$extensions` is not validated for shape or content. Malformed extension values are carried through as-is.
