using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.App.Configuration;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Ai;
using OgmaLibrary.Application.Commands;
using OgmaLibrary.Application.Ocr;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.AI;
using OgmaLibrary.Infrastructure.Commands;
using OgmaLibrary.Infrastructure.Ingestion;
using OgmaLibrary.Infrastructure.Metadata;
using OgmaLibrary.Infrastructure.Ocr;
using OgmaLibrary.Infrastructure.Pdf;
using OgmaLibrary.Workers;
using OgmaLibrary.Workers.Ocr;

namespace OgmaLibrary.App.Composition;

internal sealed class CatalogueProcessingModule : IOgmaModuleRegistrar
{
    public string Name => "catalogue-processing";

    public void Register(IServiceCollection services, OgmaRuntimeOptions options)
    {
        services.AddCatalogueContext(options.DataDirectory, options.LibraryRoot);
        services.AddSingleton(_ => new PdfWorkerClient(new PdfWorkerOptions
        {
            WorkerPath = options.PdfWorkerPath,
        }));
        services.AddIngestionPipeline(options.DataDirectory, options.LibraryRoot);
        services.AddMetadataEnrichment(
            options.LibraryRoot,
            options.EnableExternalMetadataProviders);
        services.AddSingleton<IAiProviderHealthStore>(_ => new JsonAiProviderHealthStore(
            Path.Combine(options.DataDirectory, "ai-provider-health.json")));
        services.AddSingleton<IAiUsageBudgetStore>(_ => new JsonAiUsageBudgetStore(
            Path.Combine(options.DataDirectory, "ai-usage-budget.json")));
        services.AddSingleton<IAiUsageBudgetService, AiUsageBudgetService>();
        services.AddAiGatewayCore().AddFailClosedAiRuntime();

        services.AddSingleton<JobRecoveryService>();
        services.AddSingleton<IOcrProvider, TesseractOcrProvider>();
        services.AddSingleton<OcrJobProcessor>();
        services.AddSingleton<IOcrJobProcessor>(sp => sp.GetRequiredService<OcrJobProcessor>());
        services.AddHostedService<BookIngestionWorker>();
        services.AddHostedService<SearchExtractionWorker>();
        services.AddHostedService<EmbeddingGenerationWorker>();
        services.AddHostedService<OcrWorker>();

        services.AddSingleton<ICatalogueWriteService, CatalogueWriteService>();
        services.AddSingleton<ICommandHistory, CommandHistory>();
    }
}
