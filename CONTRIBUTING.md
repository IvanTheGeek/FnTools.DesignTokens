# Building & Contributing

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- No other tooling required — the solution is self-contained

## Build

```bash
dotnet build FnTools.DesignTokens.slnx -c Release
```

## Test

```bash
dotnet run --project tests/FnTools.DesignTokens.Tests/FnTools.DesignTokens.Tests.fsproj -c Release
```

The test runner is [Expecto](https://github.com/haf/expecto). Output ends with a pass/fail summary; all 258 tests should pass.

## Publish

### Dev pre-release (local manual)

Pushes a `0.x.y-dev.<sha>` package to the Forgejo NuGet feed. Requires `~/.config/forgejo-claude.token` or `FORGEJO_TOKEN` env var.

```bash
./publish.sh --dev
```

### Stable release

1. Bump `<Version>` in all eight `.fsproj` files under `src/`
2. Commit: `git commit -m "chore: bump version to X.Y.Z"`
3. Tag: `git tag vX.Y.Z && git push && git push origin vX.Y.Z`
4. Pack and push: `./publish.sh`

The `publish-stable.yml` Forgejo Actions workflow also triggers on `v*` tags and pushes to the feed automatically once a runner is configured on the VPS.

## Project structure

```
src/
  FnTools.DesignTokens.Foundation/   — domain types, zero non-BCL deps
  FnTools.DesignTokens.Format/       — JSON parse/serialize (System.Text.Json)
  FnTools.DesignTokens.Validation/   — invariant checks (FsToolkit.ErrorHandling)
  FnTools.DesignTokens.Resolver/     — multi-set resolver document semantics
  FnTools.DesignTokens.Css/          — CSS emitter + CssIngest + CssAudit
  FnTools.DesignTokens.Bindings/     — typed F# binding emitter
  FnTools.DesignTokens.TokensStudio/ — Tokens Studio shim and export
  FnTools.DesignTokens/              — meta-package, public Api module
tests/
  FnTools.DesignTokens.Tests/        — Expecto test suite
docs/                                — API reference, spec context
LOGOS/                               — architecture decisions (ADRs), insights, open tasks
samples/                             — example token files (DTCG and Tokens Studio format)
scripts/                             — fsx experiment scripts
```

## Git conventions

- Branch names: `feature/`, `fix/`, `refactor/`, `docs/`, `experiment/`
- Merges to `main` use `--no-ff`
- No PRs — direct merge. Small branches, merged often.
- Abandoned work: merge to `graveyard` with `--no-ff`, delete the branch
- Commits explain what changed and why, not just what the diff contains

## Architecture decisions

All significant design choices are recorded in [LOGOS/decisions/](LOGOS/decisions/) as ADRs. Read these before making structural changes — they explain constraints that aren't obvious from the code alone.
