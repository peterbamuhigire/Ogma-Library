using System.Text.Json;
using System.Text.Json.Serialization;

namespace OgmaLibrary.Infrastructure.AI.Onnx;

/// <summary>Role of a file in an embedding model package.</summary>
public enum EmbeddingModelFileRole
{
    /// <summary>The ONNX graph and weights.</summary>
    Model = 0,

    /// <summary>The SentencePiece tokenizer model.</summary>
    Tokenizer = 1,
}

/// <summary>One pinned file of an embedding model.</summary>
/// <param name="Role">What the file is for.</param>
/// <param name="Path">Relative path (forward slashes) under the model folder and the source URL.</param>
/// <param name="Sha256">Lower-case hexadecimal SHA-256 of the file.</param>
/// <param name="SizeBytes">Exact file size in bytes.</param>
public sealed record EmbeddingModelFile(
    EmbeddingModelFileRole Role,
    string Path,
    string Sha256,
    long SizeBytes);

/// <summary>
/// Pinned description of the built-in embedding model: identity, revision,
/// shape, input prefixes and every file with its SHA-256 and size. The
/// default manifest is an embedded resource, so the pinned hashes ship with
/// the code and the model files never enter git (Sept-23 Phase 14, D-103,
/// D-105; currentness rows CUR-25 and CUR-27).
/// </summary>
public sealed class EmbeddingModelManifest
{
    private const string DefaultResourceName = "OgmaLibrary.Infrastructure.AI.Onnx.embedding-model-manifest.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private EmbeddingModelManifest(
        string modelId,
        string revision,
        string licence,
        int dimensions,
        int maxTokens,
        string queryPrefix,
        string passagePrefix,
        Uri source,
        IReadOnlyList<EmbeddingModelFile> files)
    {
        ModelId = modelId;
        Revision = revision;
        Licence = licence;
        Dimensions = dimensions;
        MaxTokens = maxTokens;
        QueryPrefix = queryPrefix;
        PassagePrefix = passagePrefix;
        Source = source;
        Files = files;
    }

    /// <summary>Model identifier stored with every vector.</summary>
    public string ModelId { get; }

    /// <summary>Pinned upstream revision; stored as the vector's model version.</summary>
    public string Revision { get; }

    /// <summary>SPDX licence identifier of the weights.</summary>
    public string Licence { get; }

    /// <summary>Output vector length.</summary>
    public int Dimensions { get; }

    /// <summary>Maximum input length in tokens, including the start and end tokens.</summary>
    public int MaxTokens { get; }

    /// <summary>Prefix added to search queries.</summary>
    public string QueryPrefix { get; }

    /// <summary>Prefix added to stored passages.</summary>
    public string PassagePrefix { get; }

    /// <summary>Pinned base URL the files are fetched from after the user consents.</summary>
    public Uri Source { get; }

    /// <summary>Every file of the model package.</summary>
    public IReadOnlyList<EmbeddingModelFile> Files { get; }

    /// <summary>File-system-safe folder name for <see cref="ModelId"/>.</summary>
    public string FolderName => ModelId.Replace("/", "--", StringComparison.Ordinal);

    /// <summary>Returns the single file with <paramref name="role"/>.</summary>
    public EmbeddingModelFile GetFile(EmbeddingModelFileRole role) => Files.Single(file => file.Role == role);

    /// <summary>Loads the manifest embedded in this assembly.</summary>
    public static EmbeddingModelManifest LoadDefault()
    {
        using Stream stream = typeof(EmbeddingModelManifest).Assembly.GetManifestResourceStream(DefaultResourceName)
            ?? throw new InvalidOperationException("The embedded embedding-model manifest is missing.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    /// <summary>Parses and validates a manifest.</summary>
    /// <exception cref="InvalidDataException">The manifest is malformed or unsafe.</exception>
    public static EmbeddingModelManifest Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        ManifestDto dto;
        try
        {
            dto = JsonSerializer.Deserialize<ManifestDto>(json, SerializerOptions)
                ?? throw new InvalidDataException("The embedding-model manifest is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The embedding-model manifest is not valid JSON.", exception);
        }

        Require(!string.IsNullOrWhiteSpace(dto.ModelId), "modelId is required.");
        Require(!string.IsNullOrWhiteSpace(dto.Revision) && IsSafeSegment(dto.Revision!), "revision is required and must be a plain name.");
        Require(!string.IsNullOrWhiteSpace(dto.Licence), "licence is required.");
        Require(dto.Dimensions > 0, "dimensions must be positive.");
        Require(dto.MaxTokens > 2, "maxTokens must leave room for the start and end tokens.");
        Require(dto.QueryPrefix is not null && dto.PassagePrefix is not null, "queryPrefix and passagePrefix are required.");
        Require(
            Uri.TryCreate(dto.Source, UriKind.Absolute, out Uri? source) && source.Scheme == Uri.UriSchemeHttps,
            "source must be an absolute https URL.");
        Require(dto.Files is { Count: > 0 }, "files are required.");

        string folderName = dto.ModelId!.Replace("/", "--", StringComparison.Ordinal);
        Require(IsSafeSegment(folderName), "modelId must map to a plain folder name.");

        var files = new List<EmbeddingModelFile>(dto.Files!.Count);
        foreach (FileDto file in dto.Files!)
        {
            Require(file.Role is not null, "every file needs a role.");
            Require(file.Path is not null && IsSafeRelativePath(file.Path), $"file path '{file.Path}' is not a safe relative path.");
            Require(file.Sha256 is not null && IsSha256(file.Sha256), $"file '{file.Path}' needs a 64-character hexadecimal SHA-256.");
            Require(file.SizeBytes > 0, $"file '{file.Path}' needs a positive size.");
            files.Add(new EmbeddingModelFile(file.Role!.Value, file.Path!, file.Sha256!.ToLowerInvariant(), file.SizeBytes));
        }

        Require(files.Count(file => file.Role == EmbeddingModelFileRole.Model) == 1, "exactly one model file is required.");
        Require(files.Count(file => file.Role == EmbeddingModelFileRole.Tokenizer) == 1, "exactly one tokenizer file is required.");
        Require(
            files.Select(file => file.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() == files.Count,
            "file paths must be unique.");

        return new EmbeddingModelManifest(
            dto.ModelId!,
            dto.Revision!,
            dto.Licence!,
            dto.Dimensions,
            dto.MaxTokens,
            dto.QueryPrefix!,
            dto.PassagePrefix!,
            source!,
            files);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException("Invalid embedding-model manifest: " + message);
        }
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(char.IsAsciiHexDigit);

    private static bool IsSafeRelativePath(string path)
    {
        if (path.Length == 0 || path.Contains('\\', StringComparison.Ordinal) || path.Contains(':', StringComparison.Ordinal)
            || path.StartsWith('/'))
        {
            return false;
        }

        return path.Split('/').All(IsSafeSegment);
    }

    private static bool IsSafeSegment(string segment) =>
        segment.Length > 0
        && segment != "."
        && segment != ".."
        && segment.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.');

    private sealed class ManifestDto
    {
        public string? ModelId { get; set; }

        public string? Revision { get; set; }

        public string? Licence { get; set; }

        public int Dimensions { get; set; }

        public int MaxTokens { get; set; }

        public string? QueryPrefix { get; set; }

        public string? PassagePrefix { get; set; }

        public string? Source { get; set; }

        public List<FileDto>? Files { get; set; }
    }

    private sealed class FileDto
    {
        public EmbeddingModelFileRole? Role { get; set; }

        public string? Path { get; set; }

        public string? Sha256 { get; set; }

        public long SizeBytes { get; set; }
    }
}
