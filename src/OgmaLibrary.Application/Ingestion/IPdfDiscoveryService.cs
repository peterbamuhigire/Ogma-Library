using System.Threading.Channels;

namespace OgmaLibrary.Application.Ingestion;

/// <summary>
/// Recursively enumerates PDF files under a library root, honoring an excluded-folder
/// list, and streams results via a channel (FR-LIB-002). Path-safe: no traversal
/// outside the root is permitted.
/// </summary>
public interface IPdfDiscoveryService
{
    /// <summary>
    /// Starts recursive PDF discovery and writes <see cref="DiscoveredFile"/> items
    /// to the supplied channel writer. Completes the writer when enumeration finishes
    /// or the token is cancelled.
    /// </summary>
    /// <param name="rootPath">The absolute path to the library root.</param>
    /// <param name="excludedFolders">Folder names or relative sub-paths to exclude.</param>
    /// <param name="writer">The channel writer that receives discovered files.</param>
    /// <param name="directoryDiagnosticSink">Optional sink for safe per-directory progress and error diagnostics.</param>
    /// <param name="resumeAfterRelativeDirectory">Optional last completed directory from an interrupted pass.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task DiscoverAsync(
        string rootPath,
        IReadOnlyList<string> excludedFolders,
        ChannelWriter<DiscoveredFile> writer,
        Func<DiscoveryDirectoryDiagnostic, ValueTask>? directoryDiagnosticSink = null,
        string? resumeAfterRelativeDirectory = null,
        CancellationToken cancellationToken = default);
}
