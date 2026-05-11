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

The test runner is [Expecto](https://github.com/haf/expecto). Output ends with a pass/fail summary; all 339 tests should pass.

## Reproducing CI locally with Docker

CI runs inside `mcr.microsoft.com/dotnet/sdk:10.0.203`. If you want to verify in the exact same environment before pushing:

```bash
# Build
docker run --rm \
  -v "$(pwd):/repo" \
  -w /repo \
  mcr.microsoft.com/dotnet/sdk:10.0.203 \
  dotnet build FnTools.DesignTokens.slnx -c Release --nologo

# Test
docker run --rm \
  -v "$(pwd):/repo" \
  -w /repo \
  mcr.microsoft.com/dotnet/sdk:10.0.203 \
  dotnet run --project tests/FnTools.DesignTokens.Tests/FnTools.DesignTokens.Tests.fsproj -c Release

# Pack (produces artifacts/*.nupkg)
docker run --rm \
  -v "$(pwd):/repo" \
  -w /repo \
  mcr.microsoft.com/dotnet/sdk:10.0.203 \
  dotnet pack FnTools.DesignTokens.slnx -c Release --nologo --no-build -o artifacts/
```

The workflows in `.forgejo/workflows/` are plain shell — no JavaScript actions — so the Docker environment is the full picture: the SDK container plus bash.

## CI

Three Forgejo Actions workflows run on the VPS-hosted runner (label `ubuntu-latest`, Docker executor using `mcr.microsoft.com/dotnet/sdk:10.0.203`):

| Workflow | Trigger | What it does |
|---|---|---|
| `ci.yml` | push to `main`, PRs | build + test |
| `publish-dev.yml` | push to `main` | build + test + pack `0.x.y-dev.<sha>` + push to feed |
| `publish-stable.yml` | push of `v*` tag | build + test + pack with tag version + push to feed |

The NuGet feed is `https://forgejo.ivanthegeek.com/api/packages/FnTools/nuget/index.json`. The `FORGEJO_NUGET_TOKEN` secret is set in the repo settings on Forgejo.

## Manual publish

If CI is unavailable, `publish.sh` does the same thing locally. It reads the token from `~/.config/forgejo-claude.token` or `FORGEJO_TOKEN` env var.

```bash
./publish.sh        # stable — version from .fsproj files
./publish.sh --dev  # pre-release — appends -dev.<shortsha>
```

## Stable release process

1. Bump `<Version>` in all eight `.fsproj` files under `src/`
2. `git commit -m "chore: bump version to X.Y.Z"`
3. `git tag vX.Y.Z && git push && git push origin vX.Y.Z`

The `publish-stable.yml` workflow fires on the tag push and handles the rest. The `.fsproj` version is what appears in `dev` builds; the tag version overrides it for stable builds.

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
.forgejo/workflows/                  — CI/CD workflow definitions
```

## Git conventions

- Branch names: `feature/`, `fix/`, `refactor/`, `docs/`, `experiment/`
- Merges to `main` use `--no-ff`
- No PRs — direct merge. Small branches, merged often.
- Abandoned work: merge to `graveyard` with `--no-ff`, delete the branch
- Commits explain what changed and why, not just what the diff contains

## Architecture decisions

All significant design choices are recorded in [LOGOS/decisions/](LOGOS/decisions/) as ADRs. Read these before making structural changes — they explain constraints that aren't obvious from the code alone.
