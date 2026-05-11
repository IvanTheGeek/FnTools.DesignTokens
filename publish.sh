#!/usr/bin/env bash
# Pack all layers and push to the Forgejo NuGet feed.
#
# PREFERRED PATH FOR STABLE RELEASES: push a 'v0.X.Y' git tag instead of
# running this script. The Forgejo build runner produces deterministic
# builds via .forgejo/workflows/publish-stable.yml (checkout the tag SHA,
# build, test, pack with -p:Version="${TAG#v}", push with --skip-duplicate).
# Same artifact every time for the same tag; tag is the canonical record
# of what was published.
#
# This script remains useful for:
#   - --dev pre-release iterations (versioned with dev.<shortsha> suffix)
#   - Local sanity-check pack before tagging
#   - Emergency manual publish if the CI runner is unavailable
#
# Usage:
#   ./publish.sh              # stable — version from .fsproj (prefer tag instead)
#   ./publish.sh --dev        # pre-release — version suffix: dev.<shortsha>
#   FORGEJO_TOKEN=xxx ./publish.sh [--dev]

set -euo pipefail

FEED="https://forgejo.ivanthegeek.com/api/packages/FnTools/nuget/index.json"
TOKEN="${FORGEJO_TOKEN:-$(cat ~/.config/forgejo-claude.token 2>/dev/null)}"
DEV=false

for arg in "$@"; do
    case "$arg" in
        --dev) DEV=true ;;
        *) echo "error: unknown argument: $arg" >&2; exit 1 ;;
    esac
done

if [ -z "$TOKEN" ]; then
    echo "error: set FORGEJO_TOKEN or create ~/.config/forgejo-claude.token" >&2
    exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
cd "$SCRIPT_DIR"

if [ "$DEV" = false ]; then
    echo "" >&2
    echo "note: stable releases normally use tag-triggered CI." >&2
    echo "      Push a 'v0.X.Y' tag; the Forgejo runner produces deterministic builds" >&2
    echo "      via .forgejo/workflows/publish-stable.yml. Continuing with local build —" >&2
    echo "      use --dev for pre-release versioning if that was your intent." >&2
    echo "" >&2
fi

echo "--- building solution ---"
dotnet build FnTools.DesignTokens.slnx -c Release --nologo -v quiet

echo "--- packing ---"
rm -rf artifacts/

if [ "$DEV" = true ]; then
    SHORT_SHA="$(git rev-parse --short HEAD)"
    BASE_VERSION="$(grep -m1 '<Version>' src/FnTools.DesignTokens/FnTools.DesignTokens.fsproj | sed 's/.*<Version>\(.*\)<\/Version>.*/\1/')"
    DEV_VERSION="${BASE_VERSION}-dev.${SHORT_SHA}"
    echo "    dev pre-release: ${DEV_VERSION}"
    dotnet pack FnTools.DesignTokens.slnx -c Release --nologo -v quiet \
        --no-build \
        -o artifacts/ \
        -p:Version="${DEV_VERSION}"
else
    dotnet pack FnTools.DesignTokens.slnx -c Release --nologo -v quiet \
        --no-build \
        -o artifacts/
fi

PACKAGES=(artifacts/*.nupkg)
if [ ${#PACKAGES[@]} -eq 0 ]; then
    echo "error: no .nupkg files found in artifacts/" >&2
    exit 1
fi

echo "--- pushing ${#PACKAGES[@]} package(s) ---"
for pkg in "${PACKAGES[@]}"; do
    echo "  pushing $(basename "$pkg")"
    dotnet nuget push "$pkg" \
        --source "$FEED" \
        --api-key "$TOKEN" \
        --skip-duplicate
done

echo "--- done ---"
