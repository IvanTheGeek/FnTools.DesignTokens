#!/usr/bin/env bash
# Pack all layers and push to the Forgejo NuGet feed.
#
# Usage:
#   ./publish.sh              # stable — version from .fsproj
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

echo "--- building solution ---"
dotnet build FnTools.DesignTokens.slnx -c Release --nologo -v quiet

echo "--- packing ---"
rm -rf artifacts/

if [ "$DEV" = true ]; then
    SHORT_SHA="$(git rev-parse --short HEAD)"
    BASE_VERSION="$(grep -m1 '<Version>' src/FnTools.DesignTokens/FnTools.DesignTokens.fsproj | sed 's/.*<Version>\(.*\)<\/Version>.*/\1/')"
    DEV_VERSION="${BASE_VERSION}-dev.${SHORT_SHA}"
    echo "    dev pre-release: ${DEV_VERSION}"
    dotnet pack src/FnTools.DesignTokens/FnTools.DesignTokens.fsproj -c Release --nologo -v quiet \
        --no-build \
        -o artifacts/ \
        -p:Version="${DEV_VERSION}"
else
    dotnet pack src/FnTools.DesignTokens/FnTools.DesignTokens.fsproj -c Release --nologo -v quiet \
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
