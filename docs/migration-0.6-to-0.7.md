# Migrating from 0.6 to 0.7

Released: 2026-05-10. Tracking ADR: [`028-em-dimension-unit-extension.md`](../LOGOS/decisions/028-em-dimension-unit-extension.md) (addendum).

---

## TL;DR

0.7.0 is **purely additive**. One new opt-in public function:
`Api.validateStrictDtcg`. Existing API signatures and behavior are unchanged.
No source code that compiles against 0.6 needs any modification — `dotnet
add package` and rebuild, you're done. The new function is there if you want
a "this file contains no library extensions beyond DTCG 2025.10" guarantee
before exporting to a strict downstream consumer.

```diff
- <PackageReference Include="FnTools.DesignTokens" Version="0.6.0" />
+ <PackageReference Include="FnTools.DesignTokens" Version="0.7.0" />
```

That's it for upgrades. The rest of this doc explains the new function.

---

## What's new

### `Api.validateStrictDtcg : TokenFile -> Result<unit, ValidationError list>`

Reports `ConstraintViolation` errors for any feature that is valid in this
library's domain but **not** in the published DTCG 2025.10 spec. Today the
only such feature is `DimensionUnit.Em` — added per ADR-028 for Tokens
Studio / Penpot round-trip fidelity, but explicitly outside the spec's list
of valid dimension units (`px` and `rem` only, per DTCG §7.4.6).

Use it when you want a hard pre-export check that nothing extension-flavoured
is about to be written to a file someone else will consume strictly:

```fsharp
open FnTools.DesignTokens

// Pre-export safety check
match Api.validateStrictDtcg file with
| Ok () ->
    let json = Api.export tokens
    writeFile "tokens.json" json
| Error errors ->
    errors
    |> List.iter (fun e -> eprintfn "%s" (ValidationError.format e))
    failwith "file contains library extensions; refusing to export"
```

**What gets checked**: every literal `DimensionValue` position — direct
`TokenValue.Dimension`, and the dimension sub-fields inside `Border`,
`Shadow`, `Typography`, and `StrokeStyle` composites. Reports
`ConstraintViolation (path, message)` with the full dot-path to each
violation (e.g. `"font.heading.h1.letterSpacing"`).

**What is *not* checked**: references. If `tracking.wide` aliases another
token that resolves to an `Em` value, only the referenced token is flagged —
not the alias. Strict checks on resolved tokens are a future concern; if you
need them, run `Api.import` to get the resolved sequence, then walk it for
`ResolvedDimension { Unit = Em }` yourself.

**Multiple violations are collected** (not short-circuited on the first) —
the function returns every `ConstraintViolation` in one pass, matching
`Validation.validate`'s contract (ADR-002).

### Why it's a separate check, not part of `Validation.validate`

`Em` is a legal value in this library's domain. ADR-028 added it deliberately
for round-trip fidelity with Tokens Studio and Penpot, both of which accept
`em` natively. Files that contain `Em` are valid input and valid internal
state — `Validation.validate` continues to accept them.

Strict compliance is a different question: "is this `TokenFile` exportable
as pure DTCG 2025.10 with no library extensions?" — which is opt-in because
most consumers don't care. The two checks share `ValidationError` so the
results can be combined or compared, but they answer different questions.

### Why it's a validator, not a serializer

The original ADR-028 wording mentioned "a future strict-mode serialiser
should treat `Em` as an error or map it to `px` with a data-loss
acknowledgment." On review (ADR-028 addendum, 2026-05-10), the validator
shape was chosen instead:

- `Format.serialize` stays infallible — preserves ADR-012's principle that
  serialisation of a structurally valid file cannot fail.
- Validation is the natural home for "is this acceptable?" — mirrors how
  ADR-033 placed the cross-type alias check there rather than in the emitter.
- `Em → Px` coercion is semantically wrong: `em` is element-relative and has
  no general numeric conversion.

So strict mode is `validateStrictDtcg` (opt-in pre-flight) plus the existing
infallible `serialize` (which will happily write `"em"` if you ask it to —
that's now your choice to make explicitly).

---

## Migration scenarios

### Scenario A — you don't care about strict DTCG compliance

Do nothing. Bump the version and rebuild. The new function is opt-in.

### Scenario B — you want to enforce strict DTCG compliance on your tokens

Add a `Api.validateStrictDtcg` call in your pipeline at whatever point you
treat "I am about to ship this file to someone outside my control":

```fsharp
let exportStrict (file: TokenFile) : Result<string, ValidationError list> =
    file
    |> Api.validateStrictDtcg
    |> Result.map (fun () -> Format.serialize file)
```

### Scenario C — you use `Em` deliberately for TS / Penpot round-trip

You can either skip the strict check (it would fail for legitimate reasons),
or run it conditionally — strict-checking only the files destined for
non-extension-aware consumers. The information is there if you want it; the
library doesn't force you to use it.

### Scenario D — you use `Em` accidentally

If you imported tokens from a source that included `em` values without
realising it, `validateStrictDtcg` is how you'd find out before shipping.
Run it once in your CI as a hygiene check.

---

## Other changes

None to consumer code. Internal additions:

- `Validation.validateStrictDtcg` (new public function in the
  `FnTools.DesignTokens.Validation` package).
- `Api.validateStrictDtcg` and `Primitives.validateStrictDtcg` in the
  meta-package.
- 10 new tests bringing the suite to 291/291.
- ADR-028 addendum documenting the validator-vs-serializer choice and the
  "extensions gather here" pattern for future library deviations from the
  spec.

---

## Upgrade steps

1. Update your `PackageReference`:
   ```xml
   <PackageReference Include="FnTools.DesignTokens" Version="0.7.0" />
   ```
   Or individual layers — `Foundation`, `Format`, `Validation`, `Resolver`,
   `Css`, `Bindings`, `TokensStudio` — all at 0.7.0.

2. Build. No compile-time changes expected.

3. Optionally add `Api.validateStrictDtcg` calls where you want a
   strict-compliance pre-flight (see Scenario B).

---

## Reference

- **ADR-028 addendum** — `LOGOS/decisions/028-em-dimension-unit-extension.md`
  — full rationale for the validator-not-serializer choice.
- **`docs/api-reference.md`** — `Api.validateStrictDtcg` reference entry.
- **Tests** — 10 new cases in `ValidationTests.fs` covering literal `Em`
  positions in dimension/border/shadow/typography/strokeStyle, alias
  not-followed, multi-violation collection, deep-path reporting, and
  separation-of-concerns (regular `validate` still accepts `Em`).
