## FnTools.DesignTokens

F# library implementing the **DTCG 2025.10** spec (Design Tokens W3C Community Group) as a strongly-typed domain with parse, validate, serialize, and resolve capabilities.

- Repo: `forgejo.ivanthegeek.com/FnTools/FnTools.DesignTokens` (primary); mirrored to `IvanTheGeek/FnTools.DesignTokens` on GitHub (formerly `NEXUS-Tokens` at `/home/ivan/nexus/NEXUS-Tokens`; moved 2026-05-01)
- Path: `/home/ivan/DEVELOPMENT/FnTools/FnTools.DesignTokens`
- Namespace: `FnTools.DesignTokens` / AssemblyName: `FnTools.DesignTokens`
- Minimal dependencies — `System.Text.Json` (BCL) + `FsToolkit.ErrorHandling` (validation CE for error accumulation in parsers)
- Spec research and full implementation plan: `docs/spec-context.md`
- JSON schemas (ground truth for type shapes): `/home/ivan/nexus/VARIOUS/community-group/www/public/schemas/2025.10/`
- License: **AGPL-3.0** (see `LICENSE`)

## Architecture

Seven layered packages plus a meta-package, all under the `FnTools.DesignTokens` namespace:

- `FnTools.DesignTokens.Foundation` — pure types, smart constructors, zero non-BCL deps.
- `FnTools.DesignTokens.Format` — JSON parse/serialize (`System.Text.Json`).
- `FnTools.DesignTokens.Validation` — invariants (alpha range, fontWeight, alias cycles, hex/component consistency).
- `FnTools.DesignTokens.Resolver` — multi-set / modifier resolver document semantics.
- `FnTools.DesignTokens.Css` — CSS custom-property emitter + `CssIngest` / `CssAudit` for ingesting existing stylesheets.
- `FnTools.DesignTokens.FSharp` — F# emitter (resolved tokens → `Tokens.*` module). Renamed from `Bindings` in v0.12.0; see ADR-039.
- `FnTools.DesignTokens.TokensStudio` — Tokens Studio import/export with alias-preserving round-trip.
- `FnTools.DesignTokens` — meta-package; depends on all seven above.

## Git Workflow Notes

- Branches convey what is being worked on (`feature/`, `experiment/`, `fix/`, `docs/`, `refactor/`).
- Commits made during a turn should explain **what changed and why** — Claude writes them explicitly. The Stop hook is a *safety net*: it commits any leftover changes with a generic `chore: turn-end safety net <ts>` message and pushes the current branch.
- Merges to `main` use `--no-ff` (always — preserves branch topology).
- Abandoned work: merge to `graveyard` with `--no-ff`, delete the branch.
- No PRs — direct merges. CI in this project means *continuous integration* in the original sense: small branches merged often, history preserved, story traceable.

## Claude Code Notes

- Project Stop hook (`.claude/settings.json`): commits any leftover working-tree changes with a safety-net message, then pushes the current branch.
- Global Stop hook (`~/.claude/settings.json`): commits and pushes the transcript repo at `~/.claude/projects` to remote `machines/kvmtest`.
