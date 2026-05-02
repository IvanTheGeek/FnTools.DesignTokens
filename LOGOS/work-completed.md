## Phase 6 — CSS emitter + typed F# bindings emitter (completed 2026-05-02)

### CSS emitter — `FnTools.DesignTokens.Css`

- [x] `CssEmitter.fs` — all 13 DTCG token types → CSS custom properties; typography expands to 5 sub-vars
- [x] OKLCH, OKLab, LCH, Lab, HSL, HWB → dedicated CSS functions; other spaces → `color()` generic syntax
- [x] `emit` function: resolved token seq → `:root { }` CSS block
- [x] 26 tests covering color, scalar, shadow, varName, emit (including LaundryLog integration)
- [x] LaundryLog `ll.css` emitted: 124 custom properties, 5148 bytes
- [x] Added to slnx and meta-package

### Typed F# bindings emitter — `FnTools.DesignTokens.Bindings`

- [x] `BindingsEmitter.fs` — resolved token seq → nested `module` F# source with `var()` string constants
- [x] `toFsharpIdent`: N-prefix for numeric segments, PascalCase via hyphen-splitting, no backtick needed (keywords capitalise cleanly)
- [x] Typography tokens expand to 5 sub-property bindings matching CSS emitter suffixes
- [x] Generated file has zero runtime dependencies
- [x] 22 tests: ident conversion, module structure, typography expansion, LaundryLog integration
- [x] LaundryLog `Tokens.fs` emitted: 161 lines covering all cb + ll tokens
- [x] Added to slnx and meta-package

## Phase 5 — Namespace rename + layer split (completed 2026-05-02)

Phases 1–4 (single-assembly NEXUS.DesignTokens) are complete and tracked in `tasks-completed.md`. This phase finished the relocation: renamed the namespace and split the assembly into layers under the FnTools brand.

### Namespace rename — NEXUS.DesignTokens → FnTools.DesignTokens

- [x] Rename namespace in every `.fs` file under `src/` and `tests/`
- [x] Update every `open NEXUS.DesignTokens*` in tests and any future consumers
- [x] Rename `src/NEXUS.DesignTokens/` directory + `NEXUS.DesignTokens.fsproj` → `FnTools.DesignTokens.fsproj`
- [x] Rename `tests/NEXUS.DesignTokens.Tests/` directory + `NEXUS.DesignTokens.Tests.fsproj` → `FnTools.DesignTokens.Tests.fsproj`
- [x] Update `AssemblyName` / `RootNamespace` in both `.fsproj` files
- [x] Update test project's `<ProjectReference>` to the renamed source project
- [x] Update `CLAUDE.md` — move the "Namespace _(current)_" line to "Namespace: FnTools.DesignTokens" once the rename lands; remove the "Planned rename" note
- [x] Verify: `dotnet build` (0 warnings, 0 errors) + `dotnet test` (50/50 pass)

### Solution file

- [x] Decide: `.sln` or no `.sln` — added `FnTools.DesignTokens.slnx` (SDK default format) covering all 5 source projects + 1 test project

### Layer split

- [x] `FnTools.DesignTokens.Foundation` — pure types, smart constructors, zero non-BCL deps
  - `Errors.fs`, `Domain.fs` (no Json/Validation; pure model only)
- [x] `FnTools.DesignTokens.Format` — JSON parse/serialize via `System.Text.Json`
  - depends on `Foundation`; contains `Json.fs`, `Format.fs`
- [x] `FnTools.DesignTokens.Validation` — invariants (alpha/component ranges, fontWeight, alias cycles, hex/components consistency)
  - depends on `Foundation`; `FsToolkit.ErrorHandling` dependency lives here
- [x] `FnTools.DesignTokens.Resolver` — multi-set / modifier resolver document semantics
  - depends on `Foundation` + `Format` (to parse resolver JSON)
- [x] `FnTools.DesignTokens` — meta-package re-exporting the four above as `FnTools.DesignTokens.Api`
  - depends on all four
- [x] Test project `FnTools.DesignTokens.Tests` references the meta-package and stays as one assembly

## Reference smoke tests (completed 2026-05-02)

- [x] Parse spec's own example token files from `community-group` repo with zero errors and serialize back round-trip
  - 26 tests in `SmokeTests.fs`, drawn from `design-token.md`, `groups.md`, `composite-types.md`, `aliases.md`, and `types.md`
  - Covers all 13 token types, `$root`, `$extends`, curly-brace and JSON Pointer aliases, composite sub-value aliases
  - All 76 tests pass

## NuGet packaging metadata + publish script (completed 2026-05-02)

- [x] `PackageId`, `Version`, `Authors`, `Description`, `PackageLicenseExpression`, `RepositoryUrl` added to all 5 `.fsproj` files
- [x] `<IsPackable>false</IsPackable>` added to test project to prevent accidental packing
- [x] `publish.sh` — builds, packs, and pushes all 5 packages to Forgejo NuGet feed
  - Feed: `https://forgejo.ivanthegeek.com/api/packages/FnTools/nuget/index.json`
  - Token from `FORGEJO_TOKEN` env or `~/.config/forgejo-claude.token`
  - `--skip-duplicate` prevents re-push failures

## Remote migration to Forgejo (completed 2026-05-02)

- [x] Primary remote moved from GitHub to `https://forgejo.ivanthegeek.com/FnTools/FnTools.DesignTokens`
- [x] Forgejo push mirror configured: auto-syncs to `https://github.com/IvanTheGeek/FnTools.DesignTokens` on every commit (`sync_on_commit: true`)
- [x] `CLAUDE.md` updated: Forgejo is primary; GitHub is mirror
