using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Ingestion;
using OgmaLibrary.Infrastructure.Sidecar;

namespace OgmaLibrary.Tests.Catalogue;

/// <summary>
/// Sept-23 Phase 05 (K20, K27, D-04): derived assets live in the app-data store and
/// legacy <c>&lt;library&gt;/.ogma</c> folders are moved there with hash verification,
/// removing only the folders Ogma created and never a user's file.
/// </summary>
public sealed class LegacyAssetMigrationTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), $"ogma-p05-assets-{Guid.NewGuid():N}");
    private readonly CatalogueDbContext _context = CatalogueTestHelper.CreateInMemoryContext();

    public void Dispose()
    {
        _context.Dispose();
        try
        {
            Directory.Delete(_temp, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup.
        }
    }

    [Fact]
    public async Task LegacyAssets_AreMovedIntoAppData_AndOnlyOgmaFoldersAreRemoved()
    {
        string library = Path.Combine(_temp, "library");
        string data = Path.Combine(_temp, "data");
        string cover = Write(library, ".ogma/covers/ab/abcd.jpg", "cover-bytes");
        string spine = Write(library, ".ogma/spines/ab/abcd.jpg", "spine-bytes");
        string userFile = Write(library, ".ogma/notes-from-user.txt", "keep me");
        string backup = Write(library, ".ogma/backups/ab/abcd.pdf", "%PDF-backup");
        string book = Write(library, "Science/book.pdf", "%PDF-book");

        var roots = new LibraryRootService(_context, new FileSystemLibraryRootPlatformAdapter());
        await roots.AddAsync(library);
        var migration = new LegacyAssetMigrationService(_context, data);

        LegacyAssetMigrationResult result = await migration.MigrateAsync([]);

        Assert.Equal(2, result.Copied);
        Assert.Equal(2, result.Removed);
        Assert.Equal("cover-bytes", await File.ReadAllTextAsync(Path.Combine(data, ".ogma", "covers", "ab", "abcd.jpg")));
        Assert.Equal("spine-bytes", await File.ReadAllTextAsync(Path.Combine(data, ".ogma", "spines", "ab", "abcd.jpg")));
        Assert.False(File.Exists(cover));
        Assert.False(File.Exists(spine));
        Assert.False(Directory.Exists(Path.Combine(library, ".ogma", "covers")));
        Assert.False(Directory.Exists(Path.Combine(library, ".ogma", "spines")));

        // Never the user's files, the write-back backups, or the library's PDFs.
        Assert.True(File.Exists(userFile));
        Assert.True(File.Exists(backup));
        Assert.True(File.Exists(book));
        Assert.Equal(1, await _context.AuditEvents.CountAsync(evt => evt.EventType == "LegacyAssetsMigrated"));

        // The locator resolves the unchanged catalogue key into the store.
        var locator = new AssetLocator(data);
        Assert.Equal(
            Path.Combine(Path.GetFullPath(data), ".ogma", "covers", "ab", "abcd.jpg"),
            locator.GetAbsolutePath(".ogma/covers/ab/abcd.jpg"));
    }

    [Fact]
    public async Task LegacyAssets_OnlyDerivedFolders_TheWholeOgmaFolderIsRemoved()
    {
        string library = Path.Combine(_temp, "library");
        string data = Path.Combine(_temp, "data");
        Write(library, ".ogma/covers/ab/abcd.jpg", "cover-bytes");
        Write(library, ".ogma/thumbnails/ab/abcd.jpg", "thumb-bytes");

        var migration = new LegacyAssetMigrationService(_context, data);
        await migration.MigrateAsync([library]);

        Assert.False(Directory.Exists(Path.Combine(library, ".ogma")));
        Assert.True(Directory.Exists(library));
    }

    [Fact]
    public async Task LegacyAssets_ConflictingTarget_KeepsTheSourceUntouched()
    {
        string library = Path.Combine(_temp, "library");
        string data = Path.Combine(_temp, "data");
        string source = Write(library, ".ogma/covers/ab/abcd.jpg", "library-version");
        Write(data, ".ogma/covers/ab/abcd.jpg", "store-version");

        LegacyAssetMigrationResult result = await new LegacyAssetMigrationService(_context, data)
            .MigrateAsync([library]);

        Assert.Equal(1, result.Kept);
        Assert.True(File.Exists(source));
        Assert.Equal("store-version", await File.ReadAllTextAsync(Path.Combine(data, ".ogma", "covers", "ab", "abcd.jpg")));
    }

    [Fact]
    public void AssetStore_IsTheDataDirectory_NotTheLibraryRoot()
    {
        // K20/K27 regression: the sidecar used to resolve against the startup library
        // root, so a library chosen with OGMA_LIBRARY_ROOT received a hidden .ogma folder.
        string library = Path.Combine(_temp, "library");
        string data = Path.Combine(_temp, "data");
        Directory.CreateDirectory(library);
        using ServiceProvider provider = new ServiceCollection()
            .AddCatalogueContext(data, library)
            .BuildServiceProvider();

        IAssetLocator locator = provider.GetRequiredService<IAssetLocator>();
        ISidecarService sidecar = provider.GetRequiredService<ISidecarService>();
        string coverPath = sidecar.Resolve(new string('a', 64), SidecarClass.Covers);

        Assert.Equal(Path.GetFullPath(data), locator.AssetStoreRoot);
        Assert.StartsWith(Path.GetFullPath(data), coverPath, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(Path.Combine(library, ".ogma")));
        Assert.Null(locator.GetAbsolutePath(".ogma/covers/../../escape.jpg"));
        Assert.Null(locator.GetAbsolutePath("covers/not-a-key.jpg"));
    }

    private static string Write(string root, string relative, string content)
    {
        string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }
}
