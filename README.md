# Duplicate & Unmatched Finder (Jellyfin 12.2 plugin)

An admin-only dashboard page that scans selected libraries for:

- **Duplicates**: movies, series, episodes and albums, plus items Jellyfin has merged into one entry with several versions.
- **Unmatched items**: movies, series, episodes and music with no metadata provider id, and matched items missing an overview or image.

Each row links to Jellyfin's own Identify dialog, metadata editor and details page. **Download CSV** exports the list.

## Install

1. In Jellyfin, go to Dashboard → Plugins → Repositories and add the repository URL (`…/manifest.json`).
2. Install **Duplicate & Unmatched Finder** from the catalog.
3. Restart Jellyfin.
4. Open Dashboard → Plugins → **Duplicate & Unmatched Finder** in the sidebar.

## Build

Requires the .NET 10 SDK, Python 3, and `zip`.

```bash
dotnet build -c Release                         # warnings are errors
dotnet test                                     # detection rules
node --test tests/web/*.test.mjs                # page helpers (Node 22+)
PLUGIN_VERSION=1.0.0.0 scripts/build-repo.sh http://<host>:8765
scripts/serve-repo.sh                           # serves repo/ on port 8765
```

Live checks: the API check `scripts/crosscheck.py` and the UI check `tests/e2e/ui-check.mjs` read their configuration only from environment variables (`JF_URL`, `JF_ADMIN_USER`, `JF_ADMIN_PW`, `JF_NONADMIN_USER`, `JF_NONADMIN_PW`), so no server address or credential is ever written to disk.

## Documentation

- Behaviour: `specs/dupe-finder.md`
- Decisions: `docs/adr/`
- Visual rules: `specs/design.md`
