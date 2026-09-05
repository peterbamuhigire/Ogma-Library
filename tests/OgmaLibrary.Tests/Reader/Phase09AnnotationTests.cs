using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Domain;
using OgmaLibrary.Infrastructure;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;
using OgmaLibrary.Infrastructure.Localization;
using OgmaLibrary.Infrastructure.Pathing;
using OgmaLibrary.Infrastructure.Sidecar;
using OgmaLibrary.Reader.Annotations;
using OgmaLibrary.Tests.Catalogue;

namespace OgmaLibrary.Tests.Reader;

/// <summary>
/// Phase 09 backend tests for annotations, bookmarks, layers, citations, and
/// reading-memory persistence.
/// </summary>
public sealed class Phase09AnnotationTests : IDisposable
{
    private const string BookId = "P09BOOK00000000000000001";

    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;

    public Phase09AnnotationTests()
    {
        (_context, _dbPath) = CatalogueTestHelper.CreateTempFileContext();
        _context.Database.Migrate();
        _context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow
        {
            BookId = BookId,
            Title = "Phase 09 Test Book",
        });
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        SqliteConnection.ClearAllPools();
        CatalogueTestHelper.DeleteTempDb(_dbPath);
    }

    [Fact]
    public void CatalogueDbContext_FileBackedContext_UsesWalJournalMode()
    {
        using var command = _context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "PRAGMA journal_mode;";

        string? journalMode = command.ExecuteScalar()?.ToString();

        Assert.Equal("wal", journalMode, ignoreCase: true);
    }

    [Fact]
    public void CatalogueServiceExtensions_DiContext_UsesWalJournalMode()
    {
        string tempDirectory = Path.Combine(Path.GetTempPath(), $"ogma-di-{Guid.NewGuid():N}");

        try
        {
            var services = new ServiceCollection();
            services.AddCatalogueContext(tempDirectory, tempDirectory);

            using ServiceProvider provider = services.BuildServiceProvider();
            var context = provider.GetRequiredService<CatalogueDbContext>();
            context.Database.Migrate();

            using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "PRAGMA journal_mode;";
            string? journalMode = command.ExecuteScalar()?.ToString();

            command.CommandText = "PRAGMA foreign_keys;";
            long foreignKeys = (long)(command.ExecuteScalar() ?? 0L);

            Assert.Equal("wal", journalMode, ignoreCase: true);
            Assert.Equal(1L, foreignKeys);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(tempDirectory))
            {
                DeleteDirectoryWithRetry(tempDirectory);
            }
        }
    }

    [Fact]
    public async Task AnnotationRepository_CommittedAnnotation_SurvivesFreshContextReopen()
    {
        CatalogueDbContext? context = null;
        string dbPath = string.Empty;

        try
        {
            (context, dbPath) = CatalogueTestHelper.CreateTempFileContext();
            context.Database.Migrate();
            context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow
            {
                BookId = BookId,
                Title = "Reopen Test Book",
            });
            context.SaveChanges();

            var annotation = new AnnotationV2
            {
                Id = "REOPENANNOTATION0000000001",
                BookId = BookId,
                Kind = AnnotationKind.Highlight,
                Regions = [new AnnotationRegion(1, 0.2, 0.1, 0.3, 0.04)],
                HighlightColor = "#FFCC66",
                QuoteText = "Durable quote",
                CreatedUtc = DateTimeOffset.UtcNow,
                ModifiedUtc = DateTimeOffset.UtcNow,
            };

            var repository = new AnnotationV2Repository(context);
            await repository.CreateAsync(annotation, CancellationToken.None);

            using (CatalogueDbContext concurrentReader = CreateContextForPath(dbPath))
            {
                AnnotationV2? concurrentPersisted = await new AnnotationV2Repository(concurrentReader)
                    .FindAsync(annotation.Id, CancellationToken.None);

                Assert.NotNull(concurrentPersisted);
                Assert.Equal("Durable quote", concurrentPersisted.QuoteText);
            }

            context.Dispose();
            context = null;

            using CatalogueDbContext reopened = CreateContextForPath(dbPath);
            AnnotationV2? persisted = await new AnnotationV2Repository(reopened)
                .FindAsync(annotation.Id, CancellationToken.None);

            using var command = reopened.Database.GetDbConnection().CreateCommand();
            command.CommandText = "PRAGMA journal_mode;";
            string? journalMode = command.ExecuteScalar()?.ToString();

            Assert.NotNull(persisted);
            Assert.Equal("Durable quote", persisted.QuoteText);
            Assert.Equal("wal", journalMode, ignoreCase: true);
        }
        finally
        {
            context?.Dispose();
            if (!string.IsNullOrEmpty(dbPath))
            {
                CatalogueTestHelper.DeleteTempDb(dbPath);
            }
        }
    }

    [Fact]
    public void AnnotationRenderHelper_NoRotation_IsIdentity()
    {
        var region = new AnnotationRegion(0, 0.1, 0.2, 0.3, 0.1);

        ScreenRect rect = AnnotationRenderHelper.ToScreenRect(region, 1000, 1414, 0);

        Assert.Equal(100, rect.X, precision: 3);
        Assert.Equal(282.8, rect.Y, precision: 3);
        Assert.Equal(300, rect.Width, precision: 3);
        Assert.Equal(141.4, rect.Height, precision: 3);
    }

    [Fact]
    public void AnnotationRenderHelper_90DegRotation_TransposesRegion()
    {
        var region = new AnnotationRegion(0, 0.1, 0.2, 0.3, 0.1);

        ScreenRect rect = AnnotationRenderHelper.ToScreenRect(region, 1000, 1414, 90);

        Assert.Equal(989.8, rect.X, precision: 3);
        Assert.Equal(100, rect.Y, precision: 3);
        Assert.Equal(141.4, rect.Width, precision: 3);
        Assert.Equal(300, rect.Height, precision: 3);
    }

    [Fact]
    public void AnnotationRenderHelper_180DegRotation_InvertsBothAxes()
    {
        var region = new AnnotationRegion(0, 0.1, 0.2, 0.3, 0.1);

        ScreenRect rect = AnnotationRenderHelper.ToScreenRect(region, 1000, 1414, 180);

        Assert.Equal(600, rect.X, precision: 3);
        Assert.Equal(989.8, rect.Y, precision: 3);
        Assert.Equal(300, rect.Width, precision: 3);
        Assert.Equal(141.4, rect.Height, precision: 3);
    }

    [Fact]
    public void AnnotationRenderHelper_ZoomFactor_DoublesDimensionsAndPosition()
    {
        var region = new AnnotationRegion(0, 0.1, 0.2, 0.3, 0.1);

        ScreenRect rect = AnnotationRenderHelper.ToScreenRect(
            region,
            renderedWidthPx: 1000,
            renderedHeightPx: 1414,
            rotationDegrees: 0,
            zoomFactor: 2.0);

        Assert.Equal(200, rect.X, precision: 3);
        Assert.Equal(565.6, rect.Y, precision: 3);
        Assert.Equal(600, rect.Width, precision: 3);
        Assert.Equal(282.8, rect.Height, precision: 3);
    }

    [Fact]
    public async Task Annotation_RotatedPage_Reload_KeepsScreenRectWithinOnePixel()
    {
        RotatedAnnotationFixture fixture = LoadRotatedAnnotationFixture();
        CatalogueDbContext? context = null;
        string dbPath = string.Empty;

        try
        {
            (context, dbPath) = CatalogueTestHelper.CreateTempFileContext();
            context.Database.Migrate();
            context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow
            {
                BookId = BookId,
                Title = "Rotated Fixture Book",
            });
            context.SaveChanges();

            var annotation = new AnnotationV2
            {
                Id = "ROTATEDRELOADANNOTATION001",
                BookId = BookId,
                Kind = AnnotationKind.Highlight,
                Regions =
                [
                    new AnnotationRegion(
                        fixture.PageIndex,
                        fixture.Region.NormLeft,
                        fixture.Region.NormTop,
                        fixture.Region.NormWidth,
                        fixture.Region.NormHeight),
                ],
                HighlightColor = "#FFCC66",
                QuoteText = "Rotated page passage",
                CreatedUtc = DateTimeOffset.UtcNow,
                ModifiedUtc = DateTimeOffset.UtcNow,
            };
            ScreenRect creationRect = AnnotationRenderHelper.ToScreenRect(
                annotation.Regions[0],
                renderedWidthPx: fixture.RenderedWidthPx,
                renderedHeightPx: fixture.RenderedHeightPx,
                rotationDegrees: fixture.RotationDegrees,
                zoomFactor: fixture.ZoomFactor);

            await new AnnotationV2Repository(context).CreateAsync(annotation, CancellationToken.None);
            context.Dispose();
            context = null;

            using CatalogueDbContext reopened = CreateContextForPath(dbPath);
            AnnotationV2? reloaded = await new AnnotationV2Repository(reopened)
                .FindAsync(annotation.Id, CancellationToken.None);

            Assert.NotNull(reloaded);
            AnnotationRegion region = Assert.Single(reloaded.Regions);
            ScreenRect reloadedRect = AnnotationRenderHelper.ToScreenRect(
                region,
                renderedWidthPx: fixture.RenderedWidthPx,
                renderedHeightPx: fixture.RenderedHeightPx,
                rotationDegrees: fixture.RotationDegrees,
                zoomFactor: fixture.ZoomFactor);

            AssertWithinOnePixel(fixture.ExpectedScreenRect.ToScreenRect(), creationRect);
            AssertWithinOnePixel(creationRect, reloadedRect);
        }
        finally
        {
            context?.Dispose();
            if (!string.IsNullOrEmpty(dbPath))
            {
                CatalogueTestHelper.DeleteTempDb(dbPath);
            }
        }
    }

    [Fact]
    public void AnnotationOverlay_RenderOverhead_100Annotations_Under10msP95()
    {
        var benchmark = new StopwatchBenchmarkContext();
        List<AnnotationRegion> regions = Enumerable.Range(0, 100)
            .Select(index => new AnnotationRegion(
                PageIndex: index % 5,
                NormLeft: 0.05 + (index % 10) * 0.07,
                NormTop: 0.04 + (index / 10) * 0.07,
                NormWidth: 0.05,
                NormHeight: 0.025))
            .ToList();
        var durations = new List<TimeSpan>(capacity: 20);

        for (int iteration = 0; iteration < 20; iteration++)
        {
            using (benchmark.Measure("AnnotationOverlay.Render100"))
            {
                foreach (AnnotationRegion region in regions)
                {
                    _ = AnnotationRenderHelper.ToScreenRect(
                        region,
                        renderedWidthPx: 720,
                        renderedHeightPx: 960,
                        rotationDegrees: iteration % 2 == 0 ? 90 : 0,
                        zoomFactor: 1.5);
                }
            }

            durations.Add(benchmark.GetLastDuration("AnnotationOverlay.Render100"));
        }

        TimeSpan p95 = Percentile95(durations);

        Assert.True(
            p95 <= TimeSpan.FromMilliseconds(10),
            $"100 annotation overlay transforms should stay under 10 ms P95; actual {p95.TotalMilliseconds:F3} ms.");
    }

    [Fact]
    public async Task AnnotationService_CreateAndDelete_PersistsAndEmitsBookScopedEvents()
    {
        var repository = new AnnotationV2Repository(_context);
        using var service = new AnnotationService(repository);
        var events = new List<AnnotationEvent>();
        using IDisposable subscription = ((IAnnotationReadModel)service).Events.Subscribe(events.Add);

        AnnotationV2 saved = await service.CreateHighlightAsync(
            BookId,
            layerId: null,
            regions: [new AnnotationRegion(3, 0.1, 0.2, 0.3, 0.04)],
            color: "#FFCC66",
            quoteText: "quoted text",
            CancellationToken.None);

        IReadOnlyList<AnnotationV2> pageAnnotations = await service.GetForPageAsync(
            BookId,
            3,
            CancellationToken.None);

        await service.DeleteAsync(saved.Id, CancellationToken.None);

        IReadOnlyList<AnnotationV2> afterDelete = await repository.ListForBookAsync(
            BookId,
            CancellationToken.None);

        Assert.Single(pageAnnotations);
        Assert.Empty(afterDelete);
        Assert.Contains(events, e => e is AnnotationEvent.AnnotationCreated created
            && created.BookId == BookId
            && created.Annotation.Id == saved.Id);
        Assert.Contains(events, e => e is AnnotationEvent.AnnotationDeleted deleted
            && deleted.BookId == BookId
            && deleted.AnnotationId == saved.Id);
    }

    [Fact]
    public async Task FaultInjection_RepositoryFailure_DoesNotEmitAnnotationEvents()
    {
        var repository = new FailingAnnotationRepository();
        using var service = new AnnotationService(repository);
        var events = new List<AnnotationEvent>();
        using IDisposable subscription = ((IAnnotationReadModel)service).Events.Subscribe(events.Add);

        await Assert.ThrowsAsync<IOException>(
            () => service.CreateHighlightAsync(
                BookId,
                layerId: null,
                regions: [new AnnotationRegion(0, 0.1, 0.2, 0.3, 0.04)],
                color: "#FFCC66",
                quoteText: "Faulted quote",
                CancellationToken.None));
        await Assert.ThrowsAsync<IOException>(
            () => service.UpdateAsync(
                new AnnotationV2
                {
                    Id = "FAULTUPDATEANNOTATION00001",
                    BookId = BookId,
                    Kind = AnnotationKind.Note,
                    Regions = [new AnnotationRegion(0, 0.1, 0.2, 0.03, 0.03)],
                    NoteText = "Dirty note",
                    CreatedUtc = DateTimeOffset.UtcNow,
                    ModifiedUtc = DateTimeOffset.UtcNow,
                },
                CancellationToken.None));

        Assert.Empty(events);
    }

    [Fact]
    public async Task AnnotationWrite_P95_Under200ms()
    {
        var repository = new AnnotationV2Repository(_context);
        using var service = new AnnotationService(repository);
        var durations = new List<TimeSpan>(capacity: 50);

        for (int index = 0; index < 50; index++)
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();

            await service.CreateHighlightAsync(
                BookId,
                layerId: null,
                regions:
                [
                    new AnnotationRegion(
                        PageIndex: index % 10,
                        NormLeft: 0.05 + (index % 5) * 0.08,
                        NormTop: 0.08 + (index / 5) * 0.02,
                        NormWidth: 0.12,
                        NormHeight: 0.03),
                ],
                color: "#FFCC66",
                quoteText: $"Performance quote {index}",
                CancellationToken.None);

            durations.Add(System.Diagnostics.Stopwatch.GetElapsedTime(started));
        }

        TimeSpan p95 = Percentile95(durations);

        Assert.True(
            p95 <= TimeSpan.FromMilliseconds(200),
            $"Annotation writes should stay under 200 ms P95; actual {p95.TotalMilliseconds:F3} ms.");
    }

    [Fact]
    public async Task FaultInjection_ConcurrentWrite_CompletesWithoutPartialRows()
    {
        CatalogueDbContext? context = null;
        string dbPath = string.Empty;

        try
        {
            (context, dbPath) = CatalogueTestHelper.CreateTempFileContext();
            context.Database.Migrate();
            context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow
            {
                BookId = BookId,
                Title = "Concurrent Write Test Book",
            });
            await context.SaveChangesAsync();
            await context.DisposeAsync();
            context = null;

            Task<AnnotationV2> first = CreateAnnotationInFreshContextAsync(
                dbPath,
                "CONCURRENTANNOTATION000001",
                0);
            Task<AnnotationV2> second = CreateAnnotationInFreshContextAsync(
                dbPath,
                "CONCURRENTANNOTATION000002",
                1);

            AnnotationV2[] saved = await Task.WhenAll(first, second);

            using CatalogueDbContext verifyContext = CreateContextForPath(dbPath);
            IReadOnlyList<AnnotationV2> persisted = await new AnnotationV2Repository(verifyContext)
                .ListForBookAsync(BookId, CancellationToken.None);

            Assert.Equal(2, saved.Length);
            Assert.Equal(2, persisted.Count);
            Assert.All(persisted, annotation =>
            {
                AnnotationRegion region = Assert.Single(annotation.Regions);
                Assert.InRange(region.NormWidth, 0.01, 1.0);
                Assert.InRange(region.NormHeight, 0.01, 1.0);
            });
        }
        finally
        {
            context?.Dispose();
            if (!string.IsNullOrEmpty(dbPath))
            {
                CatalogueTestHelper.DeleteTempDb(dbPath);
            }
        }
    }

    [Fact]
    public async Task FaultInjection_PartialRegionJson_LoadsEmptyRegionsAndCanBeRepaired()
    {
        const string AnnotationId = "PARTIALREGIONJSON000000001";
        var row = new Infrastructure.Catalogue.Entities.AnnotationV2Row
        {
            AnnotationId = AnnotationId,
            BookId = BookId,
            Type = (int)AnnotationKind.Highlight,
            RegionsJson = "[{\"p\":0,\"l\":0.1,",
            ColorKey = "#FFCC66",
            QuoteText = "Interrupted region write",
            CreatedUtc = DateTimeOffset.UtcNow,
            ModifiedUtc = DateTimeOffset.UtcNow,
        };

        _context.AnnotationsV2.Add(row);
        await _context.SaveChangesAsync();

        var repository = new AnnotationV2Repository(_context);
        AnnotationV2? corrupted = await repository.FindAsync(AnnotationId, CancellationToken.None);

        Assert.NotNull(corrupted);
        Assert.Empty(corrupted.Regions);

        await repository.UpdateAsync(
            new AnnotationV2
            {
                Id = corrupted.Id,
                BookId = corrupted.BookId,
                LayerId = corrupted.LayerId,
                Kind = corrupted.Kind,
                Regions = [new AnnotationRegion(0, 0.2, 0.2, 0.3, 0.05)],
                HighlightColor = corrupted.HighlightColor,
                QuoteText = corrupted.QuoteText,
                CreatedUtc = corrupted.CreatedUtc,
                ModifiedUtc = DateTimeOffset.UtcNow,
            },
            CancellationToken.None);

        AnnotationV2? repaired = await repository.FindAsync(AnnotationId, CancellationToken.None);

        Assert.NotNull(repaired);
        AnnotationRegion region = Assert.Single(repaired.Regions);
        Assert.Equal(0.3, region.NormWidth);
    }

    [Fact]
    public async Task AnnotationRepository_InvalidBook_RollsBackAnnotationAndBody()
    {
        var repository = new AnnotationV2Repository(_context);
        var annotation = new AnnotationV2
        {
            Id = "BADBOOKANNOTATION000000001",
            BookId = "MISSINGBOOK000000000000001",
            Kind = AnnotationKind.Highlight,
            Regions = [new AnnotationRegion(0, 0.1, 0.1, 0.2, 0.05)],
            HighlightColor = "#FFCC66",
            QuoteText = "Uncommitted quote",
            CreatedUtc = DateTimeOffset.UtcNow,
            ModifiedUtc = DateTimeOffset.UtcNow,
        };

        await Assert.ThrowsAsync<DbUpdateException>(
            () => repository.CreateAsync(annotation, CancellationToken.None));

        AnnotationV2 valid = await repository.CreateAsync(
            new AnnotationV2
            {
                Id = "GOODBOOKANNOTATION00000001",
                BookId = BookId,
                Kind = AnnotationKind.Highlight,
                Regions = [new AnnotationRegion(0, 0.2, 0.1, 0.2, 0.05)],
                HighlightColor = "#88AA77",
                QuoteText = "Recovered quote",
                CreatedUtc = DateTimeOffset.UtcNow,
                ModifiedUtc = DateTimeOffset.UtcNow,
            },
            CancellationToken.None);

        Assert.Empty(_context.AnnotationsV2.Where(a => a.AnnotationId == annotation.Id));
        Assert.Empty(_context.AnnotationBodies.Where(b => b.AnnotationId == annotation.Id));
        Assert.NotNull(await repository.FindAsync(valid.Id, CancellationToken.None));
    }

    [Fact]
    public async Task FaultInjection_DiskFull_TransactionRolledBack()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"ogma-diskfull-{Guid.NewGuid():N}.db");

        try
        {
            await using (CatalogueDbContext setup = CreateContextForPath(dbPath))
            {
                await setup.Database.MigrateAsync();
                setup.Books.Add(new Infrastructure.Catalogue.Entities.BookRow
                {
                    BookId = BookId,
                    Title = "Disk Full Fault Test Book",
                });
                await setup.SaveChangesAsync();
            }

            await using (CatalogueDbContext faulted = CreateContextForPath(
                dbPath,
                new DiskFullSaveChangesInterceptor()))
            {
                var repository = new AnnotationV2Repository(faulted);
                var annotation = new AnnotationV2
                {
                    Id = "DISKFULLANNOTATION0000001",
                    BookId = BookId,
                    Kind = AnnotationKind.Highlight,
                    Regions = [new AnnotationRegion(0, 0.1, 0.1, 0.2, 0.05)],
                    HighlightColor = "#FFCC66",
                    QuoteText = "Uncommitted disk-full quote",
                    CreatedUtc = DateTimeOffset.UtcNow,
                    ModifiedUtc = DateTimeOffset.UtcNow,
                };

                await Assert.ThrowsAsync<IOException>(
                    () => repository.CreateAsync(annotation, CancellationToken.None));

                Assert.Empty(faulted.ChangeTracker.Entries());
            }

            await using CatalogueDbContext verify = CreateContextForPath(dbPath);
            Assert.Empty(verify.AnnotationsV2.Where(a => a.AnnotationId == "DISKFULLANNOTATION0000001"));

            var recoveredRepository = new AnnotationV2Repository(verify);
            AnnotationV2 recovered = await recoveredRepository.CreateAsync(
                new AnnotationV2
                {
                    Id = "DISKFULLRECOVEREDANNOT001",
                    BookId = BookId,
                    Kind = AnnotationKind.Highlight,
                    Regions = [new AnnotationRegion(0, 0.2, 0.2, 0.2, 0.05)],
                    HighlightColor = "#88AA77",
                    QuoteText = "Recovered after disk-full simulation",
                    CreatedUtc = DateTimeOffset.UtcNow,
                    ModifiedUtc = DateTimeOffset.UtcNow,
                },
                CancellationToken.None);

            Assert.NotNull(await recoveredRepository.FindAsync(recovered.Id, CancellationToken.None));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            CatalogueTestHelper.DeleteTempDb(dbPath);
        }
    }

    [Fact]
    public async Task AnnotationRepository_UpdateWithInvalidLayer_RestoresTrackedStateAndRecovers()
    {
        var repository = new AnnotationV2Repository(_context);
        AnnotationV2 saved = await repository.CreateAsync(
            new AnnotationV2
            {
                Id = "UPDATEFAILANNOTATION000000",
                BookId = BookId,
                Kind = AnnotationKind.Note,
                Regions = [new AnnotationRegion(0, 0.2, 0.1, 0.2, 0.05)],
                NoteText = "Original note",
                CreatedUtc = DateTimeOffset.UtcNow,
                ModifiedUtc = DateTimeOffset.UtcNow,
            },
            CancellationToken.None);

        saved.LayerId = "MISSINGLAYER0000000000001";
        saved.NoteText = "Dirty note";
        saved.ModifiedUtc = DateTimeOffset.UtcNow;

        await Assert.ThrowsAsync<DbUpdateException>(
            () => repository.UpdateAsync(saved, CancellationToken.None));

        AnnotationV2? afterFailure = await repository.FindAsync(saved.Id, CancellationToken.None);
        Assert.NotNull(afterFailure);
        Assert.Null(afterFailure.LayerId);
        Assert.Equal("Original note", afterFailure.NoteText);

        saved.LayerId = null;
        saved.NoteText = "Recovered note";
        saved.ModifiedUtc = DateTimeOffset.UtcNow;
        await repository.UpdateAsync(saved, CancellationToken.None);

        AnnotationV2? recovered = await repository.FindAsync(saved.Id, CancellationToken.None);

        Assert.NotNull(recovered);
        Assert.Equal("Recovered note", recovered.NoteText);
        Assert.Null(recovered.LayerId);
    }

    [Fact]
    public async Task AnnotationService_UpdateNote_PersistsAndEmitsUpdatedEvent()
    {
        var repository = new AnnotationV2Repository(_context);
        using var service = new AnnotationService(repository);
        var events = new List<AnnotationEvent>();
        using IDisposable subscription = ((IAnnotationReadModel)service).Events.Subscribe(events.Add);

        AnnotationV2 saved = await service.CreateNoteAsync(
            BookId,
            layerId: null,
            region: new AnnotationRegion(2, 0.2, 0.3, 0.04, 0.04),
            noteText: "Initial note",
            CancellationToken.None);

        saved.NoteText = "Updated note";
        await service.UpdateAsync(saved, CancellationToken.None);

        AnnotationV2? persisted = await repository.FindAsync(saved.Id, CancellationToken.None);

        Assert.NotNull(persisted);
        Assert.Equal("Updated note", persisted.NoteText);
        Assert.Contains(events, e => e is AnnotationEvent.AnnotationUpdated updated
            && updated.BookId == BookId
            && updated.Annotation.Id == saved.Id
            && updated.Annotation.NoteText == "Updated note");
    }

    [Fact]
    public async Task BookmarkService_CreateRenameDelete_RoundTripsAndEmitsBookScopedDelete()
    {
        var repository = new BookmarkRepository(_context);
        using var service = new BookmarkService(repository);
        var events = new List<AnnotationEvent>();
        using IDisposable subscription = service.Events.Subscribe(events.Add);

        Bookmark created = await service.CreateAsync(
            BookId,
            pageIndex: 6,
            label: null,
            CancellationToken.None);

        Assert.Null(created.Label);

        await service.RenameAsync(created.Id, "Important page", CancellationToken.None);
        IReadOnlyList<Bookmark> renamed = await service.GetForBookAsync(BookId, CancellationToken.None);

        await service.DeleteAsync(created.Id, CancellationToken.None);
        IReadOnlyList<Bookmark> afterDelete = await service.GetForBookAsync(BookId, CancellationToken.None);

        Assert.Single(renamed);
        Assert.Equal("Important page", renamed[0].Label);
        Assert.Empty(afterDelete);
        Assert.Contains(events, e => e is AnnotationEvent.BookmarkCreated createdEvent
            && createdEvent.BookId == BookId
            && createdEvent.Bookmark.PageIndex == 6);
        Assert.Contains(events, e => e is AnnotationEvent.BookmarkDeleted deletedEvent
            && deletedEvent.BookId == BookId
            && deletedEvent.BookmarkId == created.Id);
    }

    [Fact]
    public async Task AnnotationReadModel_SharedProjection_EmitsBookmarkAndLayerEvents()
    {
        using var readModel = new AnnotationReadModel();
        using var bookmarkService = new BookmarkService(new BookmarkRepository(_context), readModel);
        var layerService = new AnnotationLayerService(new AnnotationLayerRepository(_context), readModel);
        var events = new List<AnnotationEvent>();
        using IDisposable subscription = readModel.Events.Subscribe(events.Add);

        Bookmark bookmark = await bookmarkService.CreateAsync(
            BookId,
            pageIndex: 2,
            label: "Projection bookmark",
            CancellationToken.None);
        await bookmarkService.RenameAsync(bookmark.Id, "Renamed projection bookmark", CancellationToken.None);
        await bookmarkService.DeleteAsync(bookmark.Id, CancellationToken.None);

        AnnotationLayer first = await layerService.CreateLayerAsync(
            BookId,
            "Projection primary",
            "#FFCC66",
            CancellationToken.None);
        AnnotationLayer second = await layerService.CreateLayerAsync(
            BookId,
            "Projection secondary",
            "#88AA77",
            CancellationToken.None);
        await layerService.RenameLayerAsync(first.Id, "Renamed primary", CancellationToken.None);
        await layerService.SetVisibilityAsync(first.Id, false, CancellationToken.None);
        await layerService.MergeLayersAsync(BookId, second.Id, first.Id, CancellationToken.None);

        Assert.Contains(events, e => e is AnnotationEvent.BookmarkCreated created
            && created.BookId == BookId
            && created.Bookmark.Id == bookmark.Id);
        Assert.Contains(events, e => e is AnnotationEvent.BookmarkUpdated updated
            && updated.BookId == BookId
            && updated.Bookmark.Id == bookmark.Id
            && updated.Bookmark.Label == "Renamed projection bookmark");
        Assert.Contains(events, e => e is AnnotationEvent.BookmarkDeleted deleted
            && deleted.BookId == BookId
            && deleted.BookmarkId == bookmark.Id);
        Assert.Contains(events, e => e is AnnotationEvent.LayerChanged changed
            && changed.BookId == BookId
            && changed.LayerId == first.Id);
        Assert.Contains(events, e => e is AnnotationEvent.LayerChanged changed
            && changed.BookId == BookId
            && changed.LayerId == second.Id);
    }

    [Fact]
    public async Task BookmarkRepository_CreateWithInvalidBook_RecoversOnSameContext()
    {
        var repository = new BookmarkRepository(_context);
        var invalid = new Bookmark
        {
            Id = 0,
            BookId = "MISSINGBOOK000000000000001",
            PageIndex = 4,
            Label = "Invalid bookmark",
            CreatedUtc = DateTimeOffset.UtcNow,
        };

        await Assert.ThrowsAsync<DbUpdateException>(
            () => repository.CreateAsync(invalid, CancellationToken.None));

        Bookmark valid = await repository.CreateAsync(
            new Bookmark
            {
                Id = 0,
                BookId = BookId,
                PageIndex = 5,
                Label = "Recovered bookmark",
                CreatedUtc = DateTimeOffset.UtcNow,
            },
            CancellationToken.None);

        Assert.Empty(_context.Bookmarks.Where(b => b.BookId == invalid.BookId));
        Assert.True(valid.Id > 0);
        Assert.NotNull(await repository.FindAsync(valid.Id, CancellationToken.None));
    }

    [Fact]
    public async Task FaultInjection_BookmarkAfterSave_Reopen_Present()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"ogma-bookmark-reopen-{Guid.NewGuid():N}.db");
        long bookmarkId;

        try
        {
            await using (CatalogueDbContext setup = CreateContextForPath(dbPath))
            {
                await setup.Database.MigrateAsync();
                setup.Books.Add(new Infrastructure.Catalogue.Entities.BookRow
                {
                    BookId = BookId,
                    Title = "Bookmark Reopen Fault Test Book",
                });
                await setup.SaveChangesAsync();
            }

            await using (CatalogueDbContext writeContext = CreateContextForPath(dbPath))
            {
                var repository = new BookmarkRepository(writeContext);
                Bookmark saved = await repository.CreateAsync(
                    new Bookmark
                    {
                        Id = 0,
                        BookId = BookId,
                        PageIndex = 7,
                        Label = "Saved before simulated termination",
                        CreatedUtc = DateTimeOffset.UtcNow,
                    },
                    CancellationToken.None);
                bookmarkId = saved.Id;
            }

            await using CatalogueDbContext reopened = CreateContextForPath(dbPath);
            var reopenedRepository = new BookmarkRepository(reopened);
            Bookmark? loaded = await reopenedRepository.FindAsync(bookmarkId, CancellationToken.None);

            Assert.NotNull(loaded);
            Assert.Equal(BookId, loaded.BookId);
            Assert.Equal(7, loaded.PageIndex);
            Assert.Equal("Saved before simulated termination", loaded.Label);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            CatalogueTestHelper.DeleteTempDb(dbPath);
        }
    }

    [Fact]
    public async Task FaultInjection_BookmarkAbortBeforeSave_LeavesNoRowAndRecovers()
    {
        var repository = new BookmarkRepository(_context);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var aborted = new Bookmark
        {
            Id = 0,
            BookId = BookId,
            PageIndex = 8,
            Label = "Aborted bookmark",
            CreatedUtc = DateTimeOffset.UtcNow,
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repository.CreateAsync(aborted, cts.Token));

        Bookmark recovered = await repository.CreateAsync(
            new Bookmark
            {
                Id = 0,
                BookId = BookId,
                PageIndex = 9,
                Label = "Recovered after abort",
                CreatedUtc = DateTimeOffset.UtcNow,
            },
            CancellationToken.None);

        IReadOnlyList<Bookmark> bookmarks = await repository.ListForBookAsync(BookId, CancellationToken.None);

        Assert.DoesNotContain(bookmarks, b => b.Label == aborted.Label);
        Assert.Contains(bookmarks, b => b.Id == recovered.Id && b.Label == "Recovered after abort");
    }

    [Fact]
    public async Task FaultInjection_LastLayerDeleteFailure_DoesNotEmitProjectionEvent()
    {
        using var readModel = new AnnotationReadModel();
        var layerService = new AnnotationLayerService(new AnnotationLayerRepository(_context), readModel);
        var events = new List<AnnotationEvent>();
        using IDisposable subscription = readModel.Events.Subscribe(events.Add);

        AnnotationLayer layer = await layerService.CreateLayerAsync(
            BookId,
            "Only layer",
            "#FFCC66",
            CancellationToken.None);
        events.Clear();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => layerService.DeleteAsync(BookId, layer.Id, CancellationToken.None));

        Assert.Empty(events);
    }

    [Fact]
    public async Task AnnotationLayerService_RenameVisibilityMergeAndLastLayerConstraint_Work()
    {
        var layerRepository = new AnnotationLayerRepository(_context);
        var annotationRepository = new AnnotationV2Repository(_context);
        var layerService = new AnnotationLayerService(layerRepository);

        AnnotationLayer defaultLayer = await layerService.CreateLayerAsync(
            BookId,
            "Key arguments",
            "#FFCC66",
            CancellationToken.None);
        AnnotationLayer counterLayer = await layerService.CreateLayerAsync(
            BookId,
            "Counterpoints",
            "#88AA77",
            CancellationToken.None);

        await layerService.RenameLayerAsync(counterLayer.Id, "Questions", CancellationToken.None);
        await layerService.SetVisibilityAsync(counterLayer.Id, false, CancellationToken.None);

        AnnotationV2 annotation = await annotationRepository.CreateAsync(
            new AnnotationV2
            {
                Id = "P09ANNOTATION000000000000",
                BookId = BookId,
                LayerId = counterLayer.Id,
                Kind = AnnotationKind.Highlight,
                Regions = [new AnnotationRegion(0, 0.1, 0.1, 0.2, 0.05)],
                HighlightColor = counterLayer.Color,
                CreatedUtc = DateTimeOffset.UtcNow,
                ModifiedUtc = DateTimeOffset.UtcNow,
            },
            CancellationToken.None);

        await layerService.MergeLayersAsync(
            BookId,
            sourceLayerId: counterLayer.Id,
            targetLayerId: defaultLayer.Id,
            CancellationToken.None);

        IReadOnlyList<AnnotationLayer> layersAfterMerge = await layerService.GetLayersAsync(
            BookId,
            CancellationToken.None);
        AnnotationV2? movedAnnotation = await annotationRepository.FindAsync(
            annotation.Id,
            CancellationToken.None);
        InvalidOperationException lastLayerException = await Assert.ThrowsAsync<InvalidOperationException>(
            () => layerService.DeleteAsync(BookId, defaultLayer.Id, CancellationToken.None));

        Assert.Single(layersAfterMerge);
        Assert.Equal(defaultLayer.Id, movedAnnotation?.LayerId);
        Assert.Contains("last remaining", lastLayerException.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnnotationLayerService_Delete_MovesAnnotationsToDefaultLayer()
    {
        var layerRepository = new AnnotationLayerRepository(_context);
        var annotationRepository = new AnnotationV2Repository(_context);
        var layerService = new AnnotationLayerService(layerRepository);

        AnnotationLayer defaultLayer = await layerService.CreateLayerAsync(
            BookId,
            "Key arguments",
            "#FFCC66",
            CancellationToken.None);
        AnnotationLayer scratchLayer = await layerService.CreateLayerAsync(
            BookId,
            "Scratch",
            "#88AA77",
            CancellationToken.None);
        AnnotationV2 annotation = await annotationRepository.CreateAsync(
            new AnnotationV2
            {
                Id = "P09DELETE_LAYER_MOVE_0001",
                BookId = BookId,
                LayerId = scratchLayer.Id,
                Kind = AnnotationKind.Highlight,
                Regions = [new AnnotationRegion(0, 0.1, 0.1, 0.2, 0.05)],
                HighlightColor = scratchLayer.Color,
                QuoteText = "Move on direct delete",
                CreatedUtc = DateTimeOffset.UtcNow,
                ModifiedUtc = DateTimeOffset.UtcNow,
            },
            CancellationToken.None);

        await layerService.DeleteAsync(BookId, scratchLayer.Id, CancellationToken.None);

        IReadOnlyList<AnnotationLayer> remainingLayers = await layerService.GetLayersAsync(
            BookId,
            CancellationToken.None);
        AnnotationV2? movedAnnotation = await annotationRepository.FindAsync(
            annotation.Id,
            CancellationToken.None);

        AnnotationLayer remainingLayer = Assert.Single(remainingLayers);
        Assert.Equal(defaultLayer.Id, remainingLayer.Id);
        Assert.Equal(defaultLayer.Id, movedAnnotation?.LayerId);
    }

    [Fact]
    public async Task AnnotationLayerService_Delete_IgnoresLayerFromDifferentBook()
    {
        const string OtherBookId = "P09OTHERBOOK0000000000001";
        _context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow
        {
            BookId = OtherBookId,
            Title = "Other Phase 09 Book",
        });
        await _context.SaveChangesAsync();

        using var readModel = new AnnotationReadModel();
        var layerRepository = new AnnotationLayerRepository(_context);
        var layerService = new AnnotationLayerService(layerRepository, readModel);
        var events = new List<AnnotationEvent>();
        using IDisposable subscription = readModel.Events.Subscribe(events.Add);

        await layerService.CreateLayerAsync(BookId, "Key arguments", "#FFCC66", CancellationToken.None);
        await layerService.CreateLayerAsync(BookId, "Questions", "#88AA77", CancellationToken.None);
        AnnotationLayer otherLayer = await layerService.CreateLayerAsync(
            OtherBookId,
            "Other layer",
            "#C7795A",
            CancellationToken.None);
        events.Clear();

        await layerService.DeleteAsync(BookId, otherLayer.Id, CancellationToken.None);

        IReadOnlyList<AnnotationLayer> otherBookLayers = await layerService.GetLayersAsync(
            OtherBookId,
            CancellationToken.None);

        AnnotationLayer persistedOtherLayer = Assert.Single(otherBookLayers);
        Assert.Equal(otherLayer.Id, persistedOtherLayer.Id);
        Assert.Empty(events);
    }

    [Fact]
    public async Task ReadingMemoryService_Save_UpsertsAndValidatesDisposition()
    {
        var repository = new ReadingMemoryRepository(_context);
        var service = new ReadingMemoryService(repository);
        ReadingMemory memory = await service.LoadAsync(BookId, CancellationToken.None);

        memory.OpenedBecause = "Research";
        memory.KeyInsight = "Use normalized regions.";
        memory.OpenQuestions = "How should exports work?";
        memory.Disposition = 4;
        await service.SaveAsync(memory, CancellationToken.None);

        memory.KeyInsight = "Updated insight";
        await service.SaveAsync(memory, CancellationToken.None);

        ReadingMemory loaded = await service.LoadAsync(BookId, CancellationToken.None);
        memory.Disposition = 6;

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.SaveAsync(memory, CancellationToken.None));

        Assert.Equal("Updated insight", loaded.KeyInsight);
        Assert.Equal(4, loaded.Disposition);
        Assert.Single(_context.ReadingMemory.Where(m => m.BookId == BookId));
    }

    [Fact]
    public async Task ReadingMemoryRepository_SaveWithInvalidBook_RecoversOnSameContext()
    {
        var repository = new ReadingMemoryRepository(_context);
        var invalid = new ReadingMemory
        {
            BookId = "MISSINGBOOK000000000000001",
            OpenedBecause = "Invalid recovery test",
            KeyInsight = "Should not persist.",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };

        await Assert.ThrowsAsync<DbUpdateException>(
            () => repository.SaveAsync(invalid, CancellationToken.None));

        var valid = new ReadingMemory
        {
            BookId = BookId,
            OpenedBecause = "Valid recovery test",
            KeyInsight = "Context recovered.",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };

        await repository.SaveAsync(valid, CancellationToken.None);

        ReadingMemory? loaded = await repository.GetForBookAsync(BookId, CancellationToken.None);

        Assert.Empty(_context.ReadingMemory.Where(m => m.BookId == invalid.BookId));
        Assert.NotNull(loaded);
        Assert.Equal("Context recovered.", loaded.KeyInsight);
    }

    [Fact]
    public async Task CitationService_CaptureAndExport_UsesCatalogueMetadata()
    {
        string sidecarRoot = Path.Combine(Path.GetTempPath(), $"ogma-citations-{Guid.NewGuid():N}");
        string contentHash = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";
        var service = new CitationService(new StaticCatalogueReadModel(
            new BookDetailProjection(
                BookId,
                "Phase 09 Test Book",
                ["A. Reader"],
                Year: 2026,
                Isbn: null,
                Doi: null,
                Rating: null,
                Status: 0,
                CoverRelativePath: null,
                RelativePath: "phase-09.pdf",
                Sha256Hash: contentHash,
                SizeBytes: null,
                ReadingProgress: null,
                Annotations: 0,
                MetadataFields: [])),
            new SidecarService(sidecarRoot));

        try
        {
            CitationCard card = await service.CaptureAsync(
                BookId,
                pageIndex: 2,
                selectedText: "Citation passage",
                CancellationToken.None);

            string path = await service.ExportAsync(card, CancellationToken.None);

            Assert.Equal(BookId, card.BookId);
            Assert.Equal("Phase 09 Test Book", card.Title);
            Assert.Equal("A. Reader", card.Author);
            Assert.Equal(3, card.PageNumber);
            Assert.Equal("Citation passage", card.SelectedText);
            Assert.Equal("\"Citation passage\" \u2014 A. Reader, Phase 09 Test Book, p.3", card.ToPlainText());
            Assert.StartsWith(
                PathGuard.CanonicalizeRoot(sidecarRoot),
                PathGuard.CanonicalizeRoot(path),
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains(Path.Combine(".ogma", "citations", "ab"), path, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith(".txt", path, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(card.ToPlainText(), await File.ReadAllTextAsync(path));
        }
        finally
        {
            if (Directory.Exists(sidecarRoot))
            {
                Directory.Delete(sidecarRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CitationService_ExportWithoutBookHash_UsesStableBookIdFallback()
    {
        string sidecarRoot = Path.Combine(Path.GetTempPath(), $"ogma-citations-{Guid.NewGuid():N}");
        var service = new CitationService(new StaticCatalogueReadModel(
            new BookDetailProjection(
                BookId,
                "Phase 09 Test Book",
                ["A. Reader"],
                Year: 2026,
                Isbn: null,
                Doi: null,
                Rating: null,
                Status: 0,
                CoverRelativePath: null,
                RelativePath: "phase-09.pdf",
                Sha256Hash: null,
                SizeBytes: null,
                ReadingProgress: null,
                Annotations: 0,
                MetadataFields: [])),
            new SidecarService(sidecarRoot));

        try
        {
            CitationCard card = await service.CaptureAsync(
                BookId,
                pageIndex: 0,
                selectedText: "Fallback citation passage",
                CancellationToken.None);

            string path = await service.ExportAsync(card, CancellationToken.None);

            Assert.Contains(Path.Combine(".ogma", "citations", "P0"), path, StringComparison.Ordinal);
            Assert.Equal(card.ToPlainText(), await File.ReadAllTextAsync(path));
        }
        finally
        {
            if (Directory.Exists(sidecarRoot))
            {
                Directory.Delete(sidecarRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CitationService_Export_UsesLocalizedFallbackStrings()
    {
        string sidecarRoot = Path.Combine(Path.GetTempPath(), $"ogma-citations-{Guid.NewGuid():N}");
        var localization = new InMemoryLocalizationService();
        localization.SetCulture("fr");
        var service = new CitationService(new StaticCatalogueReadModel(
            new BookDetailProjection(
                BookId,
                Title: null,
                Authors: [],
                Year: null,
                Isbn: null,
                Doi: null,
                Rating: null,
                Status: 0,
                CoverRelativePath: null,
                RelativePath: "phase-09.pdf",
                Sha256Hash: null,
                SizeBytes: null,
                ReadingProgress: null,
                Annotations: 0,
                MetadataFields: [])),
            new SidecarService(sidecarRoot),
            localization);

        try
        {
            CitationCard card = await service.CaptureAsync(
                BookId,
                pageIndex: 0,
                selectedText: "Passage sans metadonnees",
                CancellationToken.None);

            string path = await service.ExportAsync(card, CancellationToken.None);

            Assert.Equal(
                "\"Passage sans metadonnees\" \u2014 Auteur inconnu, Titre inconnu, p. 1",
                await File.ReadAllTextAsync(path));
        }
        finally
        {
            if (Directory.Exists(sidecarRoot))
            {
                Directory.Delete(sidecarRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Phase09_EndToEndRestartSmoke_PersistsReaderArtifacts()
    {
        CatalogueDbContext? context = null;
        string dbPath = string.Empty;
        string sidecarRoot = Path.Combine(Path.GetTempPath(), $"ogma-p09-smoke-{Guid.NewGuid():N}");

        try
        {
            (context, dbPath) = CatalogueTestHelper.CreateTempFileContext();
            context.Database.Migrate();
            context.Books.Add(new Infrastructure.Catalogue.Entities.BookRow
            {
                BookId = BookId,
                Title = "Phase 09 Smoke Book",
                RelativePath = "phase-09-smoke.pdf",
                Sha256Hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            });
            await context.SaveChangesAsync();

            using var readModel = new AnnotationReadModel();
            using var annotationService = new AnnotationService(
                new AnnotationV2Repository(context),
                readModel);
            using var bookmarkService = new BookmarkService(
                new BookmarkRepository(context),
                readModel);
            var layerService = new AnnotationLayerService(
                new AnnotationLayerRepository(context),
                readModel);
            var memoryService = new ReadingMemoryService(
                new ReadingMemoryRepository(context));
            var citationService = new CitationService(
                new StaticCatalogueReadModel(
                    new BookDetailProjection(
                        BookId,
                        "Phase 09 Smoke Book",
                        ["Smoke Author"],
                        Year: 2026,
                        Isbn: null,
                        Doi: null,
                        Rating: null,
                        Status: 0,
                        CoverRelativePath: null,
                        RelativePath: "phase-09-smoke.pdf",
                        Sha256Hash: "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                        SizeBytes: null,
                        ReadingProgress: null,
                        Annotations: 0,
                        MetadataFields: [])),
                new SidecarService(sidecarRoot));

            Bookmark bookmark = await bookmarkService.CreateAsync(
                BookId,
                pageIndex: 4,
                label: "Smoke bookmark",
                CancellationToken.None);

            AnnotationLayer layer = await layerService.CreateLayerAsync(
                BookId,
                "Smoke layer",
                "#88AA77",
                CancellationToken.None);
            await layerService.RenameLayerAsync(layer.Id, "Reviewed layer", CancellationToken.None);

            AnnotationV2 highlight = await annotationService.CreateHighlightAsync(
                BookId,
                layer.Id,
                [new AnnotationRegion(4, 0.12, 0.18, 0.25, 0.04)],
                "#88AA77",
                "Highlighted smoke passage",
                CancellationToken.None);
            AnnotationV2 note = await annotationService.CreateNoteAsync(
                BookId,
                layer.Id,
                new AnnotationRegion(4, 0.2, 0.3, 0.05, 0.05),
                "Smoke note",
                CancellationToken.None);

            ReadingMemory memory = await memoryService.LoadAsync(BookId, CancellationToken.None);
            memory.OpenedBecause = "Validate Phase 09 closeout";
            memory.KeyInsight = "Reader artifacts survive restart.";
            memory.OpenQuestions = "What remains for manual signoff?";
            memory.Disposition = 5;
            await memoryService.SaveAsync(memory, CancellationToken.None);

            CitationCard card = await citationService.CaptureAsync(
                BookId,
                pageIndex: 4,
                selectedText: "Highlighted smoke passage",
                CancellationToken.None);
            string citationPath = await citationService.ExportAsync(card, CancellationToken.None);

            await context.DisposeAsync();
            context = null;

            await using CatalogueDbContext reopened = CreateContextForPath(dbPath);
            IReadOnlyList<Bookmark> bookmarks = await new BookmarkRepository(reopened)
                .ListForBookAsync(BookId, CancellationToken.None);
            IReadOnlyList<AnnotationLayer> layers = await new AnnotationLayerRepository(reopened)
                .ListForBookAsync(BookId, CancellationToken.None);
            IReadOnlyList<AnnotationV2> annotations = await new AnnotationV2Repository(reopened)
                .ListForBookAsync(BookId, CancellationToken.None);
            ReadingMemory? reloadedMemory = await new ReadingMemoryRepository(reopened)
                .GetForBookAsync(BookId, CancellationToken.None);

            Bookmark persistedBookmark = Assert.Single(bookmarks);
            Assert.Equal(bookmark.Id, persistedBookmark.Id);
            Assert.Equal("Smoke bookmark", persistedBookmark.Label);
            Assert.Equal(4, persistedBookmark.PageIndex);

            AnnotationLayer persistedLayer = Assert.Single(layers);
            Assert.Equal(layer.Id, persistedLayer.Id);
            Assert.Equal("Reviewed layer", persistedLayer.Name);

            Assert.Contains(annotations, annotation =>
                annotation.Id == highlight.Id &&
                annotation.Kind == AnnotationKind.Highlight &&
                annotation.LayerId == layer.Id &&
                annotation.QuoteText == "Highlighted smoke passage");
            Assert.Contains(annotations, annotation =>
                annotation.Id == note.Id &&
                annotation.Kind == AnnotationKind.Note &&
                annotation.LayerId == layer.Id &&
                annotation.NoteText == "Smoke note");

            Assert.NotNull(reloadedMemory);
            Assert.Equal("Reader artifacts survive restart.", reloadedMemory.KeyInsight);
            Assert.Equal(5, reloadedMemory.Disposition);

            Assert.True(File.Exists(citationPath), "Citation export should survive the restart smoke.");
            Assert.Equal(
                "\"Highlighted smoke passage\" \u2014 Smoke Author, Phase 09 Smoke Book, p.5",
                await File.ReadAllTextAsync(citationPath));
        }
        finally
        {
            if (context is not null)
            {
                await context.DisposeAsync();
            }

            if (!string.IsNullOrEmpty(dbPath))
            {
                CatalogueTestHelper.DeleteTempDb(dbPath);
            }

            if (Directory.Exists(sidecarRoot))
            {
                Directory.Delete(sidecarRoot, recursive: true);
            }
        }
    }

    private sealed class StaticCatalogueReadModel : ICatalogueReadModel
    {
        private readonly BookDetailProjection _book;

        public StaticCatalogueReadModel(BookDetailProjection book)
        {
            _book = book;
        }

        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<BookDetailProjection?> GetBookDetailAsync(
            string bookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(bookId == _book.BookId ? _book : null);

        public async IAsyncEnumerable<ShelfProjection> GetShelvesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<ReadingProgressProjection?> GetProgressAsync(
            string bookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ReadingProgressProjection?>(null);
    }

    private static CatalogueDbContext CreateContextForPath(
        string dbPath,
        SaveChangesInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<CatalogueDbContext>()
            .UseSqlite(
                $"Data Source={dbPath};Pooling=False",
                sqlite => sqlite.MigrationsAssembly("OgmaLibrary.Infrastructure"));

        if (interceptor is not null)
        {
            builder.AddInterceptors(interceptor);
        }

        return new CatalogueDbContext(builder.Options);
    }

    private static async Task<AnnotationV2> CreateAnnotationInFreshContextAsync(
        string dbPath,
        string annotationId,
        int pageIndex)
    {
        await using CatalogueDbContext context = CreateContextForPath(dbPath);
        var repository = new AnnotationV2Repository(context);
        return await repository.CreateAsync(
            new AnnotationV2
            {
                Id = annotationId,
                BookId = BookId,
                Kind = AnnotationKind.Highlight,
                Regions = [new AnnotationRegion(pageIndex, 0.1, 0.2, 0.25, 0.05)],
                HighlightColor = "#FFCC66",
                QuoteText = $"Concurrent quote {pageIndex}",
                CreatedUtc = DateTimeOffset.UtcNow,
                ModifiedUtc = DateTimeOffset.UtcNow,
            },
            CancellationToken.None);
    }

    private static RotatedAnnotationFixture LoadRotatedAnnotationFixture()
    {
        string path = Path.Combine(
            RepositoryTestPaths.Root,
            "tests",
            "GoldenCorpus",
            "annotations",
            "rotated-page-annotation.json");
        string json = File.ReadAllText(path);
        return System.Text.Json.JsonSerializer.Deserialize<RotatedAnnotationFixture>(
            json,
            JsonOptions)
            ?? throw new InvalidOperationException("Rotated annotation fixture could not be read.");
    }

    private static void AssertWithinOnePixel(ScreenRect expected, ScreenRect actual)
    {
        Assert.InRange(Math.Abs(expected.X - actual.X), 0, 1);
        Assert.InRange(Math.Abs(expected.Y - actual.Y), 0, 1);
        Assert.InRange(Math.Abs(expected.Width - actual.Width), 0, 1);
        Assert.InRange(Math.Abs(expected.Height - actual.Height), 0, 1);
    }

    private static TimeSpan Percentile95(IReadOnlyList<TimeSpan> durations)
    {
        TimeSpan[] sorted = durations.OrderBy(static duration => duration).ToArray();
        int index = (int)Math.Ceiling(sorted.Length * 0.95) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }

    private static void DeleteDirectoryWithRetry(string directory)
    {
        const int attempts = 3;

        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
                return;
            }
            catch (IOException) when (attempt < attempts)
            {
                SqliteConnection.ClearAllPools();
                Thread.Sleep(50);
            }
            catch (UnauthorizedAccessException) when (attempt < attempts)
            {
                SqliteConnection.ClearAllPools();
                Thread.Sleep(50);
            }
        }
    }

    private sealed class DiskFullSaveChangesInterceptor : SaveChangesInterceptor
    {
        private bool _hasThrown;

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result) => ThrowOnce(result);

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ThrowOnce(result));

        private InterceptionResult<int> ThrowOnce(InterceptionResult<int> result)
        {
            if (_hasThrown)
            {
                return result;
            }

            _hasThrown = true;
            throw new IOException("Simulated disk-full failure while flushing the annotation transaction.");
        }
    }

    private sealed record RotatedAnnotationFixture(
        string Fixture,
        string BookId,
        int PageIndex,
        int RotationDegrees,
        double RenderedWidthPx,
        double RenderedHeightPx,
        double ZoomFactor,
        RotatedAnnotationRegionFixture Region,
        ScreenRectFixture ExpectedScreenRect);

    private sealed record RotatedAnnotationRegionFixture(
        double NormLeft,
        double NormTop,
        double NormWidth,
        double NormHeight);

    private sealed record ScreenRectFixture(
        double X,
        double Y,
        double Width,
        double Height)
    {
        public ScreenRect ToScreenRect() => new(X, Y, Width, Height);
    }

    private sealed class FailingAnnotationRepository : IAnnotationV2Repository
    {
        public Task<IReadOnlyList<AnnotationV2>> ListForBookAsync(
            string bookId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AnnotationV2>>([]);

        public Task<IReadOnlyList<AnnotationV2>> ListForPageAsync(
            string bookId,
            int pageIndex,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AnnotationV2>>([]);

        public Task<AnnotationV2?> FindAsync(string annotationId, CancellationToken cancellationToken) =>
            Task.FromResult<AnnotationV2?>(null);

        public Task<AnnotationV2> CreateAsync(AnnotationV2 annotation, CancellationToken cancellationToken) =>
            Task.FromException<AnnotationV2>(new IOException("Injected persistence failure."));

        public Task UpdateAsync(AnnotationV2 annotation, CancellationToken cancellationToken) =>
            Task.FromException(new IOException("Injected persistence failure."));

        public Task DeleteAsync(string annotationId, CancellationToken cancellationToken) =>
            Task.FromException(new IOException("Injected persistence failure."));
    }
}
