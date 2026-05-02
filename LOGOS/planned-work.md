## Phase 5 — Workspace move follow-up: rename + layer split

Phases 1–4 (single-assembly NEXUS.DesignTokens) are complete and tracked in `tasks-completed.md`. This phase finishes the relocation: rename the namespace and split the assembly into layers under the FnTools brand.

### Namespace rename — NEXUS.DesignTokens → FnTools.DesignTokens

- [x] Rename namespace in every `.fs` file under `src/` and `tests/`
- [x] Update every `open NEXUS.DesignTokens*` in tests and any future consumers
- [x] Rename `src/NEXUS.DesignTokens/` directory + `NEXUS.DesignTokens.fsproj` → `FnTools.DesignTokens.fsproj`
- [x] Rename `tests/NEXUS.DesignTokens.Tests/` directory + `NEXUS.DesignTokens.Tests.fsproj` → `FnTools.DesignTokens.Tests.fsproj`
- [x] Update `AssemblyName` / `RootNamespace` in both `.fsproj` files
- [x] Update test project's `<ProjectReference>` to the renamed source project
- [x] Update `CLAUDE.md` — move the "Namespace _(current)_" line to "Namespace: FnTools.DesignTokens" once the rename lands; remove the "Planned rename" note
- [x] Verify: `dotnet build` (0 warnings, 0 errors) + `dotnet test` (50/50 pass)

### Solution file decision

- [ ] Decide: `.sln` or no `.sln`
  - NEXUS workspace convention: no `.sln` per repo
  - FnTools is multi-project but each project is its own repo, so per-repo a single `.sln` is only useful if multiple projects coexist in this repo (which the layer split below introduces)
  - Tentative: add `FnTools.DesignTokens.sln` once the layer split is done, since it'll have 5 source projects + 1 test project

### Layer split

Replace the single `src/FnTools.DesignTokens/` project with the four-layer architecture documented in `CLAUDE.md`:

- [ ] `FnTools.DesignTokens.Foundation` — pure types, smart constructors, zero non-BCL deps
  - `Errors.fs`, `Domain.fs` (no Json/Validation; pure model only)
- [ ] `FnTools.DesignTokens.Format` — JSON parse/serialize via `System.Text.Json`
  - depends on `Foundation`
  - contains `Json.fs`, `Format.fs`
- [ ] `FnTools.DesignTokens.Validation` — invariants (alpha/component ranges, fontWeight, alias cycles, hex/components consistency)
  - depends on `Foundation`
  - `FsToolkit.ErrorHandling` dependency lives here
- [ ] `FnTools.DesignTokens.Resolver` — multi-set / modifier resolver document semantics
  - depends on `Foundation` + `Format` (to parse resolver JSON)
- [ ] `FnTools.DesignTokens` — meta-package re-exporting the four above as `FnTools.DesignTokens.Api`
  - depends on all four

Test project `FnTools.DesignTokens.Tests` references the meta-package and stays as one assembly.

### NuGet packaging (future)

- [ ] Package metadata in each `.fsproj` (`PackageId`, `Description`, `Authors`, `PackageLicenseExpression=AGPL-3.0-or-later`, `RepositoryUrl`)
- [ ] Decide publication target — nuget.org public feed or private feed first
- [ ] CI publish on tag (deferred until first stable release)

### Reference smoke test (future)

- [ ] Parse spec's own example token files from `community-group` repo with zero errors and serialize back round-trip
