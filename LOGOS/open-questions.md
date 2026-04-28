# Open Questions — Pre-Implementation Review (2026-04-28)

A pre-implementation review surfaced these concerns and proposed improvements. Working through one at a time before any code is written.

## Concerns

### Q1. Order preservation of group children — **RESOLVED (a)**

`Map<TokenName, TokenNode>` sorts by key, breaking authoring order on round-trip. Decision: use `(TokenName * TokenNode) list` throughout — group children, `Sets`, `Modifiers`, `Contexts`, `Extensions`. O(n) lookup is acceptable: primary access pattern is full traversal (`flatten`); path lookup is ≤5 segments × ~tens of children per group. No new type to maintain.

### Q2. `JsonElement` lifetime for `$extensions` — **RESOLVED (a)**

`JsonElement` is tied to its parent `JsonDocument` lifetime — silently invalidates if document is disposed. Decision: use `JsonNode` (`System.Text.Json.Nodes`) — independent of any document, written back via `node.WriteTo(writer)`. Removes a footgun rather than documenting around it (matches the library's "make wrong states unrepresentable" ethos).

### Q3. Hex vs components conflict in color values — **RESOLVED (a)**

A color can carry both `components` and `hex`; spec doesn't define conflict behavior. Decision: in `Validation.fs`, for sRGB only, require hex match components within `1/255` tolerance — `ConstraintViolation` if not. Skip the check for non-sRGB (hex is approximate by design) and when any component is `Missing`. Strict-at-boundary posture catches real-world corruption without false-flagging legitimate non-sRGB approximations.

### Q4. Error collection pattern — **RESOLVED (a, via FsToolkit.ErrorHandling)**

Without an explicit accumulator pattern, parser code drifts to short-circuit on first error. Decision: applicative `Validation<'T, 'E>` via `FsToolkit.ErrorHandling`'s `validation { ... }` CE. Public API still returns `Result<'T, ParseError list>` — that's exactly what FsToolkit's `Validation` is (a type alias), so the dependency does not leak into consumers. Per user: "no external deps" is a preference, not a hard rule; FsToolkit is expected to spread across NEXUS.

### Q5. `parsedVersion` introspection — **RESOLVED (a)**

Decision: add `Version : SpecVersion` field to `TokenFile` (and `ResolverDocument`). Information that exists at parse time and might be useful later belongs on the value, not lost behind the API. `serialize` always writes 2025.10 (modernize on save); `serializeAs file.Version file` for callers that want to preserve source format.

### Q6. Round-trip property scope — **RESOLVED**

Property #1 in `PropertyTests.fs` is scoped to V2025_10 only: `parse (serialize file) = Ok file` for generated 2025.10 files. Older-version round-trips (parse-and-upgrade, `serializeAs` to a version the file fits) are covered by concrete example-based fixtures in `VersionTests.fs` — a fixed shape per version, not randomly generated. Avoids needing per-version constrained generators.

## Improvements

### Q7. `formatError` function — **RESOLVED**

One formatter per error type — `Errors.fs` has companion modules `ParseError`, `ValidationError`, `ResolveError`, `LoadError`, `ImportError`, each with a `format : T -> string` function. `formatImportError` re-exposed at top level (simple API). `formatParseError`, `formatValidationError`, `formatResolveError`, `formatLoadError` re-exposed in `Primitives`. Each returns one line `path: message`; `ImportError.format` adds a `[category]` prefix.

### Q8. Negative test for `serializeAs` lossiness — **RESOLVED**

Tests added to `VersionTests.fs`. Side-effect: `serializeAs` signature was wrong-shaped (returned `ParseError list`). Added new `SerializeError` DU with two cases (`UnsupportedInTargetVersion`, `UnsupportedColorSpace`) plus companion `format` and `Primitives.formatSerializeError`. Semantically distinct from parse and validation — the file is fine, it just doesn't fit the target version.

### Q9. Upgrade idempotence property — **RESOLVED (strengthened Property #1; not a sixth property)**

A separate idempotence property would overlap heavily with round-trip. Better: strengthen Property #1 to also assert `Version = V2025_10` on the round-tripped file. Catches detection misclassification and double-upgrade bugs without adding a sixth property. Version-detection correctness for older versions handled by example-based tests in `VersionTests.fs` (4 carefully-chosen fixtures beat 100 generated cases for a chain-of-heuristics decision).

### Q10. `$schema` property handling — **RESOLVED (b)**

Decision: preserve `$schema` verbatim on parse; use recognized URLs as the strongest version-detection signal (precedence above structural heuristics); contradiction between `$schema` URL and structure is a parse error (`SchemaVersionContradicts`); on serialize, write `$schema` only if it was present on input — never auto-inject. Library faithfully round-trips caller intent; callers who want `$schema` everywhere set the field themselves.

### Q11. Validate cubic-bezier ranges + cross-cutting finiteness — **RESOLVED**

Cubic-bezier P1x/P2x ∈ [0,1] confirmed on checklist (P1y/P2y unbounded per spec). Caught a related gap while reviewing: no constraint on float values being finite. Added cross-cutting "all `float` values must satisfy `Double.IsFinite`" check via shared `requireFinite` helper, applied to all numeric domain fields. Defensive against programmatic construction with `nan`/`infinity` — JSON parsing won't produce these by default, but F# code building values directly might.
