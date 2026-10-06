# Duplicate & Unmatched Finder Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A native Jellyfin 12.2 plugin with an admin-only dashboard page that scans selected libraries for duplicate and unmatched items, lists them, opens Jellyfin's Identify dialog / metadata editor per row, and exports the list as CSV.

**Architecture:** C# plugin (`net10.0`) whose admin-only controller (`/DupeFinder/Libraries`, `/DupeFinder/Scan`) runs a synchronous scan through `ILibraryManager`; detection rules are pure C# over a plain `ScannedItem` record so they are unit-tested without Jellyfin. The dashboard page is static HTML plus an ES-module controller (`data-controller="__plugin/dupefinderjs"`) that calls the endpoints through `window.ApiClient`, renders a table, builds the CSV in the browser, and calls `Dashboard.itemIdentifier.show`. Delivery is a self-hosted plugin repository (`repo/manifest.json` + zip) served from the dev machine.

**Tech Stack:** .NET SDK 10.0 (installed to `~/.dotnet`), `Jellyfin.Controller`/`Jellyfin.Model` 12.2.0, xUnit (SDK `dotnet new xunit` template), StyleCop + .NET analysers (template settings), vanilla ES2020 JavaScript, Node 24 `node:test`, Python 3.12 stdlib, Playwright (Chromium, headless).

**Spec:** `specs/dupe-finder.md` (behaviour, acceptance criteria AC-1…AC-14), `specs/design.md` (visual rules), `docs/adr/0001…0008` (decisions). Read all three before starting; this plan argues from them.

## Global Constraints

- DF-C1: `net10.0`; `Jellyfin.Controller` and `Jellyfin.Model` **12.2.0** with `<ExcludeAssets>runtime</ExcludeAssets>`; `targetAbi` **`12.2.0.0`**.
- DF-C2: no API keys, external scripts or services in the product; the page uses `window.ApiClient` (the admin's session).
- DF-C3: never save, modify or delete library items. Items read by the scanner are never passed back to any Jellyfin save/update method.
- DF-C4: **never write the test server's address or any account/credential detail to disk** — not in code, scripts, docs, PROGRESS.md, commit messages or test output files. Pass them only as environment variables on the command line (`JF_URL`, `JF_ADMIN_USER`, `JF_ADMIN_PW`, `JF_NONADMIN_USER`, `JF_NONADMIN_PW`).
- DF-C5: page uses only Jellyfin's built-in classes/components; the only colour value is `--df-group-band: rgba(127, 127, 127, 0.12)` (`specs/design.md`).
- Identity (spec §3): name `Duplicate & Unmatched Finder`; namespace/assembly `Jellyfin.Plugin.DupeFinder`; GUID `fee5e03c-c3e1-4067-a6a8-0ed6eb63c3a7`; release version `1.0.0.0`; category `Administration`; owner `Andy Simpson-Pirie`.
- JSON (ADR-0008): PascalCase property names, enums as strings, null properties omitted; validation errors are problem details with the exact DF-R2.4 `detail` strings.
- Analysers: the plugin project keeps the template's `TreatWarningsAsErrors`, `AnalysisMode=AllEnabledByDefault`, StyleCop and `.editorconfig`. Fix code to satisfy a rule; never add suppressions or edit `.editorconfig`. If satisfying a rule would change specified behaviour, stop and report.
- Shell: every `dotnet` command assumes `export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1` in the same command (shell state does not persist between tool calls).
- Commits: one commit per task (more if a task says so), message prefixed with the spec IDs it implements, ending with the attribution lines from the executing session's system reminder. Never push; the user pushes.

## Review Focus

Inputs the spec implies but no acceptance criterion exercises, most likely to bite first:

1. **Blank or punctuation-only names** (`""`, `"!!!"`) on unmatched items must never group by title — every such item would otherwise collapse into one giant "duplicate" group. Pinned by `Movies_UnmatchedWithBlankOrPunctuationNames_AreNeverGrouped` (Task 2).
2. **The same item reached through two selected libraries** (album artists span music libraries) must appear once, attributed to the first library by name. Pinned by `Scan_ItemFoundThroughTwoLibraries_IsReportedOnce` (Task 4).
3. **Library text containing HTML** (`<b>`, `<script>` in a title or path) must render as text, never markup, in an admin session. Pinned by the `innerHTML` audit step in Task 6 (only one static template string may use `innerHTML`).
4. **Leaving the page and coming back** must not duplicate the library list or double-bind the Scan button. Pinned by the revisit step in `tests/e2e/ui-check.mjs` (Task 7).
5. **Non-ASCII names** (accents, CJK) must survive into the CSV and open correctly in Excel (UTF-8 BOM). Pinned by `non-ASCII names survive unchanged` in `tests/web/dupefinder.test.mjs` (Task 6).

## File Structure

```
.editorconfig                         verbatim copy of jellyfin-plugin-template @ c93225a (analyser rules)
.gitignore
CLAUDE.md                             project rules for future sessions (commands, DF-C4)
Directory.Build.props                 Version 1.0.0.0
Jellyfin.Plugin.DupeFinder.slnx       (or .sln — whatever `dotnet new sln` creates)
README.md                             install/build instructions (Task 8)
src/Jellyfin.Plugin.DupeFinder/
  Jellyfin.Plugin.DupeFinder.csproj
  Plugin.cs                           plugin identity + page registration (DF-R1.1)
  Api/DupeFinderController.cs         admin-only endpoints (DF-R1.2, DF-R2)
  Model/CheckId.cs                    the nine checks, display order
  Model/ItemKind.cs                   Movie, Series, Episode, MusicAlbum, MusicArtist
  Model/Finding.cs                    one result row (public, serialised)
  Model/LibraryDto.cs                 library list entry (public, serialised)
  Model/ScanRequest.cs                scan body (public, deserialised)
  Model/ScanResponse.cs               scan result (public, serialised)
  Scanning/TextNormalizer.cs          spec §4 "normalised text"
  Scanning/ScannedItem.cs             Jellyfin-free snapshot of one item
  Scanning/LibraryInfo.cs             Jellyfin-free library descriptor
  Scanning/ILibraryItemSource.cs      seam between ScanService and Jellyfin
  Scanning/DisjointSet.cs             union-find for transitive groups
  Scanning/GroupBuilder.cs            bucket → union → grouped findings with reasons
  Scanning/DuplicateDetector.cs       DF-R3.1–R3.4, R3.6
  Scanning/VersionGroups.cs           collapsed set (§4) + DF-R3.5
  Scanning/UnmatchedDetector.cs       DF-R4.1–R4.4
  Scanning/ScanOutcome.cs             response-or-error result
  Scanning/ScanService.cs             validation, gathering, ordering, numbering (DF-R2)
  Scanning/JellyfinLibraryItemSource.cs  the only code touching ILibraryManager (ADR-0002, ADR-0008)
  Web/dupefinder.html                 page markup (DF-R5–R8)
  Web/dupefinder.js                   page controller module (ADR-0007)
tests/Jellyfin.Plugin.DupeFinder.Tests/
  TestItems.cs, FakeLibraryItemSource.cs
  TextNormalizerTests.cs, DuplicateDetectorTests.cs, VersionGroupsTests.cs,
  UnmatchedDetectorTests.cs, ScanServiceTests.cs
tests/web/dupefinder.test.mjs         node:test for the page's pure functions
tests/e2e/ui-check.mjs                Playwright live UI checks (+ package.json, package-lock.json)
scripts/build-repo.sh                 build + zip + manifest (DF-R9.1)
scripts/serve-repo.sh                 serve repo/ (DF-R9.2)
scripts/crosscheck.py                 live API checks AC-7, AC-9, AC-10, AC-11
```

Steps marked **🧑 CONTROLLER + USER** need the human (approvals, Jellyfin UI clicks, credentials). In subagent-driven execution the controller (main session) does them itself and never delegates them.

---

### Task 1: Toolchain, solution scaffold and text normalisation

**Files:**
- Create: `.editorconfig`, `.gitignore`, `CLAUDE.md`, `Directory.Build.props`, solution file
- Create: `src/Jellyfin.Plugin.DupeFinder/Jellyfin.Plugin.DupeFinder.csproj`
- Create: `src/Jellyfin.Plugin.DupeFinder/Model/CheckId.cs`, `src/Jellyfin.Plugin.DupeFinder/Model/ItemKind.cs`
- Create: `src/Jellyfin.Plugin.DupeFinder/Scanning/TextNormalizer.cs`
- Create: `tests/Jellyfin.Plugin.DupeFinder.Tests/` (template) and `tests/Jellyfin.Plugin.DupeFinder.Tests/TextNormalizerTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `public enum Jellyfin.Plugin.DupeFinder.Model.CheckId { DuplicateMovies, DuplicateSeries, DuplicateEpisodes, DuplicateAlbums, MergedVersions, UnmatchedMoviesSeries, UnmatchedEpisodes, UnmatchedMusic, IncompleteMetadata }` (declaration order = display order)
  - `public enum Jellyfin.Plugin.DupeFinder.Model.ItemKind { Movie, Series, Episode, MusicAlbum, MusicArtist }`
  - `internal static string Jellyfin.Plugin.DupeFinder.Scanning.TextNormalizer.Normalize(string? text)`
  - Test project `Jellyfin.Plugin.DupeFinder.Tests` with `InternalsVisibleTo` access to the plugin.

- [ ] **Step 1 (🧑 CONTROLLER + USER): Approve installs, install .NET 10 SDK**

Ask the user once, before any install, to approve: (a) .NET 10 SDK into `~/.dotnet` via Microsoft's `dotnet-install.sh`; (b) NuGet restore of `Jellyfin.Controller` 12.2.0, `Jellyfin.Model` 12.2.0, `SerilogAnalyzer` 0.15.0, `StyleCop.Analyzers` 1.2.0-beta.556, `SmartAnalyzers.MultithreadingAnalyzer` 1.1.31 and the xUnit packages pinned by `dotnet new xunit`; (c) later in Task 7, `npm install playwright` into `tests/e2e` and `npx playwright install chromium`. End the turn after asking. Once approved:

```bash
SCRATCH="${TMPDIR:-/tmp}"
curl -sSL https://dot.net/v1/dotnet-install.sh -o "$SCRATCH/dotnet-install.sh"
bash "$SCRATCH/dotnet-install.sh" --channel 10.0 --install-dir "$HOME/.dotnet"
export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet --version
```

Expected: a version starting `10.0.`.

- [ ] **Step 2: Copy the template's analyser config and write repo files**

```bash
curl -sSL https://raw.githubusercontent.com/jellyfin/jellyfin-plugin-template/c93225a0a5a76d3843db05b4b5b77fcfc482fba3/.editorconfig -o .editorconfig
wc -l .editorconfig
```

Expected: `542 .editorconfig`.

Create `.gitignore`:

```gitignore
bin/
obj/
.vs/
.idea/
.DS_Store
artifacts/
repo/
node_modules/
```

Create `Directory.Build.props`:

```xml
<Project>
    <PropertyGroup>
        <Version>1.0.0.0</Version>
        <AssemblyVersion>1.0.0.0</AssemblyVersion>
        <FileVersion>1.0.0.0</FileVersion>
    </PropertyGroup>
</Project>
```

Create `CLAUDE.md`:

```markdown
# Duplicate & Unmatched Finder — project rules

- Behaviour: `specs/dupe-finder.md`. Visual rules: `specs/design.md`. Decisions: `docs/adr/`. Plan: `docs/superpowers/plans/2026-10-06-dupe-finder.md`. Log: `PROGRESS.md`.
- Never write the test server's address or any account/credential detail to disk (spec DF-C4). Pass `JF_URL`, `JF_ADMIN_USER`, `JF_ADMIN_PW`, `JF_NONADMIN_USER`, `JF_NONADMIN_PW` as environment variables on the command line only.
- Jellyfin 12.x rejects legacy `X-Emby-*` auth headers: see `docs/lessons/jellyfin-12-auth-header.md`.
- Toolchain: `export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1` before any `dotnet` command.
- Build: `dotnet build -c Release` (warnings are errors). Unit tests: `dotnet test`. Page tests: `node --test tests/web/*.test.mjs`.
- Package: `PLUGIN_VERSION=<x.y.z.w> scripts/build-repo.sh http://<this-machine-LAN-IP>:8765`, then `scripts/serve-repo.sh`.
- Live checks: `scripts/crosscheck.py` and `tests/e2e/ui-check.mjs` (env vars only).
- Do not suppress analyser rules or edit `.editorconfig`; fix the code.
```

- [ ] **Step 3: Create the solution and plugin project**

```bash
export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet new sln -n Jellyfin.Plugin.DupeFinder
mkdir -p src/Jellyfin.Plugin.DupeFinder/Model src/Jellyfin.Plugin.DupeFinder/Scanning src/Jellyfin.Plugin.DupeFinder/Api src/Jellyfin.Plugin.DupeFinder/Web
```

Create `src/Jellyfin.Plugin.DupeFinder/Jellyfin.Plugin.DupeFinder.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>Jellyfin.Plugin.DupeFinder</RootNamespace>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <Nullable>enable</Nullable>
    <AnalysisMode>AllEnabledByDefault</AnalysisMode>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Jellyfin.Controller" Version="12.2.0">
      <ExcludeAssets>runtime</ExcludeAssets>
    </PackageReference>
    <PackageReference Include="Jellyfin.Model" Version="12.2.0">
      <ExcludeAssets>runtime</ExcludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="SerilogAnalyzer" Version="0.15.0" PrivateAssets="All" />
    <PackageReference Include="StyleCop.Analyzers" Version="1.2.0-beta.556" PrivateAssets="All" />
    <PackageReference Include="SmartAnalyzers.MultithreadingAnalyzer" Version="1.1.31" PrivateAssets="All" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="Jellyfin.Plugin.DupeFinder.Tests" />
  </ItemGroup>

</Project>
```

Create `src/Jellyfin.Plugin.DupeFinder/Model/CheckId.cs`:

```csharp
namespace Jellyfin.Plugin.DupeFinder.Model;

/// <summary>
/// The checks a scan can run. Declaration order is the display order (DF-R2.2).
/// </summary>
public enum CheckId
{
    /// <summary>
    /// Duplicate movies (DF-R3.1).
    /// </summary>
    DuplicateMovies,

    /// <summary>
    /// Duplicate series (DF-R3.2).
    /// </summary>
    DuplicateSeries,

    /// <summary>
    /// Duplicate episodes (DF-R3.3).
    /// </summary>
    DuplicateEpisodes,

    /// <summary>
    /// Duplicate music albums (DF-R3.4).
    /// </summary>
    DuplicateAlbums,

    /// <summary>
    /// Items Jellyfin has merged into one item with several versions (DF-R3.5).
    /// </summary>
    MergedVersions,

    /// <summary>
    /// Movies and series with no provider id (DF-R4.1).
    /// </summary>
    UnmatchedMoviesSeries,

    /// <summary>
    /// Episodes with no provider id (DF-R4.2).
    /// </summary>
    UnmatchedEpisodes,

    /// <summary>
    /// Music albums and album artists with no provider id (DF-R4.3).
    /// </summary>
    UnmatchedMusic,

    /// <summary>
    /// Matched items missing an overview or primary image (DF-R4.4).
    /// </summary>
    IncompleteMetadata
}
```

Create `src/Jellyfin.Plugin.DupeFinder/Model/ItemKind.cs`:

```csharp
namespace Jellyfin.Plugin.DupeFinder.Model;

/// <summary>
/// The item types the plugin scans (serialised as <c>ItemType</c>, DF-R2.3).
/// </summary>
public enum ItemKind
{
    /// <summary>
    /// A movie.
    /// </summary>
    Movie,

    /// <summary>
    /// A TV series.
    /// </summary>
    Series,

    /// <summary>
    /// A TV episode.
    /// </summary>
    Episode,

    /// <summary>
    /// A music album.
    /// </summary>
    MusicAlbum,

    /// <summary>
    /// A music album artist.
    /// </summary>
    MusicArtist
}
```

- [ ] **Step 4: Create the test project**

```bash
export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet new xunit -n Jellyfin.Plugin.DupeFinder.Tests -o tests/Jellyfin.Plugin.DupeFinder.Tests -f net10.0
rm -f tests/Jellyfin.Plugin.DupeFinder.Tests/UnitTest1.cs
dotnet add tests/Jellyfin.Plugin.DupeFinder.Tests reference src/Jellyfin.Plugin.DupeFinder/Jellyfin.Plugin.DupeFinder.csproj
dotnet sln add src/Jellyfin.Plugin.DupeFinder/Jellyfin.Plugin.DupeFinder.csproj tests/Jellyfin.Plugin.DupeFinder.Tests/Jellyfin.Plugin.DupeFinder.Tests.csproj
```

Edit `tests/Jellyfin.Plugin.DupeFinder.Tests/Jellyfin.Plugin.DupeFinder.Tests.csproj`: keep every generated `PackageReference` exactly as generated; in the first `PropertyGroup` make sure these four elements exist with these values (add or change them), and delete any `<Using Include="Xunit" />` item, so every test file declares its own `using` directives:

```xml
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
```

- [ ] **Step 5: Write the failing test**

Create `tests/Jellyfin.Plugin.DupeFinder.Tests/TextNormalizerTests.cs`:

```csharp
using Jellyfin.Plugin.DupeFinder.Scanning;
using Xunit;

namespace Jellyfin.Plugin.DupeFinder.Tests;

public class TextNormalizerTests
{
    [Theory]
    [InlineData("Amélie", "amelie")]
    [InlineData("Spider-Man: Far From Home", "spidermanfarfromhome")]
    [InlineData("Fast & Furious", "fastandfurious")]
    [InlineData("Fast and Furious", "fastandfurious")]
    [InlineData("  The   Matrix ", "thematrix")]
    [InlineData("ＡＢＣ", "abc")]
    [InlineData("千と千尋の神隠し", "千と千尋の神隠し")]
    [InlineData("!!!", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalize_FollowsSpecDefinition(string? input, string expected)
    {
        Assert.Equal(expected, TextNormalizer.Normalize(input));
    }
}
```

- [ ] **Step 6: Run the test to verify it fails**

Run: `export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1; dotnet test`
Expected: build FAILS with `CS0103: The name 'TextNormalizer' does not exist` (or CS0246).

- [ ] **Step 7: Implement**

Create `src/Jellyfin.Plugin.DupeFinder/Scanning/TextNormalizer.cs`:

```csharp
using System;
using System.Text;

namespace Jellyfin.Plugin.DupeFinder.Scanning;

/// <summary>
/// Spec §4 "normalised text": NFKD, marks removed, lower case, <c>&amp;</c> → "and", letters and digits only.
/// </summary>
internal static class TextNormalizer
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var decomposed = text.Replace("&", " and ", StringComparison.Ordinal).Normalize(NormalizationForm.FormKD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            // Combining marks left by NFKD are not letters or digits, so accents drop out here.
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }
}
```

- [ ] **Step 8: Run tests and a warnings-as-errors build**

Run: `export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1; dotnet test && dotnet build -c Release`
Expected: `Passed!  - Failed:     0, Passed:    10` (exact spacing varies) and `0 Warning(s)` / `0 Error(s)`.

- [ ] **Step 9: Commit**

```bash
git add .editorconfig .gitignore CLAUDE.md Directory.Build.props Jellyfin.Plugin.DupeFinder.sln* src tests
git commit -m "DF-C1 §4: scaffold plugin + tests, add text normaliser" -m "<attribution lines>"
```

---

### Task 2: Scanned items, findings and duplicate grouping

**Files:**
- Create: `src/Jellyfin.Plugin.DupeFinder/Model/Finding.cs`
- Create: `src/Jellyfin.Plugin.DupeFinder/Scanning/ScannedItem.cs`
- Create: `src/Jellyfin.Plugin.DupeFinder/Scanning/DisjointSet.cs`
- Create: `src/Jellyfin.Plugin.DupeFinder/Scanning/GroupBuilder.cs`
- Create: `src/Jellyfin.Plugin.DupeFinder/Scanning/DuplicateDetector.cs`
- Test: `tests/Jellyfin.Plugin.DupeFinder.Tests/TestItems.cs`, `tests/Jellyfin.Plugin.DupeFinder.Tests/DuplicateDetectorTests.cs`

**Interfaces:**
- Consumes: `CheckId`, `ItemKind`, `TextNormalizer.Normalize` (Task 1).
- Produces:
  - `internal sealed record ScannedItem` with `required Guid Id`, `required ItemKind Kind`, `required string LibraryName`, `required string Name`, `int? Year`, `string? Path`, `long? SizeBytes`, `IReadOnlyDictionary<string,string> ProviderIds`, `Guid? PrimaryVersionId`, `Guid? SeriesId`, `string? SeriesName`, `int? Season`, `int? Episode`, `IReadOnlyList<string> AlbumArtists`, `bool HasOverview`, `bool HasPrimaryImage` (all `init`); `bool HasAnyProviderId { get; }`; `string? GetProviderId(string key)`.
  - `public sealed record Finding` with settable `CheckId Check`, `int? Group`, `Guid ItemId`, `string LibraryName`, `ItemKind ItemType`, `string Name`, `int? Year`, `int? Season`, `int? Episode`, `string? SeriesName`, `string? Path`, `long? SizeBytes`, `IReadOnlyDictionary<string,string> ProviderIds`, `string Reason`; `internal static Finding From(ScannedItem item, CheckId check, string reason)`.
  - `internal sealed class GroupBuilder(IReadOnlyList<ScannedItem>)` with `UnionBuckets(Func<ScannedItem,string?> keySelector, Func<ScannedItem,string> reasonSelector, Func<IReadOnlyList<ScannedItem>,bool>? bucketQualifies = null)` and `IReadOnlyList<IReadOnlyList<Finding>> BuildGroups(CheckId check)` (members sorted by name then path, `Group` left null).
  - `internal static class DuplicateDetector` with `FindDuplicateMovies`, `FindDuplicateSeries`, `FindDuplicateEpisodes`, `FindDuplicateAlbums`, each `(IReadOnlyList<ScannedItem>) → IReadOnlyList<IReadOnlyList<Finding>>`.
  - Test helper `internal static class TestItems` with `Movie`, `Series`, `Episode`, `Album`, `Artist` factories (signatures in Step 1).

- [ ] **Step 1: Write the test helper**

Create `tests/Jellyfin.Plugin.DupeFinder.Tests/TestItems.cs`:

```csharp
using System;
using System.Collections.Generic;
using Jellyfin.Plugin.DupeFinder.Model;
using Jellyfin.Plugin.DupeFinder.Scanning;

namespace Jellyfin.Plugin.DupeFinder.Tests;

internal static class TestItems
{
    public static ScannedItem Movie(string name, int? year = null, string? tmdb = null, string? imdb = null, Guid? primaryVersionId = null)
        => Create(ItemKind.Movie, name, year, Ids(("Tmdb", tmdb), ("Imdb", imdb))) with { PrimaryVersionId = primaryVersionId };

    public static ScannedItem Series(string name, int? year = null, string? tvdb = null, string? tmdb = null)
        => Create(ItemKind.Series, name, year, Ids(("Tvdb", tvdb), ("Tmdb", tmdb)));

    public static ScannedItem Episode(Guid seriesId, int? season, int? episode, string? tvdb = null, Guid? primaryVersionId = null)
        => Create(ItemKind.Episode, "Pilot", null, Ids(("Tvdb", tvdb))) with
        {
            SeriesId = seriesId,
            SeriesName = "Show",
            Season = season,
            Episode = episode,
            PrimaryVersionId = primaryVersionId,
        };

    public static ScannedItem Album(string name, string[] albumArtists, string? musicBrainzAlbum = null)
        => Create(ItemKind.MusicAlbum, name, null, Ids(("MusicBrainzAlbum", musicBrainzAlbum))) with { AlbumArtists = albumArtists };

    public static ScannedItem Artist(string name, string? musicBrainzArtist = null)
        => Create(ItemKind.MusicArtist, name, null, Ids(("MusicBrainzArtist", musicBrainzArtist)));

    public static ScannedItem WithIds(ScannedItem item, params (string Key, string? Value)[] pairs)
        => item with { ProviderIds = Ids(pairs) };

    private static ScannedItem Create(ItemKind kind, string name, int? year, Dictionary<string, string> ids)
        => new()
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            LibraryName = "Library",
            Name = name,
            Year = year,
            Path = "/media/" + name,
            SizeBytes = 1000,
            ProviderIds = ids,
            HasOverview = true,
            HasPrimaryImage = true,
        };

    private static Dictionary<string, string> Ids(params (string Key, string? Value)[] pairs)
    {
        var ids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in pairs)
        {
            if (value is not null)
            {
                ids[key] = value;
            }
        }

        return ids;
    }
}
```

- [ ] **Step 2: Write the failing tests (AC-2, AC-4, AC-5, Review Focus 1)**

Create `tests/Jellyfin.Plugin.DupeFinder.Tests/DuplicateDetectorTests.cs`:

```csharp
using System;
using System.Linq;
using Jellyfin.Plugin.DupeFinder.Model;
using Jellyfin.Plugin.DupeFinder.Scanning;
using Xunit;

