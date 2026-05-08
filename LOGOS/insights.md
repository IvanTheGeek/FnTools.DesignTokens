## `JsonNode.ToString()` returns the raw value, not the JSON encoding

`System.Text.Json.Nodes.JsonValue.Create("test").ToString()` returns `test` — the raw string value — not `"test"` (the JSON-encoded form with quotes). To get the JSON encoding, use `.ToJsonString()`. The distinction matters in tests and anywhere you compare a `JsonNode` value to a JSON-formatted string. Rule: use `.ToString()` when you want the semantic value (e.g. to compare against an F# string); use `.ToJsonString()` when you want the serialized form (e.g. to check that a key appears in a JSON output).

## Annotate shim losses at the point of transformation, not at the parse boundary

The math-expression round-trip (ADR-031) and color/type annotation patterns (ADR-023/026) share a structure: the loss site (inside `transformToken` / `walkObj`) is the right place to record the original value, not the input parser or the output emitter. By the time the emitter runs, the original is gone. Annotation at the loss site keeps recovery data in `$extensions` alongside the transformed token — always present, always co-located, never needs threading through intermediate structures.

## Named warning DU cases per source type

`SetSkipped` (TS set failed after shimming) and `DtcgSetSkipped` (native DTCG set failed to parse) are separate DU cases even though the behavior is identical. Keeping them distinct means a caller reading a warning log can tell immediately which format caused the skip — without inspecting the set name for naming conventions. General rule: when the same behavior can arise from structurally different sources, prefer a case per source over a shared case with a tag field.

## Mixed-format APIs need a structural role marker, not just parameter naming

`importTokensStudioCombinedWith` adds DTCG sets to a TS-driven resolution. The key constraint — DTCG sets are always lowest priority and always theme-agnostic — is not visible from the type `(string * string) list`. A `DtcgSetRole = | AsBasePrimitives` single-case DU as a required final argument makes the call site self-documenting:

```fsharp
importTokensStudioCombinedWith config themes tsJson myDtcgSets AsBasePrimitives
```

An AI or reviewer reading this line knows the role and constraint without looking up docs. The pattern is extensible: `DtcgSetRole` can add cases (e.g. `AsHighPriorityOverrides`) later without breaking existing callers, unlike a positional boolean or a string tag.

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

## All four DTCG spec versions are supported via upgrade-on-parse

Four published versions exist. Only 2025.10 is stable; the rest are superseded drafts. The library auto-detects the version on parse and upgrades all older formats losslessly to the 2025.10 domain model. Callers always work against one type system regardless of what version the file came from.

| Version | Date | Key changes |
|---|---|---|
| First Editors' Draft | 2021-09-23 | `type`/`value` (no `$`); color = hex string; dimension = "12px" string |
| Second Editors' Draft | 2022-06-14 | `$type`/`$value`; `fontWeight` added; still string formats |
| Third Editors' Draft | 2025-08-04 | Color object, dimension/duration objects, 7 new composite types; no resolver |
| **2025.10** | 2025-10-28 | Resolver module; shadow `inset` field |

All upgrade paths are lossless. Hex color strings parse to `{ColorSpace=SRGB; Components=...; Hex=Some "..."}` — the original hex is preserved in the `Hex` field. Dimension strings like `"12px"` parse to `{Value=12.0; Unit=Px}` with no information dropped.

`serializeAs` allows writing older formats when a consuming tool requires them. It may be lossy (a 2025.10 file with composite types cannot round-trip to First ED) and returns an error in that case rather than silently dropping data.

Technical reports for all versions: `/home/ivan/nexus/VARIOUS/community-group/www/public/TR/`

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

## Module stratification makes the simple path obvious and the advanced path explicit

The public API has two tiers in one file using F# nested modules:

- **Top-level `FnTools.DesignTokens`**: three functions (`import`, `importWithResolver`, `export`), one error type (`ImportError`). Covers every real use case. `export` is infallible — no `Result` wrapper. An AI or developer sees three functions and picks the right one by name.
- **`FnTools.DesignTokens.Primitives`**: all raw functions. The module name signals "advanced — you don't need this unless you have a specific reason."

Key properties that make the simple tier idiot-proof:
1. Always returns `ResolvedToken` — `Alias` is structurally absent, type is non-optional. The consumer cannot encounter an unresolved state.
2. `export` has no `Result` — `ResolvedToken` values are structurally valid; serialization cannot fail. No chance of writing broken error-handling code around it.
3. Single `ImportError` type — one match expression handles all failure modes.
4. XML doc comments on every function answer: what do I pass, what do I get, what can go wrong.

This pattern applies broadly: any library with a multi-step pipeline should offer a composed simple API alongside the primitives. The complexity lives in the library once; every caller gets it right automatically.

## File extension is a caller concern — the library only sees string content

The spec recommends `.tokens.json` and `.resolver.json` but allows plain `.json`. The library takes string content, not file paths, so extension handling is entirely the caller's responsibility. When a caller has an ambiguous `.json` file, `parseAuto` detects from content whether it is a token file or resolver document — detection is a single root property check (`version` + `resolutionOrder` present → resolver; otherwise → token file).

## FsToolkit.ErrorHandling for `validation { ... }` CE

Error collection (not short-circuit) is a spec-level expectation. F#'s natural styles (`let!`, `Result.bind`) short-circuit; without an explicit accumulator pattern, parser code drifts to first-error behaviour. FsToolkit.ErrorHandling provides the applicative `validation { ... }` CE and `Validation<'T, 'E> = Result<'T, 'E list>` type alias — public API shape is unchanged (`Result<_, ParseError list>` is exactly what their type is), so no leakage to consumers.

"No external dependencies" was an early framing — actually a *preference* about being selective, not a rule. FsToolkit will spread across NEXUS over time; it earns its place here by removing a structural footgun (drift to short-circuit) for ~zero cost beyond the package reference.

## Hedgehog YES, Verify NO — and why

Hedgehog (property-based testing) adds genuine value here: five non-trivial properties cover things unit tests can't easily enumerate — round-trip parse/serialize identity, `flattenResolved` Alias-free guarantee, error collection completeness (all errors in one pass, not first-only), DAG invariant (no cycles survive), and merge order correctness. Shrinking on failure produces minimal reproducible cases for free.

Verify (snapshot testing) was evaluated and rejected. The primary value proposition — locking in serialization output — is already covered by round-trip properties (serialize then parse must round-trip to the original). Verify would add a snapshot maintenance burden that is especially high here given 4 supported spec versions: every format change requires updating snapshots for all version upgrade paths. No net gain, clear maintenance cost.

Framework: **Expecto** (test runner) + **Hedgehog 2.x** (generators + properties). No other test dependencies.

## Promotion Candidates → NEXUS-LOGOS

- "Error collection is a first-class design constraint" — applies to any domain parser in NEXUS
- "I/O belongs to the caller" — general principle for all NEXUS libraries with external sources

---

## Three-tier token model — files vs. code

The standard DTCG guidance separates tokens into three tiers:
1. **Primitive** — raw named values: `color.blue.N500 = oklch(56% 0.14 230)`
2. **Semantic** — purpose aliases: `color.action.default = {color.blue.N500}`
3. **Component** — per-component slots: `button.background = {color.action.default}`

The component tier explodes token count if pushed into files. A component with 5 states × 10 variants × 8 properties = 400 tokens per component. This becomes unmaintainable.

**Our decision**: keep only primitive + semantic in `.tokens.json` files. The component layer lives in Fun.Blazor code, where semantic tokens are referenced directly by `CssVar` name. Code is the component token layer. No separate component token files, no third tier in DTCG files.

This is a deliberate clean break and not a temporary constraint — the F# type system enforces correct token references at compile time, which token files cannot do.

## Numeric scales as F# identifiers

Token files follow industry convention for numeric scales: `color.brand.500`, `fontWeight.bold.700`.
These are valid DTCG names. They are not valid F# identifiers.

**Resolution**: DTCG files use numeric scale names as-is. The typed-bindings emitter adds an `N` prefix in generated F# code: `color.brand.500` → `Tokens.Color.Brand.N500`. The emitter is the only place this transformation happens — one callsite, not scattered.

## Fun.Css is the right CSS binding for Fun.Blazor

For CSS-in-F# with Fun.Blazor:
- **Fun.Css** (`slaveOftime/Fun.Css`) — same author as Fun.Blazor, same design philosophy, composable CSS atoms via F# computation expressions. This is the right choice.
- **FSS** (`Bjorn-Strom/fss`) — type-safe CSS in F#, good project but separate ecosystem. Extra dependency, no clear advantage over Fun.Css in this stack.

Fun.Css `CssVar` is the direct binding type for emitted token bindings: `Tokens.Color.Action.default` is a `CssVar` value, not a string.

## DTCG JSONC authoring — no custom format needed

DTCG parser already has `JsonCommentHandling.Skip` enabled. Comments work today in `.tokens.json` files. There is no need for a custom TOML or other authoring format for DTCG tokens.

TOML is the right choice for **non-DTCG tokens** (FnHCI: console, TUI, thermal, braille) which have shallow/config-like structure with no nested group semantics.

## Penpot as design surface — round-trip workflow

Penpot stores tokens in **Tokens Studio's multi-set format** (DTCG 2nd editor's draft, hex
strings), not DTCG 2025.10. The `hex` sub-field in a DTCG 2025.10 color object is the bridge:
a `serializePenpot` adapter reads `$value.hex` and emits the hex string Penpot requires.

