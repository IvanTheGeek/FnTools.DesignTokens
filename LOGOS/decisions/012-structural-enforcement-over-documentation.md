---
id: 012
title: Footguns are eliminated structurally, not by documentation
status: accepted
date: 2026-05-03
---

## Context

`TokenValue` has an `Alias` case. After `Resolver.resolve`, the returned `TokenFile` still contains `Alias` values — aliases are not automatically followed. A caller (or AI agent) encountering `| Alias ref -> ???` will either skip it or mishandle it. The naive fix is an XML doc warning.

Separately, `ResolvedToken.Type` could be `string option` if a token's type were ever ambiguous, and `export` could return `Result` if serialization of a resolved token could fail. Both would push error-handling onto callers who have no way to trigger those cases in practice.

## Decision

Remove footguns from the type system, not from the documentation:

- Introduce `ResolvedToken` — a separate type with no `Alias` case. `flattenResolved` returns `ResolvedToken seq`; callers on this path structurally cannot encounter an unresolved alias.
- `ResolvedToken.Type` is `string` (non-optional). A resolved token always has a type.
- `export` is infallible (no `Result`). `ResolvedToken` values are structurally valid; serialization cannot fail.

## Consequences

- Two distinct types exist: `TokenNode` (the parse-time tree, can contain aliases) and `ResolvedToken` (post-flatten, alias-free). The boundary is explicit at the type level.
- `export` having no `Result` is only safe while `ResolvedToken` remains structurally valid. Any future extension to `ResolvedToken` that introduces a potentially-invalid state must also introduce a `Result` return on `export`.
- AI consumers especially benefit: `ResolvedToken seq` tells them what's safe to iterate and use directly without reading documentation.
- The principle extends to all future API design in this library: if a misuse is possible, eliminate it from the type system before writing the warning.
