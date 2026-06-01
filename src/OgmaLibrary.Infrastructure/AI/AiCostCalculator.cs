using OgmaLibrary.Application.Ai;

namespace OgmaLibrary.Infrastructure.AI;

/// <summary>In-memory AI model price table used for per-call cost attribution.</summary>
public sealed class AiCostCalculator : IAiCostCalculator
{
    private readonly Dictionary<string, AiModelPrice> _prices;

    /// <summary>Initializes a new instance of <see cref="AiCostCalculator"/>.</summary>
    public AiCostCalculator(IEnumerable<AiModelPrice>? prices = null)
    {
        _prices = (prices ?? [])
            .ToDictionary(
                price => Key(price.Provider, price.Model),
                StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public decimal? EstimateCostUsd(AiRequest request, AiCompletion completion)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(completion);
        if (completion.PromptTokens is null && completion.CompletionTokens is null)
        {
            return null;
        }

        _prices.TryGetValue(Key(request.Provider, request.Model), out AiModelPrice? price);
        decimal inputCost = CostForTokens(completion.PromptTokens, price?.InputUsdPerMillionTokens ?? 0m);
        decimal outputCost = CostForTokens(completion.CompletionTokens, price?.OutputUsdPerMillionTokens ?? 0m);
        return decimal.Round(inputCost + outputCost, 8, MidpointRounding.AwayFromZero);
    }

    private static decimal CostForTokens(int? tokens, decimal usdPerMillionTokens)
    {
        int tokenCount = tokens.GetValueOrDefault();
        return tokenCount <= 0
            ? 0m
            : (tokenCount / 1_000_000m) * usdPerMillionTokens;
    }

    private static string Key(string provider, string model) => $"{provider}:{model}";
}

/// <summary>Provider/model token pricing in USD per one million tokens.</summary>
public sealed record AiModelPrice(
    string Provider,
    string Model,
    decimal InputUsdPerMillionTokens,
    decimal OutputUsdPerMillionTokens);
