---
id: 007
title: FnTools.DesignTokens stays standalone — not renamed to FnTools.FnHCI.Tokens.Design
status: accepted
date: 2026-05-02
---

## Context

FnHCI is a planned broader token framework covering console, TUI, thermal, and braille targets in addition to DTCG visual tokens. Two namespace options were considered:

1. Rename everything now to `FnTools.FnHCI.Tokens.Design.*`
2. Keep `FnTools.DesignTokens.*` as-is; build `FnTools.FnHCI.Tokens` as a future aggregator

## Decision

Keep `FnTools.DesignTokens.*` as a standalone library. When FnHCI work begins, `FnTools.FnHCI.Tokens` will be a new aggregator package that depends on `FnTools.DesignTokens` alongside the other token targets.

## Consequences

- `FnTools.DesignTokens` can be published to NuGet and consumed by anyone working with DTCG in .NET, with no FnHCI dependency.
- `FnTools.FnHCI.Tokens` (future) will have a clear dependency graph: it composes existing libraries rather than owning their internals.
- Do not rename `FnTools.DesignTokens.*` namespaces. Any future namespace change requires a new ADR and a deprecation notice on the old package.
- The `FnTools.DesignTokens.Foundation` assembly is the natural shared type surface for the FnHCI family — its zero non-BCL dependency constraint must be maintained.
