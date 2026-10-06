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
