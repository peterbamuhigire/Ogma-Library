using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.App;
using OgmaLibrary.App.Configuration;
using OgmaLibrary.App.Startup;
using OgmaLibrary.App.ViewModels;
using OgmaLibrary.Application.Ai;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Infrastructure.Pdf;

namespace OgmaLibrary.Tests.App;

/// <summary>Phase 02 composition matrices and redacted configuration validation.</summary>
public sealed class Phase02CompositionTests
{
    [Fact]
    public void DefaultMatrix_ResolvesAllModules_WithoutExternalProvidersOrAi()
    {
        string directory = CreateTempDirectory();
        try
        {
            ServiceCollection descriptors = [];
            descriptors.AddOgmaLibrary(new OgmaRuntimeOptions
            {
                DataDirectory = directory,
                LibraryRoot = directory,
            });

            Assert.DoesNotContain(descriptors, item => item.ServiceType == typeof(IMetadataProvider));
            Assert.DoesNotContain(descriptors, item => item.ServiceType == typeof(IAiProvider));
            Assert.DoesNotContain(descriptors, item => item.ServiceType == typeof(IAiGateway));
            Assert.Single(descriptors, item => item.ServiceType == typeof(PdfWorkerClient));

            using ServiceProvider services = descriptors.BuildServiceProvider(
                new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
            Assert.NotNull(services.GetRequiredService<IApplicationStartupCoordinator>());
            StartupShellViewModel startupShell =
                services.GetRequiredService<StartupShellViewModel>();
            Assert.False(startupShell.MainShell?.IsHostSharingVisible);
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [Fact]
    public void ExplicitMetadataMatrix_RegistersProviderAdapters_ButNeverAi()
    {
        string directory = CreateTempDirectory();
        try
        {
            ServiceCollection descriptors = [];
            descriptors.AddOgmaLibrary(new OgmaRuntimeOptions
            {
                DataDirectory = directory,
                LibraryRoot = directory,
                EnableExternalMetadataProviders = true,
                EnableClassroomHost = true,
            });

            Assert.Equal(2, descriptors.Count(item => item.ServiceType == typeof(IMetadataProvider)));
            Assert.DoesNotContain(descriptors, item => item.ServiceType == typeof(IAiProvider));
            Assert.DoesNotContain(descriptors, item => item.ServiceType == typeof(IAiGateway));

            using ServiceProvider services = descriptors.BuildServiceProvider(
                new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
            Assert.Equal(2, services.GetServices<IMetadataProvider>().Count());
            Assert.True(services
                .GetRequiredService<StartupShellViewModel>()
                .MainShell?
                .IsHostSharingVisible);
        }
        finally
        {
            DeleteTempDirectory(directory);
        }
    }

    [Fact]
    public void InvalidBoolean_IsRejectedWithoutEchoingConfiguredValue()
    {
        const string privateValue = "private-provider-token-like-value";

        OgmaConfigurationException failure = Assert.Throws<OgmaConfigurationException>(() =>
            OgmaRuntimeOptions.FromEnvironment(key =>
                key == "OGMA_ENABLE_METADATA_PROVIDERS" ? privateValue : null));

        Assert.Equal("OGMA_ENABLE_METADATA_PROVIDERS", failure.SettingName);
        Assert.DoesNotContain(privateValue, failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidPath_IsRejectedWithoutEchoingConfiguredValue()
    {
        const string privateValue = "relative/private/library";
        var options = new OgmaRuntimeOptions
        {
            DataDirectory = privateValue,
            LibraryRoot = Path.GetTempPath(),
        };

        OgmaConfigurationException failure = Assert.Throws<OgmaConfigurationException>(options.Validate);

        Assert.Equal(nameof(OgmaRuntimeOptions.DataDirectory), failure.SettingName);
        Assert.DoesNotContain(privateValue, failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingWorker_IsRejectedWithoutEchoingConfiguredPath()
    {
        string privatePath = Path.Combine(
            Path.GetTempPath(),
            $"private-worker-{Guid.NewGuid():N}.exe");
        var options = new OgmaRuntimeOptions
        {
            DataDirectory = Path.GetTempPath(),
            LibraryRoot = Path.GetTempPath(),
            PdfWorkerPath = privatePath,
        };

        OgmaConfigurationException failure = Assert.Throws<OgmaConfigurationException>(options.Validate);

        Assert.Equal(nameof(OgmaRuntimeOptions.PdfWorkerPath), failure.SettingName);
        Assert.DoesNotContain(privatePath, failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ogma-phase02-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTempDirectory(string directory)
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
