# Spec: Duplicate & Unmatched Finder (Jellyfin plugin)

Status: Approved design, 2026-10-06
Target: Jellyfin Server 12.2.0 (jellyfin and jellyfin-web tag `v12.2`)
Decisions: ADR-0001 … ADR-0009 in `docs/adr/`. Visual rules: `specs/design.md`.

## 1. Purpose

An administrator opens a page inside the Jellyfin dashboard, picks libraries and checks,
runs a scan, reviews duplicate and unmatched items in a table, jumps into Jellyfin's own
Identify dialog or metadata editor to fix unmatched items, and downloads the findings as CSV.

## 2. Constraints

- **DF-C1** Native server plugin: `net10.0`, compiled against `Jellyfin.Controller` and
  `Jellyfin.Model` 12.2.0, `targetAbi` `12.2.0.0` (ADR-0005).
- **DF-C2** No API keys, external scripts or external services. The page talks only to the
  plugin's own endpoints using the admin's existing web session (ADR-0002).
- **DF-C3** Read-only towards the library. The plugin never saves, modifies or deletes items.
  Metadata changes happen only through Jellyfin's own Identify dialog / metadata editor,
  opened by the admin.
- **DF-C4** The test server's address and any credential details are never written to disk
  (source, scripts, docs, logs, commit messages). They are supplied at run time via
  environment variables.
- **DF-C5** The UI uses only Jellyfin's built-in dashboard components and styles
  (`specs/design.md`).

## 3. Identity

| Field | Value |
|---|---|
| Display name | `Duplicate & Unmatched Finder` |
| Assembly / root namespace | `Jellyfin.Plugin.DupeFinder` |
| Plugin GUID | `fee5e03c-c3e1-4067-a6a8-0ed6eb63c3a7` |
| Initial version | `1.0.0.0` |
| Manifest category | `Administration` |
| Owner (shown in the plugin catalog) | `Andy Simpson-Pirie` |

The name does not appear in the official stable plugin manifest (checked 2026-10-06).

## 4. Definitions

- **Scanned libraries**: the libraries (Jellyfin virtual folders) selected by the admin.
- **Item set**: non-virtual items inside the scanned libraries, excluding extras (items with an
  `ExtraType`) and additional parts (owned items that are not alternate versions). Alternate
  versions are included even though Jellyfin 12.2 stores auto-detected ones as owned items
  (amended 2026-10-06, see ADR-0008).
- **Has provider id**: the item's `ProviderIds` contains at least one non-blank value.
- **Normalised text**: Unicode NFKD, combining marks removed, invariant lower case, `&`
  replaced by `and`, then every character that is not a letter or digit removed.
  Example: `Amélie` → `amelie`; `Spider-Man: Far From Home` → `spidermanfarfromhome`.
- **Version group**: an item that Jellyfin has merged with alternate versions, plus every item
  whose `PrimaryVersionId` is that item's id. The primary is the item with no
  `PrimaryVersionId`.
- **Collapsed set**: the item set with every version group replaced by its primary item.
- **Finding**: one row in the results. Duplicate checks produce groups of ≥2 findings that share
  a group number; unmatched checks produce single findings (no group number).

## 5. Requirements

### DF-R1 Admin-only access

- **DF-R1.1** The plugin implements `IHasWebPages` and registers page `dupefinder`
  (`EnableInMainMenu = true`, `DisplayName = "Duplicate & Unmatched Finder"`,
  `MenuIcon = "content_copy"`), plus its JS module page `dupefinderjs`.
- **DF-R1.2** Every plugin endpoint requires `Policies.RequiresElevation`.
- **DF-R1.3** The page's HTML and JS contain no library data. (Jellyfin serves plugin page
  resources without authentication; the data endpoints carry the access control.)

### DF-R2 Endpoints

JSON follows Jellyfin's API conventions (ADR-0008): PascalCase property names, enum values as
strings, and properties whose value is null are omitted (shown as `?` below). Request property
names are matched case-insensitively.

- **DF-R2.1** `GET /DupeFinder/Libraries` returns the server's libraries:
  `[{ "Id": string (GUID, 32 hex digits), "Name": string, "CollectionType"?: string }]`,
  ordered by name.
