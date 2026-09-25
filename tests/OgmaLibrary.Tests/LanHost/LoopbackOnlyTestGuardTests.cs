using System.Text.RegularExpressions;

namespace OgmaLibrary.Tests.LanHost;

/// <summary>
/// Guards the test suite against opening non-loopback listeners, which raise OS firewall
/// prompts and make runs depend on the machine's network (Sept-23 Kaizen K04).
/// </summary>
public sealed class LoopbackOnlyTestGuardTests
{
    [Fact]
    public void TestsThatComposeLanHostServices_AlwaysForceLoopback()
    {
        string testsRoot = FindTestsRoot();
        string self = Path.GetFileName(typeof(LoopbackOnlyTestGuardTests).Name + ".cs");
        var offenders = new List<string>();

        foreach (string file in Directory.EnumerateFiles(testsRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (IsBuildOutput(file) || file.EndsWith(self, StringComparison.Ordinal) ||
                file.EndsWith("LoopbackLanHostTestServices.cs", StringComparison.Ordinal))
            {
                continue;
            }

            string text = File.ReadAllText(file);
            if (text.Contains(".AddLanHostServices(", StringComparison.Ordinal) &&
                !text.Contains(".UseLoopbackLanHost()", StringComparison.Ordinal))
            {
                offenders.Add(Path.GetRelativePath(testsRoot, file));
            }

            if (Regex.IsMatch(text, @"IPAddress\.(Any|IPv6Any)\b") ||
                text.Contains("ListenAnyIP", StringComparison.Ordinal))
            {
                offenders.Add(Path.GetRelativePath(testsRoot, file) + " (binds all interfaces)");
            }
        }

        Assert.True(offenders.Count == 0, "Tests must stay on loopback: " + string.Join(", ", offenders));
    }

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string FindTestsRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OgmaLibrary.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, "tests");
    }
}
