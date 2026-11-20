using System.Text;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.Tokenizers;
using OgmaLibrary.Application.Search;

namespace OgmaLibrary.Infrastructure.AI.Onnx;

/// <summary>
/// Built-in, in-process embedding provider: a pinned sentence-embedding model
/// run with the ONNX Runtime CPU package (Sept-23 Phase 14, D-103, D-105).
/// Inference makes no network call. The model is loaded lazily, only after the
/// store has verified every file, and runs one input at a time behind a
/// semaphore with intra-op threads capped at half the cores.
/// </summary>
public sealed class OnnxEmbeddingProvider : IEmbeddingProvider, IDisposable
{
    /// <summary>Provider key stored with every vector.</summary>
    public const string Key = "builtin-onnx";

    // Fairseq/XLM-R layout of the model's vocabulary: SentencePiece ids are
    // shifted by one, with <s>=0, <pad>=1, </s>=2 and <unk>=3 (CUR-27).
    private const long BeginOfSentenceId = 0;
    private const long EndOfSentenceId = 2;
    private const long UnknownModelId = 3;
    private const int FairseqOffset = 1;

    private readonly EmbeddingModelStore _store;
    private readonly EmbeddingModelManifest _manifest;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _availabilityLock = new();

    private Task<EmbeddingModelStatus>? _status;
    private InferenceSession? _session;
    private SentencePieceTokenizer? _tokenizer;
    private bool _sessionStale;
    private bool _disposed;

    /// <summary>Initializes the provider over a model store.</summary>
    public OnnxEmbeddingProvider(EmbeddingModelStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        _manifest = store.Manifest;
        _store.Changed += OnStoreChanged;
    }

    /// <inheritdoc />
    public string ProviderKey => Key;

    /// <inheritdoc />
    public bool IsLocalOnly => true;

    /// <inheritdoc />
    public string ModelId => _manifest.ModelId;

    /// <inheritdoc />
    public string ModelVersion => _manifest.Revision;

    /// <inheritdoc />
    public int Dimensions => _manifest.Dimensions;

