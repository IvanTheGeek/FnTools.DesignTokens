## `"none"` is not null — it is a semantic keyword

In CSS Color Level 4 (which DTCG Color module is based on), the `none` keyword in color components means "missing" or "inapplicable" — it participates in color interpolation differently from `0`. For example, in `oklch(0.5 0.2 none)`, the missing hue means the hue channel is powerless during interpolation and can adopt the other color's hue. Modeling this as `float option` or `0.0` would be semantically wrong. It must be a distinct DU case.

## Gradient in DTCG is only color stops

DTCG gradient does not specify direction, angle, type (linear/radial/conic), or repeat. Those are platform rendering concerns. The spec intentionally excludes them — the token captures only the color progression (stops + positions). Code generators apply the stops to platform-specific gradient syntax. This means a `GradientValue` in this library is simply `GradientStop list` — no direction field, ever.

## `$type` is declared, never inferred

The spec explicitly forbids inferring token type from value shape. A bare JSON number could be `number`, `fontWeight (Numeric)`, `cubicBezier (P1x)`, or a component of something else — there is no way to know without `$type`. Missing or ambiguous type is always a hard error, never a guess. This keeps the parse function simple: dispatch on declared type, fail if none.

## Two reference syntaxes serve different purposes

Curly-brace `{group.token}` references an entire token node — used for aliasing one token to another. JSON Pointer `{ "$ref": "#/..." }` references a sub-property of a value — used when you need one component of a color, one stop of a gradient, etc. Curly-brace cannot do sub-property access. Most real-world files only use curly-brace; JSON Pointer is a power-user escape hatch.

## Error collection is a first-class design constraint

The `parse` and `validate` functions must collect all errors before returning, not short-circuit on the first. This is explicitly because design tool authors need to fix multiple issues at once — showing one error, fixing it, then showing the next error is a poor UX for token file authors. The `ParseError list` / `ValidationError list` return types are not an implementation detail; they are a spec-level expectation.

## `$extensions` preservation is a contract, not an option

Tools that consume DTCG files must round-trip `$extensions` data they do not understand. This is why `Extensions` is `Map<string, JsonElement>` — the `JsonElement` type preserves the raw JSON value without parsing it into a domain type. Any serializer that drops unknown extensions is non-conformant, even if its own logic never reads them.

## I/O belongs to the caller

The resolver's `loadFile: string -> Result<string, string>` parameter keeps all file system access outside the library. This is not just a testability convenience — it is the only correct design for a library that will run in Figma plugins, .NET CLI tools, WASM targets, and CI pipelines. Each host has different file access constraints. The library is pure; the host provides the I/O.

## Convenience tier is a library responsibility, not a caller responsibility

"Parse then validate" is the canonical composition — every caller wants it, in that order. "Resolve then follow aliases" is the same. Leaving callers to independently compose primitives means every caller can independently get the composition wrong. When the correct composition is unambiguous, the library should offer it directly. This applies to any library with multi-step pipelines.

The specific pattern: expose a **primitives tier** (parse, validate, flatten, resolve) for advanced use, and a **convenience tier** (load, flattenResolved, resolveAll) for common paths. The convenience functions are thin compositions of primitives — no logic that couldn't be written by the caller, but named and documented so callers default to them.

## Eliminate footguns structurally, not documentarily

`TokenValue` has an `Alias` case. After calling `resolve`, the returned `TokenFile` still contains `Alias` values — aliases are not automatically followed. An AI agent or junior consumer encountering `| Alias ref -> ???` will either skip it or mishandle it. Documentation warning them is not sufficient.

The fix: `flattenResolved` follows every alias before returning, making it structurally impossible to receive an `Alias` token through that path. The XML doc comment guarantees this explicitly. For AI consumers especially, removing the footgun from the type system (or the API contract) is always better than documenting around it.

## Scope: this library is the DTCG interchange boundary

This library's job ends at the file format boundary. It reads and writes DTCG-compliant files; it has no opinion about design systems, components, states, or ATLAS internals. ATLAS maintains its own internal token representation and calls this library only when crossing the DTCG boundary (Figma import, tool export). The translation between ATLAS internals and DTCG domain types is ATLAS's responsibility.

Corollary: this library should be strict about DTCG compliance. Lax parsing at the interchange boundary silently corrupts data crossing tool boundaries.

## The full layer model — where this library sits

```
[ 7 ] Prototype / Walker       clickable path simulation
[ 6 ] Screen / Layout          composing components into screens
[ 5 ] Variants & States        component variant axes + interaction states
[ 4 ] Component Builder        define structure, bind tokens, preview, catalog
[ 3 ] Design System            three-tier token model, named sets, inheritance chain
[ 2 ] ATLAS token runtime      internal live graph (reactive, richer than DTCG)
[ 1 ] NEXUS-Tokens             DTCG codec — import/export boundary  ← this library
[ 0 ] .tokens.json files       Figma, Penpot, Style Dictionary output
```

NEXUS-Tokens is Layer 1. It is touched only when crossing the interchange boundary — importing from an external tool or exporting back out. Layers 2 and above use their own richer runtime representations. The codec produces a `TokenFile` (or `ResolvedToken seq` via the convenience tier) that the Layer 2 runtime initialises from.

## Design system inheritance maps directly to the DTCG resolver

The design system inheritance chain (Cheddar → CheddarBooks → LaundryLog) is modelled as a `.resolver.json` with sources in resolution order:

```json
"resolutionOrder": [
  { "set": "cheddar-primitives" },
  { "set": "cheddar-semantic" },
  { "set": "cheddar-books-overrides" },
  { "set": "laundrylog-overrides" },
  { "modifier": "theme" }
]
```

Later entries win. LaundryLog defines only its differences from CheddarBooks; CheddarBooks defines only its differences from Cheddar. The resolver produces the complete merged token set. This library handles that merge — the inheritance chain for tokens is already solved at Layer 1.

Component-level inheritance (a LaundryLog button extending a CheddarBooks button) is above this library's scope — that belongs to the Layer 3/4 component model.

## Promotion Candidates → NEXUS-LOGOS

- "Error collection is a first-class design constraint" — applies to any domain parser in NEXUS
- "I/O belongs to the caller" — general principle for all NEXUS libraries with external sources
