using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.AI.Onnx;

namespace OgmaLibrary.Tests.Ai.Onnx;

/// <summary>
/// Sept-23 Phase 14 slice 3: the consented model download. A stub handler stands in
/// for the network, so these tests never leave the machine.
/// </summary>
public sealed class EmbeddingModelDownloaderTests : IDisposable
{
    private const string SourceBase = "https://models.example.org/repo/resolve/rev1/";
    private const string CdnHost = "cdn.example.org";

    private static readonly byte[] ModelBytes = Enumerable.Range(0, 200_000).Select(i => (byte)(i * 7)).ToArray();
    private static readonly byte[] TokenizerBytes = Encoding.UTF8.GetBytes("fake sentencepiece tokenizer bytes");

    private readonly string _root = Path.Combine(Path.GetTempPath(), "ogma-onnx-dl-" + Guid.NewGuid().ToString("N"));
    private readonly EmbeddingModelManifest _manifest = CreateManifest();

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task Install_DownloadsVerifiesAndMovesEveryFile_ThroughTheAllowedRedirect()
    {
        var handler = new StubModelServer(redirectToCdn: true);
        (EmbeddingModelDownloader downloader, EmbeddingModelStore store) = Create(handler);
        int invalidations = 0;
        store.Changed += (_, _) => invalidations++;
        var reports = new List<EmbeddingModelInstallProgress>();

        EmbeddingModelInstallResult result = await downloader.InstallAsync(new SyncProgress(reports), CancellationToken.None);

        Assert.Equal(EmbeddingModelInstallOutcome.Installed, result.Outcome);
        Assert.Equal(EmbeddingModelStatus.Ready, await store.VerifyAsync(CancellationToken.None));
        Assert.False(File.Exists(store.GetFilePath(EmbeddingModelFileRole.Model) + ".partial"));
        Assert.Equal(1, invalidations);
        Assert.Contains(handler.Requests, uri => uri.Host == CdnHost);
        Assert.Equal(ModelBytes.Length + TokenizerBytes.Length, reports[^1].BytesReceived);
        Assert.Equal(ModelBytes.Length + TokenizerBytes.Length, reports[^1].TotalBytes);

        EmbeddingModelInstallStatus status = await downloader.GetStatusAsync(CancellationToken.None);
        Assert.Equal(EmbeddingModelInstallState.Installed, status.State);
        Assert.Equal(0, status.DownloadSizeBytes);
        Assert.Equal(ModelBytes.Length + TokenizerBytes.Length, status.DiskUseBytes);
    }

