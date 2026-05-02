@../AGENTS.md

## FnTools.DesignTokens

F# library implementing the **DTCG 2025.10** spec (Design Tokens W3C Community Group) as a strongly-typed domain with parse, validate, serialize, and resolve capabilities.

- Repo: `IvanTheGeek/FnTools.DesignTokens` (formerly `NEXUS-Tokens` at `/home/ivan/nexus/NEXUS-Tokens`; moved 2026-05-01)
- Path: `/home/ivan/DEVELOPMENT/FnTools/FnTools.DesignTokens`
- Namespace _(current)_: `NEXUS.DesignTokens` / AssemblyName: `NEXUS.DesignTokens`
  - **Planned rename to `FnTools.DesignTokens`** — see `LOGOS/planned-work.md`
- Minimal dependencies — `System.Text.Json` (BCL) + `FsToolkit.ErrorHandling` (validation CE for error accumulation in parsers)
- Spec research and full implementation plan: `docs/spec-context.md`
- JSON schemas (ground truth for type shapes): `/home/ivan/nexus/VARIOUS/community-group/www/public/schemas/2025.10/`
- License: **AGPL-3.0** (see `LICENSE`)

## Architecture (planned split — not yet executed)

The current single-assembly design will be split into layered packages, all under the `FnTools.DesignTokens` namespace. See `LOGOS/planned-work.md` for the migration plan.

- `FnTools.DesignTokens.Foundation` — pure types, smart constructors, zero non-BCL deps. Other layers and external consumers code against this contract.
- `FnTools.DesignTokens.Format` — JSON parse/serialize (`System.Text.Json`).
- `FnTools.DesignTokens.Validation` — invariants (alpha range, fontWeight, alias cycles, hex/component consistency).
- `FnTools.DesignTokens.Resolver` — multi-set / modifier resolver document semantics.
- `FnTools.DesignTokens` — meta-package re-exporting the four above as `FnTools.DesignTokens.Api`.

## Git Workflow Notes

- Branches convey what is being worked on (`feature/`, `experiment/`, `fix/`, `docs/`, `refactor/`).
- Commits made during a turn should explain **what changed and why** — Claude writes them explicitly. The Stop hook is a *safety net*: it commits any leftover changes with a generic `chore: turn-end safety net <ts>` message and pushes the current branch.
- Merges to `main` use `--no-ff` (always — preserves branch topology).
- Abandoned work: merge to `graveyard` with `--no-ff`, delete the branch.
- No PRs — direct merges. CI in this project means *continuous integration* in the original sense: small branches merged often, history preserved, story traceable.

## Claude Code Notes

- Project Stop hook (`.claude/settings.json`): commits any leftover working-tree changes with a safety-net message, then pushes the current branch.
- Global Stop hook (`~/.claude/settings.json`): commits and pushes the transcript repo at `~/.claude/projects` to remote `machines/kvmtest`.
