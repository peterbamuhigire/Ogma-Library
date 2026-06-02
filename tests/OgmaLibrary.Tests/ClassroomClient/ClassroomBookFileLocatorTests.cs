using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Infrastructure;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;
using OgmaLibrary.Infrastructure.ClassroomClient;
using OgmaLibrary.Reader.Cache;
using OgmaLibrary.Reader.Progress;
using OgmaLibrary.Reader.Session;
using OgmaLibrary.Tests.Catalogue;
using OgmaLibrary.Tests.Reader;

namespace OgmaLibrary.Tests.ClassroomClient;

/// <summary>Phase 17 reader file-location switch tests for Client mode.</summary>
public sealed class ClassroomBookFileLocatorTests
{
    private const string Fingerprint = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task ClassroomBookFileLocator_StandaloneMode_UsesLocalLocator()
    {
        var local = new FakeLocalBookFileLocator("C:/library/local.pdf");
        var materializer = new FakeBookFileMaterializer("C:/classroom/book.pdf");
        var mode = new InMemoryClassroomModeService();
        var connection = new InMemoryClassroomHostConnectionService();
        var locator = new ClassroomBookFileLocator(local, mode, connection, materializer);

        string? path = await locator.LocateAsync("book-1", CancellationToken.None);

        Assert.Equal("C:/library/local.pdf", path);
        Assert.Equal(1, local.Calls);
        Assert.Equal(0, materializer.Calls);
    }

    [Fact]
    public async Task ClassroomBookFileLocator_ClientModeWithoutConnection_ReturnsNull()
    {
        var local = new FakeLocalBookFileLocator("C:/library/local.pdf");
        var materializer = new FakeBookFileMaterializer("C:/classroom/book.pdf");
        var mode = new InMemoryClassroomModeService();
        await mode.SaveModeAsync(new ClassroomModeSettings(LibraryRuntimeMode.ConnectToHost));
        var connection = new InMemoryClassroomHostConnectionService();
        var locator = new ClassroomBookFileLocator(local, mode, connection, materializer);

        string? path = await locator.LocateAsync("book-1", CancellationToken.None);

        Assert.Null(path);
        Assert.Equal(0, local.Calls);
        Assert.Equal(0, materializer.Calls);
    }

    [Fact]
    public async Task ClassroomBookFileLocator_ClientModeWithConnection_UsesMaterializer()
    {
        var local = new FakeLocalBookFileLocator("C:/library/local.pdf");
        var materializer = new FakeBookFileMaterializer("C:/classroom/book.pdf");
        var mode = new InMemoryClassroomModeService();
        await mode.SaveModeAsync(new ClassroomModeSettings(LibraryRuntimeMode.ConnectToHost));
        var connection = new InMemoryClassroomHostConnectionService();
        var request = new ClassroomJoinRequest("192.168.1.13", 7473, Fingerprint);
        await connection.SetActiveAsync(new ClassroomHostConnection(
            request,
            "session-token",
            new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero)));
        var locator = new ClassroomBookFileLocator(local, mode, connection, materializer);

        string? path = await locator.LocateAsync("book-1", CancellationToken.None);

        Assert.Equal("C:/classroom/book.pdf", path);
        Assert.Equal(0, local.Calls);
        Assert.Equal(1, materializer.Calls);
        Assert.Equal(request, materializer.Request);
        Assert.Equal("session-token", materializer.SessionToken);
        Assert.Equal("book-1", materializer.BookId);
    }

    [Fact]
    public async Task ReaderSessionService_OpenAsync_UsesClassroomLocatorInClientMode()
    {
        await using CatalogueDbContext db = CatalogueTestHelper.CreateInMemoryContext();
        using var progressService = new ReadingProgressService(new ReadingProgressRepository(db));
        var rendererFactory = new MockPdfRendererFactory(3);
        using var cache = new PageRenderCache(rendererFactory, new StopwatchBenchmarkContext());
        var mode = new InMemoryClassroomModeService();
        await mode.SaveModeAsync(new ClassroomModeSettings(LibraryRuntimeMode.ConnectToHost));
        var connection = new InMemoryClassroomHostConnectionService();
        await connection.SetActiveAsync(new ClassroomHostConnection(
            new ClassroomJoinRequest("192.168.1.13", 7473, Fingerprint),
            "session-token",
            new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero)));
        var materializer = new FakeBookFileMaterializer("C:/classroom/book.pdf");
        var locator = new ClassroomBookFileLocator(
            new FakeLocalBookFileLocator(null),
            mode,
            connection,
            materializer);
        using var sessions = new ReaderSessionService(rendererFactory, progressService, locator, cache);

        ReaderSession session = await sessions.OpenAsync("host-book-1", pageHint: null, CancellationToken.None);

        Assert.Equal("host-book-1", session.BookId);
        Assert.Equal("C:/classroom/book.pdf", session.FilePath);
        Assert.Equal(3, session.PageCount);
        Assert.Equal(1, materializer.Calls);
    }

    [Fact]
    public async Task ClassroomHostConnectionService_IsRegisteredInClassroomClientServices()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            using ServiceProvider provider = new ServiceCollection()
                .AddClassroomClientServices(dataDirectory)
                .BuildServiceProvider();

            IClassroomHostConnectionService service =
                provider.GetRequiredService<IClassroomHostConnectionService>();

            Assert.IsType<InMemoryClassroomHostConnectionService>(service);
            Assert.Null(await service.GetActiveAsync());
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    private static string CreateTempDirectory()
    {
        string dataDirectory = Path.Combine(Path.GetTempPath(), $"ogma-book-locator-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dataDirectory);
        return dataDirectory;
    }

    private static void CleanupTempDirectory(string dataDirectory)
    {
        if (Directory.Exists(dataDirectory))
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    private sealed class FakeLocalBookFileLocator : IBookFileLocator
    {
        private readonly string? _path;

        public FakeLocalBookFileLocator(string? path) => _path = path;

        public int Calls { get; private set; }

        public Task<string?> LocateAsync(string bookId, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(_path);
        }
    }

    private sealed class FakeBookFileMaterializer : IClassroomBookFileMaterializer
    {
        private readonly string _path;

        public FakeBookFileMaterializer(string path) => _path = path;

        public int Calls { get; private set; }

        public ClassroomJoinRequest? Request { get; private set; }

        public string? SessionToken { get; private set; }

        public string? BookId { get; private set; }

        public Task<string> MaterializeAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            string bookId,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            Request = request;
            SessionToken = sessionToken;
            BookId = bookId;
            return Task.FromResult(_path);
        }
    }
}
