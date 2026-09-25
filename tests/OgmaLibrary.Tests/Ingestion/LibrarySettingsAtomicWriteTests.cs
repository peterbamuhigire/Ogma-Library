using OgmaLibrary.Infrastructure.Ingestion;

namespace OgmaLibrary.Tests.Ingestion;

/// <summary>Sept-23 Phase 05 (K28, T05.10): atomic library settings with corrupt-file recovery.</summary>
public sealed class LibrarySettingsAtomicWriteTests : IDisposable
{
    private readonly string _dataDir = Path.Combine(Path.GetTempPath(), $"ogma-settings-{Guid.NewGuid():N}");

    private string SettingsPath => Path.Combine(_dataDir, "library-settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dataDir))
        {
            Directory.Delete(_dataDir, recursive: true);
        }
    }

    [Fact]
    public async Task LibrarySettings_SecondSave_KeepsBackupAndLeavesNoTemporaryFile()
    {
        using var settings = new LibrarySettingsService(_dataDir);
        await settings.SetLibraryRootAsync(@"C:\first");
        await settings.SetLibraryRootAsync(@"C:\second");

        Assert.Equal(@"C:\second", await settings.GetLibraryRootAsync());
        Assert.True(File.Exists(SettingsPath + ".bak"));
        Assert.Contains("first", await File.ReadAllTextAsync(SettingsPath + ".bak"), StringComparison.Ordinal);
        Assert.False(File.Exists(SettingsPath + ".tmp"));
    }

    [Fact]
    public async Task LibrarySettings_TornFile_RecoversFromBackupWithoutThrowing()
    {
        using (var writer = new LibrarySettingsService(_dataDir))
        {
            await writer.SetLibraryRootAsync(@"C:\good");
            await writer.SetExcludedFoldersAsync(["private"]);
        }

        // Simulate a torn write: the main file ends mid-document.
        await File.WriteAllTextAsync(SettingsPath, "{\"LibraryRoot\":\"C:\\\\go");

        using var settings = new LibrarySettingsService(_dataDir);
        string? root = await settings.GetLibraryRootAsync();

        Assert.Equal(@"C:\good", root);
        // The recovered copy was written back, so the next read is clean.
        Assert.Contains("good", await File.ReadAllTextAsync(SettingsPath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LibrarySettings_CorruptFileWithoutBackup_ReturnsDefaultsAndCanSave()
    {
        Directory.CreateDirectory(_dataDir);
        await File.WriteAllTextAsync(SettingsPath, "not json at all");

        using var settings = new LibrarySettingsService(_dataDir);
        Assert.Null(await settings.GetLibraryRootAsync());
        Assert.Empty(await settings.GetExcludedFoldersAsync());

        await settings.SetLibraryRootAsync(@"C:\fresh");
        Assert.Equal(@"C:\fresh", await settings.GetLibraryRootAsync());
    }

    [Fact]
    public async Task LibrarySettings_CorruptFileWithoutBackup_ReportsResetAndKeepsDamagedCopy()
    {
        Directory.CreateDirectory(_dataDir);
        await File.WriteAllTextAsync(SettingsPath, "not json at all");

        using var settings = new LibrarySettingsService(_dataDir);
        Assert.Null(settings.LastRecovery);
        _ = await settings.GetLibraryRootAsync();

        // K95: a reset must be reportable, and the damaged file kept for support.
        Assert.NotNull(settings.LastRecovery);
        Assert.False(settings.LastRecovery!.RestoredFromBackup);
        Assert.NotNull(settings.LastRecovery.QuarantinedFileName);
        string quarantined = Path.Combine(_dataDir, settings.LastRecovery.QuarantinedFileName!);
        Assert.Equal("not json at all", await File.ReadAllTextAsync(quarantined));

        // The replacement is clean, so a second read does not re-trigger recovery.
        using var reread = new LibrarySettingsService(_dataDir);
        _ = await reread.GetLibraryRootAsync();
        Assert.Null(reread.LastRecovery);
    }

    [Fact]
    public async Task LibrarySettings_TornFileWithBackup_ReportsRestore()
    {
        using (var writer = new LibrarySettingsService(_dataDir))
        {
            await writer.SetLibraryRootAsync(@"C:\good");
            await writer.SetExcludedFoldersAsync(["private"]);
        }

        await File.WriteAllTextAsync(SettingsPath, "{\"LibraryRoot\":");

        using var settings = new LibrarySettingsService(_dataDir);
        _ = await settings.GetLibraryRootAsync();

        Assert.NotNull(settings.LastRecovery);
        Assert.True(settings.LastRecovery!.RestoredFromBackup);
    }
}
