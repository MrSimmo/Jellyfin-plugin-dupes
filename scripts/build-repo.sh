#!/usr/bin/env bash
# DF-R9.1 / ADR-0004: build the plugin and write a Jellyfin plugin-repository manifest into repo/.
# Usage: [PLUGIN_VERSION=x.y.z.w] scripts/build-repo.sh <base-url>
#   <base-url> is where repo/ will be served, e.g. http://<this-machine-LAN-IP>:8765 (never committed).
set -euo pipefail
BASE_URL="${1:?usage: scripts/build-repo.sh <base-url>}"
BASE_URL="${BASE_URL%/}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}" DOTNET_CLI_TELEMETRY_OPTOUT=1
export PATH="$DOTNET_ROOT:$PATH"

VERSION="${PLUGIN_VERSION:-$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$ROOT/Directory.Build.props")}"
OUT="$ROOT/artifacts/publish"
ZIP_NAME="dupe-finder_${VERSION}.zip"

rm -rf "$OUT"
dotnet publish "$ROOT/src/Jellyfin.Plugin.DupeFinder/Jellyfin.Plugin.DupeFinder.csproj" -c Release -o "$OUT" \
  -p:Version="$VERSION" -p:AssemblyVersion="$VERSION" -p:FileVersion="$VERSION"

mkdir -p "$ROOT/repo"
rm -f "$ROOT/repo/$ZIP_NAME"
# Only our assembly: Jellyfin's own assemblies in the plugin folder would conflict at load time.
(cd "$OUT" && zip -q -X "$ROOT/repo/$ZIP_NAME" Jellyfin.Plugin.DupeFinder.dll)

CHECKSUM="$(md5 -q "$ROOT/repo/$ZIP_NAME" 2>/dev/null || md5sum "$ROOT/repo/$ZIP_NAME" | cut -d' ' -f1)"
TIMESTAMP="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

python3 - "$ROOT/repo/manifest.json" "$VERSION" "$BASE_URL/$ZIP_NAME" "$CHECKSUM" "$TIMESTAMP" <<'PY'
import json, sys
path, version, source_url, checksum, timestamp = sys.argv[1:6]
manifest = [{
    "guid": "fee5e03c-c3e1-4067-a6a8-0ed6eb63c3a7",
    "name": "Duplicate & Unmatched Finder",
    "description": "Scans selected libraries for duplicate and unmatched items, lists them for review, "
                   "opens Jellyfin's Identify dialog or metadata editor per item, and exports the list as CSV.",
    "overview": "Find duplicate and unmatched library items.",
    "owner": "Andy Simpson-Pirie",
    "category": "Administration",
    "versions": [{
        "version": version,
        "changelog": "Release " + version + ".",
        "targetAbi": "12.2.0.0",
        "sourceUrl": source_url,
        "checksum": checksum,
        "timestamp": timestamp,
    }],
}]
with open(path, "w", encoding="utf-8") as handle:
    json.dump(manifest, handle, indent=2)
    handle.write("\n")
PY

echo "Built $ZIP_NAME (md5 $CHECKSUM)"
echo "Repository URL: $BASE_URL/manifest.json"
