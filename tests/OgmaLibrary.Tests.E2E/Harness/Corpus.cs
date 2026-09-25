using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OgmaLibrary.Tests.E2E.Harness;

/// <summary>One valid work in the corpus oracle.</summary>
public sealed record CorpusBook(
    [property: JsonPropertyName("file")] string File,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("author")] string? Author,
    [property: JsonPropertyName("pages")] int Pages);

/// <summary>An invalid file the catalogue must not present as a normal book.</summary>
public sealed record CorpusInvalid(
    [property: JsonPropertyName("file")] string File,
    [property: JsonPropertyName("displayHint")] string DisplayHint,
    [property: JsonPropertyName("reason")] string Reason);

/// <summary>A search probe and the title expected in the top three.</summary>
public sealed record CorpusSearch(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("query")] string Query,
    [property: JsonPropertyName("expectTitle")] string ExpectTitle);

/// <summary>The reader probe.</summary>
public sealed record CorpusReader(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("pages")] int Pages,
    [property: JsonPropertyName("resumePage")] int ResumePage);

/// <summary><c>tests/fixtures/corpus/expected.json</c>.</summary>
public sealed record CorpusOracle(
    [property: JsonPropertyName("fileCount")] int FileCount,
    [property: JsonPropertyName("catalogueCount")] int CatalogueCount,
    [property: JsonPropertyName("catalogue")] IReadOnlyList<CorpusBook> Catalogue,
    [property: JsonPropertyName("invalid")] IReadOnlyList<CorpusInvalid> Invalid,
    [property: JsonPropertyName("searches")] IReadOnlyList<CorpusSearch> Searches,
    [property: JsonPropertyName("reader")] CorpusReader Reader);

/// <summary>
/// The synthetic corpus (T01.4): generated once per machine and generator version by
/// <c>tests/fixtures/corpus/New-SyntheticCorpus.py</c>, then copied into each test's library folder.
/// </summary>
public static class Corpus
{
    private static readonly Lazy<string> SourceValue = new(EnsureGenerated);
    private static readonly Lazy<CorpusOracle> OracleValue = new(() =>
        JsonSerializer.Deserialize<CorpusOracle>(File.ReadAllText(Path.Combine(FixtureDirectory, "expected.json")))
        ?? throw new InvalidDataException("expected.json is empty."));

    /// <summary>The fixture folder in the repository.</summary>
    public static string FixtureDirectory => Path.Combine(E2ESettings.RepositoryRoot, "tests", "fixtures", "corpus");

    /// <summary>The generated corpus folder.</summary>
    public static string SourceDirectory => SourceValue.Value;

    /// <summary>The journey oracle.</summary>
    public static CorpusOracle Oracle => OracleValue.Value;

    /// <summary>Copies the corpus into <paramref name="destination"/>.</summary>
    public static void CopyTo(string destination)
    {
        string source = SourceDirectory;
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static string EnsureGenerated()
    {
        if (E2ESettings.CorpusOverride is { } overridden)
        {
            return overridden;
        }

        string generator = Path.Combine(FixtureDirectory, "New-SyntheticCorpus.py");
        string version = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(generator)))[..12].ToLowerInvariant();
        string target = Path.Combine(Path.GetTempPath(), "ogma-e2e", "corpus-" + version);
        if (Directory.Exists(target) &&
            Directory.EnumerateFiles(target, "*", SearchOption.AllDirectories).Count() == Oracle.FileCount)
        {
            return target;
        }

        string staging = target + ".tmp-" + Environment.ProcessId;
        if (Directory.Exists(staging))
        {
            Directory.Delete(staging, recursive: true);
        }

        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("OGMA_E2E_PYTHON") ?? "python")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add(generator);
        start.ArgumentList.Add(staging);
        start.Environment["PYTHONUTF8"] = "1";
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Could not start python.");
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        if (!process.WaitForExit(600_000) || process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Corpus generation failed (pip install pymupdf pypdf): {output}");
        }

        if (Directory.Exists(target))
        {
            Directory.Delete(target, recursive: true);
        }

        Directory.Move(staging, target);
        return target;
    }
}