    /// <inheritdoc />
    /// <remarks>
    /// True only when the store reports <see cref="EmbeddingModelStatus.Ready"/>.
    /// The verification result is cached until the store signals a change, so
    /// repeated calls cost nothing and never touch the network.
    /// </remarks>
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await GetStatusAsync(cancellationToken).ConfigureAwait(false) == EmbeddingModelStatus.Ready;
    }

    /// <inheritdoc />
    public async Task<EmbeddingResult> EmbedAsync(
        string text,
        EmbeddingInputKind kind,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);

        await EnsureReadyAsync(cancellationToken).ConfigureAwait(false);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Queries stay on the thread pool for latency; background passage indexing runs on a
            // below-normal-priority thread so it never competes with the interface (slice 4).
            float[] vector = kind == EmbeddingInputKind.Passage
                ? await BelowNormalPriorityRunner.RunAsync(() => EmbedCore(text, kind), cancellationToken).ConfigureAwait(false)
                : await Task.Run(() => EmbedCore(text, kind), cancellationToken).ConfigureAwait(false);
            return new EmbeddingResult(ProviderKey, ModelId, ModelVersion, vector);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Embeds several inputs in order, checking cancellation between inputs.
    /// <paramref name="onEmbedded"/> receives each result as soon as it exists.
    /// </summary>
    public async Task<IReadOnlyList<EmbeddingResult>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        EmbeddingInputKind kind,
        Action<EmbeddingResult>? onEmbedded,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(texts);
        var results = new List<EmbeddingResult>(texts.Count);
        foreach (string text in texts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EmbeddingResult result = await EmbedAsync(text, kind, cancellationToken).ConfigureAwait(false);
            results.Add(result);
            onEmbedded?.Invoke(result);
        }

        return results;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _store.Changed -= OnStoreChanged;
        _gate.Wait();
        try
        {
            _session?.Dispose();
            _session = null;
            _tokenizer = null;
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    /// <summary>Intra-op thread count: half the logical cores, at least one.</summary>
    internal static int IntraOpThreadsFor(int processorCount) => Math.Max(1, processorCount / 2);

    /// <summary>
    /// Trims and collapses whitespace (the reference tokenizer differs on
    /// trailing spaces, CUR-27), then adds the role prefix.
    /// </summary>
    internal static string PrepareInput(string text, EmbeddingInputKind kind, EmbeddingModelManifest manifest)
    {
        var builder = new StringBuilder(text.Length + 16);
        builder.Append(kind == EmbeddingInputKind.Query ? manifest.QueryPrefix : manifest.PassagePrefix);
        bool pendingSpace = false;
        bool any = false;
        foreach (char character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = any;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
            any = true;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Maps SentencePiece ids to the model's vocabulary, truncates to
    /// <paramref name="maxTokens"/> including the start and end tokens, and
    /// wraps the sequence.
    /// </summary>
    internal static long[] ToModelInputIds(IReadOnlyList<int> pieceIds, int unknownId, int maxTokens)
    {
        int contentLength = Math.Min(pieceIds.Count, maxTokens - 2);
        var ids = new long[contentLength + 2];
        ids[0] = BeginOfSentenceId;
        for (int i = 0; i < contentLength; i++)
        {
            int id = pieceIds[i];
            ids[i + 1] = id == unknownId ? UnknownModelId : id + FairseqOffset;
        }

        ids[^1] = EndOfSentenceId;
        return ids;
    }

    /// <summary>
    /// Attention-masked mean of the token vectors, then L2 normalisation.
    /// <paramref name="hidden"/> is row-major [sequenceLength, dimensions].
    /// </summary>
    internal static float[] MeanPoolAndNormalize(
        ReadOnlySpan<float> hidden,
        ReadOnlySpan<long> attentionMask,
        int sequenceLength,
        int dimensions)
    {
        if (hidden.Length < sequenceLength * dimensions || attentionMask.Length < sequenceLength)
        {
            throw new InvalidOperationException("The model output does not match the input length.");
        }

        var sums = new double[dimensions];
        long count = 0;
        for (int token = 0; token < sequenceLength; token++)
        {
            if (attentionMask[token] == 0)
            {
                continue;
            }

            count++;
            ReadOnlySpan<float> row = hidden.Slice(token * dimensions, dimensions);
            for (int d = 0; d < dimensions; d++)
            {
                sums[d] += row[d];
            }
        }

        if (count == 0)
        {
            throw new InvalidOperationException("The attention mask selects no tokens.");
        }

        double squared = 0;
        for (int d = 0; d < dimensions; d++)
        {
            sums[d] /= count;
            squared += sums[d] * sums[d];
        }

        if (!double.IsFinite(squared) || squared <= 0)
        {
            throw new InvalidOperationException("The model produced a zero or non-finite embedding.");
        }

        double norm = Math.Sqrt(squared);
        var vector = new float[dimensions];
        for (int d = 0; d < dimensions; d++)
        {
            vector[d] = (float)(sums[d] / norm);
        }

        return vector;
    }

    private Task<EmbeddingModelStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        Task<EmbeddingModelStatus> status;
        lock (_availabilityLock)
        {
            if (_status is null || _status.IsFaulted || _status.IsCanceled)
            {
                // Verification is not tied to one caller's token, so a
                // cancelled caller does not poison the cached result.
                _status = _store.VerifyAsync(CancellationToken.None);
            }

            status = _status;
        }

        return status.WaitAsync(cancellationToken);
    }

    private async Task EnsureReadyAsync(CancellationToken cancellationToken)
    {
        EmbeddingModelStatus status = await GetStatusAsync(cancellationToken).ConfigureAwait(false);
        if (status != EmbeddingModelStatus.Ready)
        {
            throw new InvalidOperationException(status == EmbeddingModelStatus.Corrupt
                ? "The built-in embedding model failed verification."
                : "The built-in embedding model is not installed.");
        }
    }

    private void OnStoreChanged(object? sender, EventArgs e)
    {
        lock (_availabilityLock)
        {
            _status = null;
            _sessionStale = true;
        }
    }

    // Runs inside _gate, so the session and tokenizer are used by one input at a time.
    private float[] EmbedCore(string text, EmbeddingInputKind kind)
    {
        EnsureSession();
        string prepared = PrepareInput(text, kind, _manifest);
        IReadOnlyList<int> pieces = _tokenizer!.EncodeToIds(
            prepared,
            addBeginningOfSentence: false,
            addEndOfSentence: false,
            considerPreTokenization: true,
            considerNormalization: true);
        long[] inputIds = ToModelInputIds(pieces, _tokenizer.UnknownId, _manifest.MaxTokens);
        int length = inputIds.Length;
        long[] attentionMask = new long[length];
        Array.Fill(attentionMask, 1L);
        long[] shape = [1, length];

        var names = new List<string>(3);
        var values = new List<OrtValue>(3);
        try
        {
            AddInput(names, values, "input_ids", OrtValue.CreateTensorValueFromMemory(inputIds, shape));
            AddInput(names, values, "attention_mask", OrtValue.CreateTensorValueFromMemory(attentionMask, shape));
            if (_session!.InputMetadata.ContainsKey("token_type_ids"))
            {
                AddInput(names, values, "token_type_ids", OrtValue.CreateTensorValueFromMemory(new long[length], shape));
            }

            using var runOptions = new RunOptions();
            using IDisposableReadOnlyCollection<OrtValue> outputs = _session.Run(
                runOptions,
                names,
                values,
                ["last_hidden_state"]);
            OrtValue hidden = outputs[0];
            long[] outputShape = hidden.GetTensorTypeAndShape().Shape;
            if (outputShape.Length != 3 || outputShape[1] != length || outputShape[2] != _manifest.Dimensions)
            {
                throw new InvalidOperationException("The model output shape does not match the manifest.");
            }

            return MeanPoolAndNormalize(hidden.GetTensorDataAsSpan<float>(), attentionMask, length, _manifest.Dimensions);
        }
        finally
        {
            foreach (OrtValue value in values)
            {
                value.Dispose();
            }
        }
    }

    private static void AddInput(List<string> names, List<OrtValue> values, string name, OrtValue value)
    {
        names.Add(name);
        values.Add(value);
    }

    private void EnsureSession()
    {
        bool stale;
        lock (_availabilityLock)
        {
            stale = _sessionStale;
            _sessionStale = false;
        }

        if (stale)
        {
            _session?.Dispose();
            _session = null;
            _tokenizer = null;
        }

        if (_session is not null)
        {
            return;
        }

        using (FileStream tokenizerStream = File.OpenRead(_store.GetFilePath(EmbeddingModelFileRole.Tokenizer)))
        {
            _tokenizer = SentencePieceTokenizer.Create(
                tokenizerStream,
                false,
                false);
        }

        using var options = new SessionOptions
        {
            IntraOpNumThreads = IntraOpThreadsFor(Environment.ProcessorCount),
            InterOpNumThreads = 1,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
        };
        _session = new InferenceSession(_store.GetFilePath(EmbeddingModelFileRole.Model), options);
    }
}
