# 0005. Target Jellyfin 12.2 ABI on .NET 10, retargeting the template by hand

Date: 2026-10-06
Status: Accepted
Spec: specs/dupe-finder.md (DF-C1)

## Context
Jellyfin renumbered 10.12 as 12.0 (released 2026-09-08); the test server runs 12.2.0. The 12.0
release notes require plugins to retarget to .NET 10. `jellyfin-plugin-template` master still
targets net9.0, packages 10.11.5 and targetAbi 10.11.0.0 (Renovate PR open). NuGet has
`Jellyfin.Controller` and `Jellyfin.Model` 12.2.0. The server treats `targetAbi` as a minimum
version (`PluginManager.LoadManifest`).

## Decision
`net10.0`, `Jellyfin.Controller` and `Jellyfin.Model` 12.2.0 with `ExcludeAssets=runtime`,
`targetAbi` `12.2.0.0`. Keep the template's analyser settings (nullable, warnings as errors,
StyleCop) so the code matches official plugin conventions.

## Alternatives Considered
- `targetAbi` 12.0.0.0 against 12.0.0 packages: wider compatibility not needed; 12.2 is the target.
- Start from the template unchanged: does not load on 12.x.

## Consequences
Needs .NET 10 SDK locally (installed to `~/.dotnet` via `dotnet-install.sh`). Will not load on
10.x servers.
