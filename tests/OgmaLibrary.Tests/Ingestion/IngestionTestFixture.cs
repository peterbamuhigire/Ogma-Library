using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Assets;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Ingestion;
using OgmaLibrary.Infrastructure.Sidecar;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace OgmaLibrary.Tests.Ingestion;

/// <summary>
/// Creates a self-contained temp directory with synthetic PDFs and a wired
/// ingestion pipeline for integration testing.
/// </summary>
internal sealed class IngestionTestFixture : IDisposable
{
    /// <summary>The root directory of the test library.</summary>
    public string RootDir { get; }

    /// <summary>The sub-directory that should be excluded from scans.</summary>
    public string ExcludedDir { get; }

    /// <summary>The SQLite DB path.</summary>
    public string DbPath { get; }

    /// <summary>The catalogue DB context.</summary>
    public CatalogueDbContext Context { get; }

    /// <summary>The fully-wired ingestion orchestrator.</summary>
    public IngestionOrchestrator Orchestrator { get; }

    /// <summary>The discovery service.</summary>
    public PdfDiscoveryService DiscoveryService { get; }

    /// <summary>The book registration service.</summary>
    public BookRegistrationService RegistrationService { get; }

    /// <summary>The unavailable-file flag service.</summary>
    public UnavailableFileFlagService FlagService { get; }

    /// <summary>The scan progress service.</summary>
    public ScanProgressService ProgressService { get; }

    /// <summary>The library settings service.</summary>
    public LibrarySettingsService SettingsService { get; }

    /// <summary>The job recovery service.</summary>
    public OgmaLibrary.Workers.JobRecoveryService JobRecovery { get; }

    /// <summary>The scan health service.</summary>
    public ScanHealthService HealthService { get; }

    static IngestionTestFixture()
    {
        // Configure QuestPDF Community license.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private IngestionTestFixture(
        string rootDir,
        string excludedDir,
        string dbPath,
        CatalogueDbContext context,
        IngestionOrchestrator orchestrator,
        PdfDiscoveryService discoveryService,
        BookRegistrationService registrationService,
        UnavailableFileFlagService flagService,
        ScanProgressService progressService,
        LibrarySettingsService settingsService,
        OgmaLibrary.Workers.JobRecoveryService jobRecovery,
        ScanHealthService healthService)
    {
        RootDir = rootDir;
        ExcludedDir = excludedDir;
        DbPath = dbPath;
        Context = context;
        Orchestrator = orchestrator;
        DiscoveryService = discoveryService;
        RegistrationService = registrationService;
        FlagService = flagService;
        ProgressService = progressService;
        SettingsService = settingsService;
        JobRecovery = jobRecovery;
        HealthService = healthService;
    }

    /// <summary>
    /// Creates a fixture with the specified number of PDFs plus one in an excluded subfolder.
    /// </summary>
    /// <param name="pdfCount">Number of PDFs to generate in the main library root.</param>
    public static IngestionTestFixture Create(int pdfCount = 5)
    {
        string rootDir = Path.Combine(Path.GetTempPath(), $"ogma-itest-{Guid.NewGuid():N}");
        string booksDir = Path.Combine(rootDir, "books");
        string subDir = Path.Combine(rootDir, "books", "subdir");
        string excludedDir = Path.Combine(rootDir, "excluded");
        string settingsDir = Path.Combine(rootDir, ".settings");

        Directory.CreateDirectory(booksDir);
        Directory.CreateDirectory(subDir);
        Directory.CreateDirectory(excludedDir);
        Directory.CreateDirectory(settingsDir);

        // Generate PDFs in books/ and books/subdir/.
        for (int i = 0; i < pdfCount; i++)
        {
            string dir = i % 3 == 0 ? subDir : booksDir;
            WriteSyntheticPdf(
                Path.Combine(dir, $"book-{i:D3}.pdf"),
                $"Test Book {i}",
                $"Author {i}");
        }

        // One PDF in the excluded directory.
        WriteSyntheticPdf(
            Path.Combine(excludedDir, "excluded-book.pdf"),
            "Excluded Book",
            "Excluded Author");

        // Set up the database.
        string dbPath = Path.Combine(rootDir, "catalogue.db");
        var options = new DbContextOptionsBuilder<CatalogueDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        var context = new CatalogueDbContext(options);
        context.Database.EnsureCreated();

        // Wire services.
        var sidecar = new SidecarService(rootDir);
        var identity = new BookIdentityService(context);
        var settings = new LibrarySettingsService(settingsDir);
        var progress = new ScanProgressService();
        var registration = new BookRegistrationService(context);
        var flagService = new UnavailableFileFlagService(context);
        var discovery = new PdfDiscoveryService();
        var metadataExtraction = new MetadataExtractionService(context);
        var thumbnails = new ThumbnailService(sidecar);
        var spineService = new SpineService(sidecar);
        var health = new ScanHealthService(context);
        var jobRecovery = new OgmaLibrary.Workers.JobRecoveryService(context);

        // Pre-configure library root.
        settings.SetLibraryRootAsync(rootDir).GetAwaiter().GetResult();

        var orchestrator = new IngestionOrchestrator(
            settings, discovery, identity, registration, flagService, progress, context);

        return new IngestionTestFixture(
            rootDir, excludedDir, dbPath, context,
            orchestrator, discovery, registration, flagService,
            progress, settings, jobRecovery, health);
    }

    /// <summary>Writes a minimal synthetic PDF with the given title and author using QuestPDF.</summary>
    public static void WriteSyntheticPdf(string path, string title, string author)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(20);
                page.Content()
                    .Column(col =>
                    {
                        col.Item().Text($"Title: {title}").FontSize(16);
                        col.Item().Text($"Author: {author}").FontSize(12);
                        col.Item().Text("This is a synthetic test PDF generated by QuestPDF.").FontSize(10);
                    });
            });
        }).GeneratePdf(path);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Context.Dispose();
        SqliteConnection.ClearAllPools();

        try
        {
            if (Directory.Exists(RootDir))
            {
                Directory.Delete(RootDir, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
    }
}