Three interaction surfaces in 2.14.4 — see `penpot-api.md`:
- REST `update-file` with `set-token-set` / `set-token` change ops (headless, CI-friendly)
- MCP server (`@penpot/mcp`) wrapping the Plugins API (requires browser open)
- Claude browser extension (interactive, no setup)

Penpot also supports SVG export (reliable) and HTML import (new, untested as of 2026-05-02).

**Proposed workflow**:
1. Author Fun.Blazor components in F# using typed token bindings
2. Render to HTML, import into Penpot for visual design iteration
3. Refine in Penpot (variants, states, layout), export SVG
4. Use SVG as reference to update Fun.Blazor component structure

**Reverse direction**: Penpot variants/states → document component structure decisions → inform Fun.Blazor component parameters. Penpot is the visual exploration tool; Fun.Blazor is ground truth.

The HTML import direction is unexplored. Penpot SVG export is the reliable path. Test both and document the gap in `experiments-planned.md`.

## Token naming — post-2025.10 community guidance

DTCG 2025.10 itself does not mandate naming conventions beyond path syntax. Community guidance emerging since the spec:

- **Tier prefix as top-level group**: `primitive.color.blue.N500` / `semantic.color.action.default` keeps the tier visible in the tree but creates deep paths. Alternative: separate files per tier (cb.tokens.json for primitives, ll.tokens.json for semantic) — no tier prefix needed because the file name is the tier signal.
- **Brand / global namespacing**: `color.brand.N500` for brand-specific primitives. Avoids collision when merging multiple brands via the resolver.
- **No `color-` CSS prefix duplication**: token path is `color.action.default`; CSS var name is `--color-action-default`. The `color` segment in the path becomes the CSS var prefix naturally.

