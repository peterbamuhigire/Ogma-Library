using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.App;
using OgmaLibrary.Application.Reader;
using Xunit;

namespace OgmaLibrary.Tests.Architecture;

/// <summary>Sept-23 Phase 12 slice 5: split-view session factory wiring (READ-012).</summary>
public sealed class Phase12SplitViewArchitectureTests
{
    [Fact]
    public void SessionFactory_IsBoundOnlyInTheCompositionRoot()
    {
        ServiceCollection services = [];
        services.AddOgmaLibrary();
        Assert.Contains(services, d =>
            d.ServiceType == typeof(IReaderSessionServiceFactory) &&
            d.ImplementationFactory is not null &&
            d.Lifetime == ServiceLifetime.Singleton);

        string src = Path.Combine(LocateRoot(), "src");
        string composition = Path.Combine(src, "OgmaLibrary.App", "Composition") + Path.DirectorySeparatorChar;
        var offenders = Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => !f.StartsWith(composition, StringComparison.OrdinalIgnoreCase))
            .Where(f => File.ReadAllText(f).Contains("new ReaderSessionServiceFactory(", StringComparison.Ordinal))
            .ToList();
        Assert.Empty(offenders);
    }

    private static string LocateRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "OgmaLibrary.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