namespace Jellyfin.Plugin.DupeFinder.Tests;

public class DuplicateDetectorTests
{
    [Fact]
    public void Movies_SameTmdb_AreGrouped()
    {
        var a = TestItems.Movie("The Matrix", 1999, tmdb: "603");
        var b = TestItems.Movie("Matrix, The", 1999, tmdb: "603");

        var group = Assert.Single(DuplicateDetector.FindDuplicateMovies([a, b]));

        Assert.Equal(2, group.Count);
        Assert.All(group, f => Assert.Equal(CheckId.DuplicateMovies, f.Check));
        Assert.All(group, f => Assert.Equal("Same TMDb id 603", f.Reason));
        Assert.All(group, f => Assert.Null(f.Group));
    }

    [Fact]
    public void Movies_SameImdbDifferentCase_AreGrouped()
    {
        var a = TestItems.Movie("The Matrix", 1999, imdb: "tt0133093");
        var b = TestItems.Movie("The Matrix", 1999, imdb: "TT0133093");

        var group = Assert.Single(DuplicateDetector.FindDuplicateMovies([a, b]));

        Assert.Equal(2, group.Count);
        Assert.Contains(group, f => f.Reason == "Same IMDb id tt0133093");
        Assert.Contains(group, f => f.Reason == "Same IMDb id TT0133093");
    }

    [Fact]
    public void Movies_BothMatchedDifferentTmdb_SameTitleAndYear_AreNotGrouped()
    {
        var a = TestItems.Movie("Heat", 1995, tmdb: "949");
        var b = TestItems.Movie("Heat", 1995, tmdb: "12345");

        Assert.Empty(DuplicateDetector.FindDuplicateMovies([a, b]));
    }

    [Fact]
    public void Movies_MatchedAndUnmatched_SameTitleAndYear_AreGrouped()
    {
        var matched = TestItems.Movie("Heat", 1995, tmdb: "949");
        var unmatched = TestItems.Movie("Heat", 1995);

        var group = Assert.Single(DuplicateDetector.FindDuplicateMovies([matched, unmatched]));

        Assert.Equal(2, group.Count);
        Assert.All(group, f => Assert.Equal("Same title + year (unmatched)", f.Reason));
    }

    [Fact]
    public void Movies_UnmatchedAccentVariants_AreGrouped()
    {
        var a = TestItems.Movie("Amélie", 2001);
        var b = TestItems.Movie("Amelie", 2001);

        Assert.Equal(2, Assert.Single(DuplicateDetector.FindDuplicateMovies([a, b])).Count);
    }

    [Fact]
    public void Movies_UnmatchedDifferentYears_AreNotGrouped()
    {
        var a = TestItems.Movie("Solaris", 1972);
        var b = TestItems.Movie("Solaris", 2002);

        Assert.Empty(DuplicateDetector.FindDuplicateMovies([a, b]));
    }

    [Fact]
    public void Movies_ChainedByTmdbThenImdb_FormOneGroupOfThree()
    {
        var a = TestItems.Movie("Alien", 1979, tmdb: "348");
        var b = TestItems.Movie("Alien", 1979, tmdb: "348", imdb: "tt0078748");
        var c = TestItems.Movie("Alien (Director's Cut)", 2003, imdb: "tt0078748");

        var group = Assert.Single(DuplicateDetector.FindDuplicateMovies([a, b, c]));

        Assert.Equal(3, group.Count);
        Assert.Equal("Same TMDb id 348; Same IMDb id tt0078748", group.Single(f => f.ItemId == b.Id).Reason);
    }

    [Fact]
    public void Movies_UnmatchedWithBlankOrPunctuationNames_AreNeverGrouped()
    {
        var a = TestItems.Movie(string.Empty);
        var b = TestItems.Movie("!!!");
        var c = TestItems.Movie("???");

        Assert.Empty(DuplicateDetector.FindDuplicateMovies([a, b, c]));
    }

    [Fact]
    public void Movies_GroupMembers_AreOrderedByNameThenPath()
    {
        var b = TestItems.Movie("B", 2000, tmdb: "1");
        var a = TestItems.Movie("A", 2000, tmdb: "1");

        var group = Assert.Single(DuplicateDetector.FindDuplicateMovies([b, a]));

        Assert.Equal(new[] { "A", "B" }, group.Select(f => f.Name));
    }

    [Fact]
    public void Series_SameTvdb_AreGrouped()
    {
        var a = TestItems.Series("Lost", 2004, tvdb: "73739");
        var b = TestItems.Series("Lost (2004)", 2004, tvdb: "73739");

        var group = Assert.Single(DuplicateDetector.FindDuplicateSeries([a, b]));

        Assert.All(group, f => Assert.Equal("Same TVDb id 73739", f.Reason));
        Assert.All(group, f => Assert.Equal(CheckId.DuplicateSeries, f.Check));
    }

    [Fact]
    public void Episodes_SameSeriesSeasonEpisode_AreGrouped()
    {
        var series = Guid.NewGuid();
        var a = TestItems.Episode(series, 1, 3);
        var b = TestItems.Episode(series, 1, 3);

        var group = Assert.Single(DuplicateDetector.FindDuplicateEpisodes([a, b]));

        Assert.All(group, f => Assert.Equal("Same episode S01E03", f.Reason));
    }

    [Fact]
    public void Episodes_SameNumbersDifferentSeries_AreNotGrouped()
    {
        var a = TestItems.Episode(Guid.NewGuid(), 1, 3);
        var b = TestItems.Episode(Guid.NewGuid(), 1, 3);

        Assert.Empty(DuplicateDetector.FindDuplicateEpisodes([a, b]));
    }

    [Fact]
    public void Episodes_NullEpisodeNumber_AreNeverGrouped()
    {
        var series = Guid.NewGuid();
        var a = TestItems.Episode(series, 1, null);
        var b = TestItems.Episode(series, 1, null);

        Assert.Empty(DuplicateDetector.FindDuplicateEpisodes([a, b]));
    }

    [Fact]
    public void Albums_SameMusicBrainzRelease_AreGrouped()
    {
        var a = TestItems.Album("Abbey Road", ["The Beatles"], musicBrainzAlbum: "mb-1");
        var b = TestItems.Album("Abbey Road (Remastered)", ["The Beatles"], musicBrainzAlbum: "mb-1");

        var group = Assert.Single(DuplicateDetector.FindDuplicateAlbums([a, b]));

        Assert.All(group, f => Assert.Equal("Same MusicBrainz release mb-1", f.Reason));
    }

    [Fact]
    public void Albums_SameArtistAndNameDifferentRelease_AreGrouped()
    {
        var a = TestItems.Album("Abbey Road", ["The Beatles"], musicBrainzAlbum: "mb-1");
        var b = TestItems.Album("Abbey Road", ["The Beatles"], musicBrainzAlbum: "mb-2");

        var group = Assert.Single(DuplicateDetector.FindDuplicateAlbums([a, b]));

        Assert.All(group, f => Assert.Equal("Same album artist + album", f.Reason));
    }

    [Fact]
    public void Albums_SameNameDifferentArtist_AreNotGrouped()
    {
        var a = TestItems.Album("Greatest Hits", ["Queen"]);
        var b = TestItems.Album("Greatest Hits", ["ABBA"]);

        Assert.Empty(DuplicateDetector.FindDuplicateAlbums([a, b]));
    }

