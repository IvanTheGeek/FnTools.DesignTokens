---
id: 005
title: FsToolkit.ErrorHandling for the validation computation expression
status: accepted
date: 2026-05-02
---

## Context

Error accumulation (see ADR-002) requires applicative composition. Without a library, the parser code would need an explicit accumulator pattern at every multi-field record construction site. Without it, `let!` in a `result { }` CE short-circuits — a structural footgun that causes drift back to first-error behaviour over time.

"No external dependencies" was considered but rejected as an overcorrection. The real principle is selectivity, not zero deps.

## Decision

`FsToolkit.ErrorHandling` (NuGet) lives in the `Validation` layer. It provides:
- `validation { }` CE with applicative `and!` / `let!` composition
- `Validation<'T, 'E> = Result<'T, 'E list>` type alias

The public API shape is unchanged — `Result<_, ParseError list>` is exactly what the library's type alias resolves to, so no FsToolkit types leak to consumers.

## Consequences

- `FsToolkit.ErrorHandling` is a transitive dependency for anyone referencing `Validation` or the meta-package.
- The package is well-maintained and widely used in the F# ecosystem — low abandonment risk.
- If FsToolkit is ever removed, all `validation { }` CEs must be replaced with explicit applicative composition — isolated to `Validation` layer only.
