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
