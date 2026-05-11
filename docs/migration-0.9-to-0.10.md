# Migrating from 0.9 to 0.10

Released: 2026-05-10. Tracking ADRs: [`035-validate-options-opt-in-laxness.md`](../LOGOS/decisions/035-validate-options-opt-in-laxness.md), [`036-deprecate-resolveall-expose-flattenaliases.md`](../LOGOS/decisions/036-deprecate-resolveall-expose-flattenaliases.md), [`037-validation-warning-channel-deferred.md`](../LOGOS/decisions/037-validation-warning-channel-deferred.md) *(deferred — future possible route)*. Source request: [`request_2026-05-10_04.md`](../LOGOS/requests/request_2026-05-10_04.md).

---

## TL;DR

Two related changes, both closing friction surfaced by `request_2026-05-10_04`:

1. **`ValidateOptions`** — opt into laxness for the canonical Tokens Studio scale pattern (a `dimension` token aliasing a `number` token, ADR-033 friction) at the call site, without weakening the default safety net. Strict default unchanged.
2. **`Resolver.resolveAll` is deprecated**; **`Resolver.flattenAliases` is now public**. The `All` suffix was a trap — `resolveAll = resolve >>= flattenAliases` consumed the alias graph that post-resolve passes like `evaluateMathExtensionsInFile` (0.9.0) need to propagate. Replace with `resolve` + downstream emitters (common pipeline) or `resolve` + `flattenAliases` (narrow case).

No existing code breaks. Calls to `resolveAll` get FS0044 deprecation warnings with a clear migration message. Removal target: v1.0.0.

```diff
- <PackageReference Include="FnTools.DesignTokens" Version="0.9.0" />
+ <PackageReference Include="FnTools.DesignTokens" Version="0.10.0" />
```

---

## What's new

### 1. `ValidateOptions` — opt-in laxness (ADR-035)

```fsharp
type ValidateOptions = {
    AllowDimensionAliasingNumber : bool
}

module ValidateOptions =
    val strict     : ValidateOptions   // default — all checks active
    val permissive : ValidateOptions   // dimension→number alias allowed
```

The `*With` variants of every relevant function accept it:

```fsharp
Validation.validateWith                                : ValidateOptions -> TokenFile -> Result<unit, ValidationError list>
Api.importWith                                         : ValidateOptions -> string -> Result<...>
Api.importWithResolverWith                             : ValidateOptions -> loadFile -> context -> string -> Result<...>
Api.importWithResolverEvaluatingExtensionsWith         : ValidateOptions -> loadFile -> context -> string -> Result<...>
```

The legacy `validate` / `import` / `importWithResolver` / `importWithResolverEvaluatingExtensions` keep their existing signatures, now implemented as `*With ValidateOptions.strict ...`. **No existing call site changes; no existing behavior changes.**

**What `permissive` actually does:** suppresses the ADR-033 `TypeMismatch` error *only* for `dimension → number` aliases. Every other structural check still fires. Other cross-type alias mismatches (`dimension → color`, etc.) remain hard errors. The flag is a one-pattern whitelist, not a general type-coercion gate.

### 2. `Resolver.resolveAll` deprecated; `Resolver.flattenAliases` now public (ADR-036)

```fsharp
// 0.9.0 — implicit: resolveAll = resolve >>= flattenAliases (private)
// 0.10.0 — both pieces explicit:
Resolver.resolve         : loadFile -> ResolverInput -> ResolverDocument -> Result<TokenFile, ResolveError list>
Resolver.flattenAliases  : TokenFile -> Result<TokenFile, ResolveError list>   // newly public
Resolver.resolveAll      ⚠️ deprecated — replace per migration message
```

`flattenAliases` walks a `TokenFile` and replaces every `TokenValue.Alias` with the literal value it points to. **Most consumers don't need it** — `flattenResolved` and the typed emitter family follow aliases themselves as part of their work. The narrow case it serves: "I want a nested-structure `TokenFile` with all alias values inlined as literals, but I don't want a flat `ResolvedToken seq`."

`resolveAll` is now `[<System.Obsolete>]`. The deprecation message names both replacement paths.

### 3. Validation warning channel — deferred (ADR-037)

Not built. ADR-037 documents the warning-channel alternative considered while landing ValidateOptions, why it wasn't built for the dimension→number case, and what kind of advisory issues (unused tokens, scale outliers, etc.) it might fit later. Filed for traceability so future agents and human contributors know it was considered.

---

## Migration scenarios

### Scenario A — you only use the strict / default functions

Do nothing. Bump the version. None of your code paths change.

### Scenario B — you have a Tokens Studio SoT (dimension tokens aliasing number tokens)

You were on the manual Primitives composition path before 0.10.0 because the convenience wrappers hard-failed on your files. **You can now use the convenience wrappers:**

