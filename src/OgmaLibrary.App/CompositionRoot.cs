using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OgmaLibrary.App.ViewModels;
using OgmaLibrary.App.ViewModels.Ai;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.ViewModels.Reader;
using OgmaLibrary.App.ViewModels.Search;
using OgmaLibrary.App.ViewModels.Shelf3D;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Commands;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Application.Ocr;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Bookshelf3D.Bridge;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure;
using OgmaLibrary.Infrastructure.AI;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Commands;
using OgmaLibrary.Infrastructure.Ingestion;
using OgmaLibrary.Infrastructure.LanHost;
using OgmaLibrary.Infrastructure.Localization;
using OgmaLibrary.Infrastructure.Metadata;
using OgmaLibrary.Infrastructure.Ocr;
using OgmaLibrary.Infrastructure.Pdf;
using OgmaLibrary.Infrastructure.Security;
using OgmaLibrary.Reader.Annotations;
using OgmaLibrary.Reader.Cache;
using OgmaLibrary.Reader.Progress;
using OgmaLibrary.Reader.Session;
using OgmaLibrary.Reader.TextLayer;
using OgmaLibrary.Workers;
using OgmaLibrary.Workers.Ocr;

namespace OgmaLibrary.App;

/// <summary>
/// The single composition root for Ogma Library (HLD §2.3). This is the only place
/// in the solution permitted to bind a concrete implementation to an interface, so
/// the single off-device egress chokepoint (SI-1) and the bounded-context contracts
/// are enforceable by inspecting one type.
/// </summary>
public static class CompositionRoot
{
    /// <summary>
    /// Registers every bounded-context service with the dependency-injection
    /// container. As contexts come online in later phases their implementations are
    /// registered here and nowhere else.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddOgmaLibrary(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Cross-cutting: performance-budget instrumentation (Phase 02).
        services.AddSingleton<IBenchmarkContext, StopwatchBenchmarkContext>();

        // Localization: MVP English + French (Phase 02).
        services.AddSingleton<ILocalizationService, InMemoryLocalizationService>();

        // View models — registered after ingestion pipeline so DI resolves all deps.
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<RecommendationPanelViewModel>();
        services.AddTransient<ReadingPlanViewModel>();
        services.AddTransient<Bookshelf3DViewModel>();
        services.AddTransient<SplitViewViewModel>();
        services.AddTransient<PasswordUnlockViewModel>();

        // Phase 04 — Catalogue & Data Layer.
        // The data directory defaults to "Ogma Library Data" under OS app-data.
        string dataDirectory = CatalogueServiceExtensions.GetDefaultDataDirectory();
        services.AddCatalogueContext(
            dataDirectory: dataDirectory,
            libraryRoot: dataDirectory);
        services.AddAiGatewayCore();
        services.AddSingleton<IWebViewBridge>(_ =>
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? new WKWebViewBridge()
                : new WebView2Bridge());
        services.AddSingleton<IPasswordProvider>(_ =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new WindowsPasswordProvider()
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? new MacOsKeychainPasswordProvider()
                    : new UnsupportedPasswordProvider());

        // Phase 05 — Ingestion Pipeline (Infrastructure services).
        services.AddIngestionPipeline(dataDirectory: dataDirectory);

        // Phase 07 — deterministic metadata enrichment and PDF DocInfo write-back.
        services.AddMetadataEnrichment(libraryRoot: dataDirectory);

        // Phase 16 — opt-in LAN Library Host bounded-context scaffold.
        services.AddLanHostServices();

        // Phase 05 — Workers: background job worker + crash-recovery service.
        services.AddSingleton<JobRecoveryService>();
        services.AddSingleton<IOcrProvider, TesseractOcrProvider>();
        services.AddSingleton<OcrJobProcessor>();
        services.AddSingleton<IOcrJobProcessor>(sp => sp.GetRequiredService<OcrJobProcessor>());
        services.AddHostedService<BookIngestionWorker>();
        services.AddHostedService<SearchExtractionWorker>();
        services.AddHostedService<EmbeddingGenerationWorker>();
        services.AddHostedService<OcrWorker>();

        // Phase 06 — Catalogue Browsing.

        // Write service.
        services.AddSingleton<ICatalogueWriteService, CatalogueWriteService>();

        // Command history (ring-buffer undo stack).
        services.AddSingleton<ICommandHistory, CommandHistory>();

