namespace OgmaLibrary.Application.LanHost;

/// <summary>Controls the opt-in LAN Library Host runtime.</summary>
public interface ILibraryHostService
{
    /// <summary>Starts Host mode using persisted settings.</summary>
    Task<LibraryHostStatus> StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops Host mode and revokes active LAN client sessions.</summary>
    Task<LibraryHostStatus> StopAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the current Host-mode status without starting a listener.</summary>
    Task<LibraryHostStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}

