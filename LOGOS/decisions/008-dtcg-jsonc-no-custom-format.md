---
id: 008
title: DTCG .tokens.json with JSONC comments — no custom authoring format needed
status: accepted
date: 2026-05-02
---

## Context

Token file authoring in plain JSON can be verbose. TOML and other formats were considered as a more ergonomic authoring surface for DTCG tokens.

## Decision

`Format.parse` uses `JsonCommentHandling.Skip` — JSONC comments (`//` and `/* */`) work today in `.tokens.json` files. No custom authoring format is needed for DTCG tokens.

TOML is the right choice for **non-DTCG FnHCI tokens** (console, TUI, thermal, braille) which have shallow, config-like structure with no nested group semantics.

## Consequences

- Authors can add `// comments` to `.tokens.json` files without any pre-processing step.
- The spec's `.tokens.json` file extension is preserved — tools like Penpot that expect DTCG files work without conversion.
- TOML support (via Tomlyn) will be added as part of FnHCI non-visual token work only.
- If a future need for richer authoring syntax appears (e.g., variable interpolation), evaluate at that time; do not pre-build.
