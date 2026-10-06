# 0004. Deliver via a self-hosted plugin repository served from the dev machine

Date: 2026-10-06
Status: Accepted
Spec: specs/dupe-finder.md (DF-R9)

## Context
The user wants plugin-repository installation, tested locally first, with a dev server the
Jellyfin server can point at. The server cannot be reached by file copy from this machine.

## Decision
`scripts/build-repo.sh <base-url>` builds, zips the plugin DLL, and writes a Jellyfin-format
`repo/manifest.json` (fields as in the official stable manifest: guid, name, description,
overview, owner, category, versions[version, changelog, targetAbi, sourceUrl, checksum (MD5),
timestamp]). `scripts/serve-repo.sh` serves `repo/` with `python3 -m http.server`. The base URL is
an argument, so no address is committed (DF-C4). `repo/` is git-ignored.

## Alternatives Considered
- Manual copy into the server's plugins folder: not wanted; no file access to the server.
- Hosting on GitHub Releases/Pages now: the user will push to a remote later; the build script
  works unchanged with a public base URL.

## Consequences
The macOS firewall may prompt to allow incoming connections for Python. Each rebuild needs a new
version number (or reinstall) for Jellyfin to pick it up.
