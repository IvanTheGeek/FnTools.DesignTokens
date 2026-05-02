---
id: 006
title: Expecto + Hedgehog for tests; Verify (snapshot) explicitly rejected
status: accepted
date: 2026-05-02
---

## Context

Three testing approaches were considered: example-based unit tests, property-based tests, and snapshot/approval tests (Verify).

## Decision

**Expecto** (test runner) + **Hedgehog** (property-based, with shrinking). No Verify.

Hedgehog properties that earn their place:
- Round-trip identity: `serialize (parse json) = parse json` for all valid inputs
- `flattenResolved` guarantee: no `Alias` token survives the call
- Error collection completeness: all errors returned in one pass, not just the first
- DAG invariant: no alias cycles survive resolution
- Merge order correctness: later sets win in resolution order

Verify was rejected because:
- Its primary value (locking serialization output) is already covered by round-trip properties
- It would require snapshot maintenance across four supported spec versions — every format change updates 4× the snapshots
- No net gain over what Hedgehog already provides

## Consequences

- Failing property tests produce a minimal shrunk counterexample automatically.
- Snapshot files do not exist in this repo and must not be added without revisiting this decision.
- Adding a new token type requires a new Hedgehog generator, not a snapshot update.
