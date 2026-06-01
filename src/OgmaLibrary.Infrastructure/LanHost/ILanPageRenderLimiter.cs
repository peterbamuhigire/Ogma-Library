namespace OgmaLibrary.Infrastructure.LanHost;

/// <summary>Limits concurrent LAN page-render work.</summary>
internal interface ILanPageRenderLimiter
{
    /// <summary>Attempts to acquire a render slot without queuing.</summary>
    bool TryAcquire(out IDisposable lease);
}
