using Avalonia.Controls;

namespace OgmaLibrary.App.About;

/// <summary>
/// Opens an external address (web page, email or phone link) in the platform handler. Only
/// addresses allowed by <see cref="ExternalLinkPolicy"/> are ever handed to the platform.
/// </summary>
public interface IExternalLinkLauncher
{
    /// <summary>Opens <paramref name="uri"/> when the policy allows it.</summary>
    /// <param name="uri">The address.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns><see langword="true"/> when the platform accepted the address.</returns>
    Task<bool> TryOpenAsync(Uri uri, CancellationToken cancellationToken = default);
}

/// <summary>
/// The allow-list for external links opened from the App: the https, mailto and tel schemes,
/// and only the known credit addresses in <see cref="AppCredits.KnownLinks"/>.
/// </summary>
public static class ExternalLinkPolicy
{
    private static readonly HashSet<string> AllowedSchemes =
        new(StringComparer.OrdinalIgnoreCase) { Uri.UriSchemeHttps, Uri.UriSchemeMailto, "tel" };

    /// <summary>Whether <paramref name="uri"/> may be opened.</summary>
    /// <param name="uri">The address.</param>
    /// <returns><see langword="true"/> for an allowed scheme and a known address.</returns>
    public static bool IsAllowed(Uri? uri) =>
        uri is { IsAbsoluteUri: true } &&
        AllowedSchemes.Contains(uri.Scheme) &&
        AppCredits.KnownLinks.Any(known => Uri.Compare(
            known,
            uri,
            UriComponents.AbsoluteUri,
            UriFormat.UriEscaped,
            StringComparison.OrdinalIgnoreCase) == 0);
}

/// <summary>
/// Opens allowed links through Avalonia's <see cref="TopLevel.Launcher"/>. The launcher never
/// throws for a refused address; it returns <see langword="false"/>.
/// </summary>
public sealed class AvaloniaExternalLinkLauncher : IExternalLinkLauncher
{
    private readonly Func<TopLevel?> _topLevel;

    /// <summary>Initializes a new instance of the <see cref="AvaloniaExternalLinkLauncher"/> class.</summary>
    /// <param name="topLevel">Resolves the window whose launcher is used.</param>
    public AvaloniaExternalLinkLauncher(Func<TopLevel?> topLevel)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        _topLevel = topLevel;
    }

    /// <inheritdoc />
    public async Task<bool> TryOpenAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!ExternalLinkPolicy.IsAllowed(uri) || _topLevel() is not { } topLevel)
        {
            return false;
        }

        // The launcher must be called on the UI thread; do not leave it.
        return await topLevel.Launcher.LaunchUriAsync(uri).ConfigureAwait(true);
    }
}
