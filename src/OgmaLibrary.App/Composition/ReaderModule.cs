using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.App.Configuration;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.ClassroomClient;
using OgmaLibrary.Infrastructure.Pdf;
using OgmaLibrary.Infrastructure.Reader;
using OgmaLibrary.Reader.Annotations;
using OgmaLibrary.Reader.Cache;
using OgmaLibrary.Reader.Progress;
using OgmaLibrary.Reader.Session;
using OgmaLibrary.Reader.TextLayer;

namespace OgmaLibrary.App.Composition;

internal sealed class ReaderModule : IOgmaModuleRegistrar
{
    public string Name => "reader";

    public void Register(IServiceCollection services, OgmaRuntimeOptions options)
    {
        services.AddSingleton<IPdfRendererFactory, IsolatedPdfRendererFactory>();
        services.AddSingleton<BookFileLocator>();
        services.AddSingleton<IBookFileLocator>(sp => new ClassroomBookFileLocator(
            sp.GetRequiredService<BookFileLocator>(),
            sp.GetRequiredService<IClassroomModeService>(),
            sp.GetRequiredService<IClassroomHostConnectionService>(),
            sp.GetRequiredService<IClassroomBookFileMaterializer>()));
        services.AddSingleton<IReadingProgressService, ReadingProgressService>();
        services.AddSingleton<PageRenderCache>(sp => new PageRenderCache(
            sp.GetRequiredService<IPdfRendererFactory>(),
            sp.GetRequiredService<IBenchmarkContext>()));
        services.AddSingleton<IPageRenderCache>(sp => sp.GetRequiredService<PageRenderCache>());
        services.AddSingleton<ReaderSessionService>(sp => new ReaderSessionService(
            sp.GetRequiredService<IPdfRendererFactory>(),
            sp.GetRequiredService<IReadingProgressService>(),
            sp.GetRequiredService<IBookFileLocator>(),
            sp.GetRequiredService<PageRenderCache>()));
        services.AddSingleton<IReaderSessionService>(sp => sp.GetRequiredService<ReaderSessionService>());
        services.AddSingleton<IReaderSessionReadModel>(sp => sp.GetRequiredService<ReaderSessionService>());
        services.AddSingleton<TextLayerService>(sp => new TextLayerService(
            sp.GetRequiredService<IPdfRendererFactory>(),
            sp.GetRequiredService<IReaderSessionService>()));
        services.AddSingleton<ITextLayerService>(sp => sp.GetRequiredService<TextLayerService>());
        services.AddSingleton<IInDocumentSearchService, InDocumentSearchService>();

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
        services.AddSingleton<IReaderPortabilityService>(sp => new ReaderPortabilityService(
            sp.GetRequiredService<IDbContextFactory<CatalogueDbContext>>()));
    }
}
