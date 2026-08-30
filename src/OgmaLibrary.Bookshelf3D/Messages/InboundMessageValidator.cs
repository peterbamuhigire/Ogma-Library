using System.Text.RegularExpressions;

namespace OgmaLibrary.Bookshelf3D.Messages;

/// <summary>Validates JavaScript to C# bridge messages before dispatch.</summary>
public static partial class InboundMessageValidator
{
    private const double SceneLimit = 1_000.0;

    /// <summary>Validates one inbound bridge message.</summary>
    public static InboundMessageValidationResult Validate(InboundMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return message switch
        {
            BookClickedMessage clicked => ValidateBookId(clicked.BookId),
            BookDoubleClickedMessage doubleClicked => ValidateBookId(doubleClicked.BookId),
            BookHoveredMessage hovered => ValidateBookId(hovered.BookId),
            CameraChangedMessage cameraChanged => ValidateCamera(cameraChanged.Camera),
            WebGl2StatusMessage => InboundMessageValidationResult.Dispatch,
            PerformanceWarningMessage warning => ValidateFinite(warning.AverageFps, nameof(warning.AverageFps)),
            PerformanceMetricsMessage metrics => ValidateMetrics(metrics),
            UnknownInboundMessage unknown => InboundMessageValidationResult.Discard($"Unknown inbound message type '{unknown.UnknownType}'."),
            _ => InboundMessageValidationResult.Discard($"Unsupported inbound message type '{message.Type}'."),
        };
    }

    private static InboundMessageValidationResult ValidateBookId(string bookId)
    {
        if (!BookIdPattern().IsMatch(bookId))
        {
            return InboundMessageValidationResult.Discard("BookId must be a local 26-character Crockford base-32 identifier.");
        }

        return InboundMessageValidationResult.Dispatch;
    }

    private static InboundMessageValidationResult ValidateCamera(CameraState camera)
    {
        double[] values =
        [
            camera.X,
            camera.Y,
            camera.Z,
            camera.TargetX,
            camera.TargetY,
            camera.TargetZ,
        ];

        if (values.Any(value => !double.IsFinite(value) || Math.Abs(value) > SceneLimit))
        {
            return InboundMessageValidationResult.Discard("Camera coordinates must be finite and inside scene bounds.");
        }

        if (!double.IsFinite(camera.Fov) || camera.Fov is < 1.0 or > 120.0)
        {
            return InboundMessageValidationResult.Discard("Camera field of view must be finite and between 1 and 120 degrees.");
        }

        return InboundMessageValidationResult.Dispatch;
    }

    private static InboundMessageValidationResult ValidateFinite(double value, string propertyName) =>
        double.IsFinite(value)
            ? InboundMessageValidationResult.Dispatch
            : InboundMessageValidationResult.Discard($"{propertyName} must be finite.");

    private static InboundMessageValidationResult ValidateMetrics(PerformanceMetricsMessage metrics)
    {
        if (!double.IsFinite(metrics.AverageFps) || !double.IsFinite(metrics.FrameTimeMs))
        {
            return InboundMessageValidationResult.Discard("Performance metrics must be finite.");
        }

        if (metrics.AverageFps < 0 || metrics.FrameTimeMs < 0 ||
            metrics.SceneBookCount < 0 || metrics.ResidentBookCount < 0 ||
            metrics.ResidentBookCount > metrics.SceneBookCount ||
            metrics.SceneBookCount > 100_000)
        {
            return InboundMessageValidationResult.Discard("Performance metrics are outside the permitted bounds.");
        }

        return InboundMessageValidationResult.Dispatch;
    }

    [GeneratedRegex("^[0-9A-HJKMNP-TV-Z]{26}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BookIdPattern();
}

/// <summary>Validation result for an inbound WebView bridge message.</summary>
/// <param name="ShouldDispatch">Whether the message may be dispatched to feature handlers.</param>
/// <param name="Error">Validation error, if discarded.</param>
public sealed record InboundMessageValidationResult(bool ShouldDispatch, string? Error)
{
    /// <summary>A message that may be dispatched.</summary>
    public static InboundMessageValidationResult Dispatch { get; } = new(true, null);

    /// <summary>Creates a discarded-message result.</summary>
    public static InboundMessageValidationResult Discard(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new InboundMessageValidationResult(false, error);
    }
}
