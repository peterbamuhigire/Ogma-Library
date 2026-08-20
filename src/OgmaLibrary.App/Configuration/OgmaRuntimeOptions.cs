using OgmaLibrary.Infrastructure.Catalogue;

namespace OgmaLibrary.App.Configuration;

/// <summary>
/// Validated, non-secret configuration used to compose the desktop application.
/// Provider credentials are intentionally excluded and remain in OS-backed stores.
/// </summary>
public sealed record OgmaRuntimeOptions
{
    /// <summary>Directory containing the local catalogue and derived application data.</summary>
    public string DataDirectory { get; init; } = CatalogueServiceExtensions.GetDefaultDataDirectory();

    /// <summary>
    /// Compatibility root used by the current single-root implementation. Phase 5
    /// replaces this with the canonical multi-root model.
    /// </summary>
    public string LibraryRoot { get; init; } = CatalogueServiceExtensions.GetDefaultDataDirectory();

    /// <summary>Whether external bibliographic provider adapters may be activated.</summary>
    public bool EnableExternalMetadataProviders { get; init; }

    /// <summary>Whether the pre-release 3D shelf surface may be offered for capability detection.</summary>
    public bool EnableThreeDimensionalShelf { get; init; }

    /// <summary>Whether the opt-in classroom Host capability may be offered.</summary>
    public bool EnableClassroomHost { get; init; }

    /// <summary>Optional explicit PDF worker executable or assembly path.</summary>
    public string? PdfWorkerPath { get; init; }

    /// <summary>Reads supported, non-secret environment settings and validates their syntax.</summary>
    public static OgmaRuntimeOptions FromEnvironment(
        Func<string, string?>? readEnvironmentVariable = null)
    {
        readEnvironmentVariable ??= Environment.GetEnvironmentVariable;

        string defaultDataDirectory = CatalogueServiceExtensions.GetDefaultDataDirectory();
        string dataDirectory = ReadPath(
            readEnvironmentVariable,
            "OGMA_LIBRARY_DATA_DIR",
            defaultDataDirectory);
        string libraryRoot = ReadPath(
            readEnvironmentVariable,
            "OGMA_LIBRARY_ROOT",
            dataDirectory);
        string? workerPath = ReadOptionalPath(readEnvironmentVariable, "OGMA_PDF_WORKER_PATH");

        return new OgmaRuntimeOptions
        {
            DataDirectory = dataDirectory,
            LibraryRoot = libraryRoot,
            EnableExternalMetadataProviders = ReadBoolean(
                readEnvironmentVariable,
                "OGMA_ENABLE_METADATA_PROVIDERS"),
            EnableThreeDimensionalShelf = ReadBoolean(
                readEnvironmentVariable,
                "OGMA_ENABLE_3D_SHELF"),
            EnableClassroomHost = ReadBoolean(
                readEnvironmentVariable,
                "OGMA_ENABLE_CLASSROOM_HOST"),
            PdfWorkerPath = workerPath,
        }.Validate();
    }

    /// <summary>Validates configuration without including configured values in errors.</summary>
    public OgmaRuntimeOptions Validate()
    {
        ValidateAbsolutePath(DataDirectory, nameof(DataDirectory));
        ValidateAbsolutePath(LibraryRoot, nameof(LibraryRoot));

        if (!string.IsNullOrWhiteSpace(PdfWorkerPath))
        {
            ValidateAbsolutePath(PdfWorkerPath, nameof(PdfWorkerPath));
            if (!File.Exists(PdfWorkerPath))
            {
                throw new OgmaConfigurationException(
                    nameof(PdfWorkerPath),
                    "The configured PDF worker file is unavailable.");
            }
        }

        return this;
    }

    private static string ReadPath(
        Func<string, string?> reader,
        string key,
        string fallback)
    {
        string? value = reader(key);
        return NormalizePath(string.IsNullOrWhiteSpace(value) ? fallback : value, key);
    }

    private static string? ReadOptionalPath(Func<string, string?> reader, string key)
    {
        string? value = reader(key);
        return string.IsNullOrWhiteSpace(value) ? null : NormalizePath(value, key);
    }

    private static string NormalizePath(string value, string settingName)
    {
        try
        {
            return Path.GetFullPath(value);
        }
        catch (Exception exception) when (exception is ArgumentException or
                                          NotSupportedException or
                                          PathTooLongException)
        {
            throw new OgmaConfigurationException(
                settingName,
                "The setting does not contain a valid filesystem path.");
        }
    }

    private static bool ReadBoolean(Func<string, string?> reader, string key)
    {
        string? value = reader(key);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (bool.TryParse(value, out bool result))
        {
            return result;
        }

        if (string.Equals(value, "1", StringComparison.Ordinal))
        {
            return true;
        }

        if (string.Equals(value, "0", StringComparison.Ordinal))
        {
            return false;
        }

        throw new OgmaConfigurationException(
            key,
            "The setting must be true, false, 1, or 0.");
    }

    private static void ValidateAbsolutePath(string path, string settingName)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            throw new OgmaConfigurationException(
                settingName,
                "The setting must contain an absolute path.");
        }

        _ = NormalizePath(path, settingName);
    }
}

/// <summary>A redacted configuration validation failure.</summary>
public sealed class OgmaConfigurationException : Exception
{
    /// <summary>Initializes a failure whose message contains a setting name but no value.</summary>
    public OgmaConfigurationException(string settingName, string safeMessage)
        : base($"Configuration setting '{settingName}' is not usable. {safeMessage}")
    {
        SettingName = settingName;
    }

    /// <summary>The non-secret setting name that failed validation.</summary>
    public string SettingName { get; }
}
