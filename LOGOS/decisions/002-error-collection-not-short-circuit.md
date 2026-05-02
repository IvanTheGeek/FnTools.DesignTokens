---
id: 002
title: Parse and validate collect all errors — no short-circuit
status: accepted
date: 2026-05-02
---

## Context

F#'s natural `Result.bind` / `let!` composition short-circuits on the first error. For a library consumed by design tool authors editing token files, showing one error at a time is poor UX — the author fixes it, reruns, finds the next one, and so on.

## Decision

`Format.parse` and `Validation.validate` return `Result<_, 'e list>` and accumulate all errors before returning. `FsToolkit.ErrorHandling`'s `validation { }` CE provides applicative composition for accumulation without reimplementing it.

The return type `Result<TokenFile, ParseError list>` is not an implementation detail — it is the public contract and reflects the spec-level expectation.

## Consequences

- All callers receive the complete error set from a single call.
- Functions that can only have one failure mode (e.g., serialization, which is infallible by design) are not wrapped in `Result` at all — no false symmetry with the parsing tier.
- Short-circuit paths (`Result.bind`) are still used internally where only one error is possible at a step; accumulation only applies at boundaries that can legitimately produce multiple independent errors.
- `FsToolkit.ErrorHandling` is a required dependency of the `Validation` layer.