- **DF-R2.2** `POST /DupeFinder/Scan`, JSON body
  `{ "LibraryIds": string[], "Checks": string[] }`. Valid check ids, in display order:
  `DuplicateMovies`, `DuplicateSeries`, `DuplicateEpisodes`, `DuplicateAlbums`,
  `MergedVersions`, `UnmatchedMoviesSeries`, `UnmatchedEpisodes`, `UnmatchedMusic`,
  `IncompleteMetadata`.
- **DF-R2.3** Scan response:
  `{ "ScannedItemCount": int, "DurationMs": int, "Summary": { "<check id>": int },
  "Findings": [ { "Check": "<check id>", "Group"?: int, "ItemId": string, "LibraryName": string,
  "ItemType": "Movie"|"Series"|"Episode"|"MusicAlbum"|"MusicArtist", "Name": string,
  "Year"?: int, "Season"?: int, "Episode"?: int, "SeriesName"?: string, "Path"?: string,
  "SizeBytes"?: int, "ProviderIds": { "<key>": "<value>" }, "Reason": string } ] }`.
  `Summary` has one entry per requested check. For duplicate checks and `MergedVersions` the
  count is the number of groups; for the others it is the number of findings.
  Findings are ordered by check (display order), then group, then name, then path.
  Group numbers run 1, 2, 3… across the whole response in output order. Within a group, members
  are ordered by name then path, except `MergedVersions`, where the primary comes first.
  Within one check an item appears at most once; `LibraryName` is the first scanned library
  (by name) in which the item was found. `ProviderIds` holds only non-blank values, trimmed.
- **DF-R2.4** A scan request with an empty `LibraryIds`, an unknown library id, an empty
  `Checks`, or an unknown check id returns HTTP 400 with an RFC 7807 problem-details body whose
  `detail` names the problem: `Select at least one library.`, `Unknown library id '<id>'.`,
  `Select at least one check.`, `Unknown check '<id>'.`

### DF-R3 Duplicate detection

Duplicate groups are transitive: if A matches B and B matches C, then A, B and C form one group.
Only groups with two or more items are reported. Matching runs across all scanned libraries.
Provider id values are compared after trimming surrounding whitespace, ignoring case.
A normalised name (or joined album artist) that is empty never matches anything (ADR-0009).

- **DF-R3.1 DuplicateMovies** — over `Movie` items in the collapsed set, two items match when:
  (a) they share a non-blank TMDb id; or (b) they share a non-blank IMDb id; or (c) at least
  one of the two has no provider id, their normalised names are equal and non-empty, and their
  `ProductionYear` values are equal (both null counts as equal).
  Two items that both have provider ids never match on name and year alone.
- **DF-R3.2 DuplicateSeries** — as DF-R3.1, over `Series` items, with provider keys TVDb, TMDb
  and IMDb.
- **DF-R3.3 DuplicateEpisodes** — over `Episode` items in the collapsed set, two items match when
  `SeriesId`, `ParentIndexNumber` (season) and `IndexNumber` (episode) are all non-null and
  equal. `IndexNumberEnd` is ignored. A series duplicated across libraries is reported by
  DF-R3.2, not by repeating every episode.
- **DF-R3.4 DuplicateAlbums** — over `MusicAlbum` items, two items match when they share a
  non-blank MusicBrainz album (release) id, or when both have at least one album artist and
  their normalised joined album artists and normalised names are equal and non-empty.
- **DF-R3.5 MergedVersions** — every version group of `Movie` or `Episode` items with two or
  more members is reported as one group, primary first.
- **DF-R3.6 Reasons** — each duplicate finding's `reason` lists, joined by `; `, the distinct
  rules that put it in its group: `Same TMDb id <id>`, `Same IMDb id <id>`, `Same TVDb id <id>`,
  `Same title + year (unmatched)`, `Same episode S<ss>E<ee>`, `Same MusicBrainz release <id>`,
  `Same album artist + album`, `Merged versions`.

### DF-R4 Unmatched detection

- **DF-R4.1 UnmatchedMoviesSeries** — `Movie` and `Series` items in the collapsed set with no
  provider id. Reason: `No provider ids`.
- **DF-R4.2 UnmatchedEpisodes** — `Episode` items in the collapsed set with no provider id.
  Reason: `No provider ids`. The UI labels this check "often noisy".
