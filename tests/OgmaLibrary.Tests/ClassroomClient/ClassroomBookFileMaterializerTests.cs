using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.ClassroomClient;
using OgmaLibrary.Infrastructure.ClassroomClient;

namespace OgmaLibrary.Tests.ClassroomClient;

/// <summary>Phase 17 Host PDF file-stream materialization tests.</summary>
public sealed class ClassroomBookFileMaterializerTests
{
    private const string Fingerprint = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task Materializer_WritesHostPdfToLocalReaderPath()
    {
        string dataDirectory = CreateTempDirectory();
        var client = new RecordingHostClient
        {
            Resource = new LibraryHostResource(
                "books/book-1/file",
                "application/pdf",
                "\"v1\"",
                "%PDF-1.7\n% Ogma classroom fixture\n"u8.ToArray()),
        };
        var materializer = new ClassroomBookFileMaterializer(dataDirectory, client);
        var request = new ClassroomJoinRequest("192.168.1.13", 7473, Fingerprint);

        try
        {
            string path = await materializer.MaterializeAsync(request, "session-token", "book-1");

            Assert.Equal(1, client.FileStreamCalls);
            Assert.EndsWith(".pdf", path, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith(
                Path.Combine(dataDirectory, "classroom", "files"),
                path,
                StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(path));
            Assert.Equal(client.Resource.Content, await File.ReadAllBytesAsync(path));
            Assert.Single(Directory.EnumerateFiles(Path.GetDirectoryName(path)!, "*.json"));
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task Materializer_ReusesStablePathForSameHostResource()
    {
        string dataDirectory = CreateTempDirectory();
        var client = new RecordingHostClient
        {
            Resource = new LibraryHostResource(
                "books/book-1/file",
                "application/pdf",
                "\"v1\"",
                "%PDF-1.7\n% Ogma classroom fixture\n"u8.ToArray()),
        };
        var materializer = new ClassroomBookFileMaterializer(dataDirectory, client);
        var request = new ClassroomJoinRequest("192.168.1.13", 7473, Fingerprint);

        try
        {
            string firstPath = await materializer.MaterializeAsync(request, "session-token", "book-1");
            string secondPath = await materializer.MaterializeAsync(request, "session-token", "book-1");

            Assert.Equal(firstPath, secondPath);
            Assert.Equal(2, client.FileStreamCalls);
            Assert.Single(Directory.EnumerateFiles(Path.GetDirectoryName(firstPath)!, "*.pdf"));
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public async Task Materializer_RejectsNonPdfResource()
    {
        string dataDirectory = CreateTempDirectory();
        var client = new RecordingHostClient
        {
            Resource = new LibraryHostResource(
                "books/book-1/file",
                "text/plain",
                null,
                "not a pdf"u8.ToArray()),
        };
        var materializer = new ClassroomBookFileMaterializer(dataDirectory, client);
        var request = new ClassroomJoinRequest("192.168.1.13", 7473, Fingerprint);

        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                materializer.MaterializeAsync(request, "session-token", "book-1"));

            Assert.False(Directory.Exists(Path.Combine(dataDirectory, "classroom", "files")));
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    [Fact]
    public void Materializer_IsRegisteredInClassroomClientServices()
    {
        string dataDirectory = CreateTempDirectory();

        try
        {
            using ServiceProvider provider = new ServiceCollection()
                .AddClassroomClientServices(dataDirectory)
                .BuildServiceProvider();

            IClassroomBookFileMaterializer service =
                provider.GetRequiredService<IClassroomBookFileMaterializer>();

            Assert.IsType<ClassroomBookFileMaterializer>(service);
        }
        finally
        {
            CleanupTempDirectory(dataDirectory);
        }
    }

    private static string CreateTempDirectory()
    {
        string dataDirectory = Path.Combine(Path.GetTempPath(), $"ogma-book-materializer-{Guid.NewGuid():N}");
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

    private sealed class RecordingHostClient : ILibraryHostClient
    {
        public LibraryHostResource Resource { get; init; } = new(
            "books/book-1/file",
            "application/pdf",
            null,
            "%PDF-1.7\n"u8.ToArray());

        public int FileStreamCalls { get; private set; }

        public Task<LibraryHostHealth> GetHealthAsync(
            ClassroomJoinRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LibraryHostSession> IssueSessionAsync(
            ClassroomJoinRequest request,
            Guid profileId,
            ClassroomRole role,
            TimeSpan lifetime,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LibraryHostCataloguePage> GetCataloguePageAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            LibraryHostCatalogueQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LibraryHostBookDetail> GetBookAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            string bookId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LibraryHostSearchPage> SearchCatalogueAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            LibraryHostSearchQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LibraryHostAiPayloadPreview> PreviewAiSearchAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            LibraryHostAiSearchRequest query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LibraryHostAiSearchResult> SearchAiAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            LibraryHostAiSearchRequest query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LibraryHostResource> GetPageRenderAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            string bookId,
            int pageNumber,
            int widthPx,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LibraryHostResource> GetFileStreamAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            string bookId,
            CancellationToken cancellationToken = default)
        {
            FileStreamCalls++;
            return Task.FromResult(Resource);
        }

        public Task<LibraryHostResource> GetAssetAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            string assetUrl,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UploadProfileSyncBlobAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            EncryptedClassroomSyncBlob blob,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EncryptedClassroomSyncBlob?> DownloadProfileSyncBlobAsync(
            ClassroomJoinRequest request,
            string sessionToken,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
