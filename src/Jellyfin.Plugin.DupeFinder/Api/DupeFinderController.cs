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