```fsharp
// Pre-0.10.0 — manual Primitives composition (still works, no change required)
match Api.Primitives.parseResolver json with
| Ok doc ->
    match Resolver.resolve loadFile input doc with
    | Ok mergedFile ->
        let r = Api.evaluateMathExtensionsInFile mergedFile
        match Api.Primitives.flattenResolved r.File with
        | Ok tokens -> Ok (List.ofSeq tokens, r.Warnings)
        | Error es -> ...
    | Error es -> ...
| Error es -> ...

// 0.10.0 — one-call convenience with explicit opt-in (preferred)
Api.importWithResolverEvaluatingExtensionsWith
    ValidateOptions.permissive loadFile input json
```

The convenience function does the same parse + resolve + evaluate + flatten internally, and ValidateOptions.permissive opts into the dimension→number alias laxness so validation doesn't hard-fail.

### Scenario C — you call `Resolver.resolveAll` (or `Api.Primitives.resolveAll`)

You'll see FS0044 deprecation warnings at the call sites. The deprecation message names two replacement paths. Migrate per use case:

**If you call `resolveAll` then pass the result to `flattenResolved` or an emitter:**
```diff
- match Resolver.resolveAll loadFile input doc with
- | Ok file ->
-     match Api.Primitives.flattenResolved file with
-     | Ok tokens -> ...
+ match Resolver.resolve loadFile input doc with
+ | Ok mergedFile ->
+     match Api.Primitives.flattenResolved mergedFile with
+     | Ok tokens -> ...      // flattenResolved follows aliases itself
```

**If you call `resolveAll` because you specifically want a `TokenFile` with inlined aliases (not a flat seq):**
```diff
- match Resolver.resolveAll loadFile input doc with
- | Ok file -> ...
+ match Resolver.resolve loadFile input doc with
+ | Ok mergedFile ->
+     match Resolver.flattenAliases mergedFile with
+     | Ok file -> ...        // now explicit
```

**If you can't migrate immediately** and want to silence the warning at known-good call sites:
```fsharp
#nowarn 44
// ... call sites that intentionally use the deprecated function ...
#warnon 44
```
Use scoped suppression (F# 10's `#nowarn` / `#warnon` pair) rather than a file-wide `#nowarn` — the AGENTS.md policy applies.

### Scenario D — you use `Api.import` / `Api.importWithResolver` / `Api.importWithResolverEvaluatingExtensions`

Do nothing — these wrappers keep their signatures and strict-validation defaults. If your source contains the TS scale pattern, switch to the `*With` variant with `ValidateOptions.permissive` (see Scenario B).

---

## What did NOT change

- `Format.parse` / `Format.serialize` — no API or behavior changes.
- `Resolver.resolve` — no API or behavior changes. (`resolveAll` is the function deprecated; `resolve` is one of the recommended replacements.)
- `Validation.validate` — same behavior as 0.9.0 (now implemented as `validateWith ValidateOptions.strict`).
- `Api.validateStrictDtcg` — unchanged (ADR-028 addendum / 0.7.0).
- `Api.evaluateMathExtensionsInFile` — unchanged (ADR-034 addendum / 0.9.0).
- `Api.evaluateMathExtensions` — still `[<Obsolete>]` (from 0.9.0); no further changes.
- The shape of `ValidationError`, `ResolveError`, `ImportError`, `ResolveWithExtensionsResult`, `EvaluateMathInFileResult`, `ExtensionEvaluationWarning` — all unchanged.

---

## Upgrade steps

1. Update your `PackageReference`:
   ```xml
   <PackageReference Include="FnTools.DesignTokens" Version="0.10.0" />
   ```
   Or individual layers — all 8 packages at 0.10.0.

2. Build. If you call `Resolver.resolveAll` or `Api.Primitives.resolveAll`, you'll get FS0044 warnings — migrate per Scenario C.

3. If you have a Tokens Studio SoT with dimension→number aliases, switch from the manual Primitives composition to the new `*With ValidateOptions.permissive` convenience wrapper (Scenario B). This eliminates the manual composition AND removes the `resolve` vs `resolveAll` trap from your call sites.

4. Tests: 328/328 pass in this release. If you have tests that call the deprecated `resolveAll`, wrap them in scoped `#nowarn 44` / `#warnon 44` (the AGENTS.md policy explicitly allows this for known-good regression coverage of deprecated functions).

---

## Reference

- **ADR-035** — `LOGOS/decisions/035-validate-options-opt-in-laxness.md` — full rationale (4 options considered, why option 2 won).
- **ADR-036** — `LOGOS/decisions/036-deprecate-resolveall-expose-flattenaliases.md` — why deprecation over renaming.
- **ADR-037** — `LOGOS/decisions/037-validation-warning-channel-deferred.md` — option 3 considered, deferred, documented as future possible route.
- **Original bug report** — `LOGOS/requests/request_2026-05-10_04.md`.
- **Outside conversation** — `LOGOS/outside-conversations/outside-conversations_2026-05-10_01.md` — the back-and-forth that arrived at the ADR-035 / ADR-036 / ADR-037 trio.
- **Tests** — 328/328 pass. New tests in `ValidationTests.fs` (validateWith strict + permissive + narrowness), `ResolverTests.fs` (flattenAliases public + cycle detection + Primitives parity + deprecated resolveAll regression), and `ExtensionEvaluationTests.fs` (the FRICTION test now flipped to STRICT default + PERMISSIVE convenience-wrapper propagation).
