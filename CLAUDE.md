# Duplicate & Unmatched Finder — project rules

- Behaviour: `specs/dupe-finder.md`. Visual rules: `specs/design.md`. Decisions: `docs/adr/`. Plan: `docs/superpowers/plans/2026-10-06-dupe-finder.md`. Log: `PROGRESS.md`.
- Never write the test server's address or any account/credential detail to disk (spec DF-C4). Pass `JF_URL`, `JF_ADMIN_USER`, `JF_ADMIN_PW`, `JF_NONADMIN_USER`, `JF_NONADMIN_PW` as environment variables on the command line only.
- Jellyfin 12.x rejects legacy `X-Emby-*` auth headers: see `docs/lessons/jellyfin-12-auth-header.md`.
- Toolchain: `export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1` before any `dotnet` command.
- Build: `dotnet build -c Release` (warnings are errors). Unit tests: `dotnet test`. Page tests: `node --test tests/web/*.test.mjs`.
- Package: `PLUGIN_VERSION=<x.y.z.w> scripts/build-repo.sh http://<this-machine-LAN-IP>:8765`, then `scripts/serve-repo.sh`.
- Live checks: `scripts/crosscheck.py` and `tests/e2e/ui-check.mjs` (env vars only).
- Do not suppress analyser rules or edit `.editorconfig`; fix the code.
