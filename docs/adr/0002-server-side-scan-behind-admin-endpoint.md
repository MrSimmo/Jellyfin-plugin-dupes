# 0002. Server-side scan behind one admin-only plugin endpoint

Date: 2026-10-06
Status: Accepted
Spec: specs/dupe-finder.md (DF-C2, DF-R1, DF-R2)

## Context
The user wants the plugin to "avoid needing the API" and stay simple. Any plugin page must talk to
the server somehow, so the real choice is where the scan runs. Jellyfin 12.2's user-facing
`/Items` API groups results by presentation key (collapsing duplicate episodes) and hides
alternate versions, which are exactly what this plugin must see. Jellyfin also serves plugin page
HTML/JS without authentication (`DashboardController.GetDashboardConfigurationPage` has no
`[Authorize]`), so access control must sit on the data endpoints.

## Decision
The scan runs in C# inside the plugin via `ILibraryManager` (`GetItemList` with
`ParentId = libraryId, Recursive = true, IncludeAlternateVersions = true`, `User` left null so no
per-user grouping or filtering applies; `GetAlbumArtists` with `AncestorIds = [libraryId]` for
artists). One controller, `DupeFinderController`, exposes `GET /DupeFinder/Libraries` and
`POST /DupeFinder/Scan`, both `[Authorize(Policy = Policies.RequiresElevation)]`. The page calls
them through `window.ApiClient`, which attaches the logged-in admin's token. The scan is
synchronous: one request, one JSON response. The plugin derives from the non-generic
`BasePlugin` because it stores no settings.

## Alternatives Considered
- Page-only, querying Jellyfin's `/Items` API from the browser: blind to collapsed episode
  duplicates and hidden versions; many paged requests on large libraries.
- Scheduled task that writes a report the page displays: handles very large libraries but adds
  persistence, polling and stale results. Revisit only if a synchronous scan of the test server's
  full library exceeds 120 s.
- API key or external script: contradicts the "no API" requirement.

## Consequences
No setup for the admin. Detection logic is plain C# and unit-testable. Items loaded this way must
never be saved back (the query uses reduced DTO options). A very large library makes one long
HTTP request; acceptable for an on-demand admin tool.
