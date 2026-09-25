using System.Globalization;

namespace OgmaLibrary.Tests.E2E.Harness;

/// <summary>A window size in physical pixels, written <c>1280x800</c>.</summary>
public readonly record struct WindowSize(int Width, int Height)
{
    /// <summary>The smallest width the shell supports (DesktopShellWindow MinWidth).</summary>
    public const int MinimumWidth = 860;

    /// <summary>Parses <c>WIDTHxHEIGHT</c>.</summary>
    public static WindowSize Parse(string text)
    {
        string[] parts = text.Trim().ToLowerInvariant().Split('x');
        if (parts.Length != 2 ||
            !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int width) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int height))
        {
            throw new FormatException($"Window size '{text}' is not WIDTHxHEIGHT.");
        }

        return new WindowSize(width, height);
    }

    /// <inheritdoc />
    public override string ToString() => $"{Width}x{Height}";
}

/// <summary>
/// Run settings passed by <c>Invoke-GoldenJourneys.ps1</c> through <c>OGMA_E2E_*</c> environment
/// variables. Defaults match the wrapper's defaults so plain <c>dotnet test</c> also works.
/// </summary>
public static class E2ESettings
{
    private static readonly Lazy<string> RunIdValue = new(() =>
        Environment.GetEnvironmentVariable("OGMA_E2E_RUN_ID") is { Length: > 0 } id
            ? id
            : DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));

    /// <summary>The repository root (the folder that holds OgmaLibrary.sln).</summary>
    public static string RepositoryRoot { get; } = LocateRepositoryRoot();

    /// <summary>The run identifier; evidence goes to <c>artifacts/e2e/&lt;run-id&gt;</c>.</summary>
    public static string RunId => RunIdValue.Value;

    /// <summary>The evidence root for this run.</summary>
    public static string ArtifactsDirectory =>
        Environment.GetEnvironmentVariable("OGMA_E2E_ARTIFACTS") is { Length: > 0 } dir
            ? dir
            : Path.Combine(RepositoryRoot, "artifacts", "e2e", RunId);

    /// <summary>The app under test: <c>OGMA_E2E_EXE</c> or the Release build output.</summary>
    public static string ExePath =>
        Environment.GetEnvironmentVariable("OGMA_E2E_EXE") is { Length: > 0 } exe
            ? Path.GetFullPath(exe)
            : Path.Combine(RepositoryRoot, "src", "OgmaLibrary.App", "bin", "Release", "net10.0", "OgmaLibrary.App.exe");

    /// <summary>The window sizes to run (<c>OGMA_E2E_SIZES</c>, default both reference sizes).</summary>
    public static IReadOnlyList<WindowSize> Sizes =>
        (Environment.GetEnvironmentVariable("OGMA_E2E_SIZES") is { Length: > 0 } sizes ? sizes : "1280x800,1920x1080")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(WindowSize.Parse)
            .ToArray();

    /// <summary>xUnit member data: one row per configured size.</summary>
    public static TheoryData<string> SizeData
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (WindowSize size in Sizes)
            {
                data.Add(size.ToString());
            }

            return data;
        }
    }

    /// <summary>The requested theme (<c>Light</c> by default).</summary>
    public static string Theme => Read("OGMA_E2E_THEME", "Light");

    /// <summary>The requested UI culture (<c>en</c> by default).</summary>
    public static string Culture => Read("OGMA_E2E_CULTURE", "en");

    /// <summary>The OS text scale scenario in percent (100 by default).</summary>
    public static int TextScale => int.Parse(Read("OGMA_E2E_TEXT_SCALE", "100"), CultureInfo.InvariantCulture);

    /// <summary>Drive through keyboard input only.</summary>
    public static bool KeyboardOnly => Flag("OGMA_E2E_KEYBOARD_ONLY");

    /// <summary>Fail on unnamed or record-dump accessible names.</summary>
    public static bool UiaAudit => Flag("OGMA_E2E_UIA_AUDIT");

    /// <summary>Fail when localised text is truncated or clipped.</summary>
    public static bool ClippingCheck => Flag("OGMA_E2E_CLIPPING_CHECK");

    /// <summary>Start the local mock AI provider (Phases 15–16).</summary>
    public static bool UseMockAiServer => Flag("OGMA_E2E_MOCK_AI");

    /// <summary>Keep the per-test temp data after the run (for debugging).</summary>
    public static bool KeepTempData => Flag("OGMA_E2E_KEEP");

    /// <summary>How journeys that only need a library choose the folder: <c>auto</c> (hook when compiled in, else the dialog) or <c>dialog</c>.</summary>
    public static string SeedMode => Read("OGMA_E2E_SEED", "auto");

    /// <summary>A pre-generated corpus folder (<c>OGMA_E2E_CORPUS</c>); otherwise one is generated.</summary>
    public static string? CorpusOverride => Environment.GetEnvironmentVariable("OGMA_E2E_CORPUS") is { Length: > 0 } c ? c : null;

    private static string Read(string name, string fallback) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : fallback;

    private static bool Flag(string name) =>
        Environment.GetEnvironmentVariable(name) is "1" or "true" or "True";

    private static string LocateRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OgmaLibrary.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
