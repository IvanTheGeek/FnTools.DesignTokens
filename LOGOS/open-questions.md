## Naming

**`ColorComponent.None` shadows F# `Option.None`**

The plan uses `type ColorComponent = Channel of float | None`. F# allows this, but `None` inside the module will shadow the global `Option.None`, making option code in the same file noisy. Alternatives: `Missing`, `NoneKeyword`, `Absent`. The DTCG spec uses the string `"none"` — the name should communicate that it represents the CSS `none` keyword, not absence of a value.

Decision needed before: `Domain.fs` is written.

---

**`Keyword` case name collision**

Both `StrokeStyleValue` and `FontWeightValue` have a `Keyword` case. They are separate types so there is no actual conflict, but in pattern matches without qualification it may be confusing. Whether to qualify (e.g., `StrokeStyleValue.Keyword`) everywhere or use more distinct names (`StrokeKeyword`, `WeightKeyword`) is a style question.

Decision needed before: `Domain.fs` is written.

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