**Our naming convention**: separate files per tier, no tier prefix in token names. CSS var names derived from token paths. Numeric scales with N prefix in generated F#. Full detail in `design-system-context.md`.

## Single-case DU enforces explicit acknowledgment at call sites

`type ExportLossAcknowledged = | IAcceptDataLoss` — a single-case DU used as a required parameter on lossy export paths. The caller must write the literal case name at the call site:

```fsharp
Format.serializeAs SecondEditorsDraft IAcceptDataLoss file
```

This is better than a `bool` flag or a `unit` argument because:
- It cannot be stored in a `let accepted = true` variable and then passed transparently
- Code review sees the data loss acknowledgment explicitly
- Refactors that swap `true` for `false` don't accidentally suppress the acknowledgment
- The type name documents *what* is being acknowledged, not just *that* a decision was made

Pattern: use a single-case DU whenever a boolean parameter would hide intent or allow silent suppression.

## Function-parameter polymorphism for shared serializer pipelines

When two output formats share identical structure but differ in one sub-operation (e.g., how a color is written), thread the differing operation as a function parameter through the call chain. Example: `cw: Utf8JsonWriter -> ColorValue -> unit` was threaded through `writeBorderValue → writeShadowObject → writeTokenValue → writeNode`. No duplication; no new type required. Pattern: prefer function parameters over interfaces/DUs when the variation is a single leaf operation in an otherwise identical pipeline.

## `JsonNode.DeepClone()` — nodes have exactly one parent

`System.Text.Json.Nodes.JsonNode` is a tree with ownership: every node has at most one parent. Assigning a `JsonNode` that is already a child of another object throws `"The node already has a parent"`. Fix: always call `.DeepClone()` before moving a node to a new parent. This applies to any value extracted from a `JsonObject` and re-inserted elsewhere.

## `serializeAs` is lossy by design — and that's correct

