---
id: 017
title: Component tokens live in F# code, not in .tokens.json files
status: accepted
date: 2026-05-03
---

## Context

The standard DTCG three-tier model places component tokens in token files alongside primitives and semantic tokens. A moderately complex component with 5 states × 10 variants × 8 tokenisable properties produces 400 tokens per component. Multiplied across a real design system, the file count and maintenance burden become significant.

Four options were considered:

1. Full three-tier in DTCG files — standard approach used by large design systems (Salesforce, Adobe)
2. Two-tier in files + component tier in files for complex systems only
3. Component tokens as plain string constants in F# (no type check, just `let buttonBg = "var(--color-accent)"`)
4. Component tokens referenced directly in Fun.Blazor component code via typed `CssVar` bindings generated from semantic tokens

## Decision

Keep only primitive and semantic tiers in `.tokens.json` files. The component token layer lives in Fun.Blazor F# code: semantic tokens are referenced directly by their generated `CssVar` name (e.g., `Tokens.Color.Accent.default`). Code is the component token layer.

## Consequences

- Token file count stays manageable. Component token explosion is contained in F# modules where the compiler checks references at build time.
- The F# type system enforces that referenced semantic tokens exist — DTCG files cannot do this without tooling.
- Penpot and Figma see only primitive and semantic tokens when importing the library's output. Component bindings are not represented in the design tool. This is a known and accepted gap — the design tool is for visual exploration, not for component logic.
- Generated bindings (`Tokens.Color.Accent.default : string`) are zero-dependency string constants. They work directly in Fun.Css property builders without any runtime coupling to this library.
- This is a deliberate clean break from the standard three-tier file model. It is not a temporary constraint. Revisit only if Fun.Blazor component tooling reaches a point where F# code is not the natural authoring surface.
