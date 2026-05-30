using NetArchTest.Rules;
using OgmaLibrary.Application;
using OgmaLibrary.Domain;
using Xunit;

namespace OgmaLibrary.Tests.Architecture;

/// <summary>
/// Enforces the bounded-context dependency rules from HLD §2.2 as executable tests.
/// A reference that violates the inward-pointing direction fails the build.
/// </summary>
public sealed class ArchitectureTests
{
    private const string Application = "OgmaLibrary.Application";
    private const string Infrastructure = "OgmaLibrary.Infrastructure";
    private const string Reader = "OgmaLibrary.Reader";
    private const string Bookshelf3D = "OgmaLibrary.Bookshelf3D";
    private const string Workers = "OgmaLibrary.Workers";
    private const string App = "OgmaLibrary.App";
    private const string Http = "System.Net.Http";
    private const string DependencyInjection = "Microsoft.Extensions.DependencyInjection";

    /// <summary>The Domain project must depend on no other project (strict isolation).</summary>
    [Fact]
    public void Architecture_DomainProject_HasNoOutwardDependencies()
    {
        var result = Types.InAssembly(typeof(Book).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(Application, Infrastructure, Reader, Bookshelf3D, Workers, App)
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    /// <summary>The Domain and Application layers must make no direct HTTP calls (egress chokepoint, SI-1).</summary>
    [Fact]
    public void Architecture_OnlyInfrastructureUsesHttpClient()
    {
        var domain = Types.InAssembly(typeof(Book).Assembly)
            .ShouldNot().HaveDependencyOn(Http).GetResult();
        var application = Types.InAssembly(typeof(IBenchmarkContext).Assembly)
            .ShouldNot().HaveDependencyOn(Http).GetResult();

        Assert.True(domain.IsSuccessful, Describe(domain));
        Assert.True(application.IsSuccessful, Describe(application));
    }

    /// <summary>Only the App composition root may reference the DI container (HLD §2.3).</summary>
    [Fact]
    public void Architecture_OnlyAppBindsImplementations()
    {
        var domain = Types.InAssembly(typeof(Book).Assembly)
            .ShouldNot().HaveDependencyOn(DependencyInjection).GetResult();
        var application = Types.InAssembly(typeof(IBenchmarkContext).Assembly)
            .ShouldNot().HaveDependencyOn(DependencyInjection).GetResult();

        Assert.True(domain.IsSuccessful, Describe(domain));
        Assert.True(application.IsSuccessful, Describe(application));
    }

    private static string Describe(TestResult result) =>
        result.IsSuccessful
            ? "ok"
            : "Offending types: " + string.Join(", ", result.FailingTypeNames ?? []);
}
