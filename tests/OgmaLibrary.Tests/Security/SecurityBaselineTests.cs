using System.Text.RegularExpressions;

namespace OgmaLibrary.Tests.Security;

/// <summary>Phase 04 executable checks for security gate evidence.</summary>
public sealed class SecurityBaselineTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void Phase04_SecurityDocuments_CoverRequiredThreatModelAreas()
    {
        string threatModel = ReadRepoFile("docs/security/phase-04-threat-model.md");
        string controlMatrix = ReadRepoFile("docs/security/phase-04-control-matrix.md");
        string riskRegister = ReadRepoFile("docs/security/phase-04-risk-register.md");
        string sastReport = ReadRepoFile("docs/security/phase-04-sast-report.md");
        string qaGate = ReadRepoFile("docs/qa/phase-04-security-gate.md");

        Assert.Contains("LAN host", threatModel, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AI provider", threatModel, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PDF worker", threatModel, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("classroom flows", threatModel, StringComparison.OrdinalIgnoreCase);
        foreach (string category in new[] { "Spoofing", "Tampering", "Repudiation", "Information Disclosure", "Denial of Service", "Elevation of Privilege" })
        {
            Assert.Contains(category, threatModel, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("F-SEC-001", controlMatrix, StringComparison.Ordinal);
        Assert.Contains("F-SEC-004", controlMatrix, StringComparison.Ordinal);
        Assert.Contains("F-SEC-005", controlMatrix, StringComparison.Ordinal);
        Assert.Contains("Phase 05", riskRegister, StringComparison.Ordinal);
        Assert.Contains("dotnet format analyzers", sastReport, StringComparison.Ordinal);
        Assert.Contains("Secret pattern scan", qaGate, StringComparison.Ordinal);
    }

    [Fact]
    public void CiWorkflow_EnforcesPhase04SecurityScans()
    {
        string workflow = ReadRepoFile(".github/workflows/ci.yml");

        Assert.Contains("dotnet restore OgmaLibrary.sln --locked-mode", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet list OgmaLibrary.sln package --vulnerable --include-transitive", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet format analyzers OgmaLibrary.sln --verify-no-changes --no-restore --severity warn", workflow, StringComparison.Ordinal);
        Assert.Contains("Secret pattern scan", workflow, StringComparison.Ordinal);
        Assert.Contains("No high-confidence secret patterns found.", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void SourceAndWorkflowFiles_DoNotContainHighConfidenceSecretPatterns()
    {
        var patterns = new[]
        {
            new Regex("sk_live_[A-Za-z0-9]{16,}", RegexOptions.Compiled),
            new Regex("sk-[A-Za-z0-9]{20,}", RegexOptions.Compiled),
            new Regex("ghp_[A-Za-z0-9]{36}", RegexOptions.Compiled),
            new Regex("github_pat_[A-Za-z0-9_]{20,}", RegexOptions.Compiled),
            new Regex("AIza[0-9A-Za-z\\-_]{35}", RegexOptions.Compiled),
            new Regex("-----BEGIN (RSA |EC |OPENSSH |)?PRIVATE KEY-----", RegexOptions.Compiled),
        };

        string[] files = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "*", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(RepositoryRoot, ".github"), "*", SearchOption.AllDirectories))
            .Where(IsScannableFile)
            .Where(path => !path.EndsWith(Path.Combine(".github", "workflows", "ci.yml"), StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.EndsWith("packages.lock.json", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var hits = new List<string>();
        foreach (string file in files)
        {
            string text = File.ReadAllText(file);
            if (patterns.Any(pattern => pattern.IsMatch(text)))
            {
                hits.Add(Path.GetRelativePath(RepositoryRoot, file));
            }
        }

        Assert.Empty(hits);
    }

    private static bool IsScannableFile(string path)
    {
        string extension = Path.GetExtension(path);
        return extension is ".cs" or ".axaml" or ".json" or ".yml" or ".yaml" or ".props" or ".csproj";
    }

    private static string ReadRepoFile(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "OgmaLibrary.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate OgmaLibrary.sln from the test output directory.");
    }
}
