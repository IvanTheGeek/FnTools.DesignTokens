---
id: 004
title: The library exposes a composed convenience tier alongside primitives
status: accepted
date: 2026-05-02
---

## Context

"Parse then validate" is the canonical composition for token file consumers. "Resolve then flatten aliases" is another. Without a composed entry point, every caller independently chains primitives — and can independently get the ordering or error handling wrong.

## Decision

The meta-package exposes two tiers in `DesignTokens.fs`:

- **`Api` module** — three functions: `import`, `importWithResolver`, `export`. Returns `ResolvedToken` (no `Alias` case, `Type` is non-optional). `export` is infallible (no `Result`). Single `ImportError` DU covers all failure modes.
- **`Primitives` module** — all raw functions for advanced use. The name signals "you probably don't need this."

## Consequences

- The common 95% case is three named functions. An AI or new developer encounters no footguns.
- `ResolvedToken` structurally cannot contain an unresolved alias — the type system enforces this, documentation does not.
- `export` having no `Result` wrapper is only safe because `ResolvedToken` values are structurally valid; this must be maintained if `ResolvedToken` is ever extended.
- Advanced callers who need partial pipelines (parse without resolve, resolve without flatten) use `Primitives` explicitly.
