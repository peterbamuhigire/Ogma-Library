using System.Globalization;
using OgmaLibrary.Application.Ai;

namespace OgmaLibrary.Infrastructure.AI;

/// <summary>Culture-aware formatter for AI cost estimates.</summary>
public sealed class AiCostFormatter : IAiCostFormatter
{
    /// <inheritdoc />
    public string FormatUsd(decimal? estimatedCostUsd, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return estimatedCostUsd is null
            ? "n/a"
            : string.Format(culture, "USD {0:0.000000}", estimatedCostUsd.Value);
    }
}