- **DF-R4.3 UnmatchedMusic** — `MusicAlbum` items with no provider id, and album artists
  (`MusicArtist` items returned by `ILibraryManager.GetAlbumArtists` with
  `AncestorIds = [libraryId]`, as Jellyfin's own Album Artists endpoint does) with no
  provider id. Reason: `No provider ids`.
- **DF-R4.4 IncompleteMetadata** — `Movie` and `Series` items in the collapsed set that have a
  provider id but a blank overview or no primary image, and `MusicAlbum` items that have a
  provider id but no primary image. Reason lists what is missing: `No overview`,
  `No primary image`, joined by `; `.

### DF-R5 Page: selection and scan

- **DF-R5.1** The page lists every library (name and type) with a checkbox, plus a "Select all"
  checkbox. All libraries are selected on first load.
- **DF-R5.2** The page lists the nine checks under "Duplicates" and "Unmatched" headings, each
  with a checkbox and a one-line description. All are selected on first load except
  `UnmatchedEpisodes`.
- **DF-R5.3** The Scan button is disabled while no library or no check is selected, and while a
  scan is running. During a scan Jellyfin's loading indicator shows.
- **DF-R5.4** A failed request shows `Dashboard.alert` with the server's message.

### DF-R6 Page: results

- **DF-R6.1** After a scan the page shows `Scanned <n> items in <s> s` and one summary line per
  requested check with its count.
- **DF-R6.2** One results table with columns: Check, Group, Library, Type, Name, Year, Episode
  (`S01E03` for episodes, otherwise blank), Path, Size (human-readable, e.g. `1.4 GB`),
  Provider IDs (`Key=Value` pairs), Reason, Actions. Row order follows DF-R2.3.
- **DF-R6.3** Rows of the same duplicate group sit together; alternate groups carry the
  group-band background defined in `specs/design.md`.
- **DF-R6.4** A scan with no findings shows `No issues found for the selected libraries and
  checks.` and no table.

### DF-R7 Page: row actions

- **DF-R7.1 Open** — opens `#/details?id=<itemId>&serverId=<serverId>` in a new browser tab.
- **DF-R7.2 Identify** — shown only for rows whose `ItemType` is `Movie`, `Series`, `MusicAlbum` or `MusicArtist`,
  and only when `Dashboard.itemIdentifier` exists. It calls
  `Dashboard.itemIdentifier.show(itemId, ApiClient.serverId())` (ADR-0003). When the promise
  resolves, the row shows `Identified — rescan to refresh`. When it rejects (cancelled),
  nothing changes.
- **DF-R7.3 Edit metadata** — on every row; opens `#/metadata?id=<itemId>` in a new browser tab.

### DF-R8 CSV export

- **DF-R8.1** A "Download CSV" button is enabled when the latest scan returned at least one
  finding.
- **DF-R8.2** The CSV is built in the browser from the latest scan response, with no extra
  request (ADR-0006).
- **DF-R8.3** Columns, in order: `Check, Group, Library, Type, Name, Year, Season, Episode,
  Series, Path, SizeBytes, ProviderIds, Reason, ItemId`. `ProviderIds` is `Key=Value` pairs
  joined by `; `. `Check` holds the same label the table shows. One row per finding, same order
  as the table.
- **DF-R8.4** Format: UTF-8 with BOM, comma separator, CRLF line endings. Fields containing a
  comma, double quote, CR or LF are wrapped in double quotes, with inner quotes doubled.
- **DF-R8.5** Text fields whose first character is `=`, `+`, `-`, `@`, tab or CR are prefixed
  with `'` (spreadsheet formula injection). Numeric columns are not altered.
- **DF-R8.6** File name: `dupefinder-YYYYMMDD-HHmm.csv` in the browser's local time.

### DF-R9 Delivery

- **DF-R9.1** `scripts/build-repo.sh <base-url>` runs `dotnet publish -c Release`, zips only
  `Jellyfin.Plugin.DupeFinder.dll` into `repo/dupe-finder_<version>.zip`, and writes
  `repo/manifest.json`: a one-element array with `guid`, `name`, `description`, `overview`,
  `owner`, `category`, and `versions: [{ version, changelog, targetAbi: "12.2.0.0",
  sourceUrl: "<base-url>/dupe-finder_<version>.zip", checksum: <MD5 hex of the zip>,
  timestamp: <UTC ISO-8601> }]`.
- **DF-R9.2** `scripts/serve-repo.sh [port]` serves `repo/` over HTTP on all interfaces,
  default port 8765.
- **DF-R9.3** `repo/`, `bin/` and `obj/` are git-ignored. No base URL or server address is
  committed.

## 6. Acceptance criteria

Each line is checkable. "Admin" and "non-admin" mean real accounts on the test server.

| ID | Criterion | Covers |
|---|---|---|
| AC-1 | Unit tests for detection rules pass (`dotnet test` exit 0), including every case in AC-2 … AC-6. | R3, R4 |
| AC-2 | Movies: same TMDb → grouped; same IMDb with different case → grouped; two matched movies with different TMDb ids but same title + year → not grouped; matched + unmatched with same title + year → grouped; `Amélie` vs `Amelie` (both unmatched, same year) → grouped; A–B by TMDb and B–C by IMDb → one group of 3. | R3.1, R3.6 |
| AC-3 | Versions: a version group of 2 plus a separate movie with the same TMDb id → one DuplicateMovies group of 2 (primary + separate copy) and one MergedVersions group of 2. | R3.1, R3.5 |
| AC-4 | Episodes: same series S01E03 twice → grouped; same numbers in different series → not grouped; null episode number → never grouped. | R3.3 |
| AC-5 | Albums: same MusicBrainz release → grouped; same album artist + name with different release ids → grouped; same name with different album artist → not grouped; same name with no album artist → not grouped. | R3.4 |
| AC-6 | Unmatched/incomplete: item with only blank provider values → unmatched; matched movie with no overview → IncompleteMetadata reason `No overview`; unmatched movie is not also reported as incomplete. | R4 |
| AC-7 | After adding `<base-url>/manifest.json` as a repository, the plugin appears in the catalog, installs, and after restart shows as Active, version 1.0.0.0. | R9 |
| AC-8 | Logged in as admin, the dashboard sidebar shows "Duplicate & Unmatched Finder" and opens the page. | R1.1 |
| AC-9 | `GET /DupeFinder/Libraries` and `POST /DupeFinder/Scan`: admin token → 200; non-admin token → 403; no token → 401. | R1.2 |
| AC-10 | Invalid scan bodies (empty libraries, unknown library id, empty checks, unknown check) → 400 with the DF-R2.4 `detail` text. | R2.4 |
| AC-11 | On the test server, the plugin's per-library counts for UnmatchedMoviesSeries and UnmatchedMusic (albums and album artists) equal an independent count from Jellyfin's REST API (`scripts/crosscheck.py`), and every DuplicateMovies group sharing a TMDb id appears in the cross-check's TMDb grouping. | R3.1, R4 |
| AC-12 | In a headless browser as admin: select libraries and checks, Scan, the summary and table render, Download CSV produces a file whose parsed row count equals the table's row count and whose header matches DF-R8.3. | R5, R6, R8 |
| AC-13 | Clicking Identify on an unmatched movie opens Jellyfin's Identify dialog for that item; Edit metadata opens the metadata editor for that item; Open opens its details page. | R7 |
| AC-14 | A field containing `,` and `"` round-trips through the CSV unchanged (parsed with Python's `csv` module); a name starting with `=` is written with a leading `'`. | R8.4, R8.5 |

## 7. Out of scope

Deleting, merging or renaming items; automatic fixing; scheduled or background scans;
persisting results between visits; non-admin access; Identify for episodes (Jellyfin does not
offer it); checks for books, photos, music videos and individual audio tracks.

## 8. Amendments

- 2026-10-06 (before implementation): §4 Item set now keeps alternate versions stored as owned
  items; DF-R2 rewritten to Jellyfin's PascalCase / omit-null / problem-details conventions;
  finding field `type` renamed `ItemType` (analyser rule CA1721 forbids a `Type` property next to
  `GetType()`); group numbering and member order made explicit. Rationale: ADR-0008.
- 2026-10-06 (during implementation, Task 2): DF-R3 now states that empty normalised names and
  empty joined album artists never match (DF-R3.1(c), DF-R3.2, DF-R3.4), and that every provider
  id is compared trimmed and case-insensitively (previously stated for IMDb only); DF-R2.3
  `ProviderIds` values are trimmed. Behaviour the approved plan already specified (Review Focus 1);
  the spec text had not caught up. Rationale: ADR-0009.
