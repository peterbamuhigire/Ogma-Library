namespace OgmaLibrary.Application.LanHost;

/// <summary>Issues and revokes authenticated LAN client sessions.</summary>
public interface IClientSessionService
{
    /// <summary>Issues a scoped session token for an enrolled LAN client.</summary>
    Task<ClientSessionResult> IssueAsync(
        ClientSessionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Returns whether a session token is currently valid.</summary>
    Task<bool> IsValidAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>Revokes every active session, used when Host mode stops.</summary>
    Task RevokeAllAsync(CancellationToken cancellationToken = default);
}

