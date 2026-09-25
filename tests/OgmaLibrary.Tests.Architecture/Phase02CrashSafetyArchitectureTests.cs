using System.Text.RegularExpressions;
using NetArchTest.Rules;
using OgmaLibrary.Domain;
using Xunit;

namespace OgmaLibrary.Tests.Architecture;

/// <summary>
/// Sept-23 Phase 02 release gates: no unwrapped <c>async void</c> in the App (T02.2), no silent
/// broad catch (T02.4), a logger-free Domain and a log sink composed only in the composition
/// root (T02.3), and the process safety net plus single-instance guard wired before Avalonia
/// starts (T02.1, T02.6).
/// </summary>
public sealed partial class Phase02CrashSafetyArchitectureTests
{
    private const string IgnoredMarker = "Intentionally ignored";

    [Fact]
    public void App_HasNoAsyncVoidOutsideTheSafeInvokeHelper()
    {
        string appRoot = Path.Combine(LocateRepositoryRoot(), "src", "OgmaLibrary.App");
        var offenders = new List<string>();
        foreach (string file in SourceFiles(appRoot))
        {
            string[] lines = File.ReadAllLines(file);
            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index].TrimStart();
                if (line.StartsWith("//", StringComparison.Ordinal) || !AsyncVoidPattern().IsMatch(line))
                {
                    continue;
                }

                bool isSafeInvokeHelper =
                    file.EndsWith(Path.Combine("Infrastructure", "UiActions.cs"), StringComparison.OrdinalIgnoreCase) &&
                    line.StartsWith("public static async void Run(", StringComparison.Ordinal);
                if (!isSafeInvokeHelper)
                {
                    offenders.Add($"{Path.GetRelativePath(appRoot, file)}:{index + 1}");
                }
            }
        }

        Assert.True(offenders.Count == 0, "Unwrapped async void (use UiActions.Run): " + string.Join(", ", offenders));
    }

    [Fact]
    public void BroadCatches_AreNeverSilent()
    {
        string src = Path.Combine(LocateRepositoryRoot(), "src");
        var offenders = new List<string>();
        foreach (string file in SourceFiles(src))
        {
            string text = File.ReadAllText(file);
            foreach (Match match in CatchPattern().Matches(text))
            {
                string? type = match.Groups["type"].Success ? match.Groups["type"].Value.Trim() : null;
                bool broad = type is null ||
                    Regex.IsMatch(type, @"^(System\.)?Exception(\s+\w+)?$", RegexOptions.None, TimeSpan.FromSeconds(1));
                if (!broad)
                {
                    continue;
                }

                string body = ReadBlock(text, match.Index + match.Length);
                string code = CommentPattern().Replace(body, string.Empty).Trim();
                if (code.Length == 0 && !body.Contains(IgnoredMarker, StringComparison.Ordinal))
                {
                    int line = text.AsSpan(0, match.Index).Count('\n') + 1;
                    offenders.Add($"{Path.GetRelativePath(src, file)}:{line}");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            $"Silent broad catch blocks must log or carry '// {IgnoredMarker}: <reason>': " + string.Join(", ", offenders));
    }

    [Fact]
    public void Domain_HasNoLoggingDependency()
    {
        TestResult result = Types.InAssembly(typeof(Work).Assembly)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.Extensions.Logging")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void LogSink_IsComposedOnlyInTheCompositionRoot()
    {
        string root = LocateRepositoryRoot();
        string[] allowed =
        [
            Path.Combine(root, "src", "OgmaLibrary.Infrastructure", "Diagnostics", "RollingFileLoggerProvider.cs"),
            Path.Combine(root, "src", "OgmaLibrary.App", "Infrastructure", "AppDiagnostics.cs"),
        ];
        var offenders = SourceFiles(Path.Combine(root, "src"))
            .Where(file => !allowed.Contains(file, StringComparer.OrdinalIgnoreCase))
            .Where(file =>
            {
                string text = File.ReadAllText(file);
                return text.Contains("new RollingFileLoggerProvider(", StringComparison.Ordinal) ||
                       text.Contains("LoggerFactory.Create(", StringComparison.Ordinal);
            })
            .Select(file => Path.GetRelativePath(root, file))
            .ToList();

        Assert.True(offenders.Count == 0, "Log sink composed outside the root: " + string.Join(", ", offenders));
    }

    [Fact]
    public void ProgramMain_GuardsSingleInstanceAndInstallsHandlersBeforeAvalonia()
    {
        string root = LocateRepositoryRoot();
        string program = File.ReadAllText(Path.Combine(root, "src", "OgmaLibrary.App", "Program.cs"));
        string app = File.ReadAllText(Path.Combine(root, "src", "OgmaLibrary.App", "App.axaml.cs"));

        int guard = program.IndexOf("SingleInstanceGuard.Acquire(", StringComparison.Ordinal);
        int handlers = program.IndexOf("GlobalExceptionHandlers.InstallProcessHandlers()", StringComparison.Ordinal);
        int avalonia = program.IndexOf("BuildAvaloniaApp().StartWithClassicDesktopLifetime", StringComparison.Ordinal);
        Assert.True(guard >= 0 && handlers > guard && avalonia > handlers, "Program.Main order: guard, handlers, Avalonia.");
        Assert.Contains("InstallDispatcherHandler(Dispatcher.UIThread", app, StringComparison.Ordinal);
    }

    private static IEnumerable<string> SourceFiles(string directory) =>
        Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                           !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    private static string ReadBlock(string text, int start)
    {
        int depth = 1;
        int index = start;
        while (index < text.Length && depth > 0)
        {
            depth += text[index] switch
            {
                '{' => 1,
                '}' => -1,
                _ => 0,
            };
            index++;
        }

        return text[start..Math.Max(start, index - 1)];
    }

    private static string LocateRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "OgmaLibrary.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    [GeneratedRegex(@"\basync\s+void\b")]
    private static partial Regex AsyncVoidPattern();

    [GeneratedRegex(@"\bcatch\s*(\((?<type>[^)]*)\))?\s*(when\s*\((?:[^()]|\([^()]*\))*\))?\s*\{")]
    private static partial Regex CatchPattern();

    [GeneratedRegex(@"//[^\n]*|/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex CommentPattern();
}
