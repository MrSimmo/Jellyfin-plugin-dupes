#!/usr/bin/env bash
# DF-R9.2: serve repo/ so a Jellyfin server on the LAN can install the plugin from it.
# Usage: scripts/serve-repo.sh [port]   (default 8765)
set -euo pipefail
PORT="${1:-8765}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
exec python3 -m http.server "$PORT" --bind 0.0.0.0 --directory "$ROOT/repo"
