---
id: 032
title: serializeResolver — ResolverDocument serialization
status: accepted
date: 2026-05-08
---

## Context

`parseResolver` / `validateResolver` / `resolve` / `toResolverDocument` all exist, but there
was no way to write a `ResolverDocument` back to JSON. The library could build a resolver
document from a Tokens Studio source (via `toResolverDocument`) and resolve it to a flat token
list, but could not persist the document itself. Any consumer that wanted a DTCG resolver
document as a source-of-truth file had no serialization path.

## Decision

Add `serializeResolver (doc: ResolverDocument) : string` in `Resolver.fs`.
Expose it at the top level of the meta-package (`DesignTokens.fs`) and in `Primitives`.

### JSON format produced

Mirrors the format `parseResolver` consumes:

```json
{
  "name": "...",
  "version": "2025.10",
  "description": "...",
  "sets": {
    "set-name": {
      "sources": [{ "inline": { ...TokenFile... } }],
      "description": "...",
      "$extensions": { ... }
    }
  },
  "modifiers": {
    "modifier-name": {
      "contexts": {
        "context-name": { "sources": [...] }
      },
      "default": "context-name",
      "description": "...",
      "$extensions": { ... }
    }
  },
  "resolutionOrder": [
    { "set": "set-name" },
    { "modifier": "modifier-name", "context": "context-name" }
  ]
}
```

### Specific choices

- **`Inline` sources**: embedded as `{ "inline": <Format.serialize file> }`. The inner
  `TokenFile` is serialized via the existing `Format.serialize` and parsed back as a
  `JsonNode` for embedding.
- **`FileRef` sources**: written as `{ "path": "..." }`.
- **`$ref` pointers never emitted**: `$ref` is a parse-time optimization for shared
  definitions; the serializer always writes concrete objects. Round-trip means
  `parseResolver (serializeResolver doc)` returns a structurally equivalent document,
  not byte-identical JSON.
- **Optional fields omitted when `None`**: `name`, `description`, set `description`,
  modifier `default`, modifier `description`.
- **Empty `sets`/`modifiers` omit the key**: keeps output clean for documents that use
  only sets or only modifiers.
- **`$extensions` written when non-empty**: both `SetDefinition.Extensions` and
  `ModifierDefinition.Extensions` are serialized correctly so extensions survive the
  round-trip.
- **`ResolutionItem` always object form**: `SetRef "name"` → `{"set": "name"}`,
  `ModifierRef ("m", "c")` → `{"modifier": "m", "context": "c"}`. The parser also
  accepts plain strings for `SetRef` but object form is explicit and symmetric.
- **Version always `"2025.10"`**: only version supported; written unconditionally.

### Implementation location

`Resolver.fs` — the only project that knows `ResolverDocument` and already depends on
`Format` (for `Format.parse` in `parseTokenSource`). Calls `Format.serialize` for
`Inline` sources.

## Rationale

Basic symmetry: a type with a parser needs a serializer. The format is fully determined
by `parseResolver` — no design decisions required. Placing the function in `Resolver.fs`
keeps the dependency direction clean (Format → Resolver, not the reverse).

## Consequences

- **LauraExperiment SoT workflow unlocked**: `toResolverDocument` converts the TS
  working source → `ResolverDocument`; `serializeResolver` writes it to
  `laura.resolver.json` — a single DTCG file containing all token sets and axis
  definitions. `parseResolver` + `resolve` reads it back at emit time. The TS working
  source becomes optional after the initial conversion.
- **No breaking changes**: entirely additive. New function in `Resolver.fs`; new
  binding in `Primitives` and top-level API.

## Test coverage

`ResolverTests.fs` adds five tests:

- `serializeResolver: round-trip preserves sets, modifiers, resolution order` — parse
  `basicResolverJson`, serialize, re-parse; verify name, version, set/modifier names,
  modifier default/contexts, resolution order length.
- `serializeResolver: Inline sources embedded without $ref` — output contains
  `"inline"`, does not contain `"$ref"`.
- `serializeResolver: FileRef source round-trips as path object` — `FileRef` path
  preserved after serialize → parse.
- `serializeResolver: optional name/description omitted when None` — document without
  name/description; keys absent from serialized output.
- `serializeResolver: set and modifier extensions round-trip` — extensions injected on
  a set definition survive serialize → parse; value preserved.

## References

- `src/FnTools.DesignTokens.Resolver/Resolver.fs` — `serializeResolver`,
  `tokenSourceToNode`, `sourcesToArray`, `writeExtensionsObj`.
- `src/FnTools.DesignTokens/DesignTokens.fs` — top-level `serializeResolver`,
  `Primitives.serializeResolver`.
