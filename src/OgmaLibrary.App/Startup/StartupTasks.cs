using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.App.Configuration;
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

    public JobRecoveryStartupTask(JobRecoveryService recovery) =>
        _recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));

    public string Name => "jobs.recovery";

    public StartupTaskCriticality Criticality => StartupTaskCriticality.Optional;

    public string FailureMessage =>
        "Interrupted background jobs could not be recovered. The catalogue can open, but processing stays paused until retry succeeds.";

    public Task ExecuteAsync(CancellationToken cancellationToken) =>
        _recovery.RecoverAsync(cancellationToken);
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

                job.Status = 0;
                job.RetryCount += 1;
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
    private readonly OgmaRuntimeOptions _options;
    private readonly PdfWorkerClient _pdfWorker;

    public StartupCapabilityProbe(OgmaRuntimeOptions options, PdfWorkerClient pdfWorker)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _pdfWorker = pdfWorker ?? throw new ArgumentNullException(nameof(pdfWorker));
    }

    public Task<IReadOnlyList<CapabilityHealth>> ProbeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PdfWorkerAvailability worker = _pdfWorker.GetAvailability();

        IReadOnlyList<CapabilityHealth> result =
        [
            new CapabilityHealth(
                "metadata.external",
                _options.EnableExternalMetadataProviders
                    ? CapabilityAvailability.Available
                    : CapabilityAvailability.Disabled,
                _options.EnableExternalMetadataProviders ? "configured" : "disabled_by_default",
                _options.EnableExternalMetadataProviders
                    ? "External bibliographic providers are configured; live health is checked only when used."
                    : "External bibliographic providers are disabled."),
            new CapabilityHealth(
                "ai.external",
                CapabilityAvailability.Disabled,
                "phase_27_required",
                "External AI remains disabled until the privacy gateway is completed."),
            new CapabilityHealth(
                "search.index",
                CapabilityAvailability.DetectionPending,
                "deferred_warmup",
                "Search index readiness is checked after the catalogue opens."),
            new CapabilityHealth(
                "bookshelf.3d",
                _options.EnableThreeDimensionalShelf
                    ? CapabilityAvailability.DetectionPending
                    : CapabilityAvailability.Disabled,
                _options.EnableThreeDimensionalShelf ? "runtime_detection_required" : "disabled_by_default",
                _options.EnableThreeDimensionalShelf
                    ? "3D assets are registered; WebGL and native-host capability require runtime detection."
                    : "The 3D shelf is disabled."),
            new CapabilityHealth(
                "classroom.host",
                _options.EnableClassroomHost
                    ? CapabilityAvailability.Available
                    : CapabilityAvailability.Disabled,
                _options.EnableClassroomHost ? "configured" : "disabled_by_default",
                _options.EnableClassroomHost
                    ? "Classroom Host controls are configured; no listener starts automatically."
                    : "Classroom Host is disabled."),
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
}
