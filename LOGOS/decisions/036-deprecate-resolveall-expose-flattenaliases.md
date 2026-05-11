---
id: 036
title: Deprecate Resolver.resolveAll; expose Resolver.flattenAliases publicly
status: accepted
date: 2026-05-10
---

## Context

`Resolver.resolveAll` is `resolve >>= flattenAliases` — it merges sets per
the resolver document AND follows every alias reference to a concrete value
in one call. When introduced (ADR-015), it was the obvious "do the full
resolver pipeline" convenience.

Two problems surfaced through 0.8.0–0.9.0:

1. **The `All` suffix is load-bearing but not self-evident.** Two equally
   reasonable readings of the name:
   - "resolve all the *sets*" (the merge step)
   - "resolve *all the way* down through aliases" (merge + alias-following)

   For a consumer pattern-matching function names — and especially for AI
   tools that pick API functions by surface shape — the choice between
   `resolve` and `resolveAll` looks like "lite vs. full." It doesn't read
   as "this one eats your alias graph for breakfast."

2. **Eating the alias graph is exactly wrong for the
   `evaluateMathExtensionsInFile` workflow** (ADR-034 addendum / 0.9.0).
   Post-resolveAll, formula tokens that other tokens aliased have been
   replaced with stale concrete values; the post-resolve evaluation pass
   has nothing to propagate through. `request_2026-05-10_04` is exactly
   this trap; the workflow worked once the consumer switched from
   `Primitives.resolveAll` to `Primitives.resolve`.

Additionally, **`resolveAll` is redundant in the common pipeline.** The
downstream consumers — `Primitives.flattenResolved`, `BindingsEmitter`,
the CSS emitter family — all follow aliases themselves as part of their
work (via `tryResolveAliasIn` in `DesignTokens.fs`). Calling `resolveAll`
followed by any of these does the alias-following work twice (harmlessly,
but redundantly).

The narrow case `resolveAll` uniquely serves is: "I want a nested-structure
`TokenFile` with all alias values inlined as literals, but I don't want a
flat `ResolvedToken seq`." This is uncommon — most consumers want either
the alias-preserving form (for serialization) or the flat resolved form
(for emission/binding). The in-between is rarely the goal.

## Decision

**Two paired changes:**

1. **Make `Resolver.flattenAliases` public.** It was `let private` until
   0.9.0. Promoting it to public gives the narrow "I want a TokenFile with
   inlined aliases" use case an explicit, well-named entry point. Add full
   XML docs explaining when to use it (rarely, only for the narrow case)
   and why the common pipeline doesn't need it.

2. **Deprecate `Resolver.resolveAll`** with `[<System.Obsolete>]`. The
   deprecation message names both replacement paths:
   - Common pipeline: `Resolver.resolve` + downstream (`flattenResolved`,
     `evaluateMathExtensionsInFile`, an emitter) — those handle alias
     following themselves.
   - Narrow case: `Resolver.resolve` + the now-public
     `Resolver.flattenAliases` explicitly.

The `Api.Primitives.resolveAll` re-export is similarly deprecated.
`Api.Primitives.flattenAliases` is added.

### Why not just rename `resolveAll`

Two alternatives considered:

- `resolveAll → resolveWithInlinedAliases`: explicit, verbose, ugly.
- `resolveAll → resolveFlattened`: shorter, but **we already have
  `flattenResolved`** (in the meta-package) which means something different
  (TokenFile → ResolvedToken seq). Having `resolveFlattened` next to
  `flattenResolved` would be a worse trap than the current one.

Deprecation is cleaner than renaming because:
- It forces consumers to pick the correct path explicitly.
- It avoids creating a new name that could itself be misread.
- It naturally guides AI consumers via the deprecation message at compile
  time.

### Removal timeline

Same as `Api.evaluateMathExtensions` (ADR-034 addendum, deprecated 0.9.0):
removal target is **v1.0.0**, or earlier if no external consumer is
identified.

## Consequences

- `Resolver.flattenAliases` is now part of the public API and must be
  maintained as such — input/output shape changes need migration paths.
- `Resolver.resolveAll` keeps working in 0.10.0; consumers get FS0044
  warnings pointing at the two replacement paths.
- The Primitives re-export is deprecated identically.
- `Api.importWithResolver` and `Api.importWithResolverEvaluatingExtensions`
  use `Resolver.resolve` + downstream internally (not `resolveAll`) — they
  already do the right thing; no changes needed there.
- New regression test: `flattenAliases` standalone (cycle detection,
  alias-following). Existing tests calling `resolveAll` either:
  - Migrate to `resolve` + `flattenAliases` explicitly (the canonical
    replacement), OR
  - Stay under scoped `#nowarn 44` / `#warnon 44` if their explicit purpose
    is to test the deprecated function.
- Same shared-implementation pattern as ADR-034 addendum: the public
  `Resolver.resolveAll` (with `[<Obsolete>]`) and the `Primitives.resolveAll`
  re-export both call `resolveAllImpl` (private, no Obsolete attribute), so
  the re-export doesn't trigger FS0044 in our own build.

### Migration tier

This is a behavior-preserving deprecation: same semantics, same outputs.
The migration message guides callers to the cleaner replacement path. No
behavior changes for callers who continue to use `resolveAll` despite the
warning.

Shipped in v0.10.0 (2026-05-10).
