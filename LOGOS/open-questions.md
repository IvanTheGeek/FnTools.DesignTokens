# Open Questions — Pre-Implementation Review (2026-04-28)

A pre-implementation review surfaced these concerns and proposed improvements. Working through one at a time before any code is written.

## Concerns

### Q1. Order preservation of group children — **RESOLVED (a)**

`Map<TokenName, TokenNode>` sorts by key, breaking authoring order on round-trip. Decision: use `(TokenName * TokenNode) list` throughout — group children, `Sets`, `Modifiers`, `Contexts`, `Extensions`. O(n) lookup is acceptable: primary access pattern is full traversal (`flatten`); path lookup is ≤5 segments × ~tens of children per group. No new type to maintain.

### Q2. `JsonElement` lifetime for `$extensions` — **RESOLVED (a)**

`JsonElement` is tied to its parent `JsonDocument` lifetime — silently invalidates if document is disposed. Decision: use `JsonNode` (`System.Text.Json.Nodes`) — independent of any document, written back via `node.WriteTo(writer)`. Removes a footgun rather than documenting around it (matches the library's "make wrong states unrepresentable" ethos).

### Q3. Hex vs components conflict in color values

A color can have both `components: [1, 0, 0]` and `hex: "#0000ff"`. Spec doesn't define precedence on conflict.

**Options:**
- (a) Validate they match — error if hex doesn't match components (within tolerance)
- (b) Components win; hex is informational only — document this
- (c) Preserve both as parsed; let consumer decide

### Q4. Error collection pattern

The plan commits to `Error list` everywhere, but most parser idioms in F# short-circuit naturally with `let!` / `Result.bind`. Without an explicit accumulator pattern, parser code will quietly degrade to first-error behavior.

**Options:**
- (a) Build a small `Validation<'T, 'E>` applicative-style type with a `validation { ... }` CE
- (b) Use explicit `ResizeArray<Error>` accumulators in parser functions (mutable but local)
- (c) Functional accumulation with `List.fold` and explicit error-list return values everywhere

### Q5. `parsedVersion` introspection

After upgrade-on-parse, callers can't tell what version the file was. For UX like "this file uses outdated First ED format, suggest upgrade" — common in design tools — the version needs to be exposed.

**Options:**
- (a) Add `Version : SpecVersion` field to `TokenFile`
- (b) Return `(TokenFile * SpecVersion)` from `parse`
- (c) Add `Primitives.detectVersion : string -> Result<SpecVersion, ParseError list>` separately

### Q6. Round-trip property scope

`serializeAs FirstED` of a file with composite types is intentionally lossy. The Hedgehog round-trip property must be explicit about which targets it tests.

**Decision needed:** Property should be `parse >> serialize >> parse = id` for V2025_10 only. `serializeAs` lossiness gets a separate negative test.

## Improvements

### Q7. `formatError` function

The error DUs are great for programmatic handling but humans (and AI consumers) need readable messages. One pretty-printer per error type, called by IDE plugins and CLI tools.

**Decision needed:** Add to Primitives or top-level? Single function with pattern match on a union, or one per error type?

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
