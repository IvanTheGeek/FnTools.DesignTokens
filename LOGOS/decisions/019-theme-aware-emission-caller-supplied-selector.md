---
id: 019
title: emitThemed accepts a caller-supplied selector function, not a fixed scheme
status: accepted
date: 2026-05-04
---

## Context

A theme-aware CSS emitter must produce a `:root` block for the base (light/default) tokens
and override blocks for each named theme. The override blocks need a CSS selector. Common
patterns in production:

- `[data-theme="dark"]` — data-attribute, most common, framework-agnostic
- `.dark` / `.theme-light` — class-based (Tailwind, shadcn/ui)
- `@media (prefers-color-scheme: dark)` — media query, OS-level preference
- `@layer base { [data-theme="dark"] { ... } }` — cascade layer scoped

No single scheme covers all callers. The library has no opinion on which CSS architecture
a consumer uses.

## Decision

`CssEmitter.emitThemed` accepts `selectorForTheme : string -> string` as its first
parameter. The caller constructs the selector from the theme name however it needs:

```fsharp
// data-attribute
CssEmitter.emitThemed (fun n -> sprintf "[data-theme=\"%s\"]" n) base themes

// class-based
CssEmitter.emitThemed (fun _ -> ".dark") base themes

// media query (ignores theme name, always dark)
CssEmitter.emitThemed (fun _ -> "@media (prefers-color-scheme: dark)") base themes
```

`emitMultiMode` (the two-token-set variant) takes the override selector as a plain string,
consistent with the same principle.

## Consequences

- The library emits correct CSS block structure regardless of selector scheme. The string
  returned by `selectorForTheme` becomes the verbatim selector — no validation or escaping.
- Tests use `fun n -> sprintf "[data-theme=\"%s\"]" n` as the convention. This is not
  the only valid choice — it is just what the tests use.
- Breakpoint media queries are a natural extension: pass a selector function that maps
  breakpoint theme names to `@media (max-width: Npx)` strings. Phase 5 uses this for
  Mobile and Tablet overrides.
- The library does not compose multiple override mechanisms (e.g., `@layer base { ... }`
  wrapping). If a caller needs layer scoping, it wraps the emitted string after the fact.
