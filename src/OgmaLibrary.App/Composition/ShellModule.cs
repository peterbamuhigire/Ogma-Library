using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.App.Configuration;
using OgmaLibrary.App.ViewModels;
using OgmaLibrary.App.ViewModels.Ai;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.App.ViewModels.Reader;
using OgmaLibrary.App.ViewModels.Search;
using OgmaLibrary.App.ViewModels.Shelf3D;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Ai;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Application.LanHost;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Application.Ocr;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Application.SchoolAdmin;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Bookshelf3D.Bridge;
using OgmaLibrary.Domain;

namespace OgmaLibrary.App.Composition;

internal sealed class ShellModule : IOgmaModuleRegistrar
{
    public string Name => "shell";

    public void Register(IServiceCollection services, OgmaRuntimeOptions options)
    {
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<Bookshelf3DViewModel>();
        services.AddTransient<SplitViewViewModel>();
        services.AddTransient<PasswordUnlockViewModel>();

        services.AddSingleton<MainShellViewModel>(sp => CreateMainShell(sp, options));
        services.AddSingleton<IBookDetailNavigationService>(sp =>
            sp.GetRequiredService<MainShellViewModel>());
        services.AddSingleton<IReaderNavigationService>(sp =>
            sp.GetRequiredService<MainShellViewModel>());
    }

    private static MainShellViewModel CreateMainShell(
        IServiceProvider services,
        OgmaRuntimeOptions options)
    {
        var localization = services.GetRequiredService<ILocalizationService>();
        var readModel = services.GetRequiredService<ICatalogueReadModel>();
        var writeService = services.GetRequiredService<ICatalogueWriteService>();
        var filter = new CatalogueFilterViewModel();
        var shelfSidebar = new ShelfSidebarViewModel(readModel, writeService, localization, filter);

        MainShellViewModel? shell = null;
        var navigation = new NavigationServiceProxy(() => shell!);
        var catalogue = new CatalogueViewModel(
            readModel,
            navigation,
            localization,
            services.GetRequiredService<ILibrarySettingsService>());
        var bookDetail = new BookDetailViewModel(
            readModel,
            navigation,
            localization,
            services.GetRequiredService<IBookMetadataEnrichmentService>(),
            services.GetRequiredService<IReadingMemoryService>(),
            services.GetRequiredService<IOcrJobQueueService>(),
            services.GetRequiredService<IPasswordProvider>(),
            services.GetRequiredService<IBookCurationService>(),
            options.LibraryRoot,
            writeService,
            services.GetRequiredService<IMetadataReviewService>());
        var reader = new ReaderViewModel(
            services.GetRequiredService<IReaderSessionService>(),
            services.GetRequiredService<IAnnotationService>(),
            services.GetRequiredService<IBookmarkService>(),
            services.GetRequiredService<IAnnotationLayerService>(),
            services.GetRequiredService<ICitationService>(),
            services.GetRequiredService<IReadingMemoryService>(),
            localization,
            services.GetRequiredService<ITextLayerService>(),
            services.GetRequiredService<IPageRenderCache>());
        var search = new SearchViewModel(
            services.GetRequiredService<ISemanticSearchService>(),
            navigation,
            localization);
        var indexManager = new IndexManagerViewModel(
            services.GetRequiredService<IIndexManagerService>(),
            services.GetRequiredService<IEmbeddingErasureService>(),
            localization);
        var studentSmartSearch = new StudentSmartSearchViewModel(
            services.GetRequiredService<IClassroomHostConnectionService>(),
            services.GetRequiredService<ILibraryHostClient>(),
            services.GetRequiredService<IProfileService>(),
            services.GetRequiredService<IStudentPrivateRepository>());
        var advisor = new RecommendationPanelViewModel(
            services.GetRequiredService<IAiAdvisorService>(),
            navigation,
            localization,
            services.GetService<IAdvisorFeedbackService>(),
            navigation);
        var readingPlan = new ReadingPlanViewModel(
            services.GetRequiredService<IAiAdvisorService>(),
            readModel,
            navigation,
            localization);
        var bookshelf3D = new Bookshelf3DViewModel(
            readModel,
            services.GetRequiredService<IWebViewBridge>(),
            navigation,
            localization);
        HostSharingViewModel? hostSharing = options.EnableClassroomHost
            ? new HostSharingViewModel(
                services.GetRequiredService<ILibraryHostService>(),
                services.GetRequiredService<IHostModeSettingsRepository>(),
                services.GetRequiredService<IClassroomJoinParser>(),
                services.GetRequiredService<IClassroomConnectionService>(),
                services.GetRequiredService<ISyncService>(),
                services.GetRequiredService<IClassroomModeService>(),
                services.GetRequiredService<IMdnsResolver>(),
                services.GetRequiredService<IProfileService>(),
                services.GetRequiredService<IProfileEnrollmentService>(),
                services.GetRequiredService<ISchoolAiKeyProvider>(),
                services.GetRequiredService<ISchoolAiPolicyService>(),
                services.GetRequiredService<IUsageDashboardService>(),
                services.GetRequiredService<ISchoolAiHistoryManagementService>(),
                services.GetRequiredService<IAuditRepository>())
            : null;

        shell = new MainShellViewModel(
            localization,
            catalogue,
            bookDetail,
            shelfSidebar,
            reader,
            services.GetRequiredService<ILibrarySettingsService>(),
            services.GetRequiredService<IIngestionOrchestrator>(),
            services.GetRequiredService<IScanProgressService>(),
            services.GetRequiredService<IDirectPdfOpenService>(),
            search,
            indexManager,
            studentSmartSearch,
            services.GetRequiredService<SplitViewViewModel>(),
            hostSharing,
            services.GetRequiredService<IClassroomModeService>(),
            advisor,
            readingPlan,
            bookshelf3D);

        return shell;
    }
}
