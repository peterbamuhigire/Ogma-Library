using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.LanHost;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Infrastructure.LanHost;

/// <summary>SQLite-backed LAN Host-mode settings repository.</summary>
internal sealed class HostModeSettingsRepository : IHostModeSettingsRepository
{
    private const string SingletonSettingsId = "default";
    private static readonly HostModeSettings Defaults =
        new(IsEnabled: false, Port: 7473, HostContentDeliveryMode.PageRender, "Ogma Library");

    private readonly IDbContextFactory<CatalogueDbContext>? _contextFactory;
    private readonly CatalogueDbContext? _context;

    [ActivatorUtilitiesConstructor]
    public HostModeSettingsRepository(IDbContextFactory<CatalogueDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    internal HostModeSettingsRepository(CatalogueDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public async Task<HostModeSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);

        HostModeSettingsRow? row = await lease.Context.HostModeSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SettingsId == SingletonSettingsId, cancellationToken)
            .ConfigureAwait(false);

        return row is null
            ? Defaults
            : new HostModeSettings(row.IsEnabled, row.Port, (HostContentDeliveryMode)row.ContentMode, row.DisplayName);
    }

    /// <inheritdoc />
    public async Task SaveAsync(HostModeSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Validate(settings);

        using CatalogueContextLease lease = await CatalogueContextLease
            .CreateAsync(_contextFactory, _context, cancellationToken)
            .ConfigureAwait(false);

        HostModeSettingsRow? row = await lease.Context.HostModeSettings
            .FirstOrDefaultAsync(x => x.SettingsId == SingletonSettingsId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            row = new HostModeSettingsRow { SettingsId = SingletonSettingsId };
            lease.Context.HostModeSettings.Add(row);
        }

        row.IsEnabled = settings.IsEnabled;
        row.Port = settings.Port;
        row.ContentMode = (int)settings.ContentMode;
        row.DisplayName = settings.DisplayName.Trim();
        row.UpdatedUtc = DateTimeOffset.UtcNow;

        await lease.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void Validate(HostModeSettings settings)
    {
        if (settings.Port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), settings.Port, "LAN Host port must be between 1 and 65535.");
        }

        if (string.IsNullOrWhiteSpace(settings.DisplayName))
        {
            throw new ArgumentException("LAN Host display name is required.", nameof(settings));
        }

        if (!Enum.IsDefined(settings.ContentMode))
        {
            throw new ArgumentOutOfRangeException(nameof(settings), settings.ContentMode, "Unknown LAN Host content mode.");
        }
    }
}
