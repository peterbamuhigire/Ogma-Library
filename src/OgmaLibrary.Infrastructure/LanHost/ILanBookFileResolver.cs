namespace OgmaLibrary.Infrastructure.LanHost;

/// <summary>Resolves catalogue book IDs to safe local PDF paths for Host-mode streaming.</summary>
internal interface ILanBookFileResolver
{
    /// <summary>Finds a present PDF path for the supplied book ID, or null when none is streamable.</summary>
    Task<string?> ResolveAsync(string bookId, CancellationToken cancellationToken = default);
}
