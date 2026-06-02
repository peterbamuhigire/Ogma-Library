using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Application.Reader;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

/// <summary>
/// Resolves reader files from the local catalogue in Standalone mode and from
/// Host file-stream materialization in Client mode.
/// </summary>
public sealed class ClassroomBookFileLocator : IBookFileLocator
{
    private readonly IBookFileLocator _localLocator;
    private readonly IClassroomModeService _modeService;
    private readonly IClassroomHostConnectionService _connectionService;
    private readonly IClassroomBookFileMaterializer _materializer;

    /// <summary>Initializes a new instance of <see cref="ClassroomBookFileLocator"/>.</summary>
    /// <param name="localLocator">The normal standalone catalogue-backed file locator.</param>
    /// <param name="modeService">The runtime mode service.</param>
    /// <param name="connectionService">The active Client-mode Host connection store.</param>
    /// <param name="materializer">The Host PDF stream materializer.</param>
    public ClassroomBookFileLocator(
        IBookFileLocator localLocator,
        IClassroomModeService modeService,
        IClassroomHostConnectionService connectionService,
        IClassroomBookFileMaterializer materializer)
    {
        _localLocator = localLocator ?? throw new ArgumentNullException(nameof(localLocator));
        _modeService = modeService ?? throw new ArgumentNullException(nameof(modeService));
        _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
        _materializer = materializer ?? throw new ArgumentNullException(nameof(materializer));
    }

    /// <inheritdoc />
    public async Task<string?> LocateAsync(string bookId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        ClassroomModeSettings mode = await _modeService.GetModeAsync(ct).ConfigureAwait(false);
        if (mode.Mode != LibraryRuntimeMode.ConnectToHost)
        {
            return await _localLocator.LocateAsync(bookId, ct).ConfigureAwait(false);
        }

        ClassroomHostConnection? connection = await _connectionService.GetActiveAsync(ct).ConfigureAwait(false);
        if (connection is null)
        {
            return null;
        }

        return await _materializer
            .MaterializeAsync(connection.Request, connection.SessionToken, bookId, ct)
            .ConfigureAwait(false);
    }
}
