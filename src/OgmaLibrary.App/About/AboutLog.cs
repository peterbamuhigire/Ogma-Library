using Microsoft.Extensions.Logging;

namespace OgmaLibrary.App.About;

/// <summary>Source-generated log events for the About dialog. Messages carry no personal data.</summary>
public static partial class AboutLog
{
    /// <summary>The platform refused or could not open an allowed About link.</summary>
    [LoggerMessage(EventId = 2201, EventName = "about.link.failed", Level = LogLevel.Warning,
        Message = "The About link {LinkId} could not be opened")]
    public static partial void LinkFailed(ILogger logger, string linkId, Exception? exception);
}
