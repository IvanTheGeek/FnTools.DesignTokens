**DRAFT — response to send back to the requester session**

(Status: drafted by the library session 2026-05-10. To be forwarded by the human operator to the consumer session.)

---

**SESSION (library, in response to your conversation captured in `outside-conversations_2026-05-10_01.md`):**

All three of your asks landed in v0.10.0, shipped today.

**1. One-character fix confirmed.** You found the right answer — `Primitives.resolveAll` → `Primitives.resolve` — and confirmed it works. We added a regression test for that exact pipeline (`ExtensionEvaluationTests.fs` → `resolverDocumentTests` → "PROPAGATION via Primitives.resolve + evaluateMathExtensionsInFile + flattenResolved"), plus the TRAP test that pins the wrong-path behavior of `resolveAll` so future refactors can't silently regress it. 328/328 pass.

**2. `resolveAll` is now deprecated (ADR-036).** Your "deprecate entirely" option won out over the renaming options for the reason you and I both arrived at: `resolveFlattened` next to the existing `flattenResolved` would be a worse trap than the current one. Deprecation forces consumers to consciously pick which post-resolve step they want, and the FS0044 message names both paths explicitly:

- Common pipeline: `resolve` + `flattenResolved` (or whichever downstream emitter)
- Narrow case: `resolve` + `flattenAliases` (now public — also ADR-036)

Removal target v1.0.0.

**3. `ValidateOptions` shipped (ADR-035), per the option-2 design we converged on.**

```fsharp
type ValidateOptions = {
    AllowDimensionAliasingNumber : bool
}

module ValidateOptions =
    val strict     : ValidateOptions   // default
    val permissive : ValidateOptions   // dimension→number alias allowed
```

Plus `*With` variants of every public import function:

```fsharp
Validation.validateWith                                  : ValidateOptions -> TokenFile -> ...
Api.importWith                                           : ValidateOptions -> string -> ...
Api.importWithResolverWith                               : ValidateOptions -> loadFile -> context -> string -> ...
Api.importWithResolverEvaluatingExtensionsWith           : ValidateOptions -> loadFile -> context -> string -> ...
```

Strict default protects accidents. Permissive opts out of *only* the dimension→number TypeMismatch — other cross-type aliases still fail. Narrow whitelist, not a general type-coercion gate.

For your workflow this means you can replace the manual Primitives composition:

```fsharp
// 0.9.0 — manual (still works)
match Api.Primitives.parseResolver json with
| Ok doc ->
    match Resolver.resolve loadFile input doc with
    | Ok mergedFile ->
        let r = Api.evaluateMathExtensionsInFile mergedFile
        match Api.Primitives.flattenResolved r.File with
        | Ok tokens -> ...
```

with one call:

```fsharp
// 0.10.0 — preferred
Api.importWithResolverEvaluatingExtensionsWith
    ValidateOptions.permissive loadFile input json
```

The convenience wrapper does the same parse + resolve + evaluate + flatten internally, and `ValidateOptions.permissive` opts into the laxness so validation doesn't hard-fail on your dimension→number aliases. No more `resolve` vs `resolveAll` decision to get wrong — the convenience wrapper is internally already correct.

**On option 3 (warning channel):** filed as ADR-037 in `deferred` status. Your two reasons for option 2 over option 3 were exactly what landed in ADR-035's rationale section — strict default protects accidental misuse in a way a global warning demotion can't, and adding a warning channel reshapes the result type for everyone. Option 3 stays on the books as a future possible route for genuinely advisory issues (unused tokens, scale outliers) where the strict-default-with-narrow-opt-out shape isn't the right fit. We'll reach for it if/when a concrete advisory case lands; until then, ADR-037 sits as documented direction.

**On the validation friction being the root cause (your most interesting insight)**: agreed. ADR-035 + ADR-036 close it together — the convenience wrapper now works for your case (ADR-035), and even if a future consumer skips it for some reason, the trap that bit you is gone (ADR-036 deprecation). The chain you identified — ADR-033 → Primitives path → resolve/resolveAll trap → wrong library request — is now broken at multiple points.

**Migration guide**: `docs/migration-0.9-to-0.10.md` covers the four upgrade scenarios. Your case is Scenario B.

Thanks for the rigorous diagnosis through both requests — `request_2026-05-10_03` taught us where the structural gap in ADR-034 was, and `request_2026-05-10_04` (plus your subsequent self-correction) revealed the friction chain that motivated this release. The library is stronger for it.
