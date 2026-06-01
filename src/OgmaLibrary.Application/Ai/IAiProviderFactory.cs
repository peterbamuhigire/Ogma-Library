namespace OgmaLibrary.Application.Ai;

/// <summary>Creates AI providers from user or host settings.</summary>
public interface IAiProviderFactory
{
    /// <summary>Creates an AI provider for the supplied binding.</summary>
    IAiProvider Create(AiProviderBinding binding);
}

/// <summary>Provider binding selected by settings or a future school host.</summary>
public sealed record AiProviderBinding(
    string ProviderKey,
    string Model,
    string? ApiKey = null,
    Uri? BaseAddress = null);
