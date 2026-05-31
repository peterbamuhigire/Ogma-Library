using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.Application.Navigation;

namespace OgmaLibrary.App;

/// <summary>
/// A lazy proxy for <see cref="IBookDetailNavigationService"/> and
/// <see cref="IReaderNavigationService"/> that defers resolution until the first
/// call. Used to break the circular dependency in the DI composition
/// (MainShellViewModel → CatalogueViewModel → IBookDetailNavigationService →
/// MainShellViewModel).
/// </summary>
internal sealed class NavigationServiceProxy :
    IBookDetailNavigationService,
    IReaderNavigationService
{
    private readonly Func<IShellNavigationTarget> _factory;

    /// <summary>
    /// Initializes a new instance of <see cref="NavigationServiceProxy"/>.
    /// </summary>
    /// <param name="factory">
    /// A delegate that returns the real navigation target once it is available.
    /// </param>
    public NavigationServiceProxy(Func<IShellNavigationTarget> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    /// <inheritdoc />
    public Task OpenDetailAsync(string bookId, CancellationToken cancellationToken = default) =>
        _factory().OpenDetailAsync(bookId, cancellationToken);

    /// <inheritdoc />
    public Task OpenReaderAsync(string bookId, int? pageHint = null, CancellationToken cancellationToken = default) =>
        _factory().OpenReaderAsync(bookId, pageHint, cancellationToken);
}
