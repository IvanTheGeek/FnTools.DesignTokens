#!/usr/bin/env bash
# Pack all five layers and push to the Forgejo NuGet feed.
#
# Usage:
#   ./publish.sh              # uses ~/.config/forgejo-claude.token
#   FORGEJO_TOKEN=xxx ./publish.sh

set -euo pipefail

FEED="https://forgejo.ivanthegeek.com/api/packages/FnTools/nuget/index.json"
TOKEN="${FORGEJO_TOKEN:-$(cat ~/.config/forgejo-claude.token 2>/dev/null)}"

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
dotnet pack FnTools.DesignTokens.slnx -c Release --nologo -v quiet \
    --no-build \
    -o artifacts/

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