    [Fact]
    public async Task GetStatus_BeforeInstall_DeclaresDownloadSizeAndDiskUse_WithoutNetwork()
    {
        var handler = new StubModelServer();
        (EmbeddingModelDownloader downloader, _) = Create(handler);

        EmbeddingModelInstallStatus status = await downloader.GetStatusAsync(CancellationToken.None);

        Assert.Equal(EmbeddingModelInstallState.NotInstalled, status.State);
        Assert.Equal(ModelBytes.Length + TokenizerBytes.Length, status.DownloadSizeBytes);
        Assert.Equal(ModelBytes.Length + TokenizerBytes.Length, status.DiskUseBytes);
        Assert.Equal("MIT", status.Licence);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Install_ResumesFromAPartialFile_WithARangeRequest()
    {
        var handler = new StubModelServer();
        (EmbeddingModelDownloader downloader, EmbeddingModelStore store) = Create(handler);
        string modelPath = store.GetFilePath(EmbeddingModelFileRole.Model);
        Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
        const int already = 50_000;
        await File.WriteAllBytesAsync(modelPath + ".partial", ModelBytes[..already]);

        EmbeddingModelInstallResult result = await downloader.InstallAsync(null, CancellationToken.None);

        Assert.Equal(EmbeddingModelInstallOutcome.Installed, result.Outcome);
        Assert.Equal(already, handler.RangeStarts["onnx/model.onnx"]);
        Assert.Equal(ModelBytes, await File.ReadAllBytesAsync(modelPath));
    }

    [Fact]
    public async Task Install_TamperedPayload_IsRejectedAndRemoved()
    {
        var handler = new StubModelServer { TamperModel = true };
        (EmbeddingModelDownloader downloader, EmbeddingModelStore store) = Create(handler);
        string modelPath = store.GetFilePath(EmbeddingModelFileRole.Model);

        EmbeddingModelInstallResult result = await downloader.InstallAsync(null, CancellationToken.None);

        Assert.Equal(EmbeddingModelInstallOutcome.ChecksumMismatch, result.Outcome);
        Assert.False(File.Exists(modelPath));
        Assert.False(File.Exists(modelPath + ".partial"));
        Assert.Equal(EmbeddingModelStatus.NotInstalled, await store.VerifyAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Install_Offline_ReturnsOfflineSoTheUserCanDownloadLater()
    {
        var handler = new StubModelServer { Offline = true };
        (EmbeddingModelDownloader downloader, _) = Create(handler);

        EmbeddingModelInstallResult result = await downloader.InstallAsync(null, CancellationToken.None);

        Assert.Equal(EmbeddingModelInstallOutcome.Offline, result.Outcome);
    }

    [Fact]
    public async Task Install_InsufficientSpace_WritesNothing()
    {
        var handler = new StubModelServer();
        (EmbeddingModelDownloader downloader, EmbeddingModelStore store) = Create(handler, freeSpace: 1_000);

        EmbeddingModelInstallResult result = await downloader.InstallAsync(null, CancellationToken.None);

        Assert.Equal(EmbeddingModelInstallOutcome.InsufficientSpace, result.Outcome);
        Assert.Empty(handler.Requests);
        Assert.False(File.Exists(store.GetFilePath(EmbeddingModelFileRole.Model) + ".partial"));
    }

    [Fact]
    public async Task Install_Cancelled_KeepsThePartialFileForResume()
    {
        using var cancellation = new CancellationTokenSource();
        var handler = new StubModelServer { AfterBytes = (sent) => { if (sent >= 64 * 1024) { cancellation.Cancel(); } } };
        (EmbeddingModelDownloader downloader, EmbeddingModelStore store) = Create(handler);
        string partial = store.GetFilePath(EmbeddingModelFileRole.Model) + ".partial";

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => downloader.InstallAsync(null, cancellation.Token));

        Assert.True(File.Exists(partial));
        long kept = new FileInfo(partial).Length;
        Assert.InRange(kept, 1, ModelBytes.Length - 1);
        Assert.False(File.Exists(store.GetFilePath(EmbeddingModelFileRole.Model)));
    }

    [Theory]
    [InlineData("https://evil.example.net/payload")]
    [InlineData("http://cdn.example.org/payload")]
    public async Task Install_RedirectOutsideTheAllowlistOrNotHttps_IsRefused(string location)
    {
        var handler = new StubModelServer { RedirectOverride = new Uri(location) };
        (EmbeddingModelDownloader downloader, EmbeddingModelStore store) = Create(handler);

        EmbeddingModelInstallResult result = await downloader.InstallAsync(null, CancellationToken.None);

        Assert.Equal(EmbeddingModelInstallOutcome.HostNotAllowed, result.Outcome);
        Assert.DoesNotContain(handler.Requests, uri => uri.Host == "evil.example.net" || uri.Scheme == "http");
        Assert.False(File.Exists(store.GetFilePath(EmbeddingModelFileRole.Model)));
    }

    [Fact]
    public async Task Remove_DeletesInstalledAndPartialFiles_AndInvalidates()
    {
        var handler = new StubModelServer();
        (EmbeddingModelDownloader downloader, EmbeddingModelStore store) = Create(handler);
        Assert.True((await downloader.InstallAsync(null, CancellationToken.None)).Succeeded);
        await File.WriteAllTextAsync(store.GetFilePath(EmbeddingModelFileRole.Tokenizer) + ".partial", "x");
        int invalidations = 0;
        store.Changed += (_, _) => invalidations++;

        await downloader.RemoveAsync(CancellationToken.None);

        Assert.False(Directory.Exists(store.ModelDirectory));
        Assert.Equal(1, invalidations);
        Assert.Equal(EmbeddingModelInstallState.NotInstalled, (await downloader.GetStatusAsync(CancellationToken.None)).State);
    }

    [Fact]
    public void DefaultManifest_AllowsOnlyThePinnedSourceAndItsMeasuredRedirectHost()
    {
        EmbeddingModelManifest manifest = EmbeddingModelManifest.LoadDefault();

        Assert.Equal(["huggingface.co", "us.aws.cdn.hf.co"], manifest.AllowedDownloadHosts);
    }

    private (EmbeddingModelDownloader Downloader, EmbeddingModelStore Store) Create(StubModelServer handler, long freeSpace = long.MaxValue)
    {
        var store = new EmbeddingModelStore(_root, _manifest);
        var client = new HttpClient(handler);
        var downloader = new EmbeddingModelDownloader(
            client,
            store,
            NullLogger<EmbeddingModelDownloader>.Instance,
            _ => freeSpace);
        return (downloader, store);
    }

    private static EmbeddingModelManifest CreateManifest()
    {
        string json = $$"""
            {
              "modelId": "test/fake-e5",
              "revision": "rev1",
              "licence": "MIT",
              "dimensions": 4,
              "maxTokens": 16,
              "queryPrefix": "query: ",
              "passagePrefix": "passage: ",
              "source": "{{SourceBase}}",
              "redirectHosts": [ "{{CdnHost}}" ],
              "files": [
                { "role": "model", "path": "onnx/model.onnx", "sha256": "{{Hash(ModelBytes)}}", "sizeBytes": {{ModelBytes.Length}} },
                { "role": "tokenizer", "path": "sentencepiece.bpe.model", "sha256": "{{Hash(TokenizerBytes)}}", "sizeBytes": {{TokenizerBytes.Length}} }
              ]
            }
            """;
        return EmbeddingModelManifest.Parse(json);
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private sealed class SyncProgress(List<EmbeddingModelInstallProgress> reports) : IProgress<EmbeddingModelInstallProgress>
    {
        public void Report(EmbeddingModelInstallProgress value) => reports.Add(value);
    }

    /// <summary>Serves the two fake files; optionally redirects to a CDN host first.</summary>
    private sealed class StubModelServer(bool redirectToCdn = false) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        public Dictionary<string, long> RangeStarts { get; } = [];

        public bool TamperModel { get; init; }

        public bool Offline { get; init; }

        public Uri? RedirectOverride { get; init; }

        public Action<long>? AfterBytes { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri uri = request.RequestUri!;
            Requests.Add(uri);
            if (Offline)
            {
                throw new HttpRequestException("No such host is known.");
            }

            if (uri.AbsoluteUri.StartsWith(SourceBase, StringComparison.Ordinal))
            {
                string path = uri.AbsoluteUri[SourceBase.Length..];
                if (RedirectOverride is not null || redirectToCdn)
                {
                    var redirect = new HttpResponseMessage(HttpStatusCode.Found);
                    redirect.Headers.Location = RedirectOverride ?? new Uri($"https://{CdnHost}/blob/{path}?sig=abc");
                    return Task.FromResult(redirect);
                }

                return Task.FromResult(Serve(path, request));
            }

            if (uri.Host == CdnHost && uri.Scheme == Uri.UriSchemeHttps && uri.AbsolutePath.StartsWith("/blob/", StringComparison.Ordinal))
            {
                return Task.FromResult(Serve(uri.AbsolutePath["/blob/".Length..], request));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        private HttpResponseMessage Serve(string path, HttpRequestMessage request)
        {
            byte[] content = path switch
            {
                "onnx/model.onnx" => TamperModel ? ModelBytes.Select(b => (byte)(b ^ 0xFF)).ToArray() : ModelBytes,
                "sentencepiece.bpe.model" => TokenizerBytes,
                _ => [],
            };
            if (content.Length == 0)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            long start = request.Headers.Range?.Ranges.Single().From ?? 0;
            RangeStarts[path] = start;
            var response = new HttpResponseMessage(start > 0 ? HttpStatusCode.PartialContent : HttpStatusCode.OK)
            {
                Content = new StreamContent(new NotifyingStream(content[(int)start..], AfterBytes)),
            };
            response.Content.Headers.ContentLength = content.Length - start;
            if (start > 0)
            {
                response.Content.Headers.ContentRange = new ContentRangeHeaderValue(start, content.Length - 1, content.Length);
            }

            return response;
        }
    }

    /// <summary>Read-only stream that reports how many bytes were served, in small reads.</summary>
    private sealed class NotifyingStream(byte[] data, Action<long>? afterBytes) : MemoryStream(data, writable: false)
    {
        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = base.Read(buffer, offset, Math.Min(count, 16 * 1024));
            afterBytes?.Invoke(Position);
            return read;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int read = base.Read(buffer.Span[..Math.Min(buffer.Length, 16 * 1024)]);
            afterBytes?.Invoke(Position);
            return ValueTask.FromResult(read);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }
}
