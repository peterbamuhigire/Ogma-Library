using System.Reflection;
using System.Xml.Linq;
using OgmaLibrary.App.ViewModels.Catalogue;
using Xunit;

namespace OgmaLibrary.Tests.Architecture;

/// <summary>
/// Sept-23 Phase 01 (T01.5) release gates: the real-window E2E folder-picker hook is compiled only
/// when a build opts in with <c>-p:OgmaE2EHooks=true</c>. Shipped configurations, packaging and
/// release pipelines never define the <c>OGMA_E2E</c> symbol, and the hook source is fenced by it.
/// </summary>
public sealed class Phase01E2EHookArchitectureTests
{
    private const string Symbol = "OGMA_E2E";
    private const string OptInProperty = "OgmaE2EHooks";

    [Fact]
    public void E2ESymbol_IsDefinedOnlyBehindTheOptInProperty()
    {
        string root = LocateRepositoryRoot();
        var offenders = new List<string>();
        IEnumerable<string> msbuildFiles = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.*", SearchOption.AllDirectories)
            .Where(IsMsBuildFile)
            .Where(file => !IsBuildOutput(file))
            .Append(Path.Combine(root, "Directory.Build.props"));

        foreach (string file in msbuildFiles.Where(File.Exists))
        {
            XDocument document = XDocument.Load(file);
            foreach (XElement define in document.Descendants().Where(e => e.Name.LocalName == "DefineConstants"))
            {
                if (!define.Value.Split(';').Any(part => part.Trim() == Symbol))
                {
                    continue;
                }

                string? condition = define.Parent?.Attribute("Condition")?.Value;
                bool guarded = condition is not null &&
                    condition.Replace(" ", string.Empty, StringComparison.Ordinal)
                        .Equals($"'$({OptInProperty})'=='true'", StringComparison.Ordinal);
                if (!guarded)
                {
                    offenders.Add(Path.GetRelativePath(root, file));
                }
            }
        }

        Assert.True(offenders.Count == 0, $"{Symbol} defined without the {OptInProperty} opt-in: " + string.Join(", ", offenders));
    }

    [Fact]
    public void PackagingAndReleasePipelines_NeverEnableE2EHooks()
    {
        string root = LocateRepositoryRoot();
        var candidates = new List<string>();
        candidates.AddRange(Directory.EnumerateFiles(Path.Combine(root, "packaging"), "*", SearchOption.AllDirectories));
        candidates.AddRange(Directory.EnumerateFiles(Path.Combine(root, ".github", "workflows"), "*.yml")
            .Where(file => !Path.GetFileName(file).Equals("ci.yml", StringComparison.OrdinalIgnoreCase)));

        string[] offenders = candidates
            .Where(file => File.ReadAllText(file).Contains(OptInProperty, StringComparison.OrdinalIgnoreCase))
            .Select(file => Path.GetRelativePath(root, file))
            .ToArray();
        Assert.True(offenders.Length == 0, "Release/packaging files enable E2E hooks: " + string.Join(", ", offenders));

        // ci.yml may enable the hooks only inside the dedicated e2e-windows job.
        string[] ciLines = File.ReadAllLines(Path.Combine(root, ".github", "workflows", "ci.yml"));
        int e2eJob = Array.FindIndex(ciLines, line => line.TrimEnd() == "  e2e-windows:");
        for (int index = 0; index < ciLines.Length; index++)
        {
            if (ciLines[index].Contains(OptInProperty, StringComparison.OrdinalIgnoreCase))
            {
                Assert.True(e2eJob >= 0 && index > e2eJob, $"ci.yml line {index + 1} enables E2E hooks outside the e2e-windows job.");
            }
        }
    }

    [Fact]
    public void E2EHookSource_IsFencedByTheSymbol()
    {
        string root = LocateRepositoryRoot();
        var offenders = new List<string>();
        foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
                     .Where(file => !IsBuildOutput(file)))
        {
            string[] lines = File.ReadAllLines(file);
            int depth = 0;
            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index].Trim();
                if (line.StartsWith("#if " + Symbol, StringComparison.Ordinal))
                {
                    depth++;
                }
                else if (line.StartsWith("#endif", StringComparison.Ordinal) && depth > 0)
                {
                    depth--;
                }
                else if (line.Contains("OGMA_E2E_PICK_FOLDER", StringComparison.Ordinal) && depth == 0)
                {
                    offenders.Add($"{Path.GetRelativePath(root, file)}:{index + 1}");
                }
            }
        }

        Assert.True(offenders.Count == 0, "E2E picker hook outside #if OGMA_E2E: " + string.Join(", ", offenders));
    }

    [Fact]
    public void ShippedAppAssembly_DoesNotContainThePickerHook()
    {
        MethodInfo? hook = typeof(MainShellViewModel).GetMethod(
            "PickSeededFolderForE2EAsync",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.True(hook is null, "The App assembly under test was built with OgmaE2EHooks=true; shipped builds must not contain the E2E hook.");
    }

    private static bool IsMsBuildFile(string file) =>
        file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
        file.EndsWith(".props", StringComparison.OrdinalIgnoreCase) ||
        file.EndsWith(".targets", StringComparison.OrdinalIgnoreCase);

    private static bool IsBuildOutput(string file) =>
        file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        file.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

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
