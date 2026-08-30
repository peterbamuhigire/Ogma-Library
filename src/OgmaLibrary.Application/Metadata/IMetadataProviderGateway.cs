namespace OgmaLibrary.Application.Metadata;

/// <summary>Cached, resilient gateway over deterministic metadata providers.</summary>
public interface IMetadataProviderGateway
{
    /// <summary>Looks up all providers with normalized durable cache semantics.</summary>
    Task<IReadOnlyList<ProviderMetadataResult>> SearchAsync(
        MetadataLookupRequest request,
        CancellationToken cancellationToken = default);
}
