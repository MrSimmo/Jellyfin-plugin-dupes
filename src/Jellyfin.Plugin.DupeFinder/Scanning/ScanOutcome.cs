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
