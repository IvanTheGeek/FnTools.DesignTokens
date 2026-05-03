---
id: 015
title: Design system inheritance is modelled as a DTCG resolver document
status: accepted
date: 2026-05-03
---

## Context

The design system inheritance chain — Cheddar primitives → CheddarBooks semantic overrides → LaundryLog overrides — requires a merge mechanism. Three options:

1. Custom merge layer: own format, own semantics, own implementation
2. Flat files: maintain a complete merged token file per brand, no inheritance
3. DTCG resolver: use the `resolutionOrder` mechanism from the 2025.10 spec

## Decision

Model inheritance as a `.resolver.json` with sources listed in resolution order. Later entries win. Each tier defines only its differences from the one above it.

```json
"resolutionOrder": [
  { "set": "cheddar-primitives" },
  { "set": "cheddar-semantic" },
  { "set": "cheddar-books-overrides" },
  { "set": "laundrylog-overrides" },
  { "modifier": "theme" }
]
```

## Consequences

- The `Resolver` module in this library is the implementation — no additional merge layer is needed at higher layers for the token inheritance use case.
- Each brand file stays small: LaundryLog defines only what differs from CheddarBooks; CheddarBooks only what differs from Cheddar.
- Token-level inheritance is fully handled here. Component-level inheritance (a LaundryLog button extending a CheddarBooks button) is explicitly out of scope for this library — that belongs to the Layer 3/4 component model.
- When the DTCG resolver spec evolves, updating this library's `Resolver` module updates the merge behaviour for all brands simultaneously.
- Flat files remain valid for simple single-brand systems. The resolver is opt-in.
