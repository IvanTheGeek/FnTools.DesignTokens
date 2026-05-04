---
id: 021
title: Map Tokens Studio $themes/$metadata to a DTCG ResolverDocument
status: accepted
date: 2026-05-04
---

## Context

Tokens Studio stores multi-set / multi-theme state as two proprietary keys in the token
file: `$themes` (an array of theme objects with `selectedTokenSets` maps) and `$metadata`
(with `tokenSetOrder`, `activeThemes`, `activeSets`). A DTCG spec validator reports these
as invalid tokens because they lack `$value` and use the reserved `$` namespace.

The DTCG 2025.10 spec provides an exact structural equivalent: a `ResolverDocument` with
named sets, named modifiers (each with named contexts), and a `resolutionOrder`. The
correspondence is one-to-one:

| Tokens Studio | DTCG resolver |
|---|---|
| `$metadata.tokenSetOrder` | `resolutionOrder` (ordering) |
| `$themes[].group` | modifier name |
| themes sharing the same `group` | contexts of that modifier |
| `selectedTokenSets: "enabled"` for a theme (varying across the group) | context sources |
| sets with `"source"` status, or "enabled" in all themes | base `SetRef` entries |

## Decision

Add `TokensStudio.toResolverDocument : ShimResult -> Map<string, TokenFile> -> ResolverDocument`
that performs this mapping mechanically.

**Algorithm:**

1. Group themes by `group` field → one modifier per group.
2. For each modifier group, determine *varying sets*: sets whose status
   (`"enabled"`/`"disabled"`) differs across the group's themes. These go into
   per-context `Sources`.
3. Remaining sets (present as `"source"` in any theme, or `"enabled"` in *all* themes)
   are *base sets* and become `SetRef` entries at the front of `resolutionOrder`.
4. `resolutionOrder` = base sets (in `tokenSetOrder` order) + one `ModifierRef` per
   modifier (empty context string → resolver uses the modifier's `default`).
5. Every set in `tokenSetOrder` that exists in `parsedSets` becomes a `SetDefinition`
   with one `Inline` source in the `sets` map.

**Context naming:**

Theme names follow the Tokens Studio convention `"GroupName/ContextName"`.
The context name in the `ResolverDocument` is the portion after the last `/`,
falling back to the full name if no `/` is present.

**Effect on `importTokensStudioThemed`:**

The function's internals are replaced by:
```
toResolverDocument shimResult parsedSets
→ convert themeNames to ResolverInput (theme name → group/context lookup)
→ Resolver.resolve loadFile resolverInput doc
```

The public signature (`themeNames: string list`) is unchanged; only the internals change.
The `themeNames` → `ResolverInput` conversion maps each theme name to its group/context
pair by looking up the theme in `shimResult.Themes`.

## Consequences

- The parallel bespoke merge logic in `importTokensStudioThemed` is removed; all
  multi-set resolution goes through `Resolver.resolve`.
- `toResolverDocument` is public — it enables the export workflow (ADR-022) and any
  caller that wants a spec-native handle on the Tokens Studio structure.
- The `$themes`/`$metadata` keys remain handled at shim time and never reach the
  domain model as token nodes. The validator errors disappear once a consumer uses
  `toResolverDocument` + `Resolver.resolve` instead of the raw file.
- `"source"` status has no DTCG equivalent. Treating source sets as base sets is
  correct: they exist to provide alias targets and should always be present regardless
  of the active theme context.
