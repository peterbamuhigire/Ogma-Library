using System.Text.Json;
using OgmaLibrary.Application.Search;

namespace OgmaLibrary.Infrastructure.Search;

/// <summary>Atomic file-backed store for local search evaluation artifacts.</summary>
public sealed class JsonSearchEvaluationStore : ISearchEvaluationStore
{
    private const int MaxRunIdLength = 96;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };
    private readonly string _directory;

    /// <summary>Initializes a store rooted beneath the application data directory.</summary>
    public JsonSearchEvaluationStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(_directory);
    }

    /// <inheritdoc />
    public async Task SaveAsync(SearchEvaluationRun run, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        string path = PathFor(run.RunId);
        EvaluationRunDocument document = EvaluationRunDocument.From(run);
        string temporaryPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (FileStream stream = new(
                temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                16 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    /// <inheritdoc />
    public async Task<SearchEvaluationRun?> GetAsync(string runId, CancellationToken cancellationToken = default)
    {
        string path = PathFor(runId);
        if (!File.Exists(path))
        {
            return null;
        }

        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 16 * 1024, true);
        EvaluationRunDocument? document = await JsonSerializer
            .DeserializeAsync<EvaluationRunDocument>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return document?.ToModel();
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(string runId, CancellationToken cancellationToken = default)
    {
        string path = PathFor(runId);
        cancellationToken.ThrowIfCancellationRequested();
        bool existed = File.Exists(path);
        if (existed)
        {
            File.Delete(path);
        }

        return Task.FromResult(existed);
    }

    private string PathFor(string runId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        string normalized = runId.Trim();
        if (normalized.Length > MaxRunIdLength || normalized.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_' and not '.'))
        {
            throw new ArgumentException("Evaluation run id contains unsupported characters.", nameof(runId));
        }

        return Path.Combine(_directory, normalized + ".json");
    }

    private sealed record EvaluationRunDocument(
        string RunId,
        DateTimeOffset CapturedUtc,
        IReadOnlyList<EvaluationCaseDocument> Cases,
        SearchEvaluationReport Report)
    {
        public static EvaluationRunDocument From(SearchEvaluationRun run) => new(
            run.RunId, run.CapturedUtc, run.Cases.Select(EvaluationCaseDocument.From).ToArray(), run.Report);

        public SearchEvaluationRun ToModel() => new(
            RunId, CapturedUtc, Cases.Select(item => item.ToModel()).ToArray(), Report);
    }

    private sealed record EvaluationCaseDocument(
        string QueryId,
        string QueryText,
        IReadOnlyList<string> RankedBookIds,
        IReadOnlyList<string> RelevantBookIds,
        int K)
    {
        public static EvaluationCaseDocument From(SearchEvaluationCase item) => new(
            item.QueryId, item.QueryText, item.RankedBookIds, item.RelevantBookIds.ToArray(), item.K);

        public SearchEvaluationCase ToModel() => new(
            QueryId, QueryText, RankedBookIds,
            RelevantBookIds.ToHashSet(StringComparer.Ordinal), K);
    }
}
