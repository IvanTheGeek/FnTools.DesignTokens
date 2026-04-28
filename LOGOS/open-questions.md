## Resolved type tier

**Should `Domain.fs` define a parallel `Resolved*` type tier?**

The convenience path (`flattenResolved`, `resolveAll`) should return types where `Alias` is structurally impossible and `Type` is non-optional. This requires a second set of types:

```fsharp
// Raw (mid-parse, may contain aliases, type may be inherited)
type Token = { Value: TokenValue; Type: TokenType option; Metadata: Metadata }
type TokenValue = ... | Alias of TokenRef   // 14 cases

// Resolved (post-resolution, compiler-enforced guarantees)
type ResolvedToken = { Value: ResolvedTokenValue; Type: TokenType; Metadata: Metadata }
type ResolvedTokenValue = ...               // 13 cases — no Alias
```

Decision needed before: `Domain.fs` is written.

---

**If resolved types exist, should composite fields also be resolved?**

Composite types (`BorderValue`, `ShadowObject`, `TransitionValue`, `GradientStop`, `TypographyValue`) use `ValueOrRef<'T>` for their fields — each field can be a literal or a token reference. Two options:

Option A — leave composites as-is, only resolve the top-level `Alias` case:
```fsharp
// ResolvedBorderValue still has ValueOrRef fields
type ResolvedBorderValue = BorderValue   // same type
```
Caller may still encounter `| Reference ref ->` inside composite fields.

Option B — full resolved composite tier:
```fsharp
type ResolvedBorderValue = {
    Color : ColorValue        // always literal
    Width : DimensionValue
    Style : StrokeStyleValue
}
```
More types to define but the convenience path is completely clean — no `Reference` or `Alias` anywhere in the resolved domain.

Option B is recommended for AI-usability. Decision needed before: `Domain.fs` is written.

---

## Naming

**`ColorComponent.None` shadows F# `Option.None`**

The plan uses `type ColorComponent = Channel of float | None`. F# allows this, but `None` inside the module will shadow the global `Option.None`, making option code in the same file noisy. Alternatives: `Missing`, `NoneKeyword`, `Absent`. The DTCG spec uses the string `"none"` — the name should communicate that it represents the CSS `none` keyword, not absence of a value.

Decision needed before: `Domain.fs` is written.

---

**`Keyword` case name collision**

Both `StrokeStyleValue` and `FontWeightValue` have a `Keyword` case. They are separate types so there is no actual conflict, but in pattern matches without qualification it may be confusing. Whether to qualify (e.g., `StrokeStyleValue.Keyword`) everywhere or use more distinct names (`StrokeKeyword`, `WeightKeyword`) is a style question.

Decision needed before: `Domain.fs` is written.

---

## API design

**`LoadError` shape — wrapper DU or flat union?**

The convenience `load` function needs a single error type covering both `ParseError` and `ValidationError`. Two options:

```fsharp
// Option A — wrapper DU (what the plan currently has)
type LoadError =
    | ParseError      of ParseError
    | ValidationError of ValidationError

// Option B — flat union merging all cases
type LoadError =
    | InvalidJson            of message: string
    | MissingRequiredField   of path: string * field: string
    // ... all ParseError cases ...
    | CircularReference      of cycle: string list
    // ... all ValidationError cases ...
```

Option A preserves the distinction between structural (parse) and semantic (validation) errors, which is useful for tooling that wants to report them differently. Option B is simpler to match against but loses the categorisation. Current plan uses Option A.

Decision needed before: `Errors.fs` and `DesignTokens.fs` are written.

---

## Parsing

**`FontWeightValue (Numeric n)` — int or float?**

The spec says numeric font weight is `[1, 1000]`. CSS and most design tools use integers, but JSON allows floats (`700.0`). The plan uses `int`. If a file contains `700.0`, strict int parsing would reject it. Options: parse as `float` and validate range, or parse as `float` and floor/round to `int`.

Decision needed before: `Format.fs` font weight parser.

---

**`ShadowValue` — single vs. array ambiguity**

The spec says shadow `$value` can be a single object or an array. The plan models this as `Single of ShadowObject | Multiple of ShadowOrRef list`. Question: should a one-element array serialize as a bare object or a single-element array? The spec does not mandate either for serialization. Round-trip fidelity would favor preserving the original form.

Decision needed before: `Format.fs` shadow serializer.

---

## Validation

**`$type` inheritance — what counts as "nearest"?**

The spec says type is inherited from the nearest ancestor group. If a group has `$type: color` and a child group has no `$type`, and that child has a token with `$type: dimension`, which wins? The token's explicit type (answer: yes, token explicit type always wins). Needs confirmation against the JSON schema before `Validation.fs` encodes the precedence logic.

Decision needed before: `Validation.fs` type-resolution logic.

---

## Resolver

**Deep merge semantics for `$extends`**

The spec says `$extends` does a deep-merge with local overrides winning. What happens when the inherited group has a token at `foo.bar` and the local group also has a group (not a token) at `foo.bar`? Node type mismatch on merge. Is this a `ValidationError` or silently resolved by local-wins?

Decision needed before: `Resolver.fs` merge algorithm.
