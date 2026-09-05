using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Infrastructure.Ingestion;
using Xunit;

namespace OgmaLibrary.Tests.Ingestion;

/// <summary>Persistence and corruption-boundary tests for the Phase 19 view state.</summary>
public sealed class Phase19CatalogueViewStateStoreTests
{
    [Fact]
    public async Task FileStore_RoundTripsState_AndIgnoresCorruptPreferences()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"ogma-view-state-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            using var store = new FileCatalogueViewStateStore(directory);
            var expected = new CatalogueViewState(
                nameof(CatalogueView.List),
                "systems",
                "Meadows",
                0,
                2,
                5,
                true,
                "shelf-1",
                "Year",
                false,
                3);

            await store.SaveAsync(expected);
            Assert.Equal(expected, await store.LoadAsync());

            await File.WriteAllTextAsync(
                Path.Combine(directory, "catalogue-view-state.json"),
                "{ not valid json");
            Assert.Null(await store.LoadAsync());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
