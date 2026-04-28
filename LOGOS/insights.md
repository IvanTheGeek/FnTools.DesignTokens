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

## Promotion Candidates → NEXUS-LOGOS

- "Error collection is a first-class design constraint" — applies to any domain parser in NEXUS
- "I/O belongs to the caller" — general principle for all NEXUS libraries with external sources