        // Phase 06 — Catalogue browsing.
        // The MainShellViewModel is the composition root for its subtree.
        // It creates its own child VMs internally to avoid circular-dependency.
        services.AddSingleton<MainShellViewModel>(sp =>
        {
            var localization = sp.GetRequiredService<ILocalizationService>();
            var readModel = sp.GetRequiredService<ICatalogueReadModel>();
            var writeService = sp.GetRequiredService<ICatalogueWriteService>();

            var filterVm = new CatalogueFilterViewModel();

            var shelfSidebar = new ShelfSidebarViewModel(
                readModel, writeService, localization, filterVm);

            // The shell itself implements both navigation services; pass it as a
            // lazy provider so CatalogueViewModel can be created before the shell
            // is fully constructed.
            MainShellViewModel? shell = null;
            var navProxy = new NavigationServiceProxy(() => shell!);

            var catalogueVm = new CatalogueViewModel(readModel, navProxy, localization);
            var bookDetailVm = new BookDetailViewModel(
                readModel,
                navProxy,
                localization,
                sp.GetRequiredService<IBookMetadataEnrichmentService>(),
                sp.GetRequiredService<IReadingMemoryService>(),
                sp.GetRequiredService<IOcrJobQueueService>(),
                sp.GetRequiredService<IPasswordProvider>());
            var readerVm = new ReaderViewModel(
                sp.GetRequiredService<IReaderSessionService>(),
                sp.GetRequiredService<IAnnotationService>(),
                sp.GetRequiredService<IBookmarkService>(),
                sp.GetRequiredService<IAnnotationLayerService>(),
                sp.GetRequiredService<ICitationService>(),
                sp.GetRequiredService<IReadingMemoryService>(),
                localization,
                sp.GetRequiredService<ITextLayerService>());
            var searchVm = new SearchViewModel(
                sp.GetRequiredService<ISemanticSearchService>(),
                navProxy,
                localization);
            var indexManagerVm = new IndexManagerViewModel(
                sp.GetRequiredService<IIndexManagerService>(),
                sp.GetRequiredService<IEmbeddingErasureService>(),
                localization);
            var splitViewVm = sp.GetRequiredService<SplitViewViewModel>();

            shell = new MainShellViewModel(
                localization,
                catalogueVm,
                bookDetailVm,
                shelfSidebar,
                readerVm,
                sp.GetRequiredService<ILibrarySettingsService>(),
                sp.GetRequiredService<IIngestionOrchestrator>(),
                sp.GetRequiredService<IScanProgressService>(),
                sp.GetRequiredService<IDirectPdfOpenService>(),
                searchVm,
                indexManagerVm,
                splitViewVm);

            return shell;
        });

        // Navigation services — resolved from the MainShellViewModel singleton.
        services.AddSingleton<IBookDetailNavigationService>(sp =>
            sp.GetRequiredService<MainShellViewModel>());
        services.AddSingleton<IReaderNavigationService>(sp =>
            sp.GetRequiredService<MainShellViewModel>());

        // Phase 08 — Reader bounded context.
        services.AddSingleton<IPdfRendererFactory, PdfiumAdapterFactory>();
        services.AddSingleton<IBookFileLocator, BookFileLocator>();
        services.AddSingleton<IReadingProgressService, ReadingProgressService>();
        services.AddSingleton<PageRenderCache>(sp =>
            new PageRenderCache(
                sp.GetRequiredService<IPdfRendererFactory>(),
                sp.GetRequiredService<IBenchmarkContext>()));
        services.AddSingleton<IPageRenderCache>(sp => sp.GetRequiredService<PageRenderCache>());

        services.AddSingleton<ReaderSessionService>(sp =>
            new ReaderSessionService(
                sp.GetRequiredService<IPdfRendererFactory>(),
                sp.GetRequiredService<IReadingProgressService>(),
                sp.GetRequiredService<IBookFileLocator>(),
                sp.GetRequiredService<PageRenderCache>()));
        services.AddSingleton<IReaderSessionService>(sp =>
            sp.GetRequiredService<ReaderSessionService>());
        services.AddSingleton<IReaderSessionReadModel>(sp =>
            sp.GetRequiredService<ReaderSessionService>());

        services.AddSingleton<TextLayerService>(sp => new TextLayerService(
            sp.GetRequiredService<IPdfRendererFactory>(),
            sp.GetRequiredService<IReaderSessionService>()));
        services.AddSingleton<ITextLayerService>(sp => sp.GetRequiredService<TextLayerService>());
        services.AddSingleton<IInDocumentSearchService, InDocumentSearchService>();

        // Phase 09 — Annotations, Bookmarks & Reading Memory.
        services.AddSingleton<AnnotationReadModel>();
        services.AddSingleton<IAnnotationReadModel>(sp => sp.GetRequiredService<AnnotationReadModel>());
        services.AddSingleton<IAnnotationEventPublisher>(sp => sp.GetRequiredService<AnnotationReadModel>());

        services.AddSingleton<AnnotationService>(sp => new AnnotationService(
            sp.GetRequiredService<IAnnotationV2Repository>(),
            sp.GetRequiredService<IAnnotationEventPublisher>()));
        services.AddSingleton<IAnnotationService>(sp => sp.GetRequiredService<AnnotationService>());

        services.AddSingleton<BookmarkService>(sp => new BookmarkService(
            sp.GetRequiredService<IBookmarkRepository>(),
            sp.GetRequiredService<IAnnotationEventPublisher>()));
        services.AddSingleton<IBookmarkService>(sp => sp.GetRequiredService<BookmarkService>());

        services.AddSingleton<IAnnotationLayerService>(sp => new AnnotationLayerService(
            sp.GetRequiredService<IAnnotationLayerRepository>(),
            sp.GetRequiredService<IAnnotationEventPublisher>()));

        services.AddSingleton<ICitationService>(sp => new CitationService(
            sp.GetRequiredService<ICatalogueReadModel>(),
            sp.GetRequiredService<ISidecarService>(),
            sp.GetRequiredService<ILocalizationService>()));

        services.AddSingleton<IReadingMemoryService, ReadingMemoryService>();

        // Bounded-context registrations (Search, AI,
        // Bookshelf, Settings & Security, Packaging) are added here in Phases 10+.
        return services;
    }
}
