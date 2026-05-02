---
id: 009
title: TOML via Tomlyn for FnHCI non-visual token files
status: accepted
date: 2026-05-02
---

## Context

FnHCI non-visual token targets (ConsoleTokens, TuiTokens, ThermalTokens, BrailleTokens) have shallow, config-like structure. DTCG's nested group semantics and JSON schema are unnecessary overhead for these. A simpler authoring format is warranted.

## Decision

Non-DTCG FnHCI token files use TOML. The parser library is **Tomlyn** (`xoofx/Tomlyn`, NuGet) — a TOML 1.0 implementation with F#-friendly bindings.

## Consequences

- FnHCI non-visual token projects take a dependency on Tomlyn.
- `FnTools.DesignTokens` has no Tomlyn dependency — it is DTCG/JSON only.
- TOML files use `.tokens.toml` extension by convention to distinguish from DTCG `.tokens.json` files.
- The FnHCI resolver (future) will need to handle both JSON token sources and TOML token sources.
