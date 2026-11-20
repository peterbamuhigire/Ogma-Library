using System.Security.Cryptography;
using System.Text;
using OgmaLibrary.Application.Search;
using OgmaLibrary.Infrastructure.AI.Onnx;

namespace OgmaLibrary.Tests.Ai.Onnx;

/// <summary>
/// Manifest, store verification and provider maths. None of these tests needs
/// the real model; they run on every machine.
/// </summary>
public sealed class OnnxEmbeddingStoreTests : IDisposable
{
    private static readonly byte[] ModelBytes = Encoding.UTF8.GetBytes("fake onnx model bytes");
    private static readonly byte[] TokenizerBytes = Encoding.UTF8.GetBytes("fake sentencepiece bytes");

    private readonly string _root = Path.Combine(Path.GetTempPath(), "ogma-onnx-store-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Manifest_Default_ParsesPinnedE5Small()
    {
        EmbeddingModelManifest manifest = EmbeddingModelManifest.LoadDefault();

        Assert.Equal("intfloat/multilingual-e5-small", manifest.ModelId);
        Assert.Equal("614241f622f53c4eeff9890bdc4f31cfecc418b3", manifest.Revision);
        Assert.Equal(384, manifest.Dimensions);
        Assert.Equal(512, manifest.MaxTokens);
        Assert.Equal("query: ", manifest.QueryPrefix);
        Assert.Equal("passage: ", manifest.PassagePrefix);
        Assert.Equal(2, manifest.Files.Count);
        EmbeddingModelFile model = manifest.GetFile(EmbeddingModelFileRole.Model);
        Assert.Equal("onnx/model_qint8_avx512_vnni.onnx", model.Path);
        Assert.Equal(118_346_824, model.SizeBytes);
        Assert.Equal("dd476dd0c2514e9b9be83aeb3853fac0763e0bdf4a71645407587d77c48a2d88", model.Sha256);
        EmbeddingModelFile tokenizer = manifest.GetFile(EmbeddingModelFileRole.Tokenizer);
        Assert.Equal("cfc8146abe2a0488e9e2a0c56de7952f7c11ab059eca145a0a727afce0db2865", tokenizer.Sha256);
        Assert.Equal(5_069_051, tokenizer.SizeBytes);
        Assert.Equal("intfloat--multilingual-e5-small", manifest.FolderName);
    }

    [Theory]
    [InlineData("../escape.onnx")]
    [InlineData("/abs/model.onnx")]
    [InlineData("C:/abs/model.onnx")]
    [InlineData("onnx/../../model.onnx")]
    public void Manifest_UnsafeFilePath_IsRejected(string path)
    {
        string json = ManifestJson(path, Sha(ModelBytes), ModelBytes.Length);

        Assert.Throws<InvalidDataException>(() => EmbeddingModelManifest.Parse(json));
    }

    [Theory]
    [InlineData("not-a-hash", 10)]
    [InlineData("dd476dd0c2514e9b9be83aeb3853fac0763e0bdf4a71645407587d77c48a2d8", 10)]
    [InlineData("dd476dd0c2514e9b9be83aeb3853fac0763e0bdf4a71645407587d77c48a2d88", 0)]
    public void Manifest_InvalidHashOrSize_IsRejected(string sha, long size)
    {
        Assert.Throws<InvalidDataException>(() => EmbeddingModelManifest.Parse(ManifestJson("model.onnx", sha, size)));
    }

    [Fact]
    public void Store_ResolvesVersionedFolderUnderAppData()
    {
        EmbeddingModelManifest manifest = EmbeddingModelManifest.LoadDefault();

        var store = new EmbeddingModelStore(_root, manifest);

        Assert.Equal(
            Path.Combine(_root, "models", "embeddings", "intfloat--multilingual-e5-small", manifest.Revision),
            store.ModelDirectory);
    }

    [Fact]
    public async Task Store_AllFilesMatch_IsReady()
    {
        EmbeddingModelStore store = CreateStoreWithFiles();

        Assert.Equal(EmbeddingModelStatus.Ready, await store.VerifyAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Store_MissingFile_IsNotInstalled_AndProviderUnavailable()
    {
        EmbeddingModelStore store = CreateStoreWithFiles();
        File.Delete(Path.Combine(store.ModelDirectory, "model.onnx"));

        Assert.Equal(EmbeddingModelStatus.NotInstalled, await store.VerifyAsync(CancellationToken.None));
        using var provider = new OnnxEmbeddingProvider(store);
        Assert.False(await provider.IsAvailableAsync(CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.EmbedAsync("light", EmbeddingInputKind.Query, CancellationToken.None));
    }

    [Fact]
    public async Task Store_EmptyFolder_IsNotInstalled()
    {
        var store = EmbeddingModelStore.ForModelDirectory(Path.Combine(_root, "absent"), TestManifest());

        Assert.Equal(EmbeddingModelStatus.NotInstalled, await store.VerifyAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Store_TamperedFileSameSize_IsCorrupt_AndProviderUnavailable()
    {
        EmbeddingModelStore store = CreateStoreWithFiles();
        byte[] tampered = (byte[])ModelBytes.Clone();
        tampered[0] ^= 0xFF;
        await File.WriteAllBytesAsync(Path.Combine(store.ModelDirectory, "model.onnx"), tampered);

        Assert.Equal(EmbeddingModelStatus.Corrupt, await store.VerifyAsync(CancellationToken.None));
        using var provider = new OnnxEmbeddingProvider(store);
        Assert.False(await provider.IsAvailableAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Store_WrongSize_IsCorrupt()
    {
        EmbeddingModelStore store = CreateStoreWithFiles();
        await File.AppendAllTextAsync(Path.Combine(store.ModelDirectory, "tokenizer.model"), "extra");

        Assert.Equal(EmbeddingModelStatus.Corrupt, await store.VerifyAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Provider_Availability_IsCachedUntilStoreInvalidates()
    {
        EmbeddingModelStore store = CreateStoreWithFiles();
        using var provider = new OnnxEmbeddingProvider(store);
        Assert.True(await provider.IsAvailableAsync(CancellationToken.None));

        File.Delete(Path.Combine(store.ModelDirectory, "model.onnx"));
        Assert.True(await provider.IsAvailableAsync(CancellationToken.None));

        store.Invalidate();
        Assert.False(await provider.IsAvailableAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Store_CancelledVerification_Throws()
    {
        EmbeddingModelStore store = CreateStoreWithFiles();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.VerifyAsync(cancellation.Token));
    }

    [Fact]
    public void Pooling_UsesAttentionMaskAndNormalises()
    {
        // Sequence of 3 tokens, 2 dimensions; the third token is padding.
        float[] hidden = [1f, 2f, 3f, 4f, 100f, 100f];
        long[] mask = [1, 1, 0];

        float[] pooled = OnnxEmbeddingProvider.MeanPoolAndNormalize(hidden, mask, sequenceLength: 3, dimensions: 2);

        // Mean of (1,2) and (3,4) is (2,3); its norm is sqrt(13).
        double norm = Math.Sqrt(13);
        Assert.Equal(2 / norm, pooled[0], precision: 6);
        Assert.Equal(3 / norm, pooled[1], precision: 6);
    }

    [Fact]
    public void Pooling_ZeroVector_IsRejected()
    {
        Assert.Throws<InvalidOperationException>(() =>
            OnnxEmbeddingProvider.MeanPoolAndNormalize([0f, 0f], [1], sequenceLength: 1, dimensions: 2));
    }

    [Fact]
    public void TokenIds_MapToFairseqLayout_AndTruncate()
    {
        // SentencePiece ids: 0 is <unk>; the model expects <s>=0, </s>=2, <unk>=3, others +1.
        long[] ids = OnnxEmbeddingProvider.ToModelInputIds([5, 0, 9, 11, 13], unknownId: 0, maxTokens: 5);

        Assert.Equal([0L, 6L, 3L, 10L, 2L], ids);
    }

    [Theory]
    [InlineData("  light \t and\r\n colour  ", EmbeddingInputKind.Query, "query: light and colour")]
    [InlineData("Les royaumes", EmbeddingInputKind.Passage, "passage: Les royaumes")]
    public void Input_IsWhitespaceNormalisedAndPrefixed(string text, EmbeddingInputKind kind, string expected)
    {
        Assert.Equal(expected, OnnxEmbeddingProvider.PrepareInput(text, kind, EmbeddingModelManifest.LoadDefault()));
    }

    [Fact]
    public void Provider_IntraOpThreads_IsHalfTheCoresAtLeastOne()
    {
        Assert.Equal(1, OnnxEmbeddingProvider.IntraOpThreadsFor(1));
        Assert.Equal(1, OnnxEmbeddingProvider.IntraOpThreadsFor(2));
        Assert.Equal(4, OnnxEmbeddingProvider.IntraOpThreadsFor(8));
    }

    private EmbeddingModelStore CreateStoreWithFiles()
    {
        string directory = Path.Combine(_root, "model");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "model.onnx"), ModelBytes);
        File.WriteAllBytes(Path.Combine(directory, "tokenizer.model"), TokenizerBytes);
        return EmbeddingModelStore.ForModelDirectory(directory, TestManifest());
    }

    private static EmbeddingModelManifest TestManifest() => EmbeddingModelManifest.Parse($$"""
        {
          "modelId": "test/fake-model",
          "revision": "0000000000000000000000000000000000000001",
          "licence": "MIT",
          "dimensions": 4,
          "maxTokens": 16,
          "queryPrefix": "query: ",
          "passagePrefix": "passage: ",
          "source": "https://huggingface.co/test/fake-model/resolve/0000000000000000000000000000000000000001/",
          "files": [
            { "role": "model", "path": "model.onnx", "sha256": "{{Sha(ModelBytes)}}", "sizeBytes": {{ModelBytes.Length}} },
            { "role": "tokenizer", "path": "tokenizer.model", "sha256": "{{Sha(TokenizerBytes)}}", "sizeBytes": {{TokenizerBytes.Length}} }
          ]
        }
        """);

    private static string ManifestJson(string modelPath, string sha, long size) => $$"""
        {
          "modelId": "test/fake-model",
          "revision": "r1",
          "licence": "MIT",
          "dimensions": 4,
          "maxTokens": 16,
          "queryPrefix": "query: ",
          "passagePrefix": "passage: ",
          "source": "https://huggingface.co/test/fake-model/resolve/r1/",
          "files": [
            { "role": "model", "path": "{{modelPath}}", "sha256": "{{sha}}", "sizeBytes": {{size}} },
            { "role": "tokenizer", "path": "tokenizer.model", "sha256": "{{Sha(TokenizerBytes)}}", "sizeBytes": {{TokenizerBytes.Length}} }
          ]
        }
        """;

    private static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
