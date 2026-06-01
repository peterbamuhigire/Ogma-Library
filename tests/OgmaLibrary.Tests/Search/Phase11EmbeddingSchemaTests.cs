using System.Data.Common;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.AI.Ollama;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Search;

/// <summary>
/// Phase 11 WP1 embedding schema, repository, and local Ollama adapter tests.
/// </summary>
public sealed class Phase11EmbeddingSchemaTests : IDisposable
{
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;

    public Phase11EmbeddingSchemaTests()
    {
        (_context, _dbPath) = CatalogueTestHelper.CreateTempFileContext();
        _context.Database.Migrate();
    }

    public void Dispose()
    {
        _context.Dispose();
        CatalogueTestHelper.DeleteTempDb(_dbPath);
    }

    [Fact]
    public void Phase11Migration_AddsEmbeddingStatusAndModelVersionedVectorIndex()
    {
        Assert.Contains("EmbeddingStatus", GetColumns("Books"));
        Assert.Contains("ModelName", GetColumns("EmbeddingVectors"));
        Assert.Contains("ModelVersion", GetColumns("EmbeddingVectors"));
        Assert.Contains("GeneratedAtUtc", GetColumns("EmbeddingVectors"));
        Assert.True(IndexExists("IX_Books_EmbeddingStatus"));
        Assert.True(IndexExists("UX_EmbeddingVectors_ChunkId_ModelName_ModelVersion"));
    }

    [Fact]
    public async Task EmbeddingVectorRepository_CreateAndRead_RoundTripsFloatBlob()
    {
        long chunkId = SeedChunk();
        var repository = new EmbeddingVectorRepository(_context);
        var vector = new EmbeddingVectorRecord(
            Id: 0,
            ChunkId: chunkId,
            ModelName: "nomic-embed-text",
            ModelVersion: "nomic-embed-text:latest",
            Vector: [0.25f, -0.5f, 0.75f],
            DimensionCount: 3,
            GeneratedAtUtc: DateTimeOffset.UtcNow);

        EmbeddingVectorRecord saved = await repository.CreateAsync(vector, CancellationToken.None);
        EmbeddingVectorRecord? reloaded = await repository.GetForChunkAsync(
            chunkId,
            "nomic-embed-text",
            "nomic-embed-text:latest",
            CancellationToken.None);
        IReadOnlyList<EmbeddingVectorRecord> forBook = await repository.GetAllForBookAsync(
            "P11EMBEDBOOK000000000001",
            CancellationToken.None);

        Assert.True(saved.Id > 0);
        Assert.NotNull(reloaded);
        Assert.Equal(vector.DimensionCount, reloaded.DimensionCount);
        Assert.Equal(vector.Vector, reloaded.Vector);
        Assert.Single(forBook);
    }

    [Fact]
    public async Task OllamaEmbeddingAdapter_PostsOnlyToLoopbackEmbeddingEndpoint()
    {
        using var http = new HttpClient(new RecordingHandler(request =>
        {
            Assert.NotNull(request.RequestUri);
            Assert.True(request.RequestUri.IsLoopback);
            Assert.Equal("/api/embeddings", request.RequestUri.AbsolutePath);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"model":"nomic-embed-text:latest","embedding":[0.1,0.2,0.3]}""",
                    Encoding.UTF8,
                    "application/json"),
            };
        }))
        {
            BaseAddress = new Uri("http://localhost:11434"),
        };
        var adapter = new OllamaEmbeddingAdapter(http);

        OllamaEmbeddingResult result = await adapter.EmbedAsync(
            "semantic search text",
            "nomic-embed-text",
            CancellationToken.None);

        Assert.Equal("ollama", adapter.ProviderKey);
        Assert.True(adapter.IsLocalOnly);
        Assert.Equal("nomic-embed-text:latest", result.ModelVersion);
        Assert.Equal([0.1f, 0.2f, 0.3f], result.Vector);
    }

    [Fact]
    public async Task OllamaEmbeddingAdapter_IsAvailable_ReturnsFalseWhenLocalServiceFails()
    {
        using var http = new HttpClient(new RecordingHandler(_ =>
            throw new HttpRequestException("No local Ollama service.")))
        {
            BaseAddress = new Uri("http://localhost:11434"),
        };
        var adapter = new OllamaEmbeddingAdapter(http);

        bool available = await adapter.IsAvailableAsync(CancellationToken.None);

        Assert.False(available);
    }

    private long SeedChunk()
    {
        const string bookId = "P11EMBEDBOOK000000000001";
        if (!_context.Books.Any(book => book.BookId == bookId))
        {
            _context.Books.Add(new BookRow
            {
                BookId = bookId,
                Title = "Phase 11 Embedding Book",
                Status = 0,
                IndexStatus = (int)SearchBookIndexStatus.Indexed,
                EmbeddingStatus = (int)SearchEmbeddingStatus.NotEmbedded,
            });
            _context.SaveChanges();
        }

        var chunk = new SearchChunkRow
        {
            BookId = bookId,
            ChunkIndex = 0,
            ChunkText = "semantic bridge chunk",
            Source = (int)SearchChunkSource.Page,
            TokenCount = 3,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        _context.SearchChunks.Add(chunk);
        _context.SaveChanges();
        return chunk.ChunkId;
    }

    private HashSet<string> GetColumns(string tableName)
    {
        _context.Database.OpenConnection();
        using DbCommand command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";
        using DbDataReader reader = command.ExecuteReader();

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    private bool IndexExists(string indexName)
    {
        _context.Database.OpenConnection();
        using DbCommand command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT COUNT(1)
            FROM sqlite_master
            WHERE type = 'index' AND name = $name
            """;
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = "$name";
        parameter.Value = indexName;
        command.Parameters.Add(parameter);
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) > 0;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_handler(request));
    }
}