2025.10 → Second Editors' Draft export is explicitly lossy: OKLCH colors without a stored hex are serialized as 2025.10 object form (not Second ED hex strings), because gamut-mapping OKLCH → sRGB without rounding errors requires color-math that belongs outside the codec. The `IAcceptDataLoss` requirement documents this gap. Correct behavior is: fail visibly on the caller for the feature you haven't built yet, not silently produce wrong data.

## OKLCH → sRGB gamut mapping is permanently out of scope for this library

This library is a DTCG codec. Gamut mapping (converting OKLCH colors to sRGB without hue shift or clipping artifacts) requires color-math — an OKLab chroma-reduction loop, full sRGB gamut boundary test, and an iteration budget. That is a separate utility concern, not a codec concern. ADR-013 states this explicitly.

**The correct pattern**: token authors set `ColorValue.Hex` when authoring OKLCH tokens that will cross DTCG boundaries:

```fsharp
{ ColorSpace = Oklch; Components = [0.56; 0.14; 230.0]; Alpha = None; Hex = Some "#3a7fd4" }
```

When `serializePenpot` or `serializeAs SecondEditorsDraft` is called, it reads `Hex` directly. Gamut mapping happened at token-authoring time, by the person who knows whether the approximation is acceptable. The codec never performs color space conversions.

**Not a deferred feature**: there is no plan to add gamut mapping here. If it is ever needed, it belongs in a separate `FnTools.ColorMath` package that the caller uses before invoking this library.

When classifying CSS property values, dispatch on property name first (e.g., `box-shadow`, `font-family`) before pattern-matching on value shape. A shadow shorthand like `0 4px 20px rgba(26,110,26,0.26)` doesn't start with a color pattern — it would be misclassified as Unknown if you pattern-match the value without first checking the property. Property name → semantic category → value type.

## Bootstrap workflow validates the library against real-world CSS

Running `CssIngest + CssAudit` against `ivanthegeek.com` revealed a gap immediately: the audit needed property-name dispatch for `box-shadow`. These gaps would not have appeared in synthetic test fixtures. Maintaining a real-world sample file (`samples/ivanthegeek.tokens.json`) that must parse and round-trip is a cheap regression net for the codec.

## DTCG dimension units are `px` and `rem` — `em` is a deliberate spec extension

The DTCG 2025.10 spec explicitly limits `$value.unit` for dimension tokens to `"px"` and `"rem"`. These two units are intentionally chosen for platform portability: `px` maps to Android `dp`/iOS `pt`; `rem` maps to a system font-size multiple. `em` is CSS-specific — it means "relative to the current element's font-size" and has no cross-platform equivalent. The spec authors excluded it deliberately.

**Library extension (ADR-028)**: `DimensionUnit.Em` is supported in the domain as a deliberate, documented extension beyond the spec. The motivation is TS/Penpot round-trip fidelity: Tokens Studio and Penpot letter-spacing tokens commonly use `em` units, and lossless round-trip requires preserving the original unit. `Em` is valid inside the domain and in TS/Penpot export; it is not valid in strict DTCG 2025.10 output.

**CssIngest behavior**: when CssIngest encounters `letter-spacing: 0.22em`, it emits an explicit `Skipped` warning and produces no token. This is correct for the authoring direction — `em` is a component-level CSS detail, not a portable design decision. The domain supporting `Em` does not change this: ingest is about creating portable tokens from scratch, not about replicating CSS with its relative units.

**Rule of thumb**: if a CSS value is relative to font-size (`em`) or viewport (`vw`, `vh`, `%`), ask whether it is a design decision or a layout/sizing implementation. Design decisions (spacing scale, type scale in `rem`) go in tokens. Layout implementations stay in component code.

`CssAudit` reflects this: `isDimensionValue` matches only `px` and `rem`. Values in `em` and other relative units are classified as `CssNative` and surfaced explicitly so the bootstrap workflow can route them to the component layer rather than silently discarding them.

## LaundryLog existing components use hard-coded CSS class names

The Fun.Blazor components in `/home/ivan/nexus/LaundryLog/src/LaundryLog.UI/Components/` reference CSS class names from the old `--ll-*`/`--cb-*` design system (e.g., `ll-machine-chip`, `ll-machine-group`). These class names will need to be updated when the CSS emitter + typed bindings are available. The components are a concrete test case for the migration path — they represent real UI using the old system.
