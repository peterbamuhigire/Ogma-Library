using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Pdf;
using OgmaLibrary.Workers;

namespace OgmaLibrary.App.Startup;

internal sealed class CatalogueMigrationStartupTask : IApplicationStartupTask
{
    private readonly CatalogueMigrator _migrator;

    public CatalogueMigrationStartupTask(CatalogueMigrator migrator) =>
        _migrator = migrator ?? throw new ArgumentNullException(nameof(migrator));

    public string Name => "catalogue.migration";

    public StartupTaskCriticality Criticality => StartupTaskCriticality.Required;

    public string FailureMessage =>
        "Ogma could not prepare the catalogue database. Your PDF files were not changed. Retry startup or export diagnostics.";

    public Task ExecuteAsync(CancellationToken cancellationToken) =>
        _migrator.ApplyAsync(cancellationToken);
}

internal sealed class JobRecoveryStartupTask : IApplicationStartupTask
{
    private readonly JobRecoveryService _recovery;
    private readonly PdfWorkerClient? _pdfWorker;

    public JobRecoveryStartupTask(JobRecoveryService recovery, PdfWorkerClient? pdfWorker = null)
    {
        _recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
        _pdfWorker = pdfWorker;
    }

    public string Name => "jobs.recovery";

    public StartupTaskCriticality Criticality => StartupTaskCriticality.Optional;

    public string FailureMessage =>
        "Interrupted background jobs could not be recovered. The catalogue can open, but processing stays paused until retry succeeds.";

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await _recovery.RecoverAsync(cancellationToken).ConfigureAwait(false);

        // Sept-23 Phase 06 (T06.10): sandboxes of a crashed process hold whole-PDF copies.
        _pdfWorker?.CleanOrphanedSandboxes(TimeSpan.FromHours(1));
    }
}

/// <summary>
/// Requeues cover jobs that failed with the known pre-fix null-bitmap error so an
/// application upgrade repairs existing libraries without rerunning unrelated work.
/// </summary>
internal sealed class ThumbnailRepairStartupTask : IApplicationStartupTask
{
    private const string KnownNullBitmapError = "Object reference not set to an instance of an object.";
    private readonly IDbContextFactory<CatalogueDbContext> _contextFactory;

    public ThumbnailRepairStartupTask(IDbContextFactory<CatalogueDbContext> contextFactory) =>
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));

    public string Name => "assets.thumbnail-repair";

    public StartupTaskCriticality Criticality => StartupTaskCriticality.Optional;

    public string FailureMessage =>
        "Previously failed cover images could not be queued for repair. Use the library health retry action.";

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        CatalogueDbContext context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        try
        {
            List<OgmaLibrary.Infrastructure.Catalogue.Entities.JobRow> jobs = await context.Jobs
                .Where(job => job.JobType == "ThumbnailGeneration" &&
                              job.Status == 3 &&
                              job.ErrorMessage == KnownNullBitmapError &&
                              job.Payload != null)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            bool changed = false;
            foreach (OgmaLibrary.Infrastructure.Catalogue.Entities.JobRow job in jobs)
            {
                if (!File.Exists(job.Payload))
                {
                    continue;
                }

                // Sept-23 Phase 06 (T06.2): a repair is not an attempt. The earlier failures
                // came from a fixed code defect, so the job gets a fresh attempt budget.
                job.Status = 0;
                job.RetryCount = 0;
                job.RequeueCount += 1;
                job.NextAttemptUtc = null;
                job.ErrorMessage = null;
                job.StartedUtc = null;
                job.CompletedUtc = null;
                changed = true;
            }

            if (changed)
            {
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            await context.DisposeAsync().ConfigureAwait(false);
        }
    }
}

internal sealed class HostedServicesStartupTask : IApplicationStartupTask, IApplicationStoppableTask
{
    private readonly IReadOnlyList<IHostedService> _hostedServices;
    private readonly HashSet<IHostedService> _started = [];

    public HostedServicesStartupTask(IEnumerable<IHostedService> hostedServices)
    {
        ArgumentNullException.ThrowIfNull(hostedServices);
        _hostedServices = hostedServices.ToList();
    }

