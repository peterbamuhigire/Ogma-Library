using Microsoft.EntityFrameworkCore;

namespace OgmaLibrary.Infrastructure.Catalogue;

/// <summary>
/// Provides a per-operation <see cref="CatalogueDbContext"/> for singleton
/// services while preserving direct-context constructors used by integration tests.
/// </summary>
public sealed class CatalogueContextLease : IAsyncDisposable, IDisposable
{
    private readonly bool _ownsContext;

    private CatalogueContextLease(CatalogueDbContext context, bool ownsContext)
    {
        Context = context;
        _ownsContext = ownsContext;
    }

    /// <summary>
    /// Gets the leased catalogue context.
    /// </summary>
    public CatalogueDbContext Context { get; }

    /// <summary>
    /// Creates a lease backed by a new factory-created context when available,
    /// otherwise by the supplied direct test context.
    /// </summary>
    /// <param name="contextFactory">The runtime factory for per-operation contexts.</param>
    /// <param name="context">An optional direct context used by integration tests.</param>
    /// <param name="cancellationToken">A token to cancel factory creation.</param>
    /// <returns>A context lease that disposes only contexts it owns.</returns>
    public static async Task<CatalogueContextLease> CreateAsync(
        IDbContextFactory<CatalogueDbContext>? contextFactory,
        CatalogueDbContext? context,
        CancellationToken cancellationToken)
    {
        if (contextFactory is not null)
        {
            CatalogueDbContext created = await contextFactory.CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);
            return new CatalogueContextLease(created, ownsContext: true);
        }

        if (context is not null)
        {
            return new CatalogueContextLease(context, ownsContext: false);
        }

        throw new InvalidOperationException("A catalogue context or context factory is required.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsContext)
        {
            Context.Dispose();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_ownsContext)
        {
            await Context.DisposeAsync().ConfigureAwait(false);
        }
    }
}
