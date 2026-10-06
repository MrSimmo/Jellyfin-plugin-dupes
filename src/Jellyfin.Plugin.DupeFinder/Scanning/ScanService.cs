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