    public string Name => "workers.start";

    public StartupTaskCriticality Criticality => StartupTaskCriticality.Optional;

    public string FailureMessage =>
        "Background processing could not start. You can browse available catalogue data and retry processing startup.";

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        foreach (IHostedService hostedService in _hostedServices)
        {
            if (_started.Contains(hostedService))
            {
                continue;
            }

            await hostedService.StartAsync(cancellationToken).ConfigureAwait(false);
            _started.Add(hostedService);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (IHostedService hostedService in _started.Reverse().ToList())
        {
            await hostedService.StopAsync(cancellationToken).ConfigureAwait(false);
        }

        _started.Clear();
    }
}

internal sealed class StartupCapabilityProbe : IStartupCapabilityProbe
{
    private readonly ICapabilitySettings _capabilities;
    private readonly PdfWorkerClient _pdfWorker;

    public StartupCapabilityProbe(ICapabilitySettings capabilities, PdfWorkerClient pdfWorker)
    {
        _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        _pdfWorker = pdfWorker ?? throw new ArgumentNullException(nameof(pdfWorker));
    }

    public Task<IReadOnlyList<CapabilityHealth>> ProbeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PdfWorkerAvailability worker = _pdfWorker.GetAvailability();

        // Sept-23 Phase 08 (8.9): the probe reports the effective Settings value (environment
        // override, then the user's choice) in plain words, with stable machine codes.
        bool metadata = _capabilities.IsEnabled(UserCapability.MetadataProviders);
        bool shelf = _capabilities.IsEnabled(UserCapability.ThreeDimensionalShelf);
        bool host = _capabilities.IsEnabled(UserCapability.ClassroomHost);
        IReadOnlyList<CapabilityHealth> result =
        [
            new CapabilityHealth(
                "metadata.external",
                metadata ? CapabilityAvailability.Available : CapabilityAvailability.Disabled,
                metadata ? Source(UserCapability.MetadataProviders) : "off_until_enabled_in_settings",
                metadata
                    ? "Online book-detail lookups are on; provider health is checked only when used."
                    : "Online book-detail lookups are off. Nothing is sent to book-detail services."),
            new CapabilityHealth(
                "ai.external",
                _capabilities.IsAiConfigured ? CapabilityAvailability.Available : CapabilityAvailability.Disabled,
                _capabilities.IsAiConfigured ? "configured" : "not_configured",
                _capabilities.IsAiConfigured
                    ? "An AI provider is configured."
                    : "No AI provider is configured, so AI features are off and nothing is sent to an AI service."),
            new CapabilityHealth(
                "search.index",
                CapabilityAvailability.DetectionPending,
                "deferred_warmup",
                "Search index readiness is checked after the catalogue opens."),
            new CapabilityHealth(
                "bookshelf.3d",
                shelf ? CapabilityAvailability.DetectionPending : CapabilityAvailability.Disabled,
                shelf ? Source(UserCapability.ThreeDimensionalShelf) : "off_until_enabled_in_settings",
                shelf
                    ? "The 3D bookshelf preview is on; the graphics support it needs is checked when it opens."
                    : "The 3D bookshelf preview is off."),
            new CapabilityHealth(
                "classroom.host",
                host ? CapabilityAvailability.Available : CapabilityAvailability.Disabled,
                host ? Source(UserCapability.ClassroomHost) : "off_until_enabled_in_settings",
                host
                    ? "Classroom Host controls are on; no listener starts until a teacher starts it."
                    : "Classroom Host is off."),
            new CapabilityHealth(
                "pdf.worker",
                worker.IsAvailable ? CapabilityAvailability.Available : CapabilityAvailability.Unavailable,
                worker.Code,
                worker.IsAvailable
                    ? "The isolated PDF worker prerequisite is available."
                    : "The isolated PDF worker is unavailable. Catalogue browsing remains available."),
        ];

        return Task.FromResult(result);
    }

    private string Source(UserCapability flag) =>
        _capabilities.IsManagedByEnvironment(flag) ? "on_by_environment" : "on_in_settings";
}