    [Fact]
    public void Albums_SameNameWithoutAlbumArtist_AreNotGrouped()
    {
        var a = TestItems.Album("Greatest Hits", []);
        var b = TestItems.Album("Greatest Hits", []);

        Assert.Empty(DuplicateDetector.FindDuplicateAlbums([a, b]));
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1; dotnet test`
Expected: build FAILS (`ScannedItem`, `DuplicateDetector`, `Finding` not found).

- [ ] **Step 4: Implement `ScannedItem`**

Create `src/Jellyfin.Plugin.DupeFinder/Scanning/ScannedItem.cs`:

```csharp
using System;
using System.Collections.Generic;
using Jellyfin.Plugin.DupeFinder.Model;

namespace Jellyfin.Plugin.DupeFinder.Scanning;

/// <summary>
/// Jellyfin-free snapshot of one library item, so detection rules can be unit-tested (ADR-0002).
/// </summary>
internal sealed record ScannedItem
{
    public required Guid Id { get; init; }

    public required ItemKind Kind { get; init; }

    public required string LibraryName { get; init; }

    public required string Name { get; init; }

    public int? Year { get; init; }

    public string? Path { get; init; }

    public long? SizeBytes { get; init; }

    public IReadOnlyDictionary<string, string> ProviderIds { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public Guid? PrimaryVersionId { get; init; }

    public Guid? SeriesId { get; init; }

    public string? SeriesName { get; init; }

    public int? Season { get; init; }

    public int? Episode { get; init; }

    public IReadOnlyList<string> AlbumArtists { get; init; } = [];

    public bool HasOverview { get; init; }

    public bool HasPrimaryImage { get; init; }

    // Spec §4 "has provider id": blank values do not count.
    public bool HasAnyProviderId
    {
        get
        {
            foreach (var value in ProviderIds.Values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public string? GetProviderId(string key)
        => ProviderIds.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
}
```

- [ ] **Step 5: Implement `Finding`**

Create `src/Jellyfin.Plugin.DupeFinder/Model/Finding.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.DupeFinder.Scanning;

namespace Jellyfin.Plugin.DupeFinder.Model;

/// <summary>
/// One row of scan results (DF-R2.3).
/// </summary>
public sealed record Finding
{
    /// <summary>
    /// Gets or sets the check that produced this finding.
    /// </summary>
    public CheckId Check { get; set; }

    /// <summary>
    /// Gets or sets the duplicate group number, or null for single-item checks.
    /// </summary>
    public int? Group { get; set; }

    /// <summary>
    /// Gets or sets the Jellyfin item id.
    /// </summary>
    public Guid ItemId { get; set; }

    /// <summary>
    /// Gets or sets the name of the first scanned library the item was found in.
    /// </summary>
    public string LibraryName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the item type.
    /// </summary>
    public ItemKind ItemType { get; set; }

    /// <summary>
    /// Gets or sets the item name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the production year.
    /// </summary>
    public int? Year { get; set; }

    /// <summary>
    /// Gets or sets the season number (episodes only).
    /// </summary>
    public int? Season { get; set; }

    /// <summary>
    /// Gets or sets the episode number (episodes only).
    /// </summary>
    public int? Episode { get; set; }

    /// <summary>
    /// Gets or sets the series name (episodes only).
    /// </summary>
    public string? SeriesName { get; set; }

    /// <summary>
    /// Gets or sets the file or folder path.
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// Gets or sets the size in bytes.
    /// </summary>
    public long? SizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the non-blank provider ids.
    /// </summary>
    public IReadOnlyDictionary<string, string> ProviderIds { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets why the item was reported (DF-R3.6, DF-R4).
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    internal static Finding From(ScannedItem item, CheckId check, string reason)
        => new()
        {
            Check = check,
            ItemId = item.Id,
            LibraryName = item.LibraryName,
            ItemType = item.Kind,
            Name = item.Name,
            Year = item.Year,
            Season = item.Season,
            Episode = item.Episode,
            SeriesName = item.SeriesName,
            Path = item.Path,
            SizeBytes = item.SizeBytes,
            ProviderIds = item.ProviderIds
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
                .ToDictionary(pair => pair.Key, pair => pair.Value.Trim(), StringComparer.OrdinalIgnoreCase),
            Reason = reason,
        };
}
```

- [ ] **Step 6: Implement `DisjointSet` and `GroupBuilder`**

Create `src/Jellyfin.Plugin.DupeFinder/Scanning/DisjointSet.cs`:

```csharp
namespace Jellyfin.Plugin.DupeFinder.Scanning;

/// <summary>
/// Union-find over item indices; makes duplicate groups transitive (DF-R3).
/// </summary>
internal sealed class DisjointSet
{
    private readonly int[] _parent;

    public DisjointSet(int count)
    {
        _parent = new int[count];
        for (var i = 0; i < count; i++)
        {
            _parent[i] = i;
        }
    }

    public int Find(int index)
    {
        while (_parent[index] != index)
        {
            _parent[index] = _parent[_parent[index]];
            index = _parent[index];
        }

        return index;
    }

    public void Union(int a, int b)
    {
        var rootA = Find(a);
        var rootB = Find(b);
        if (rootA != rootB)
        {
            _parent[rootB] = rootA;
        }
    }
}
```

Create `src/Jellyfin.Plugin.DupeFinder/Scanning/GroupBuilder.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.DupeFinder.Model;

namespace Jellyfin.Plugin.DupeFinder.Scanning;

/// <summary>
/// Items sharing a qualifying bucket key are unioned into one transitive group; each item keeps the
/// distinct reasons that put it there, in rule order (DF-R3, DF-R3.6).
/// </summary>
internal sealed class GroupBuilder
{
    private readonly IReadOnlyList<ScannedItem> _items;
    private readonly DisjointSet _set;
    private readonly List<string>[] _reasons;

    public GroupBuilder(IReadOnlyList<ScannedItem> items)
    {
        _items = items;
        _set = new DisjointSet(items.Count);
        _reasons = new List<string>[items.Count];
        for (var i = 0; i < items.Count; i++)
        {
            _reasons[i] = [];
        }
    }

    public void UnionBuckets(
        Func<ScannedItem, string?> keySelector,
        Func<ScannedItem, string> reasonSelector,
        Func<IReadOnlyList<ScannedItem>, bool>? bucketQualifies = null)
    {
        var buckets = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (var i = 0; i < _items.Count; i++)
        {
            var key = keySelector(_items[i]);
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            if (!buckets.TryGetValue(key, out var members))
            {
                members = [];
                buckets.Add(key, members);
            }

            members.Add(i);
        }

        foreach (var members in buckets.Values)
        {
            if (members.Count < 2 || (bucketQualifies is not null && !bucketQualifies(members.ConvertAll(i => _items[i]))))
            {
                continue;
            }

            foreach (var index in members)
            {
                _set.Union(members[0], index);
                var reason = reasonSelector(_items[index]);
                if (!_reasons[index].Contains(reason))
                {
                    _reasons[index].Add(reason);
                }
            }
        }
    }

    public IReadOnlyList<IReadOnlyList<Finding>> BuildGroups(CheckId check)
    {
        var groups = new Dictionary<int, List<int>>();
        for (var i = 0; i < _items.Count; i++)
        {
            // An item with no reason never joined a bucket of two or more, so it is not a duplicate.
            if (_reasons[i].Count == 0)
            {
                continue;
            }

            var root = _set.Find(i);
            if (!groups.TryGetValue(root, out var members))
            {
                members = [];
                groups.Add(root, members);
            }

            members.Add(i);
        }

        return groups.Values
            .Select(members => (IReadOnlyList<Finding>)members
                .Select(i => Finding.From(_items[i], check, string.Join("; ", _reasons[i])))
                .OrderBy(finding => finding.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(finding => finding.Path, StringComparer.OrdinalIgnoreCase)
                .ToList())
            .ToList();
    }
}
```

- [ ] **Step 7: Implement `DuplicateDetector`**

Create `src/Jellyfin.Plugin.DupeFinder/Scanning/DuplicateDetector.cs`:

```csharp
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jellyfin.Plugin.DupeFinder.Model;

namespace Jellyfin.Plugin.DupeFinder.Scanning;

/// <summary>
/// Duplicate rules DF-R3.1–R3.4 with reasons per DF-R3.6. Callers pass the collapsed set (spec §4)
/// for movies and episodes.
/// </summary>
internal static class DuplicateDetector
{
    private const string MusicBrainzAlbumKey = "MusicBrainzAlbum";

    private static readonly (string Key, string Label)[] _movieKeys = [("Tmdb", "TMDb"), ("Imdb", "IMDb")];

    private static readonly (string Key, string Label)[] _seriesKeys = [("Tvdb", "TVDb"), ("Tmdb", "TMDb"), ("Imdb", "IMDb")];

    public static IReadOnlyList<IReadOnlyList<Finding>> FindDuplicateMovies(IReadOnlyList<ScannedItem> movies)
        => FindByProviderOrTitle(movies, _movieKeys, CheckId.DuplicateMovies);

    public static IReadOnlyList<IReadOnlyList<Finding>> FindDuplicateSeries(IReadOnlyList<ScannedItem> series)
        => FindByProviderOrTitle(series, _seriesKeys, CheckId.DuplicateSeries);

    public static IReadOnlyList<IReadOnlyList<Finding>> FindDuplicateEpisodes(IReadOnlyList<ScannedItem> episodes)
    {
        var builder = new GroupBuilder(episodes);
        builder.UnionBuckets(
            EpisodeKey,
            item => string.Create(CultureInfo.InvariantCulture, $"Same episode S{item.Season:00}E{item.Episode:00}"));
        return builder.BuildGroups(CheckId.DuplicateEpisodes);
    }

    public static IReadOnlyList<IReadOnlyList<Finding>> FindDuplicateAlbums(IReadOnlyList<ScannedItem> albums)
    {
        var builder = new GroupBuilder(albums);
        builder.UnionBuckets(
            item => item.GetProviderId(MusicBrainzAlbumKey)?.ToUpperInvariant(),
            item => "Same MusicBrainz release " + item.GetProviderId(MusicBrainzAlbumKey));
        builder.UnionBuckets(AlbumArtistAndNameKey, _ => "Same album artist + album");
        return builder.BuildGroups(CheckId.DuplicateAlbums);
    }

    private static IReadOnlyList<IReadOnlyList<Finding>> FindByProviderOrTitle(
        IReadOnlyList<ScannedItem> items,
        (string Key, string Label)[] providerKeys,
        CheckId check)
    {
        var builder = new GroupBuilder(items);
        foreach (var (key, label) in providerKeys)
        {
            builder.UnionBuckets(
                item => item.GetProviderId(key)?.ToUpperInvariant(),
                item => "Same " + label + " id " + item.GetProviderId(key));
        }

        // DF-R3.1(c): title + year only links a bucket containing an unmatched item, so two matched
        // films that share a title and year (remakes) are never grouped on name alone.
        builder.UnionBuckets(TitleYearKey, _ => "Same title + year (unmatched)", bucket => bucket.Any(item => !item.HasAnyProviderId));
        return builder.BuildGroups(check);
    }

    private static string? TitleYearKey(ScannedItem item)
    {
        var name = TextNormalizer.Normalize(item.Name);
        return name.Length == 0 ? null : string.Create(CultureInfo.InvariantCulture, $"{name}|{item.Year}");
    }

    private static string? EpisodeKey(ScannedItem item)
        => item.SeriesId is { } seriesId && item.Season is { } season && item.Episode is { } episode
            ? string.Create(CultureInfo.InvariantCulture, $"{seriesId:N}|{season}|{episode}")
            : null;

    private static string? AlbumArtistAndNameKey(ScannedItem item)
    {
        var artists = TextNormalizer.Normalize(string.Join(' ', item.AlbumArtists));
        var name = TextNormalizer.Normalize(item.Name);
        return artists.Length == 0 || name.Length == 0 ? null : artists + "|" + name;
    }
}
```

- [ ] **Step 8: Run tests and build**

Run: `export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1; dotnet test && dotnet build -c Release`
Expected: all tests pass (27 = 10 + 17); `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 9: Commit**

```bash
git add src tests
git commit -m "DF-R3.1-R3.4 DF-R3.6: duplicate grouping with reasons (AC-2, AC-4, AC-5)" -m "<attribution lines>"
```

---

### Task 3: Version groups and unmatched / incomplete detection

**Files:**
- Create: `src/Jellyfin.Plugin.DupeFinder/Scanning/VersionGroups.cs`
- Create: `src/Jellyfin.Plugin.DupeFinder/Scanning/UnmatchedDetector.cs`
- Test: `tests/Jellyfin.Plugin.DupeFinder.Tests/VersionGroupsTests.cs`, `tests/Jellyfin.Plugin.DupeFinder.Tests/UnmatchedDetectorTests.cs`

**Interfaces:**
- Consumes: `ScannedItem`, `Finding.From`, `CheckId`, `ItemKind`, `TestItems` (Task 2).
- Produces:
  - `internal static IReadOnlyList<ScannedItem> VersionGroups.Collapse(IReadOnlyList<ScannedItem> items)` — drops items whose `PrimaryVersionId` names an item in the same list.
  - `internal static IReadOnlyList<IReadOnlyList<Finding>> VersionGroups.FindMergedVersions(IReadOnlyList<ScannedItem> items)` — primary first, then alternates by name/path; reason `Merged versions`.
  - `internal static IReadOnlyList<Finding> UnmatchedDetector.FindUnmatched(IReadOnlyList<ScannedItem> items, CheckId check)` — reason `No provider ids`.
  - `internal static IReadOnlyList<Finding> UnmatchedDetector.FindIncomplete(IReadOnlyList<ScannedItem> items)` — check `IncompleteMetadata`.

- [ ] **Step 1: Write the failing tests (AC-3 part, AC-6)**

Create `tests/Jellyfin.Plugin.DupeFinder.Tests/VersionGroupsTests.cs`:

```csharp
using System;
using System.Linq;
using Jellyfin.Plugin.DupeFinder.Model;
using Jellyfin.Plugin.DupeFinder.Scanning;
using Xunit;

namespace Jellyfin.Plugin.DupeFinder.Tests;

public class VersionGroupsTests
{
    [Fact]
    public void Collapse_DropsAlternateWhosePrimaryIsPresent()
    {
        var primary = TestItems.Movie("Heat", 1995, tmdb: "949");
        var alternate = TestItems.Movie("Heat", 1995, tmdb: "949", primaryVersionId: primary.Id);

        Assert.Same(primary, Assert.Single(VersionGroups.Collapse([primary, alternate])));
    }

    [Fact]
    public void Collapse_KeepsAlternateWhosePrimaryIsAbsent()
    {
        var orphan = TestItems.Movie("Heat", 1995, primaryVersionId: Guid.NewGuid());

        Assert.Same(orphan, Assert.Single(VersionGroups.Collapse([orphan])));
    }

    [Fact]
    public void MergedVersions_ListPrimaryFirst()
    {
        var alternateA = TestItems.Movie("A Heat", 1995);
        var primary = TestItems.Movie("Heat", 1995);
        alternateA = alternateA with { PrimaryVersionId = primary.Id };
        var alternateB = TestItems.Movie("B Heat", 1995, primaryVersionId: primary.Id);

        var group = Assert.Single(VersionGroups.FindMergedVersions([alternateB, alternateA, primary]));

        Assert.Equal(new[] { primary.Id, alternateA.Id, alternateB.Id }, group.Select(f => f.ItemId));
        Assert.All(group, f => Assert.Equal("Merged versions", f.Reason));
        Assert.All(group, f => Assert.Equal(CheckId.MergedVersions, f.Check));
    }

    [Fact]
    public void MergedVersions_IgnoreItemsWithoutAlternates()
    {
        Assert.Empty(VersionGroups.FindMergedVersions([TestItems.Movie("Heat", 1995), TestItems.Movie("Heat", 1995)]));
    }
}
```

Create `tests/Jellyfin.Plugin.DupeFinder.Tests/UnmatchedDetectorTests.cs`:

```csharp
using Jellyfin.Plugin.DupeFinder.Model;
using Jellyfin.Plugin.DupeFinder.Scanning;
using Xunit;

namespace Jellyfin.Plugin.DupeFinder.Tests;

public class UnmatchedDetectorTests
{
    [Fact]
    public void Unmatched_OnlyBlankProviderValues_IsReported()
    {
        var item = TestItems.WithIds(TestItems.Movie("Heat", 1995), ("Tmdb", " "), ("Imdb", string.Empty));

        var finding = Assert.Single(UnmatchedDetector.FindUnmatched([item], CheckId.UnmatchedMoviesSeries));

        Assert.Equal("No provider ids", finding.Reason);
        Assert.Equal(CheckId.UnmatchedMoviesSeries, finding.Check);
        Assert.Null(finding.Group);
        Assert.Empty(finding.ProviderIds);
    }

    [Fact]
    public void Unmatched_WithAnyProviderId_IsNotReported()
    {
        Assert.Empty(UnmatchedDetector.FindUnmatched([TestItems.Movie("Heat", 1995, imdb: "tt0113277")], CheckId.UnmatchedMoviesSeries));
    }

    [Fact]
    public void Incomplete_MatchedMovieWithoutOverview_ReportsNoOverview()
    {
        var item = TestItems.Movie("Heat", 1995, tmdb: "949") with { HasOverview = false };

        Assert.Equal("No overview", Assert.Single(UnmatchedDetector.FindIncomplete([item])).Reason);
    }

    [Fact]
    public void Incomplete_MatchedSeriesMissingBoth_ListsBothInOrder()
    {
        var item = TestItems.Series("Lost", 2004, tvdb: "73739") with { HasOverview = false, HasPrimaryImage = false };

        var finding = Assert.Single(UnmatchedDetector.FindIncomplete([item]));

        Assert.Equal("No overview; No primary image", finding.Reason);
        Assert.Equal(CheckId.IncompleteMetadata, finding.Check);
    }

    [Fact]
    public void Incomplete_UnmatchedMovie_IsNotReported()
    {
        var item = TestItems.Movie("Heat", 1995) with { HasOverview = false, HasPrimaryImage = false };

        Assert.Empty(UnmatchedDetector.FindIncomplete([item]));
    }

    [Fact]
    public void Incomplete_AlbumWithoutOverview_IsNotReported()
    {
        var item = TestItems.Album("Abbey Road", ["The Beatles"], musicBrainzAlbum: "mb-1") with { HasOverview = false };

        Assert.Empty(UnmatchedDetector.FindIncomplete([item]));
    }

    [Fact]
    public void Incomplete_AlbumWithoutImage_ReportsNoPrimaryImage()
    {
        var item = TestItems.Album("Abbey Road", ["The Beatles"], musicBrainzAlbum: "mb-1") with { HasPrimaryImage = false };

        Assert.Equal("No primary image", Assert.Single(UnmatchedDetector.FindIncomplete([item])).Reason);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1; dotnet test`
Expected: build FAILS (`VersionGroups`, `UnmatchedDetector` not found).

- [ ] **Step 3: Implement `VersionGroups`**

Create `src/Jellyfin.Plugin.DupeFinder/Scanning/VersionGroups.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.DupeFinder.Model;

namespace Jellyfin.Plugin.DupeFinder.Scanning;

/// <summary>
/// Spec §4 version groups: the collapsed set used by most checks, and the MergedVersions check (DF-R3.5).
/// </summary>
internal static class VersionGroups
{
    public static IReadOnlyList<ScannedItem> Collapse(IReadOnlyList<ScannedItem> items)
    {
        var ids = items.Select(item => item.Id).ToHashSet();

        // An alternate whose primary was not scanned stands in for its version group.
        return items.Where(item => item.PrimaryVersionId is not { } primaryId || !ids.Contains(primaryId)).ToList();
    }

    public static IReadOnlyList<IReadOnlyList<Finding>> FindMergedVersions(IReadOnlyList<ScannedItem> items)
    {
        var byId = items.ToDictionary(item => item.Id);
        var alternatesByPrimary = new Dictionary<Guid, List<ScannedItem>>();
        foreach (var item in items)
        {
            if (item.PrimaryVersionId is not { } primaryId || !byId.ContainsKey(primaryId))
            {
                continue;
            }

            if (!alternatesByPrimary.TryGetValue(primaryId, out var alternates))
            {
                alternates = [];
                alternatesByPrimary.Add(primaryId, alternates);
            }

            alternates.Add(item);
        }

        var groups = new List<IReadOnlyList<Finding>>();
        foreach (var (primaryId, alternates) in alternatesByPrimary)
        {
            var group = new List<Finding> { Finding.From(byId[primaryId], CheckId.MergedVersions, "Merged versions") };
            group.AddRange(alternates
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
                .Select(item => Finding.From(item, CheckId.MergedVersions, "Merged versions")));
            groups.Add(group);
        }

        return groups;
    }
}
```

(If analyser CA1854 flags `ContainsKey` followed by the `byId[primaryId]` indexer, restructure with `TryGetValue`; behaviour must not change.)

- [ ] **Step 4: Implement `UnmatchedDetector`**

Create `src/Jellyfin.Plugin.DupeFinder/Scanning/UnmatchedDetector.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.DupeFinder.Model;

namespace Jellyfin.Plugin.DupeFinder.Scanning;

/// <summary>
/// Unmatched and incomplete-metadata rules (DF-R4). Callers choose which item kinds to pass.
/// </summary>
internal static class UnmatchedDetector
{
    public static IReadOnlyList<Finding> FindUnmatched(IReadOnlyList<ScannedItem> items, CheckId check)
        => items
            .Where(item => !item.HasAnyProviderId)
            .Select(item => Finding.From(item, check, "No provider ids"))
            .ToList();

    public static IReadOnlyList<Finding> FindIncomplete(IReadOnlyList<ScannedItem> items)
    {
        var findings = new List<Finding>();
        foreach (var item in items)
        {
            // Unmatched items belong to the unmatched checks, not here (AC-6).
            if (!item.HasAnyProviderId)
            {
                continue;
            }

            var missing = new List<string>(2);

            // Albums commonly have no overview, so DF-R4.4 only checks overviews on movies and series.
            if (item.Kind is ItemKind.Movie or ItemKind.Series && !item.HasOverview)
            {
                missing.Add("No overview");
            }

            if (!item.HasPrimaryImage)
            {
                missing.Add("No primary image");
            }

            if (missing.Count > 0)
            {
                findings.Add(Finding.From(item, CheckId.IncompleteMetadata, string.Join("; ", missing)));
            }
        }

        return findings;
    }
}
```

(If SA1408 asks for parentheses in `item.Kind is ItemKind.Movie or ItemKind.Series && !item.HasOverview`, write `(item.Kind is ItemKind.Movie or ItemKind.Series) && !item.HasOverview`.)

- [ ] **Step 5: Run tests and build**

Run: `export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1; dotnet test && dotnet build -c Release`
Expected: all tests pass (38); `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 6: Commit**

```bash
git add src tests
git commit -m "DF-R3.5 DF-R4: version groups, unmatched and incomplete detection (AC-6)" -m "<attribution lines>"
```

---

### Task 4: Scan orchestration and request validation

**Files:**
- Create: `src/Jellyfin.Plugin.DupeFinder/Model/LibraryDto.cs`, `Model/ScanRequest.cs`, `Model/ScanResponse.cs`
- Create: `src/Jellyfin.Plugin.DupeFinder/Scanning/LibraryInfo.cs`, `Scanning/ILibraryItemSource.cs`, `Scanning/ScanOutcome.cs`, `Scanning/ScanService.cs`
- Test: `tests/Jellyfin.Plugin.DupeFinder.Tests/FakeLibraryItemSource.cs`, `tests/Jellyfin.Plugin.DupeFinder.Tests/ScanServiceTests.cs`

**Interfaces:**
- Consumes: Tasks 1–3 (`DuplicateDetector.*`, `VersionGroups.*`, `UnmatchedDetector.*`, `Finding`, `ScannedItem`).
- Produces:
  - `public sealed class LibraryDto { string Id; string Name; string? CollectionType }` (get/set).
  - `public sealed class ScanRequest { IReadOnlyList<string>? LibraryIds; IReadOnlyList<string>? Checks }` (get/set).
  - `public sealed class ScanResponse { int ScannedItemCount; long DurationMs; IReadOnlyDictionary<string,int> Summary; IReadOnlyList<Finding> Findings }` (get/set).
  - `internal sealed record LibraryInfo { required Guid Id; required string Name; string? CollectionType }`.
  - `internal interface ILibraryItemSource { IReadOnlyList<LibraryInfo> GetLibraries(); IReadOnlyList<ScannedItem> GetItems(LibraryInfo library, IReadOnlyCollection<ItemKind> kinds); }`
  - `internal sealed class ScanOutcome { ScanResponse? Response; string? Error; static Success(ScanResponse); static Invalid(string) }`.
  - `internal sealed class ScanService(ILibraryItemSource source)` with `IReadOnlyList<LibraryDto> GetLibraries()` and `ScanOutcome Scan(ScanRequest request)`.

- [ ] **Step 1: Write the fake source**

Create `tests/Jellyfin.Plugin.DupeFinder.Tests/FakeLibraryItemSource.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Plugin.DupeFinder.Model;
using Jellyfin.Plugin.DupeFinder.Scanning;

namespace Jellyfin.Plugin.DupeFinder.Tests;

internal sealed class FakeLibraryItemSource : ILibraryItemSource
{
    private readonly List<LibraryInfo> _libraries = [];
    private readonly Dictionary<Guid, List<ScannedItem>> _items = new();

    public LibraryInfo Add(string name, params ScannedItem[] items)
    {
        var library = new LibraryInfo { Id = Guid.NewGuid(), Name = name, CollectionType = "mixed" };
        _libraries.Add(library);
        _items[library.Id] = items.Select(item => item with { LibraryName = name }).ToList();
        return library;
    }

    public IReadOnlyList<LibraryInfo> GetLibraries() => _libraries;

    public IReadOnlyList<ScannedItem> GetItems(LibraryInfo library, IReadOnlyCollection<ItemKind> kinds)
        => _items[library.Id].Where(item => kinds.Contains(item.Kind)).ToList();
}
```

- [ ] **Step 2: Write the failing tests (AC-3, AC-10 logic, DF-R2.3 ordering, Review Focus 2)**

Create `tests/Jellyfin.Plugin.DupeFinder.Tests/ScanServiceTests.cs`:

```csharp
using System;
using System.Globalization;
using System.Linq;
using Jellyfin.Plugin.DupeFinder.Model;
using Jellyfin.Plugin.DupeFinder.Scanning;
using Xunit;

namespace Jellyfin.Plugin.DupeFinder.Tests;

public class ScanServiceTests
{
    [Fact]
    public void Scan_WithoutLibraries_IsRejected()
    {
        var outcome = new ScanService(new FakeLibraryItemSource()).Scan(new ScanRequest { LibraryIds = [], Checks = ["DuplicateMovies"] });

        Assert.Equal("Select at least one library.", outcome.Error);
        Assert.Null(outcome.Response);
    }

    [Fact]
    public void Scan_WithUnparseableOrUnknownLibrary_IsRejected()
    {
        var service = new ScanService(new FakeLibraryItemSource());
        var unknown = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

        Assert.Equal("Unknown library id 'nope'.", service.Scan(new ScanRequest { LibraryIds = ["nope"], Checks = ["DuplicateMovies"] }).Error);
        Assert.Equal($"Unknown library id '{unknown}'.", service.Scan(new ScanRequest { LibraryIds = [unknown], Checks = ["DuplicateMovies"] }).Error);
    }

    [Fact]
    public void Scan_WithoutChecks_IsRejected()
    {
        var source = new FakeLibraryItemSource();
        var films = source.Add("Films");

        Assert.Equal("Select at least one check.", new ScanService(source).Scan(new ScanRequest { LibraryIds = [Id(films)], Checks = [] }).Error);
    }

    [Fact]
    public void Scan_WithUnknownCheck_IsRejected()
    {
        var source = new FakeLibraryItemSource();
        var films = source.Add("Films");

        Assert.Equal("Unknown check 'Bogus'.", new ScanService(source).Scan(new ScanRequest { LibraryIds = [Id(films)], Checks = ["Bogus"] }).Error);
        Assert.Equal("Unknown check '3'.", new ScanService(source).Scan(new ScanRequest { LibraryIds = [Id(films)], Checks = ["3"] }).Error);
    }

    [Fact]
    public void Scan_VersionGroupPlusSeparateCopy_ReportsDuplicateAndMergedGroups()
    {
        var primary = TestItems.Movie("Heat", 1995, tmdb: "949");
        var alternate = TestItems.Movie("Heat", 1995, tmdb: "949", primaryVersionId: primary.Id);
        var copy = TestItems.Movie("Heat", 1995, tmdb: "949");
        var source = new FakeLibraryItemSource();
        var films = source.Add("Films", primary, alternate);
        var more = source.Add("More Films", copy);

        var response = Scan(source, [films, more], "DuplicateMovies", "MergedVersions");

        var duplicates = response.Findings.Where(f => f.Check == CheckId.DuplicateMovies).Select(f => f.ItemId).ToList();
        Assert.Equal(2, duplicates.Count);
        Assert.Contains(primary.Id, duplicates);
        Assert.Contains(copy.Id, duplicates);
        var merged = response.Findings.Where(f => f.Check == CheckId.MergedVersions).Select(f => f.ItemId).ToList();
        Assert.Equal(new[] { primary.Id, alternate.Id }, merged);
        Assert.Equal(1, response.Summary["DuplicateMovies"]);
        Assert.Equal(1, response.Summary["MergedVersions"]);
    }

    [Fact]
    public void Scan_OrdersByCheckThenGroup_AndNumbersGroupsAcrossTheResponse()
    {
        var source = new FakeLibraryItemSource();
        var library = source.Add(
            "Mixed",
            TestItems.Movie("Brazil", 1985, tmdb: "68"),
            TestItems.Movie("Alien", 1979, tmdb: "348"),
            TestItems.Movie("Brazil", 1985, tmdb: "68"),
            TestItems.Movie("Alien", 1979, tmdb: "348"),
            TestItems.Series("Lost", 2004, tvdb: "73739"),
            TestItems.Series("Lost", 2004, tvdb: "73739"),
            TestItems.Movie("Zardoz", 1974));

        var response = Scan(source, [library], "UnmatchedMoviesSeries", "DuplicateSeries", "DuplicateMovies");

        Assert.Equal(new[] { "Alien", "Alien", "Brazil", "Brazil", "Lost", "Lost", "Zardoz" }, response.Findings.Select(f => f.Name));
        Assert.Equal(new int?[] { 1, 1, 2, 2, 3, 3, null }, response.Findings.Select(f => f.Group));
        Assert.Equal(2, response.Summary["DuplicateMovies"]);
        Assert.Equal(1, response.Summary["DuplicateSeries"]);
        Assert.Equal(1, response.Summary["UnmatchedMoviesSeries"]);
    }

    [Fact]
    public void Scan_ItemFoundThroughTwoLibraries_IsReportedOnce()
    {
        var artist = TestItems.Artist("Unknown Artist");
        var source = new FakeLibraryItemSource();
        var musicB = source.Add("Music B", artist);
        var musicA = source.Add("Music A", artist);

        var response = Scan(source, [musicB, musicA], "UnmatchedMusic");

        var finding = Assert.Single(response.Findings);
        Assert.Equal("Music A", finding.LibraryName);
        Assert.Equal(1, response.ScannedItemCount);
    }

    [Fact]
    public void Scan_SummaryListsEveryRequestedCheck_EvenWhenZero()
    {
        var source = new FakeLibraryItemSource();
        var films = source.Add("Films", TestItems.Movie("Heat", 1995, tmdb: "949"));

        var response = Scan(source, [films], "DuplicateAlbums", "UnmatchedEpisodes");

        Assert.Empty(response.Findings);
        Assert.Equal(0, response.Summary["DuplicateAlbums"]);
        Assert.Equal(0, response.Summary["UnmatchedEpisodes"]);
        Assert.Equal(2, response.Summary.Count);
    }

    [Fact]
    public void GetLibraries_AreOrderedByName()
    {
        var source = new FakeLibraryItemSource();
        source.Add("TV");
        source.Add("Films");

        Assert.Equal(new[] { "Films", "TV" }, new ScanService(source).GetLibraries().Select(l => l.Name));
    }

    private static string Id(LibraryInfo library) => library.Id.ToString("N", CultureInfo.InvariantCulture);

    private static ScanResponse Scan(FakeLibraryItemSource source, LibraryInfo[] libraries, params string[] checks)
    {
        var outcome = new ScanService(source).Scan(new ScanRequest { LibraryIds = libraries.Select(Id).ToList(), Checks = checks });
        Assert.Null(outcome.Error);
        return Assert.IsType<ScanResponse>(outcome.Response);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run: `export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1; dotnet test`
Expected: build FAILS (`ScanService`, `ScanRequest`, `LibraryInfo`, `ILibraryItemSource` not found).

- [ ] **Step 4: Implement the DTOs**

Create `src/Jellyfin.Plugin.DupeFinder/Model/LibraryDto.cs`:

```csharp
namespace Jellyfin.Plugin.DupeFinder.Model;

/// <summary>
/// A library offered for scanning (DF-R2.1).
/// </summary>
public sealed class LibraryDto
{
    /// <summary>
    /// Gets or sets the library id (32 hex digits).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the library name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Jellyfin collection type, if any.
    /// </summary>
    public string? CollectionType { get; set; }
}
```

Create `src/Jellyfin.Plugin.DupeFinder/Model/ScanRequest.cs`:

```csharp
using System.Collections.Generic;

namespace Jellyfin.Plugin.DupeFinder.Model;

/// <summary>
/// Body of <c>POST /DupeFinder/Scan</c> (DF-R2.2).
/// </summary>
public sealed class ScanRequest
{
    /// <summary>
    /// Gets or sets the ids of the libraries to scan.
    /// </summary>
    public IReadOnlyList<string>? LibraryIds { get; set; }

    /// <summary>
    /// Gets or sets the check ids to run.
    /// </summary>
    public IReadOnlyList<string>? Checks { get; set; }
}
```

Create `src/Jellyfin.Plugin.DupeFinder/Model/ScanResponse.cs`:

```csharp
using System.Collections.Generic;

namespace Jellyfin.Plugin.DupeFinder.Model;

/// <summary>
/// Result of <c>POST /DupeFinder/Scan</c> (DF-R2.3).
/// </summary>
public sealed class ScanResponse
{
    /// <summary>
    /// Gets or sets the number of distinct items read from the selected libraries.
    /// </summary>
    public int ScannedItemCount { get; set; }

    /// <summary>
    /// Gets or sets how long the scan took, in milliseconds.
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Gets or sets the count per requested check: groups for duplicate checks, findings otherwise.
    /// </summary>
    public IReadOnlyDictionary<string, int> Summary { get; set; } = new Dictionary<string, int>();

    /// <summary>
    /// Gets or sets the findings, ordered by check, group, name and path.
    /// </summary>
    public IReadOnlyList<Finding> Findings { get; set; } = [];
}
```

- [ ] **Step 5: Implement the source seam and outcome**

Create `src/Jellyfin.Plugin.DupeFinder/Scanning/LibraryInfo.cs`:

```csharp
using System;

namespace Jellyfin.Plugin.DupeFinder.Scanning;

/// <summary>
/// Jellyfin-free description of one library.
/// </summary>
internal sealed record LibraryInfo
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public string? CollectionType { get; init; }
}
```

Create `src/Jellyfin.Plugin.DupeFinder/Scanning/ILibraryItemSource.cs`:

```csharp
using System.Collections.Generic;
using Jellyfin.Plugin.DupeFinder.Model;

namespace Jellyfin.Plugin.DupeFinder.Scanning;

/// <summary>
/// Where scanned items come from; the seam that keeps ScanService testable without Jellyfin.
/// </summary>
internal interface ILibraryItemSource
{
    IReadOnlyList<LibraryInfo> GetLibraries();

    /// <summary>
    /// Returns items of the given kinds in one library, with <see cref="ScannedItem.LibraryName"/> set
    /// to that library. Extras and additional parts are excluded; alternate versions are included.
    /// </summary>
    IReadOnlyList<ScannedItem> GetItems(LibraryInfo library, IReadOnlyCollection<ItemKind> kinds);
}
```

Create `src/Jellyfin.Plugin.DupeFinder/Scanning/ScanOutcome.cs`:

```csharp
using Jellyfin.Plugin.DupeFinder.Model;

namespace Jellyfin.Plugin.DupeFinder.Scanning;

/// <summary>
/// Either a scan response or the DF-R2.4 validation message.
/// </summary>
internal sealed class ScanOutcome
{
    private ScanOutcome(ScanResponse? response, string? error)
    {
        Response = response;
        Error = error;
    }

    public ScanResponse? Response { get; }

    public string? Error { get; }

    public static ScanOutcome Success(ScanResponse response) => new(response, null);

    public static ScanOutcome Invalid(string error) => new(null, error);
}
```

- [ ] **Step 6: Implement `ScanService`**

Create `src/Jellyfin.Plugin.DupeFinder/Scanning/ScanService.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Jellyfin.Plugin.DupeFinder.Model;

namespace Jellyfin.Plugin.DupeFinder.Scanning;

/// <summary>
/// Validates a scan request, gathers items once, runs the requested checks in display order and
/// numbers duplicate groups across the response (DF-R2).
/// </summary>
internal sealed class ScanService
{
    // Exact names only: Enum.TryParse would also accept numbers such as "3".
    private static readonly Dictionary<string, CheckId> _checksByName =
        Enum.GetValues<CheckId>().ToDictionary(check => check.ToString(), StringComparer.Ordinal);

    private readonly ILibraryItemSource _source;

    public ScanService(ILibraryItemSource source)
    {
        _source = source;
    }

    public IReadOnlyList<LibraryDto> GetLibraries()
        => _source.GetLibraries()
            .OrderBy(library => library.Name, StringComparer.OrdinalIgnoreCase)
            .Select(library => new LibraryDto
            {
                Id = library.Id.ToString("N", CultureInfo.InvariantCulture),
                Name = library.Name,
                CollectionType = library.CollectionType,
            })
            .ToList();

    public ScanOutcome Scan(ScanRequest request)
    {
        var error = Validate(request, _source.GetLibraries(), out var libraries, out var checks);
        if (error is not null)
        {
            return ScanOutcome.Invalid(error);
        }

        var stopwatch = Stopwatch.StartNew();
        var items = Gather(libraries, checks);
        var findings = new List<Finding>();
        var summary = new Dictionary<string, int>(StringComparer.Ordinal);
        var nextGroup = 1;
        foreach (var check in checks)
        {
            if (IsGrouped(check))
            {
                var groups = RunGroupedCheck(check, items)
                    .OrderBy(group => group[0].Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(group => group[0].Path, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                summary[check.ToString()] = groups.Count;
                foreach (var group in groups)
                {
                    var number = nextGroup++;
                    findings.AddRange(group.Select(finding => finding with { Group = number }));
                }
            }
            else
            {
                var single = RunSingleCheck(check, items)
                    .OrderBy(finding => finding.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(finding => finding.Path, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                summary[check.ToString()] = single.Count;
                findings.AddRange(single);
            }
        }

        stopwatch.Stop();
        return ScanOutcome.Success(new ScanResponse
        {
            ScannedItemCount = items.Count,
            DurationMs = stopwatch.ElapsedMilliseconds,
            Summary = summary,
            Findings = findings,
        });
    }

    private static string? Validate(
        ScanRequest request,
        IReadOnlyList<LibraryInfo> available,
        out List<LibraryInfo> libraries,
        out List<CheckId> checks)
    {
        libraries = [];
        checks = [];
        if (request.LibraryIds is null || request.LibraryIds.Count == 0)
        {
            return "Select at least one library.";
        }

        foreach (var rawId in request.LibraryIds)
        {
            var library = Guid.TryParse(rawId, out var id) ? available.FirstOrDefault(candidate => candidate.Id == id) : null;
            if (library is null)
            {
                return string.Create(CultureInfo.InvariantCulture, $"Unknown library id '{rawId}'.");
            }

            if (!libraries.Contains(library))
            {
                libraries.Add(library);
            }
        }

        if (request.Checks is null || request.Checks.Count == 0)
        {
            return "Select at least one check.";
        }

        foreach (var name in request.Checks)
        {
            if (name is null || !_checksByName.TryGetValue(name, out var check))
            {
                return string.Create(CultureInfo.InvariantCulture, $"Unknown check '{name}'.");
            }

            if (!checks.Contains(check))
            {
                checks.Add(check);
            }
        }

        // DF-R2.3: display order for checks; first library by name wins when an item is in several.
        checks.Sort();
        libraries.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
        return null;
    }

    private List<ScannedItem> Gather(List<LibraryInfo> libraries, List<CheckId> checks)
    {
        var kinds = checks.SelectMany(KindsFor).Distinct().ToList();
        var seen = new HashSet<Guid>();
        var items = new List<ScannedItem>();
        foreach (var library in libraries)
        {
            foreach (var item in _source.GetItems(library, kinds))
            {
                if (seen.Add(item.Id))
                {
                    items.Add(item);
                }
            }
        }

        return items;
    }

    private static bool IsGrouped(CheckId check)
        => check is CheckId.DuplicateMovies or CheckId.DuplicateSeries or CheckId.DuplicateEpisodes
            or CheckId.DuplicateAlbums or CheckId.MergedVersions;

    private static ItemKind[] KindsFor(CheckId check) => check switch
    {
        CheckId.DuplicateMovies => [ItemKind.Movie],
        CheckId.DuplicateSeries => [ItemKind.Series],
        CheckId.DuplicateEpisodes => [ItemKind.Episode],
        CheckId.DuplicateAlbums => [ItemKind.MusicAlbum],
        CheckId.MergedVersions => [ItemKind.Movie, ItemKind.Episode],
        CheckId.UnmatchedMoviesSeries => [ItemKind.Movie, ItemKind.Series],
        CheckId.UnmatchedEpisodes => [ItemKind.Episode],
        CheckId.UnmatchedMusic => [ItemKind.MusicAlbum, ItemKind.MusicArtist],
        CheckId.IncompleteMetadata => [ItemKind.Movie, ItemKind.Series, ItemKind.MusicAlbum],
        _ => throw new ArgumentOutOfRangeException(nameof(check)),
    };

    private static IReadOnlyList<IReadOnlyList<Finding>> RunGroupedCheck(CheckId check, List<ScannedItem> items) => check switch
    {
        CheckId.DuplicateMovies => DuplicateDetector.FindDuplicateMovies(VersionGroups.Collapse(OfKind(items, ItemKind.Movie))),
        CheckId.DuplicateSeries => DuplicateDetector.FindDuplicateSeries(OfKind(items, ItemKind.Series)),
        CheckId.DuplicateEpisodes => DuplicateDetector.FindDuplicateEpisodes(VersionGroups.Collapse(OfKind(items, ItemKind.Episode))),
        CheckId.DuplicateAlbums => DuplicateDetector.FindDuplicateAlbums(OfKind(items, ItemKind.MusicAlbum)),
        CheckId.MergedVersions => VersionGroups.FindMergedVersions(OfKind(items, ItemKind.Movie, ItemKind.Episode)),
        _ => throw new ArgumentOutOfRangeException(nameof(check)),
    };

    private static IReadOnlyList<Finding> RunSingleCheck(CheckId check, List<ScannedItem> items) => check switch
    {
        CheckId.UnmatchedMoviesSeries => UnmatchedDetector.FindUnmatched(VersionGroups.Collapse(OfKind(items, ItemKind.Movie, ItemKind.Series)), check),
        CheckId.UnmatchedEpisodes => UnmatchedDetector.FindUnmatched(VersionGroups.Collapse(OfKind(items, ItemKind.Episode)), check),
        CheckId.UnmatchedMusic => UnmatchedDetector.FindUnmatched(OfKind(items, ItemKind.MusicAlbum, ItemKind.MusicArtist), check),
        CheckId.IncompleteMetadata => UnmatchedDetector.FindIncomplete(
            [.. VersionGroups.Collapse(OfKind(items, ItemKind.Movie, ItemKind.Series)), .. OfKind(items, ItemKind.MusicAlbum)]),
        _ => throw new ArgumentOutOfRangeException(nameof(check)),
    };

    private static List<ScannedItem> OfKind(List<ScannedItem> items, params ItemKind[] kinds)
        => items.Where(item => kinds.Contains(item.Kind)).ToList();
}
```

- [ ] **Step 7: Run tests and build**

Run: `export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1; dotnet test && dotnet build -c Release`
Expected: all tests pass (47); `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 8: Commit**

```bash
git add src tests
git commit -m "DF-R2.2-R2.4: scan orchestration, ordering and validation (AC-3, AC-10)" -m "<attribution lines>"
```

---

### Task 5: Jellyfin integration, repository packaging and live API checks

**Files:**
- Create: `src/Jellyfin.Plugin.DupeFinder/Plugin.cs`
- Create: `src/Jellyfin.Plugin.DupeFinder/Api/DupeFinderController.cs`
- Create: `src/Jellyfin.Plugin.DupeFinder/Scanning/JellyfinLibraryItemSource.cs`
- Create: `scripts/build-repo.sh`, `scripts/serve-repo.sh`, `scripts/crosscheck.py`

**Interfaces:**
- Consumes: `ScanService`, `ILibraryItemSource`, `LibraryInfo`, `ScannedItem`, DTOs (Task 4).
- Produces:
  - Plugin class `Jellyfin.Plugin.DupeFinder.Plugin : BasePlugin` (Task 6 adds `IHasWebPages`).
  - Endpoints `GET /DupeFinder/Libraries` → `LibraryDto[]`; `POST /DupeFinder/Scan` → `ScanResponse` or 400 problem details (`detail` = DF-R2.4 text). Both admin-only.
  - `scripts/build-repo.sh <base-url>` (env `PLUGIN_VERSION` overrides the version) → `repo/manifest.json`, `repo/dupe-finder_<version>.zip`.
  - `scripts/serve-repo.sh [port]`.
  - `scripts/crosscheck.py` (env-configured; exit 1 on any FAIL).

- [ ] **Step 1: Implement the Jellyfin item source**

Create `src/Jellyfin.Plugin.DupeFinder/Scanning/JellyfinLibraryItemSource.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.DupeFinder.Model;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;

namespace Jellyfin.Plugin.DupeFinder.Scanning;

/// <summary>
/// The only code that reads Jellyfin's library (ADR-0002). Items are read-only snapshots and are never
/// saved back (DF-C3).
/// </summary>
internal sealed class JellyfinLibraryItemSource : ILibraryItemSource
{
    private readonly ILibraryManager _libraryManager;

    public JellyfinLibraryItemSource(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }

    public IReadOnlyList<LibraryInfo> GetLibraries()
    {
        var libraries = new List<LibraryInfo>();
        foreach (var folder in _libraryManager.GetVirtualFolders())
        {
            if (Guid.TryParse(folder.ItemId, out var id))
            {
                libraries.Add(new LibraryInfo { Id = id, Name = folder.Name, CollectionType = folder.CollectionType?.ToString() });
            }
        }

        return libraries;
    }

    public IReadOnlyList<ScannedItem> GetItems(LibraryInfo library, IReadOnlyCollection<ItemKind> kinds)
    {
        var items = new List<ScannedItem>();
        var itemKinds = kinds.Where(kind => kind != ItemKind.MusicArtist).Select(ToBaseItemKind).ToArray();
        if (itemKinds.Length > 0)
        {
            var query = new InternalItemsQuery
            {
                // LibraryManager.GetItemList turns a recursive ParentId on a library into its TopParentIds.
                // User stays null: no per-user grouping or access filtering for an admin scan.
                ParentId = library.Id,
                Recursive = true,
                IsVirtualItem = false,
                IncludeItemTypes = itemKinds,

                // ADR-0008: 12.2 stores auto-detected alternate versions as owned items, which the default
                // query drops; include owned items and filter extras and additional parts below.
                IncludeOwnedItems = true,
            };

            foreach (var item in _libraryManager.GetItemList(query))
            {
                if (!IsExtraOrPart(item))
                {
                    items.Add(Map(item, library.Name));
                }
            }
        }

        if (kinds.Contains(ItemKind.MusicArtist))
        {
            // DF-R4.3: same library scoping as Jellyfin's own Album Artists endpoint (ArtistsController).
            var artistQuery = new InternalItemsQuery
            {
                AncestorIds = [library.Id],
                DtoOptions = new DtoOptions(false) { Fields = [ItemFields.ProviderIds], EnableImages = false },
            };

            foreach (var (item, _) in _libraryManager.GetAlbumArtists(artistQuery).Items)
            {
                items.Add(Map(item, library.Name));
            }
        }

        return items;
    }

    private static bool IsExtraOrPart(BaseItem item)
        => item.ExtraType is not null
            || (!item.OwnerId.Equals(Guid.Empty) && item is not Video { PrimaryVersionId: not null });

    private static ScannedItem Map(BaseItem item, string libraryName)
    {
        var episode = item as Episode;
        return new ScannedItem
        {
            Id = item.Id,
            Kind = ToItemKind(item),
            LibraryName = libraryName,
            Name = item.Name ?? string.Empty,
            Year = item.ProductionYear,
            Path = item.Path,
            SizeBytes = item.Size,
            ProviderIds = new Dictionary<string, string>(item.ProviderIds, StringComparer.OrdinalIgnoreCase),
            PrimaryVersionId = (item as Video)?.PrimaryVersionId,
            SeriesId = episode is null || episode.SeriesId.Equals(Guid.Empty) ? null : episode.SeriesId,
            SeriesName = episode?.SeriesName,
            Season = episode?.ParentIndexNumber,
            Episode = episode?.IndexNumber,
            AlbumArtists = (item as MusicAlbum)?.AlbumArtists ?? [],
            HasOverview = !string.IsNullOrWhiteSpace(item.Overview),
            HasPrimaryImage = item.HasImage(ImageType.Primary, 0),
        };
    }

    private static ItemKind ToItemKind(BaseItem item) => item switch
    {
        Movie => ItemKind.Movie,
        Series => ItemKind.Series,
        Episode => ItemKind.Episode,
        MusicAlbum => ItemKind.MusicAlbum,
        MusicArtist => ItemKind.MusicArtist,
        _ => throw new InvalidOperationException("Unexpected item type " + item.GetType().Name),
    };

    private static BaseItemKind ToBaseItemKind(ItemKind kind) => kind switch
    {
        ItemKind.Movie => BaseItemKind.Movie,
        ItemKind.Series => BaseItemKind.Series,
        ItemKind.Episode => BaseItemKind.Episode,
        ItemKind.MusicAlbum => BaseItemKind.MusicAlbum,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
```

- [ ] **Step 2: Implement the controller and plugin class**

Create `src/Jellyfin.Plugin.DupeFinder/Api/DupeFinderController.cs`:

```csharp
using System.Collections.Generic;
using Jellyfin.Plugin.DupeFinder.Model;
using Jellyfin.Plugin.DupeFinder.Scanning;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.DupeFinder.Api;

/// <summary>
/// Admin-only endpoints behind the Duplicate &amp; Unmatched Finder page (DF-R1.2, DF-R2). Jellyfin serves
/// the page files themselves without authentication, so access control lives here.
/// </summary>
[ApiController]
[Route("DupeFinder")]
[Authorize(Policy = Policies.RequiresElevation)]
public class DupeFinderController : ControllerBase
{
    private readonly ScanService _scanService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DupeFinderController"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    public DupeFinderController(ILibraryManager libraryManager)
    {
        _scanService = new ScanService(new JellyfinLibraryItemSource(libraryManager));
    }

    /// <summary>
    /// Lists the server's libraries.
    /// </summary>
    /// <response code="200">Libraries returned, ordered by name.</response>
    /// <returns>The libraries.</returns>
    [HttpGet("Libraries")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<LibraryDto>> GetLibraries()
    {
        return Ok(_scanService.GetLibraries());
    }

    /// <summary>
    /// Scans the selected libraries with the selected checks.
    /// </summary>
    /// <param name="request">The libraries and checks to run.</param>
    /// <response code="200">Scan completed.</response>
    /// <response code="400">No, or unknown, libraries or checks.</response>
    /// <returns>The scan result.</returns>
    [HttpPost("Scan")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<ScanResponse> Scan([FromBody] ScanRequest request)
    {
        var outcome = _scanService.Scan(request);
        if (outcome.Error is not null)
        {
            // ADR-0008: problem details give the page a deterministic JSON body with the message in `detail`.
            return Problem(detail: outcome.Error, statusCode: StatusCodes.Status400BadRequest);
        }

        return outcome.Response!;
    }
}
```

Create `src/Jellyfin.Plugin.DupeFinder/Plugin.cs`:

```csharp
using System;
using MediaBrowser.Common.Plugins;

namespace Jellyfin.Plugin.DupeFinder;

/// <summary>
/// Duplicate &amp; Unmatched Finder. Stores no settings, so it derives from the non-generic <see cref="BasePlugin"/> (ADR-0002).
/// </summary>
public class Plugin : BasePlugin
{
    /// <inheritdoc />
    public override string Name => "Duplicate & Unmatched Finder";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("fee5e03c-c3e1-4067-a6a8-0ed6eb63c3a7");

    /// <inheritdoc />
    public override string Description => "Finds duplicate and unmatched items in selected libraries.";
}
```

- [ ] **Step 3: Build and run unit tests**

Run: `export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1; dotnet build -c Release && dotnet test`
Expected: `0 Warning(s)`, `0 Error(s)`; tests still pass (47).
If a Jellyfin API member does not resolve, grep the 12.2 source (`git clone --depth 1 --branch v12.2 https://github.com/jellyfin/jellyfin` into a scratch directory) for the real signature; never guess.

- [ ] **Step 4: Write the packaging scripts**

Create `scripts/build-repo.sh`:

```bash
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
```

Create `scripts/serve-repo.sh`:

```bash
#!/usr/bin/env bash
# DF-R9.2: serve repo/ so a Jellyfin server on the LAN can install the plugin from it.
# Usage: scripts/serve-repo.sh [port]   (default 8765)
set -euo pipefail
PORT="${1:-8765}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
exec python3 -m http.server "$PORT" --bind 0.0.0.0 --directory "$ROOT/repo"
```

```bash
chmod +x scripts/build-repo.sh scripts/serve-repo.sh
```

- [ ] **Step 5: Write the live API check script**

Create `scripts/crosscheck.py`:

```python
#!/usr/bin/env python3
"""Live API checks for the Duplicate & Unmatched Finder plugin (AC-7, AC-9, AC-10, AC-11).

Configuration comes only from environment variables and is never written anywhere (DF-C4):
  JF_URL            server base URL
  JF_ADMIN_USER     administrator user name       JF_ADMIN_PW     (optional, default empty)
  JF_NONADMIN_USER  non-administrator user name   JF_NONADMIN_PW  (optional, default empty)
  EXPECT_VERSION    optional: plugin version that must be installed and Active (AC-7)
Exits 1 if any check fails.
"""
import json
import os
import sys
import urllib.error
import urllib.request
from collections import defaultdict

BASE = os.environ["JF_URL"].rstrip("/")
CLIENT = 'MediaBrowser Client="DupeFinderCheck", Device="crosscheck", DeviceId="dupefinder-crosscheck", Version="1.0"'
PLUGIN_NAME = "Duplicate & Unmatched Finder"
# Collections and playlists hold links, not their own media; Jellyfin's REST API follows links there
# and the plugin (by design) does not, so they are not comparable.
SKIP_TYPES = {"boxsets", "playlists"}
failures = []


def check(ok, message):
    print(("PASS " if ok else "FAIL ") + message)
    if not ok:
        failures.append(message)


def call(method, path, token=None, body=None):
    # Jellyfin 12.x accepts only the standard Authorization header (docs/lessons/jellyfin-12-auth-header.md).
    headers = {"Authorization": CLIENT + (f', Token="{token}"' if token else "")}
    data = None
    if body is not None:
        data = json.dumps(body).encode("utf-8")
        headers["Content-Type"] = "application/json"
    request = urllib.request.Request(BASE + path, data=data, method=method, headers=headers)
    try:
        with urllib.request.urlopen(request, timeout=900) as response:
            raw = response.read()
            return response.status, json.loads(raw) if raw else None
    except urllib.error.HTTPError as error:
        raw = error.read()
        try:
            return error.code, json.loads(raw)
        except ValueError:
            return error.code, raw.decode("utf-8", "replace")


def login(user, password):
    status, body = call("POST", "/Users/AuthenticateByName", body={"Username": user, "Pw": password})
    if status != 200:
        sys.exit(f"login failed: HTTP {status}")
    return body["AccessToken"], body["User"]["Id"]


def unmatched(item):
    return not any((value or "").strip() for value in (item.get("ProviderIds") or {}).values())


def norm(item_id):
    return item_id.replace("-", "").lower()


def check_plugin(admin):
    status, plugins = call("GET", "/Plugins", admin)
    mine = [p for p in plugins or [] if p.get("Name") == PLUGIN_NAME]
    check(status == 200 and len(mine) == 1, f"plugin installed once: {[(p.get('Version'), p.get('Status')) for p in mine]}")
    expected = os.environ.get("EXPECT_VERSION")
    if expected and mine:
        check(mine[0].get("Version") == expected and mine[0].get("Status") == "Active", f"AC-7 version {expected} Active")


def check_access(admin, nonadmin, library_id):
    body = {"LibraryIds": [library_id], "Checks": ["UnmatchedMoviesSeries"]}
    for label, token, expected in (("admin", admin, 200), ("non-admin", nonadmin, 403), ("anonymous", None, 401)):
        status, _ = call("GET", "/DupeFinder/Libraries", token)
        check(status == expected, f"AC-9 GET /DupeFinder/Libraries as {label} -> {status} (want {expected})")
        status, _ = call("POST", "/DupeFinder/Scan", token, body)
        check(status == expected, f"AC-9 POST /DupeFinder/Scan as {label} -> {status} (want {expected})")


def check_validation(admin, library_id):
    cases = (
        ({"LibraryIds": [], "Checks": ["DuplicateMovies"]}, "Select at least one library."),
        ({"LibraryIds": ["not-a-library"], "Checks": ["DuplicateMovies"]}, "Unknown library id 'not-a-library'."),
        ({"LibraryIds": [library_id], "Checks": []}, "Select at least one check."),
        ({"LibraryIds": [library_id], "Checks": ["Bogus"]}, "Unknown check 'Bogus'."),
    )
    for body, detail in cases:
        status, payload = call("POST", "/DupeFinder/Scan", admin, body)
        got = payload.get("detail") if isinstance(payload, dict) else payload
        check(status == 400 and got == detail, f"AC-10 {json.dumps(body)} -> {status} {got!r}")


def rest_items(admin, user_id, path):
    status, body = call("GET", path + f"&userId={user_id}&Fields=ProviderIds", admin)
    if status != 200:
        check(False, f"REST {path} -> HTTP {status}")
        return []
    return body["Items"]


def check_counts(admin, user_id, libraries):
    for library in libraries:
        lid, name = library["Id"], library["Name"]
        status, scan = call("POST", "/DupeFinder/Scan", admin, {"LibraryIds": [lid], "Checks": ["UnmatchedMoviesSeries", "UnmatchedMusic"]})
        if status != 200:
            check(False, f"AC-11 scan of {name} -> HTTP {status}")
            continue
        movies_series = rest_items(admin, user_id, f"/Items?ParentId={lid}&Recursive=true&IncludeItemTypes=Movie,Series")
        albums = rest_items(admin, user_id, f"/Items?ParentId={lid}&Recursive=true&IncludeItemTypes=MusicAlbum")
        artists = rest_items(admin, user_id, f"/Artists/AlbumArtists?ParentId={lid}")
        rest_ms = sum(unmatched(i) for i in movies_series)
        rest_music = sum(unmatched(i) for i in albums) + sum(unmatched(i) for i in artists)
        plugin_ms = scan["Summary"]["UnmatchedMoviesSeries"]
        plugin_music = scan["Summary"]["UnmatchedMusic"]
        check(plugin_ms == rest_ms, f"AC-11 {name}: unmatched movies+series plugin={plugin_ms} rest={rest_ms}")
        check(plugin_music == rest_music, f"AC-11 {name}: unmatched albums+artists plugin={plugin_music} rest={rest_music}")


def check_tmdb_groups(admin, user_id, libraries):
    ids = [library["Id"] for library in libraries]
    status, scan = call("POST", "/DupeFinder/Scan", admin, {"LibraryIds": ids, "Checks": ["DuplicateMovies"]})
    if status != 200:
        check(False, f"AC-11 duplicate scan -> HTTP {status}")
        return
    group_of = {norm(f["ItemId"]): f.get("Group") for f in scan["Findings"]}
    by_tmdb = defaultdict(set)
    for lid in ids:
        for movie in rest_items(admin, user_id, f"/Items?ParentId={lid}&Recursive=true&IncludeItemTypes=Movie"):
            tmdb = ((movie.get("ProviderIds") or {}).get("Tmdb") or "").strip()
            if tmdb:
                by_tmdb[tmdb].add(norm(movie["Id"]))
    shared = {tmdb: items for tmdb, items in by_tmdb.items() if len(items) > 1}
    print(f"INFO {len(shared)} TMDb ids shared by 2+ movies; plugin reported {scan['Summary']['DuplicateMovies']} DuplicateMovies groups in {scan['DurationMs']} ms")
    for tmdb, items in sorted(shared.items()):
        groups = {group_of.get(item) for item in items}
        check(None not in groups and len(groups) == 1, f"AC-11 TMDb {tmdb}: {len(items)} movies share one plugin group {sorted(map(str, groups))}")


def run_checks(admin, admin_user_id, nonadmin):
    check_plugin(admin)
    status, libraries = call("GET", "/DupeFinder/Libraries", admin)
    check(status == 200 and bool(libraries), f"libraries listed: {[lib['Name'] for lib in libraries or []]}")
    if status != 200 or not libraries:
        return
    comparable = [lib for lib in libraries if (lib.get("CollectionType") or "").lower() not in SKIP_TYPES]
    check_access(admin, nonadmin, libraries[0]["Id"])
    check_validation(admin, libraries[0]["Id"])
    check_counts(admin, admin_user_id, comparable)
    check_tmdb_groups(admin, admin_user_id, comparable)


def main():
    admin, admin_user_id = login(os.environ["JF_ADMIN_USER"], os.environ.get("JF_ADMIN_PW", ""))
    nonadmin, _ = login(os.environ["JF_NONADMIN_USER"], os.environ.get("JF_NONADMIN_PW", ""))
    try:
        run_checks(admin, admin_user_id, nonadmin)
    finally:
        for token in (admin, nonadmin):
            call("POST", "/Sessions/Logout", token)
    print(f"{len(failures)} failure(s)")
    sys.exit(1 if failures else 0)


if __name__ == "__main__":
    main()
```

```bash
chmod +x scripts/crosscheck.py
python3 -m py_compile scripts/crosscheck.py && echo compiled
```

Expected: `compiled`.

- [ ] **Step 6: Package version 0.9.0.0**

```bash
LAN_IP="$(ipconfig getifaddr en1 || ipconfig getifaddr en0)"
PLUGIN_VERSION=0.9.0.0 scripts/build-repo.sh "http://$LAN_IP:8765"
unzip -l repo/dupe-finder_0.9.0.0.zip
python3 -c 'import json; m=json.load(open("repo/manifest.json")); v=m[0]["versions"][0]; print(m[0]["name"], v["version"], v["targetAbi"], v["checksum"])'
```

Expected: the zip lists exactly one file, `Jellyfin.Plugin.DupeFinder.dll`; the manifest line shows `Duplicate & Unmatched Finder 0.9.0.0 12.2.0.0 <32 hex>`.

- [ ] **Step 7 (🧑 CONTROLLER + USER): Serve the repository and install**

Start `scripts/serve-repo.sh` with `run_in_background: true`. Confirm it serves: `curl -s "http://$(ipconfig getifaddr en1 || ipconfig getifaddr en0):8765/manifest.json" | head -c 200`. Then ask the user to:
1. Allow incoming connections for Python if macOS asks.
2. In Jellyfin as admin: Dashboard → Plugins → Repositories → add `http://<this Mac's LAN IP>:8765/manifest.json` (print the exact URL for them).
3. Catalog → "Duplicate & Unmatched Finder" → Install 0.9.0.0 → restart the server.
4. Reply with the admin user name and the name of a non-admin user for the live checks (used only as env vars, never written down).

End the turn and wait.

- [ ] **Step 8: Run the live API checks (AC-9, AC-10, AC-11)**

```bash
JF_URL='<server URL from the conversation>' JF_ADMIN_USER='<admin>' JF_NONADMIN_USER='<non-admin>' EXPECT_VERSION=0.9.0.0 python3 scripts/crosscheck.py
```

Expected: every line `PASS …`, final `0 failure(s)`, exit 0. Paste the full output into the task report.
If an AC-11 count differs: do not adjust the check to fit. Use superpowers:systematic-debugging — list the item ids the two sides disagree on (add a temporary local print, not committed), inspect them in Jellyfin, and decide whether the plugin or the REST comparison is wrong. A plugin bug is fixed with a new failing unit test first. A legitimate REST difference is recorded in `docs/lessons/` and in the commit message, and the check is narrowed with a comment citing that lesson. Re-package with `PLUGIN_VERSION=0.9.0.1` (incrementing the last digit each time) and have the user update the plugin.

- [ ] **Step 9: Commit**

```bash
git add src scripts
git commit -m "DF-R1.2 DF-R2 DF-R9: Jellyfin integration, repo packaging, live API checks (AC-9, AC-10, AC-11)" -m "<attribution lines>"
```

---

### Task 6: Dashboard page and CSV export

**Files:**
- Create: `src/Jellyfin.Plugin.DupeFinder/Web/dupefinder.html`
- Create: `src/Jellyfin.Plugin.DupeFinder/Web/dupefinder.js`
- Modify: `src/Jellyfin.Plugin.DupeFinder/Plugin.cs` (add `IHasWebPages`)
- Modify: `src/Jellyfin.Plugin.DupeFinder/Jellyfin.Plugin.DupeFinder.csproj` (embedded resources)
- Test: `tests/web/dupefinder.test.mjs`

**Interfaces:**
- Consumes: endpoints and JSON shape from Task 5 (PascalCase; `Group`, `Year`, `Season`, `Episode`, `SeriesName`, `Path`, `SizeBytes`, `CollectionType` may be absent).
- Produces:
  - Page `dupefinder` (sidebar, `content_copy` icon) and module page `dupefinderjs`.
  - Named exports from `dupefinder.js`: `checkLabel(checkId)`, `formatSize(bytes)`, `episodeCode(finding)`, `providerIdsText(providerIds)`, `csvField(value, isText)`, `toCsv(findings)`, `csvFileName(date)`, `errorMessage(err)`; default export `function (view)`.
  - DOM contract used by Task 7: `#dupeFinderPage`, `#dfSelectAll`, `#dfLibraries`, `#dfDuplicateChecks`, `#dfUnmatchedChecks`, `#dfScan`, `#dfDownload`, `#dfResults`, `#dfStats`, `#dfSummary li`, `#dfEmpty`, `#dfTableWrap`, `#dfRows tr[data-item-id]`, `a.df-open`, `a.df-edit`, `button.df-identify`, `.df-status`.

- [ ] **Step 1: Write the failing page tests (AC-14, Review Focus 5)**

Create `tests/web/dupefinder.test.mjs`:

```js
import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
    checkLabel, csvField, csvFileName, episodeCode, errorMessage, formatSize, providerIdsText, toCsv
} from '../../src/Jellyfin.Plugin.DupeFinder/Web/dupefinder.js';

const HEADER = 'Check,Group,Library,Type,Name,Year,Season,Episode,Series,Path,SizeBytes,ProviderIds,Reason,ItemId';

function finding(overrides = {}) {
    return {
        Check: 'DuplicateMovies', Group: 1, ItemId: 'abc123', LibraryName: 'Films', ItemType: 'Movie',
        Name: 'Heat', Year: 1995, Path: '/media/Heat.mkv', SizeBytes: 1500000000,
        ProviderIds: { Tmdb: '949' }, Reason: 'Same TMDb id 949', ...overrides
    };
}

test('CSV has a UTF-8 BOM, the DF-R8.3 header, CRLF line endings and one line per finding', () => {
    const csv = toCsv([finding(), finding({ Group: 2 })]);
    assert.ok(csv.startsWith('﻿' + HEADER + '\r\n'));
    assert.ok(csv.endsWith('\r\n'));
    assert.equal(csv.split('\r\n').length, 4);
    assert.ok(csv.includes('\r\nDuplicate movies,1,Films,Movie,Heat,1995,,,,/media/Heat.mkv,1500000000,Tmdb=949,Same TMDb id 949,abc123\r\n'));
});

test('absent optional fields become empty cells', () => {
    const csv = toCsv([finding({ Group: undefined, Year: undefined, Path: undefined, SizeBytes: undefined, Check: 'UnmatchedMoviesSeries', Reason: 'No provider ids', ProviderIds: {} })]);
    assert.ok(csv.includes('\r\nUnmatched movie/series,,Films,Movie,Heat,,,,,,,,No provider ids,abc123\r\n'));
});

test('fields with a comma or quote are quoted and inner quotes doubled (AC-14)', () => {
    assert.equal(csvField('Say "Hi", Bob', true), '"Say ""Hi"", Bob"');
    assert.equal(csvField('two\nlines', true), '"two\nlines"');
});

test('text starting with a formula character gets a leading apostrophe (AC-14)', () => {
    assert.equal(csvField('=SUM(A1)', true), "'=SUM(A1)");
    assert.equal(csvField('+1', true), "'+1");
    assert.equal(csvField('-ish', true), "'-ish");
    assert.equal(csvField('@home', true), "'@home");
    assert.equal(csvField('\tx', true), "'\tx");
});

test('numeric columns are never prefixed', () => {
    assert.equal(csvField(-5, false), '-5');
    assert.equal(csvField(null, false), '');
    assert.equal(csvField(undefined, true), '');
});

test('non-ASCII names survive unchanged (Review Focus 5)', () => {
    assert.ok(toCsv([finding({ Name: 'Amélie 千と千尋' })]).includes(',Amélie 千と千尋,'));
});

test('file name is dupefinder-YYYYMMDD-HHmm.csv in local time', () => {
    assert.equal(csvFileName(new Date(2026, 9, 6, 9, 5)), 'dupefinder-20261006-0905.csv');
});

test('sizes are human readable', () => {
    assert.equal(formatSize(1503238553), '1.4 GB');
    assert.equal(formatSize(512), '512 B');
    assert.equal(formatSize(undefined), '');
});

test('episode codes only for episodes with both numbers', () => {
    assert.equal(episodeCode({ ItemType: 'Episode', Season: 1, Episode: 3 }), 'S01E03');
    assert.equal(episodeCode({ ItemType: 'Episode', Season: 1 }), '');
    assert.equal(episodeCode({ ItemType: 'Movie', Season: 1, Episode: 3 }), '');
});

test('provider ids render as Key=Value pairs', () => {
    assert.equal(providerIdsText({ Tmdb: '603', Imdb: 'tt0133093' }), 'Tmdb=603; Imdb=tt0133093');
    assert.equal(providerIdsText(undefined), '');
});

test('check labels match the table', () => {
    assert.equal(checkLabel('MergedVersions'), 'Merged versions');
    assert.equal(checkLabel('Unknown'), 'Unknown');
});

test('error messages come from problem details, then text', async () => {
    const response = (body, status = 400) => ({ status, text: async () => body });
    assert.equal(await errorMessage(response('{"title":"Bad Request","detail":"Select at least one check."}')), 'Select at least one check.');
    assert.equal(await errorMessage(response('{"title":"One or more validation errors occurred."}')), 'One or more validation errors occurred.');
    assert.equal(await errorMessage(response('plain failure')), 'plain failure');
    assert.equal(await errorMessage(response('', 500)), 'HTTP 500');
    assert.equal(await errorMessage('offline'), 'offline');
});
```

- [ ] **Step 2: Run to verify failure**

Run: `node --test tests/web/*.test.mjs`
Expected: FAIL — `Cannot find module …/Web/dupefinder.js`.

- [ ] **Step 3: Implement the page controller**

Create `src/Jellyfin.Plugin.DupeFinder/Web/dupefinder.js`:

```js
// Duplicate & Unmatched Finder page controller.
// ADR-0007: jellyfin-web imports this as an ES module via data-controller="__plugin/dupefinderjs" and
// calls `new default(view)`. Template literals are safe here; inside the page HTML they would be
// rewritten by Jellyfin's translation pass.

const CHECKS = [
    { id: 'DuplicateMovies', section: 'duplicates', label: 'Duplicate movies', title: 'Movies', on: true,
        description: 'Same TMDb or IMDb id, or same title and year when one copy is unmatched.' },
    { id: 'DuplicateSeries', section: 'duplicates', label: 'Duplicate series', title: 'TV series', on: true,
        description: 'Same TVDb, TMDb or IMDb id, or same title and year when one copy is unmatched.' },
    { id: 'DuplicateEpisodes', section: 'duplicates', label: 'Duplicate episodes', title: 'TV episodes', on: true,
        description: 'Same series, season and episode number.' },
    { id: 'DuplicateAlbums', section: 'duplicates', label: 'Duplicate albums', title: 'Music albums', on: true,
        description: 'Same MusicBrainz release, or same album artist and album name.' },
    { id: 'MergedVersions', section: 'duplicates', label: 'Merged versions', title: 'Merged versions', on: true,
        description: 'Movies or episodes Jellyfin has merged into one item with several versions.' },
    { id: 'UnmatchedMoviesSeries', section: 'unmatched', label: 'Unmatched movie/series', title: 'Movies and series', on: true,
        description: 'No metadata provider id at all.' },
    { id: 'UnmatchedEpisodes', section: 'unmatched', label: 'Unmatched episode', title: 'Episodes (often noisy)', on: false,
        description: 'No metadata provider id. Many correctly matched shows have no episode ids.' },
    { id: 'UnmatchedMusic', section: 'unmatched', label: 'Unmatched music', title: 'Music albums and album artists', on: true,
        description: 'No metadata provider id at all.' },
    { id: 'IncompleteMetadata', section: 'unmatched', label: 'Incomplete metadata', title: 'Incomplete metadata', on: true,
        description: 'Matched, but missing an overview or primary image.' }
];

const GROUPED = new Set(['DuplicateMovies', 'DuplicateSeries', 'DuplicateEpisodes', 'DuplicateAlbums', 'MergedVersions']);

// DF-R7.2: the scanned types jellyfin-web itself offers Identify for (itemHelper.canIdentify).
const IDENTIFIABLE = new Set(['Movie', 'Series', 'MusicAlbum', 'MusicArtist']);

const CSV_HEADER = ['Check', 'Group', 'Library', 'Type', 'Name', 'Year', 'Season', 'Episode', 'Series', 'Path',
    'SizeBytes', 'ProviderIds', 'Reason', 'ItemId'];

export function checkLabel(checkId) {
    const check = CHECKS.find((c) => c.id === checkId);
    return check ? check.label : checkId;
}

export function formatSize(bytes) {
    if (bytes === null || bytes === undefined) return '';
    const units = ['B', 'KB', 'MB', 'GB', 'TB'];
    let value = bytes;
    let unit = 0;
    while (value >= 1024 && unit < units.length - 1) {
        value /= 1024;
        unit++;
    }
    return `${unit === 0 ? value : value.toFixed(1)} ${units[unit]}`;
}

export function episodeCode(finding) {
    if (finding.ItemType !== 'Episode' || finding.Season == null || finding.Episode == null) return '';
    return `S${String(finding.Season).padStart(2, '0')}E${String(finding.Episode).padStart(2, '0')}`;
}

export function providerIdsText(providerIds) {
    return Object.entries(providerIds || {}).map(([key, value]) => `${key}=${value}`).join('; ');
}

// DF-R8.4/R8.5: RFC 4180 quoting; text cells that a spreadsheet would treat as a formula get a leading '.
export function csvField(value, isText) {
    if (value === null || value === undefined) return '';
    let text = String(value);
    if (isText && /^[=+\-@\t\r]/.test(text)) text = `'${text}`;
    if (/[",\r\n]/.test(text)) text = `"${text.replace(/"/g, '""')}"`;
    return text;
}

export function toCsv(findings) {
    const lines = [CSV_HEADER.join(',')];
    for (const f of findings) {
        lines.push([
            csvField(checkLabel(f.Check), true),
            csvField(f.Group, false),
            csvField(f.LibraryName, true),
            csvField(f.ItemType, true),
            csvField(f.Name, true),
            csvField(f.Year, false),
            csvField(f.Season, false),
            csvField(f.Episode, false),
            csvField(f.SeriesName, true),
            csvField(f.Path, true),
            csvField(f.SizeBytes, false),
            csvField(providerIdsText(f.ProviderIds), true),
            csvField(f.Reason, true),
            csvField(f.ItemId, true)
        ].join(','));
    }
    // The BOM makes Excel read the file as UTF-8 (Review Focus 5).
    return `﻿${lines.join('\r\n')}\r\n`;
}

export function csvFileName(date) {
    const pad = (n) => String(n).padStart(2, '0');
    return `dupefinder-${date.getFullYear()}${pad(date.getMonth() + 1)}${pad(date.getDate())}-${pad(date.getHours())}${pad(date.getMinutes())}.csv`;
}

// ApiClient rejects with the fetch Response on HTTP errors. Our 400s are problem details (ADR-0008);
// ASP.NET's own model-binding 400s carry only a title.
export async function errorMessage(err) {
    if (err && typeof err.text === 'function') {
        const text = await err.text();
        try {
            const body = JSON.parse(text);
            return body.detail || body.title || text;
        } catch {
            return text || `HTTP ${err.status}`;
        }
    }
    return String(err);
}

export default function (view) {
    const state = { loaded: false, scanning: false, result: null };
    const $ = (selector) => view.querySelector(selector);
    const boxes = (kind) => [...view.querySelectorAll(`input[data-kind="${kind}"]`)];
    const selected = (kind) => boxes(kind).filter((box) => box.checked).map((box) => box.value);

    function updateButtons() {
        const libraries = boxes('library');
        $('#dfSelectAll').checked = libraries.length > 0 && libraries.every((box) => box.checked);
        $('#dfScan').disabled = state.scanning || selected('library').length === 0 || selected('check').length === 0;
        $('#dfDownload').disabled = state.scanning || !state.result || state.result.Findings.length === 0;
    }

    function checkboxRow(value, title, description, checked, kind) {
        const wrapper = document.createElement('div');
        wrapper.className = 'checkboxContainer';
        // Static markup only; every piece of data goes in through value/textContent (Review Focus 3).
        wrapper.innerHTML = '<label class="emby-checkbox-label"><input is="emby-checkbox" type="checkbox"><span></span></label>';
        const input = wrapper.querySelector('input');
        input.value = value;
        input.checked = checked;
        input.dataset.kind = kind;
        input.addEventListener('change', updateButtons);
        wrapper.querySelector('span').textContent = title;
        if (description) {
            const help = document.createElement('div');
            help.className = 'fieldDescription checkboxFieldDescription';
            help.textContent = description;
            wrapper.appendChild(help);
        }
        return wrapper;
    }

    function renderChecks() {
        for (const [section, selector] of [['duplicates', '#dfDuplicateChecks'], ['unmatched', '#dfUnmatchedChecks']]) {
            const fragment = document.createDocumentFragment();
            for (const check of CHECKS.filter((c) => c.section === section)) {
                fragment.appendChild(checkboxRow(check.id, check.title, check.description, check.on, 'check'));
            }
            $(selector).replaceChildren(fragment);
        }
    }

    async function loadLibraries() {
        Dashboard.showLoadingMsg();
        try {
            const libraries = await ApiClient.getJSON(ApiClient.getUrl('DupeFinder/Libraries'));
            const fragment = document.createDocumentFragment();
            for (const library of libraries) {
                const title = library.CollectionType ? `${library.Name} (${library.CollectionType})` : library.Name;
                fragment.appendChild(checkboxRow(library.Id, title, '', true, 'library'));
            }
            $('#dfLibraries').replaceChildren(fragment);
        } catch (err) {
            Dashboard.alert({ title: 'Could not load libraries', message: await errorMessage(err) });
        } finally {
            Dashboard.hideLoadingMsg();
            updateButtons();
        }
    }

    function cell(text, className) {
        const td = document.createElement('td');
        td.className = className ? `detailTableBodyCell ${className}` : 'detailTableBodyCell';
        td.textContent = text === null || text === undefined ? '' : String(text);
        return td;
    }

    function link(text, href, className) {
        const anchor = document.createElement('a');
        anchor.href = href;
        anchor.target = '_blank';
        anchor.rel = 'noopener';
        anchor.className = `emby-button ${className}`;
        anchor.textContent = text;
        return anchor;
    }

    function actionsCell(finding) {
        const td = cell('', 'df-actions');
        td.appendChild(link('Open', `#/details?id=${finding.ItemId}&serverId=${ApiClient.serverId()}`, 'df-open'));
        td.appendChild(link('Edit metadata', `#/metadata?id=${finding.ItemId}`, 'df-edit'));
        // ADR-0003: undocumented global; hide the button if a future jellyfin-web removes it.
        if (IDENTIFIABLE.has(finding.ItemType) && typeof Dashboard.itemIdentifier?.show === 'function') {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'emby-button raised df-identify';
            button.textContent = 'Identify';
            const status = document.createElement('span');
            status.className = 'df-status';
            button.addEventListener('click', () => {
                Dashboard.itemIdentifier.show(finding.ItemId, ApiClient.serverId())
                    .then(() => {
                        status.textContent = 'Identified — rescan to refresh';
                        button.disabled = true;
                    })
                    .catch(() => {
                        // Cancelled or nothing applied: DF-R7.2 leaves the row unchanged.
                    });
            });
            td.append(button, status);
        }
        return td;
    }

    function buildRows(findings) {
        const fragment = document.createDocumentFragment();
        let band = false;
        let lastGroup = null;
        for (const finding of findings) {
            const group = finding.Group ?? null;
            if (group !== null && group !== lastGroup) band = !band;
            lastGroup = group;
            const row = document.createElement('tr');
            row.dataset.itemId = finding.ItemId;
            if (group !== null && band) row.classList.add('df-band');
            row.append(
                cell(checkLabel(finding.Check)),
                cell(group ?? ''),
                cell(finding.LibraryName),
                cell(finding.ItemType),
                cell(finding.Name),
                cell(finding.Year ?? ''),
                cell(episodeCode(finding)),
                cell(finding.Path ?? '', 'df-path'),
                cell(formatSize(finding.SizeBytes)),
                cell(providerIdsText(finding.ProviderIds)),
                cell(finding.Reason),
                actionsCell(finding)
            );
            fragment.appendChild(row);
        }
        return fragment;
    }

    function renderResults(result) {
        $('#dfResults').hidden = false;
        $('#dfStats').textContent = `Scanned ${result.ScannedItemCount} items in ${(result.DurationMs / 1000).toFixed(1)} s`;
        const summary = document.createDocumentFragment();
        for (const check of CHECKS.filter((c) => c.id in result.Summary)) {
            const count = result.Summary[check.id];
            const noun = GROUPED.has(check.id) ? (count === 1 ? 'group' : 'groups') : (count === 1 ? 'item' : 'items');
            const li = document.createElement('li');
            li.textContent = `${check.label}: ${count} ${noun}`;
            summary.appendChild(li);
        }
        $('#dfSummary').replaceChildren(summary);
        const hasFindings = result.Findings.length > 0;
        $('#dfEmpty').hidden = hasFindings;
        $('#dfTableWrap').hidden = !hasFindings;
        $('#dfRows').replaceChildren(buildRows(result.Findings));
    }

    async function scan() {
        state.scanning = true;
        updateButtons();
        Dashboard.showLoadingMsg();
        try {
            state.result = await ApiClient.ajax({
                type: 'POST',
                url: ApiClient.getUrl('DupeFinder/Scan'),
                data: JSON.stringify({ LibraryIds: selected('library'), Checks: selected('check') }),
                contentType: 'application/json',
                dataType: 'json'
            });
            renderResults(state.result);
        } catch (err) {
            Dashboard.alert({ title: 'Scan failed', message: await errorMessage(err) });
        } finally {
            state.scanning = false;
            Dashboard.hideLoadingMsg();
            updateButtons();
        }
    }

    function downloadCsv() {
        const blob = new Blob([toCsv(state.result.Findings)], { type: 'text/csv;charset=utf-8' });
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = csvFileName(new Date());
        document.body.appendChild(anchor);
        anchor.click();
        anchor.remove();
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    }

    $('#dfSelectAll').addEventListener('change', (event) => {
        for (const box of boxes('library')) box.checked = event.target.checked;
        updateButtons();
    });
    $('#dfScan').addEventListener('click', scan);
    $('#dfDownload').addEventListener('click', downloadCsv);

    view.addEventListener('viewshow', () => {
        // viewshow fires on every visit, including restores from the view cache: build once (Review Focus 4).
        if (state.loaded) return;
        state.loaded = true;
        renderChecks();
        loadLibraries();
    });
}
```

- [ ] **Step 4: Run the page tests**

Run: `node --test tests/web/*.test.mjs`
Expected: `# pass 12`, `# fail 0`.

- [ ] **Step 5: Write the page markup**

Create `src/Jellyfin.Plugin.DupeFinder/Web/dupefinder.html` (no `${` anywhere in this file — ADR-0007):

```html
<div id="dupeFinderPage" data-role="page" class="page type-interior pluginConfigurationPage" data-controller="__plugin/dupefinderjs">
    <style>
        /* specs/design.md: the only plugin-defined colour token. */
        #dupeFinderPage { --df-group-band: rgba(127, 127, 127, 0.12); }
        #dupeFinderPage [hidden] { display: none !important; }
        #dupeFinderPage .df-band { background: var(--df-group-band); }
        #dupeFinderPage .df-tableWrap { overflow-x: auto; }
        #dupeFinderPage .df-actions { white-space: nowrap; }
        #dupeFinderPage .df-path { word-break: break-all; min-width: 16em; }
        #dupeFinderPage .df-status { margin-left: 0.5em; }
    </style>
    <div data-role="content">
        <div class="content-primary">
            <div class="verticalSection">
                <div class="sectionTitleContainer flex align-items-center">
                    <h2 class="sectionTitle">Duplicate &amp; Unmatched Finder</h2>
                </div>
                <p class="fieldDescription">Find duplicate and unmatched items in the selected libraries. Nothing changes until you use Identify or Edit metadata.</p>
            </div>

            <div class="verticalSection">
                <h3 class="sectionTitle">Libraries</h3>
                <div class="checkboxContainer">
                    <label class="emby-checkbox-label">
                        <input is="emby-checkbox" type="checkbox" id="dfSelectAll" checked>
                        <span>Select all</span>
                    </label>
                </div>
                <div id="dfLibraries"></div>
            </div>

            <div class="verticalSection">
                <h3 class="sectionTitle">Duplicates</h3>
                <div id="dfDuplicateChecks"></div>
                <h3 class="sectionTitle">Unmatched</h3>
                <div id="dfUnmatchedChecks"></div>
            </div>

            <div class="verticalSection">
                <button is="emby-button" type="button" id="dfScan" class="raised button-submit block" disabled>
                    <span>Scan</span>
                </button>
                <button is="emby-button" type="button" id="dfDownload" class="raised block" disabled>
                    <span>Download CSV</span>
                </button>
            </div>

            <div class="verticalSection" id="dfResults" hidden>
                <h3 class="sectionTitle">Results</h3>
                <p id="dfStats"></p>
                <ul id="dfSummary"></ul>
                <p id="dfEmpty" hidden>No issues found for the selected libraries and checks.</p>
                <div id="dfTableWrap" class="df-tableWrap" hidden>
                    <table class="detailTable">
                        <thead>
                            <tr>
                                <th class="detailTableHeaderCell" scope="col">Check</th>
                                <th class="detailTableHeaderCell" scope="col">Group</th>
                                <th class="detailTableHeaderCell" scope="col">Library</th>
                                <th class="detailTableHeaderCell" scope="col">Type</th>
                                <th class="detailTableHeaderCell" scope="col">Name</th>
                                <th class="detailTableHeaderCell" scope="col">Year</th>
                                <th class="detailTableHeaderCell" scope="col">Episode</th>
                                <th class="detailTableHeaderCell" scope="col">Path</th>
                                <th class="detailTableHeaderCell" scope="col">Size</th>
                                <th class="detailTableHeaderCell" scope="col">Provider IDs</th>
                                <th class="detailTableHeaderCell" scope="col">Reason</th>
                                <th class="detailTableHeaderCell" scope="col">Actions</th>
                            </tr>
                        </thead>
                        <tbody id="dfRows"></tbody>
                    </table>
                </div>
            </div>
        </div>
    </div>
</div>
```

- [ ] **Step 6: Register the pages**

In `src/Jellyfin.Plugin.DupeFinder/Jellyfin.Plugin.DupeFinder.csproj`, add before `</Project>`:

```xml
  <ItemGroup>
    <None Remove="Web\dupefinder.html" />
    <None Remove="Web\dupefinder.js" />
    <EmbeddedResource Include="Web\dupefinder.html" />
    <EmbeddedResource Include="Web\dupefinder.js" />
  </ItemGroup>
```

Replace `src/Jellyfin.Plugin.DupeFinder/Plugin.cs` with:

```csharp
using System;
using System.Collections.Generic;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.DupeFinder;

/// <summary>
/// Duplicate &amp; Unmatched Finder. Stores no settings, so it derives from the non-generic <see cref="BasePlugin"/> (ADR-0002).
/// </summary>
public class Plugin : BasePlugin, IHasWebPages
{
    /// <inheritdoc />
    public override string Name => "Duplicate & Unmatched Finder";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("fee5e03c-c3e1-4067-a6a8-0ed6eb63c3a7");

    /// <inheritdoc />
    public override string Description => "Finds duplicate and unmatched items in selected libraries.";

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        var prefix = GetType().Namespace + ".Web.";
        return
        [
            // DF-R1.1: dashboard sidebar entry under "Plugins"; MenuIcon is a Material Icons ligature.
            new PluginPageInfo
            {
                Name = "dupefinder",
                DisplayName = Name,
                EmbeddedResourcePath = prefix + "dupefinder.html",
                EnableInMainMenu = true,
                MenuIcon = "content_copy",
            },

            // ADR-0007: the page's ES-module controller; the .js resource name gives it a JavaScript MIME type.
            new PluginPageInfo
            {
                Name = "dupefinderjs",
                EmbeddedResourcePath = prefix + "dupefinder.js",
            },
        ];
    }
}
```

- [ ] **Step 7: Build, test and audit**

```bash
export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet build -c Release && dotnet test && node --test tests/web/*.test.mjs
DLL=src/Jellyfin.Plugin.DupeFinder/bin/Release/net10.0/Jellyfin.Plugin.DupeFinder.dll
grep -a -o 'Jellyfin\.Plugin\.DupeFinder\.Web\.dupefinder\.[a-z]*' "$DLL" | sort -u
grep -n 'innerHTML' src/Jellyfin.Plugin.DupeFinder/Web/dupefinder.js
grep -c '\${' src/Jellyfin.Plugin.DupeFinder/Web/dupefinder.html
grep -noE '#[0-9a-fA-F]{3,8}\b|rgba?\(' src/Jellyfin.Plugin.DupeFinder/Web/dupefinder.html src/Jellyfin.Plugin.DupeFinder/Web/dupefinder.js
```

Expected:
- build `0 Warning(s)`/`0 Error(s)`; dotnet tests pass (47); node `# fail 0`;
- resource names `Jellyfin.Plugin.DupeFinder.Web.dupefinder.html` and `Jellyfin.Plugin.DupeFinder.Web.dupefinder.js`;
- exactly one `innerHTML` line — the static checkbox template (Review Focus 3);
- `0` occurrences of `${` in the HTML;
- exactly one colour value: the `rgba(127, 127, 127, 0.12)` token line (DS-AC1).

- [ ] **Step 8: Commit**

```bash
git add src tests/web
git commit -m "DF-R1.1 DF-R5-R8: dashboard page, row actions and CSV export (AC-14)" -m "<attribution lines>"
```

---

### Task 7: Live UI verification

**Files:**
- Create: `tests/e2e/ui-check.mjs`
- Create (generated): `tests/e2e/package.json`, `tests/e2e/package-lock.json`

**Interfaces:**
- Consumes: DOM contract from Task 6; deployed plugin with the page.
- Produces: verified AC-8, AC-12, AC-13, Review Focus 4, DF-R1 route guard, DS-AC1–3 evidence (screenshots under `/tmp/dupefinder-e2e/`, which is outside the repo and never committed).

- [ ] **Step 1 (🧑 CONTROLLER): Install Playwright (approved in Task 1 Step 1)**

```bash
npm install --prefix tests/e2e --save-dev playwright@1
npx --prefix tests/e2e playwright install chromium
```

Expected: `tests/e2e/package.json` lists `playwright`; Chromium download completes.

- [ ] **Step 2: Write the UI check**

Create `tests/e2e/ui-check.mjs`:

```js
// Live UI checks: AC-8, AC-12, AC-13, Review Focus 4, and the DF-R1 route guard for non-admins.
// Configuration only from environment variables, never written to disk (DF-C4):
//   JF_URL, JF_ADMIN_USER, JF_ADMIN_PW (optional), JF_NONADMIN_USER (optional), JF_NONADMIN_PW (optional),
//   OUT_DIR (optional, default /tmp/dupefinder-e2e; screenshots and the CSV land here)
import { mkdirSync } from 'node:fs';
import { chromium } from 'playwright';

const base = (process.env.JF_URL ?? '').replace(/\/$/, '');
const outDir = process.env.OUT_DIR ?? '/tmp/dupefinder-e2e';
if (!base || !process.env.JF_ADMIN_USER) {
    console.error('Set JF_URL and JF_ADMIN_USER');
    process.exit(2);
}
mkdirSync(outDir, { recursive: true });

// The view cache can keep an older copy of the page hidden in the DOM; always target the visible one.
const P = '#dupeFinderPage:not(.hide)';
const pluginUrl = `${base}/web/#/configurationpage?name=dupefinder`;
const failures = [];

function check(ok, message) {
    console.log(`${ok ? 'PASS' : 'FAIL'} ${message}`);
    if (!ok) failures.push(message);
}

async function login(page, user, password) {
    await page.goto(`${base}/web/#/login`);
    const name = page.locator('#txtManualName');
    await name.waitFor({ state: 'attached', timeout: 30000 });
    if (!(await name.isVisible())) {
        await page.locator('.btnManual').click();
    }
    await name.fill(user);
    await page.locator('#txtManualPassword').fill(password);
    await page.locator('.manualLoginForm button[type="submit"]').click();
    await page.waitForURL(/#\/home/, { timeout: 30000 });
}

async function waitForScanToFinish(page) {
    await page.waitForFunction((sel) => {
        const button = document.querySelector(`${sel} #dfScan`);
        return button && !button.disabled;
    }, P, { timeout: 600000 });
}

const browser = await chromium.launch();
try {
    const context = await browser.newContext({ acceptDownloads: true, viewport: { width: 1440, height: 900 } });
    const page = await context.newPage();
    let scanRequests = 0;
    page.on('request', (request) => {
        if (request.url().includes('/DupeFinder/Scan')) scanRequests++;
    });

    await login(page, process.env.JF_ADMIN_USER, process.env.JF_ADMIN_PW ?? '');

    // AC-8: the dashboard sidebar link opens the page.
    await page.goto(`${base}/web/#/dashboard`);
    const menuLink = page.getByText('Duplicate & Unmatched Finder', { exact: true }).first();
    await menuLink.waitFor({ timeout: 30000 });
    await menuLink.click();
    await page.waitForURL(/configurationpage\?name=dupefinder/, { timeout: 30000 });
    const libraryBoxes = page.locator(`${P} #dfLibraries input[type="checkbox"]`);
    await libraryBoxes.first().waitFor({ state: 'attached', timeout: 30000 });
    const libraryCount = await libraryBoxes.count();
    check(libraryCount > 0, `AC-8 page opened from the sidebar with ${libraryCount} libraries`);
    await page.screenshot({ path: `${outDir}/01-form.png`, fullPage: true });

    // AC-12: every check on, all libraries, scan.
    for (const box of await page.locator(`${P} #dfDuplicateChecks input, ${P} #dfUnmatchedChecks input`).all()) {
        if (!(await box.isChecked())) await box.check({ force: true });
    }
    await page.click(`${P} #dfScan`);
    await page.locator(`${P} #dfResults`).waitFor({ state: 'visible', timeout: 600000 });
    await waitForScanToFinish(page);
    const stats = (await page.textContent(`${P} #dfStats`)).trim();
    check(/^Scanned \d+ items in \d+\.\d s$/.test(stats), `AC-12 stats line "${stats}"`);
    const summary = await page.locator(`${P} #dfSummary li`).allTextContents();
    check(summary.length === 9, `AC-12 nine summary lines: ${summary.join(' | ')}`);
    const rows = page.locator(`${P} #dfRows tr`);
    const rowCount = await rows.count();
    console.log(`TABLE_ROWS ${rowCount}`);
    await page.screenshot({ path: `${outDir}/02-results.png` });

    if (rowCount > 0) {
        const [download] = await Promise.all([page.waitForEvent('download'), page.click(`${P} #dfDownload`)]);
        const fileName = download.suggestedFilename();
        check(/^dupefinder-\d{8}-\d{4}\.csv$/.test(fileName), `AC-12 CSV file name ${fileName}`);
        await download.saveAs(`${outDir}/${fileName}`);
        console.log(`CSV ${outDir}/${fileName}`);
    }

    // AC-13: Identify opens Jellyfin's dialog; cancelling leaves the row unchanged.
    const identify = page.locator(`${P} #dfRows button.df-identify`).first();
    if ((await identify.count()) === 0) {
        check(false, 'AC-13 no Identify button in the results (needs a Movie, Series, album or artist row)');
    } else {
        await identify.click();
        const lookup = page.locator('#txtLookupName');
        const opened = await lookup.waitFor({ state: 'visible', timeout: 30000 }).then(() => true, () => false);
        check(opened, 'AC-13 Identify opens Jellyfin\'s Identify dialog');
        await page.screenshot({ path: `${outDir}/03-identify.png` });
        await page.keyboard.press('Escape');
        if (await lookup.isVisible()) await page.locator('.dialog .btnCancel').first().click();
        await lookup.waitFor({ state: 'hidden', timeout: 15000 }).catch(() => {});
        const identified = await page.locator(`${P} #dfRows .df-status`, { hasText: 'Identified' }).count();
        check(identified === 0, 'AC-13 cancelling Identify leaves rows unchanged');
    }

    if (rowCount > 0) {
        const firstRow = rows.first();
        const itemId = await firstRow.getAttribute('data-item-id');
        const [editor] = await Promise.all([context.waitForEvent('page'), firstRow.locator('a.df-edit').click()]);
        const editorShown = await editor.locator('.editItemMetadataForm').first()
            .waitFor({ state: 'visible', timeout: 30000 }).then(() => true, () => false);
        check(editorShown && editor.url().includes(`#/metadata?id=${itemId}`), 'AC-13 Edit metadata opens the metadata editor for the item');
        await editor.screenshot({ path: `${outDir}/04-metadata.png` });
        await editor.close();

        const [details] = await Promise.all([context.waitForEvent('page'), firstRow.locator('a.df-open').click()]);
        await details.waitForLoadState('domcontentloaded');
        check(details.url().includes(`#/details?id=${itemId}`), 'AC-13 Open opens the item details page');
        await details.close();
    }

    // Review Focus 4: leave and come back; nothing duplicated, one click = one request.
    await page.goto(`${base}/web/#/dashboard`);
    await page.waitForTimeout(2000);
    await page.goto(pluginUrl);
    await page.locator(`${P} #dfScan`).waitFor({ timeout: 30000 });
    check((await libraryBoxes.count()) === libraryCount, 'Review Focus 4: library list not duplicated after revisit');
    const before = scanRequests;
    await page.click(`${P} #dfScan`);
    await waitForScanToFinish(page);
    check(scanRequests - before === 1, `Review Focus 4: one click sent ${scanRequests - before} scan request(s)`);

    // DF-R1: Jellyfin's dashboard route guard keeps non-admins off the page.
    if (process.env.JF_NONADMIN_USER) {
        const other = await browser.newContext();
        const userPage = await other.newPage();
        await login(userPage, process.env.JF_NONADMIN_USER, process.env.JF_NONADMIN_PW ?? '');
        await userPage.goto(pluginUrl);
        await userPage.waitForTimeout(5000);
        check((await userPage.locator('#dfScan').count()) === 0, 'DF-R1 a non-admin cannot open the plugin page');
        await other.close();
    }
} finally {
    await browser.close();
}

console.log(`${failures.length} failure(s)`);
process.exit(failures.length ? 1 : 0);
```

- [ ] **Step 3 (🧑 CONTROLLER + USER): Deploy the page build**

```bash
LAN_IP="$(ipconfig getifaddr en1 || ipconfig getifaddr en0)"
PLUGIN_VERSION=0.9.1.0 scripts/build-repo.sh "http://$LAN_IP:8765"
```

Make sure `scripts/serve-repo.sh` is still running (restart it in the background if not). Ask the user to update the plugin to 0.9.1.0 from the catalog and restart Jellyfin. End the turn and wait.

- [ ] **Step 4: Run the UI check and verify the CSV**

```bash
set -o pipefail
mkdir -p /tmp/dupefinder-e2e
JF_URL='<server URL>' JF_ADMIN_USER='<admin>' JF_NONADMIN_USER='<non-admin>' node tests/e2e/ui-check.mjs | tee /tmp/dupefinder-e2e/run.log
CSV="$(sed -n 's/^CSV //p' /tmp/dupefinder-e2e/run.log)"; ROWS="$(sed -n 's/^TABLE_ROWS //p' /tmp/dupefinder-e2e/run.log)"
python3 - "$CSV" "$ROWS" <<'PY'
import csv, sys
path, rows = sys.argv[1], int(sys.argv[2])
raw = open(path, "rb").read()
assert raw.startswith(b"\xef\xbb\xbf"), "missing BOM"
assert b"\r\n" in raw, "missing CRLF"
with open(path, encoding="utf-8-sig", newline="") as handle:
    data = list(csv.reader(handle))
header = ["Check", "Group", "Library", "Type", "Name", "Year", "Season", "Episode", "Series", "Path", "SizeBytes", "ProviderIds", "Reason", "ItemId"]
assert data[0] == header, data[0]
assert len(data) - 1 == rows, (len(data) - 1, rows)
print(f"PASS AC-12 CSV parsed: {rows} rows, header and BOM correct")
PY
```

Expected: every `PASS`, `0 failure(s)`, exit 0, then `PASS AC-12 CSV parsed: …`. Paste the output into the task report. `/tmp/dupefinder-e2e/run.log` contains no credentials (the script never prints them); do not commit anything from `/tmp/dupefinder-e2e/`.

- [ ] **Step 5: Visual check against the design spec (DS-AC1–3)**

Read `/tmp/dupefinder-e2e/01-form.png`, `02-results.png`, `03-identify.png` and `04-metadata.png` with the Read tool. Compare them with `specs/design.md`. Check that:
- the controls look native;
- alternate duplicate groups show the grey band;
- the page never scrolls sideways, and only the table container does;
- text is legible.

DS-AC3 (the light theme) is confirmed by the user. Ask them to look at the page in their own theme, and in Light if they're willing to switch, and reply. Record their answer in the task report.

- [ ] **Step 6: Fix anything that failed**

For any FAIL, use superpowers:systematic-debugging. Fix it in the owning file with a test first where one can exist: `tests/web` for pure functions, xUnit for the server. Re-package as `PLUGIN_VERSION=0.9.1.1`, `0.9.1.2`, and so on, have the user update the plugin, and re-run Step 4 until it is clean. If a selector in `ui-check.mjs` doesn't match Jellyfin 12.2's real markup, check the jellyfin-web `v12.2` source and correct the selector. Do not weaken the assertion.

- [ ] **Step 7: Commit**

```bash
git add tests/e2e/ui-check.mjs tests/e2e/package.json tests/e2e/package-lock.json
git commit -m "AC-8 AC-12 AC-13: live UI checks for the dashboard page" -m "<attribution lines>"
```

(Add any fix commits from Step 6 separately, each naming the spec ID it restores.)

---

### Task 8: Release 1.0.0.0 and handover

**Files:**
- Create: `README.md`
- Modify: `PROGRESS.md`

**Interfaces:**
- Consumes: everything above.
- Produces: an installed and verified 1.0.0.0, a README and a progress entry.

- [ ] **Step 1: Whole-branch review**

Dispatch one fresh reviewer subagent (Opus) with `specs/dupe-finder.md`, `specs/design.md`, the ADRs and `git diff $(git rev-list --max-parents=0 HEAD)..HEAD`. Ask it to report:
- spec gaps;
- DF-C3 and DF-C4 violations (grep the whole tree and git history for IP addresses and user names: `git log -p | grep -nE '([0-9]{1,3}\.){3}[0-9]{1,3}'` must show only `0.0.0.0` and version-like strings, and no `:8096`);
- analyser suppressions;
- any `innerHTML` that takes data.

Fix the confirmed findings, with tests where possible.

- [ ] **Step 2: Write `README.md`**

````markdown
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
dotnet test                                     # detection rules
node --test tests/web/*.test.mjs                          # page helpers (Node 22+)
PLUGIN_VERSION=1.0.0.0 scripts/build-repo.sh http://<host>:8765
scripts/serve-repo.sh                           # serves repo/ on port 8765
```

## Documentation

- Behaviour: `specs/dupe-finder.md`
- Decisions: `docs/adr/`
- Visual rules: `specs/design.md`
````

- [ ] **Step 3 (🧑 CONTROLLER + USER): Release 1.0.0.0**

```bash
LAN_IP="$(ipconfig getifaddr en1 || ipconfig getifaddr en0)"
scripts/build-repo.sh "http://$LAN_IP:8765"     # Directory.Build.props version: 1.0.0.0
```

Ask the user to update to 1.0.0.0 and restart. Then run:

```bash
JF_URL='<server URL>' JF_ADMIN_USER='<admin>' JF_NONADMIN_USER='<non-admin>' EXPECT_VERSION=1.0.0.0 python3 scripts/crosscheck.py
JF_URL='<server URL>' JF_ADMIN_USER='<admin>' JF_NONADMIN_USER='<non-admin>' node tests/e2e/ui-check.mjs
```

Expected: `PASS AC-7 version 1.0.0.0 Active`, every other line `PASS`, and `0 failure(s)` from both. Paste both outputs. Stop the background repository server once the user confirms they're done with it.

- [ ] **Step 4: Update `PROGRESS.md` and commit**

Append a session entry in the format the user's global rules require. Mirror the final task states, list the AC evidence, and name the next item. Then:

```bash
git add README.md PROGRESS.md
git commit -m "DF-R9: release 1.0.0.0, README and progress log" -m "<attribution lines>"
git log --oneline
```

Do not push; the user pushes.

---

## Spec coverage

| Spec item | Task |
|---|---|
| DF-C1 target / packages / ABI | 1 (csproj), 5 (manifest `targetAbi`) |
| DF-C2 no keys / external services | 6 (ApiClient only) |
| DF-C3 read-only | 5 (`JellyfinLibraryItemSource` never saves) |
| DF-C4 no server details on disk | Global Constraints, 5/7 (env-only scripts), 8 Step 1 audit |
| DF-C5 native UI | 6 (markup, DS-AC1 audit), 7 Step 5 |
| §4 definitions (normalised text, item set, version groups) | 1, 3, 5 |
| DF-R1.1 page + sidebar | 6, verified 7 (AC-8) |
| DF-R1.2 admin endpoints | 5, verified 5 Step 8 (AC-9) |
| DF-R1.3 no data in page files | 6 (static HTML/JS only) |
| DF-R2.1–R2.4 endpoint contract | 4, 5 (AC-10) |
| DF-R3.1–R3.6 duplicates | 2, 3, 4 (AC-2–AC-5), live 5 (AC-11) |
| DF-R4.1–R4.4 unmatched / incomplete | 3 (AC-6), live 5 (AC-11) |
| DF-R5 selection and scan | 6, verified 7 (AC-12) |
| DF-R6 results | 6, verified 7 (AC-12) |
| DF-R7 row actions | 6, verified 7 (AC-13) |
| DF-R8 CSV | 6 (AC-14), verified 7 (AC-12) |
| DF-R9 delivery | 5, 8 (AC-7) |
| DS-AC1–3 | 6 Step 7, 7 Step 5 |
