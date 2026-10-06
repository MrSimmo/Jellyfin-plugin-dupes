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
