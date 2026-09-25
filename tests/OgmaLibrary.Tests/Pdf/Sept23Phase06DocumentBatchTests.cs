using System.Security.Cryptography;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Infrastructure.Pdf;
using OgmaLibrary.Tests.Ingestion;
using SkiaSharp;

namespace OgmaLibrary.Tests.Pdf;

/// <summary>
/// Sept-23 Phase 06 (T06.10, K26): a book's metadata, text, cover and spine run in one
/// isolated worker process with one sandbox copy (was one copy, three hashes and one process
/// per operation), and one-shot limits scale with the file size.
/// </summary>
public sealed class Sept23Phase06DocumentBatchTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ogma-p06-batch-{Guid.NewGuid():N}");

    public Sept23Phase06DocumentBatchTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
    }

    [Fact]
    public void BookBatch_RunsAllOperationsInOneWorkerProcess()
    {
        string pdf = Path.Combine(_root, "book.pdf");
        IngestionTestFixture.WriteSyntheticPdf(pdf, "Batch Book", "Batch Author");
        var client = new PdfWorkerClient(new PdfWorkerOptions { SandboxRoot = Path.Combine(_root, "sandbox") });
        var factory = new IsolatedPdfRendererFactory(client);

        using (PdfDocumentBatch.Begin(pdf))
        {
            RunBookOperations(factory, client, pdf);
        }

        Assert.Equal(1, client.ProcessLaunches);
        Assert.Empty(Directory.EnumerateDirectories(Path.Combine(_root, "sandbox")));
        using SKBitmap cover = SKBitmap.Decode(Path.Combine(_root, "cover.jpg"));
        Assert.Equal(200, cover.Width);
        using SKBitmap spine = SKBitmap.Decode(Path.Combine(_root, "spine.jpg"));
        Assert.Equal(7, spine.Width);
    }

    [Fact]
    public void WithoutBatch_EachOperationLaunchesItsOwnWorker()
    {
        string pdf = Path.Combine(_root, "book.pdf");
        IngestionTestFixture.WriteSyntheticPdf(pdf, "Batch Book", "Batch Author");
        var client = new PdfWorkerClient(new PdfWorkerOptions { SandboxRoot = Path.Combine(_root, "sandbox") });

        RunBookOperations(new IsolatedPdfRendererFactory(client), client, pdf);

        Assert.True(client.ProcessLaunches >= 4, $"launches={client.ProcessLaunches}");
    }

    [Fact]
    public void SandboxCopy_HashesOnceWhileCopying_AndMatchesSource()
    {
        string source = Path.Combine(_root, "source.pdf");
        byte[] bytes = RandomNumberGenerator.GetBytes(3 * 1024 * 1024 + 17);
        File.WriteAllBytes(source, bytes);
        string sandbox = Path.Combine(_root, "copy");
        Directory.CreateDirectory(sandbox);

        string copy = PdfWorkerClient.CopyInputToSandbox(source, sandbox, out string sha256);

        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), sha256);
        Assert.Equal(bytes, File.ReadAllBytes(copy));
    }

    [Theory]
    [InlineData(0, 15)]
    [InlineData(4L * 1024 * 1024, 16)]
    [InlineData(200L * 1024 * 1024, 65)]
    [InlineData(2000L * 1024 * 1024, 120)]
    public void OneShotLimit_ScalesWithFileSize_AndIsCapped(long bytes, int expectedSeconds) =>
        Assert.Equal(
            TimeSpan.FromSeconds(expectedSeconds),
            PdfWorkerClient.ScaledOneShotLimit(TimeSpan.FromSeconds(15), bytes));

    private void RunBookOperations(IsolatedPdfRendererFactory factory, PdfWorkerClient client, string pdf)
    {
        using (IPdfRenderer metadata = factory.Open(pdf))
        {
            Assert.NotNull(metadata.ReadDocumentMetadata());
        }

        using (IPdfRenderer text = factory.Open(pdf))
        {
            Assert.NotEmpty(text.ExtractTextLayer(0).Words);
        }

        Assert.Throws<PdfEmbeddedCoverNotFoundException>(() =>
            client.GenerateEmbeddedCover(pdf, Path.Combine(_root, "cover.jpg"), 200, 300));
        client.GenerateCover(pdf, Path.Combine(_root, "cover.jpg"), 200, 300);
        client.GenerateSpine(pdf, Path.Combine(_root, "spine.jpg"), 7, 100);
    }
}
