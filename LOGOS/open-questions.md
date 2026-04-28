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

### Q8. Negative test for `serializeAs` lossiness

Plan says it errors on lossy downgrade — needs an explicit test feeding a 2025.10 file with `gradient`/`shadow` to `serializeAs FirstED`, asserting the error.

**Decision needed:** Add to `VersionTests.fs` — confirm.

### Q9. Upgrade idempotence property

`parseAs V2025_10 >> serialize >> parseAs V2025_10 = id` for already-2025.10 input. Catches accidental mutation in the upgrade pass.

**Decision needed:** Add as a sixth Hedgehog property in `PropertyTests.fs` — confirm.

### Q10. `$schema` property handling

Files often have `$schema` pointing to a URL (e.g. `https://designtokens.org/schemas/2025.10/...`). Currently unaddressed.

**Options:**
- (a) Ignore on parse, never write on serialize
- (b) Preserve on parse (already in `TokenFile.Schema`), use as version detection hint, write back if present
- (c) Always write `$schema` on serialize, pointing to current spec

### Q11. Validate cubic-bezier ranges in `Validation.fs`

Plan mentions P1x/P2x ∈ [0,1] in the constraint list (line 524) — confirming this is on the implementation checklist for `Validation.fs`. P1y/P2y are unbounded per spec.

**Decision needed:** Confirm on checklist — no real choice, just don't forget.
