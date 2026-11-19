using System.Text.Json;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;

namespace OgmaLibrary.Tests.Catalogue;

/// <summary>
/// Sept-23 Phase 10 (task 10.4): one criteria model. Each predicate and sort runs on a seeded
/// SQLite catalogue, the read model and the in-memory evaluator return the same identities in
/// the same order for every criterion, and every criterion round-trips through a smart shelf.
/// </summary>
public sealed class Sept23Phase10CriteriaTests
{
    private const string A = "P10-A";
    private const string B = "P10-B";
    private const string C = "P10-C";
    private const string D = "P10-D";
    private const string E = "P10-E";
    private const string F = "P10-F";
    private const string G = "P10-G";
    private const string H = "P10-H";
    private const string I = "P10-I";

    public static TheoryData<string, CatalogueFilter, string[]> Predicates => new()
    {
        { "title, case-insensitive", new CatalogueFilter(TitleContains: "ETHICS"), [E, F] },
        { "title, LIKE wildcards are literal", new CatalogueFilter(TitleContains: "100%"), [G] },
        { "author, case-insensitive", new CatalogueFilter(AuthorContains: "ada"), [A, D] },
        { "status", new CatalogueFilter(Status: 1), [C] },
        { "min rating", new CatalogueFilter(MinRating: 4), [A, E, G] },
        { "max rating", new CatalogueFilter(MaxRating: 2), [B, F] },
        { "rating range", new CatalogueFilter(MinRating: 3, MaxRating: 4), [D, E, G] },
        { "unavailable", new CatalogueFilter(IsAvailable: false), [C] },
        { "available", new CatalogueFilter(IsAvailable: true), [A, B, D, E, G, I, F] },
        { "quality below", new CatalogueFilter(QualityBelow: 0.35), [B, G, F] },
        { "missing author", new CatalogueFilter(MissingFields: CatalogueMissingFields.Author), [B, G, F] },
        { "missing ISBN", new CatalogueFilter(MissingFields: CatalogueMissingFields.Isbn), [B, E, G, C] },
        { "missing cover", new CatalogueFilter(MissingFields: CatalogueMissingFields.Cover), [B, E, G, C, I, F] },
        { "missing year", new CatalogueFilter(MissingFields: CatalogueMissingFields.Year), [B, E] },
        {
            "missing author and ISBN",
            new CatalogueFilter(MissingFields: CatalogueMissingFields.Author | CatalogueMissingFields.Isbn),
            [B, G]
        },
        { "needs OCR (locked books excluded)", new CatalogueFilter(NeedsOcr: true), [B, C] },
        { "possible duplicates (copies; ungrouped here)", new CatalogueFilter(PossibleDuplicates: true), [D] },
        {
            "combined",
            new CatalogueFilter(MinRating: 1, QualityBelow: 0.5, MissingFields: CatalogueMissingFields.Author),
            [B, G, F]
        },
    };

    public static TheoryData<CatalogueSortField, bool, string[]> Sorts => new()
    {
        { CatalogueSortField.Title, true, [A, B, D, E, G, C, I, F] },
        { CatalogueSortField.Title, false, [F, I, C, G, E, D, B, A] },
        { CatalogueSortField.Author, true, [D, A, E, C, I, B, G, F] },
        { CatalogueSortField.Year, true, [A, G, F, C, D, I, B, E] },
        { CatalogueSortField.Year, false, [I, D, C, F, G, A, B, E] },
        { CatalogueSortField.Rating, false, [A, E, G, D, B, F, C, I] },
        { CatalogueSortField.Status, false, [C, A, B, D, E, G, I, F] },
        { CatalogueSortField.DateAdded, true, [A, E, F, B, G, D, C, I] },
        { CatalogueSortField.DateAdded, false, [D, G, B, F, E, A, C, I] },
        { CatalogueSortField.LastOpened, false, [E, A, B, D, G, C, I, F] },
        { CatalogueSortField.FileSize, true, [G, B, E, F, A, D, C, I] },
        { CatalogueSortField.Quality, false, [E, A, I, C, D, G, B, F] },
    };

    [Theory]
    [MemberData(nameof(Predicates))]
    public async Task Predicate_ReadModelAndEvaluator_ReturnTheExpectedBooks(
        string name,
        CatalogueFilter filter,
        string[] expected)
    {
        using CatalogueDbContext context = await SeedAsync();
        CatalogueReadModel readModel = CreateReadModel(context);
        CatalogueFilter sorted = filter with { SortField = CatalogueSortField.Title };

        IReadOnlyList<string> fromReadModel = await IdsAsync(readModel, sorted);
        IReadOnlyList<string> fromEvaluator = CatalogueCriteriaEvaluator
            .Apply(await AllAsync(readModel), sorted)
            .Select(book => book.BookId)
            .ToList();

        Assert.True(expected.SequenceEqual(fromReadModel), $"{name}: read model {string.Join(",", fromReadModel)}");
        Assert.True(fromReadModel.SequenceEqual(fromEvaluator), $"{name}: evaluator {string.Join(",", fromEvaluator)}");
        Assert.Equal(expected.Length, await ((ICatalogueReadModel)readModel).CountAsync(sorted with { MaxResults = 1 }));
    }

    [Theory]
    [MemberData(nameof(Sorts))]
    public async Task Sort_ReadModelAndEvaluator_AgreeAndPutMissingValuesLast(
        CatalogueSortField field,
        bool ascending,
        string[] expected)
    {
        using CatalogueDbContext context = await SeedAsync();
        CatalogueReadModel readModel = CreateReadModel(context);
        var filter = new CatalogueFilter(SortField: field, SortAscending: ascending);

        IReadOnlyList<string> fromReadModel = await IdsAsync(readModel, filter);
        IReadOnlyList<string> fromEvaluator = CatalogueCriteriaEvaluator
            .Apply(await AllAsync(readModel), filter)
            .Select(book => book.BookId)
            .ToList();

        Assert.Equal(expected, fromReadModel);
        Assert.Equal(fromReadModel, fromEvaluator);
    }

    [Fact]
    public async Task SortedPage_IsTakenAfterTheSort()
    {
        using CatalogueDbContext context = await SeedAsync();
        CatalogueReadModel readModel = CreateReadModel(context);

        IReadOnlyList<string> page = await IdsAsync(
            readModel,
            new CatalogueFilter(MaxResults: 2, SkipCount: 1, SortField: CatalogueSortField.FileSize));

        Assert.Equal([B, E], page);
    }

    [Fact]
    public async Task Projection_CarriesTheNewSummaryFields()
    {
        using CatalogueDbContext context = await SeedAsync();
        IReadOnlyList<BookSummaryProjection> all = await AllAsync(CreateGroupedReadModel(context));

        BookSummaryProjection a = all.Single(book => book.BookId == A);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), a.DateAddedUtc);
        Assert.Equal(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), a.LastOpenedUtc);
        Assert.Equal(300, a.FileSizeBytes);
        Assert.True(a.HasIsbn);

        BookSummaryProjection d = all.Single(book => book.BookId == D);
        Assert.Equal(2, d.CopyCount);
        Assert.Equal(2, d.OccurrenceCount);

        BookSummaryProjection e = all.Single(book => book.BookId == E);
        Assert.Equal("P10-WORK", e.WorkGroupId);
        Assert.Equal(2, e.EditionCount);
        Assert.DoesNotContain(all, book => book.BookId == F);
        Assert.DoesNotContain(all, book => book.BookId == H);
    }

    [Theory]
    [MemberData(nameof(Predicates))]
    public void SmartShelf_EveryCriterion_RoundTrips(string name, CatalogueFilter filter, string[] expected)
    {
        _ = expected;
        string json = JsonSerializer.Serialize(SmartShelfCriteria.ToConditions(filter));

        Assert.True(SmartShelfQueryParser.TryParse(json, out IReadOnlyList<SmartShelfCondition> parsed), name);
        Assert.True(SmartShelfCriteria.TryToFilter(parsed, out CatalogueFilter restored), name);
        Assert.Equal(filter, restored);
    }

    [Theory]
    [MemberData(nameof(Predicates))]
    public async Task SmartShelf_SavedCriteria_ListTheSameBooksAsTheLiveFilter(
        string name,
        CatalogueFilter filter,
        string[] expected)
    {
        using CatalogueDbContext context = await SeedAsync();
        context.Shelves.Add(new ShelfRow
        {
            ShelfId = "P10-SMART",
            Name = "Saved filter",
            ShelfType = 1,
            Query = JsonSerializer.Serialize(SmartShelfCriteria.ToConditions(filter)),
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync();
        CatalogueReadModel readModel = CreateReadModel(context);

        IReadOnlyList<string> shelf = await IdsAsync(
            readModel,
            new CatalogueFilter(ShelfId: "P10-SMART", SortField: CatalogueSortField.Title));

        Assert.True(expected.SequenceEqual(shelf), $"{name}: smart shelf {string.Join(",", shelf)}");
    }

    [Theory]
    [InlineData("""[{"Field":"Title","Operator":"GreaterThan","Value":"x"}]""")]
    [InlineData("""[{"Field":"HasIsbn","Operator":"Equals","Value":"maybe"}]""")]
    [InlineData("""[{"Field":"QualityScore","Operator":"Contains","Value":"0.5"}]""")]
    [InlineData("""[{"Field":"QualityScore","Operator":"LessThan","Value":"NaN"}]""")]
    [InlineData("""[{"Field":"NoSuchField","Operator":"Equals","Value":"1"}]""")]
    public async Task SmartShelf_DamagedQuery_FailsClosed(string query)
    {
        using CatalogueDbContext context = await SeedAsync();
        context.Shelves.Add(new ShelfRow
        {
            ShelfId = "P10-DAMAGED",
            Name = "Damaged",
            ShelfType = 1,
            Query = query,
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync();

        Assert.False(SmartShelfQueryParser.TryParse(query, out _));
        Assert.Empty(await IdsAsync(CreateReadModel(context), new CatalogueFilter(ShelfId: "P10-DAMAGED")));
    }

    [Fact]
    public void SmartShelf_ConditionOutsideTheCriteriaModel_DoesNotConvert()
    {
        var conditions = new[] { new SmartShelfCondition(SmartShelfField.Year, SmartShelfOperator.GreaterThan, "2000") };

        Assert.False(SmartShelfCriteria.TryToFilter(conditions, out CatalogueFilter filter));
        Assert.Equal(new CatalogueFilter(), filter);
    }

    // Criteria and sort parity are checked without identity grouping: since D-37 a reviewed work
    // is one card, which Projection_CarriesTheNewSummaryFields and Sept23Phase10IdentityUiTests cover.
    private static CatalogueReadModel CreateReadModel(CatalogueDbContext context) => new(context);

    private static CatalogueReadModel CreateGroupedReadModel(CatalogueDbContext context) =>
        new(context, identityGrouping: new WorkGrouping(
            new IdentityGroupBookMembership(E, "P10-WORK", IdentityGroupKind.Work),
            new IdentityGroupBookMembership(F, "P10-WORK", IdentityGroupKind.Work)));

    private static async Task<IReadOnlyList<BookSummaryProjection>> AllAsync(CatalogueReadModel readModel)
    {
        var books = new List<BookSummaryProjection>();
        await foreach (BookSummaryProjection book in readModel.GetBookSummariesAsync(new CatalogueFilter()))
        {
            books.Add(book);
        }

        return books;
    }

    private static async Task<IReadOnlyList<string>> IdsAsync(CatalogueReadModel readModel, CatalogueFilter filter)
    {
        var ids = new List<string>();
        await foreach (BookSummaryProjection book in readModel.GetBookSummariesAsync(filter))
        {
            ids.Add(book.BookId);
        }

        return ids;
    }

    private static async Task<CatalogueDbContext> SeedAsync()
    {
        CatalogueDbContext context = CatalogueTestHelper.CreateInMemoryContext();
        DateTimeOffset seen = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

        static DateTimeOffset Day(int month, int day) => new(2026, month, day, 0, 0, 0, TimeSpan.Zero);

        context.Books.AddRange(
            new BookRow
            {
                BookId = A,
                Title = "Alpha Algebra",
                Status = 0,
                Rating = 5,
                Year = 1990,
                IsbnNormalized = "9780000000001",
                SizeBytes = 300,
                QualityScore = 0.9,
                TextStatus = 1,
                AddedUtc = Day(1, 1),
            },
            new BookRow
            {
                BookId = B,
                Title = "beta Biology",
                Status = 0,
                Rating = 2,
                SizeBytes = 100,
                QualityScore = 0.2,
                TextStatus = 3,
                AddedUtc = Day(2, 1),
            },
            new BookRow
            {
                BookId = C,
                Title = "Gamma Geometry",
                Status = 1,
                Year = 2005,
                QualityScore = 0.5,
                TextStatus = 7,
            },
            new BookRow
            {
                BookId = D,
                Title = "Delta Data",
                Status = 0,
                Rating = 3,
                Year = 2010,
                IsbnNormalized = "9780000000002",
                SizeBytes = 500,
                QualityScore = 0.4,
                TextStatus = 2,
                IsPasswordProtected = true,
                AddedUtc = Day(3, 1),
            },
            new BookRow
            {
                BookId = E,
                Title = "Epsilon Ethics",
                Status = 0,
                Rating = 4,
                SizeBytes = 200,
                QualityScore = 0.95,
                TextStatus = 1,
                AddedUtc = Day(1, 15),
            },
            new BookRow
            {
                BookId = F,
                Title = "Zeta Ethics Revised",
                Status = 0,
                Rating = 1,
                Year = 2001,
                IsbnNormalized = "9780000000003",
                SizeBytes = 250,
                QualityScore = 0.1,
                TextStatus = 1,
                AddedUtc = Day(1, 20),
            },
            new BookRow
            {
                BookId = G,
                Title = "Eta 100% _pure_",
                Status = 0,
                Rating = 4,
                Year = 1999,
                SizeBytes = 50,
                QualityScore = 0.3,
                TextStatus = 1,
                AddedUtc = Day(2, 15),
            },
            new BookRow
            {
                BookId = H,
                Title = "Theta Damaged",
                Status = 0,
                Rating = 5,
                QualityScore = 0.1,
                TextStatus = 3,
            },
            new BookRow
            {
                BookId = I,
                Title = "Iota 1000 xpure",
                Status = 0,
                Year = 2020,
                IsbnNormalized = "9780000000004",
                QualityScore = 0.6,
                TextStatus = 1,
            });

        context.BookFiles.AddRange(
            File(A, "a.pdf"),
            File(B, "b.pdf"),
            File(C, "c.pdf", fileStatus: 1),
            File(D, "d.pdf", validity: 4),
            File(D, "copy/d.pdf", validity: 4),
            File(E, "e.pdf"),
            File(F, "f.pdf"),
            File(G, "g.pdf"),
            File(H, "h.pdf", validity: 2),
            File(I, "i.pdf"));

        context.Authors.AddRange(
            new AuthorRow { AuthorId = 1, NormalizedName = "Ada Lovelace" },
            new AuthorRow { AuthorId = 2, NormalizedName = "Grace Hopper" },
            new AuthorRow { AuthorId = 3, NormalizedName = "Ada Byron" },
            new AuthorRow { AuthorId = 4, NormalizedName = "Ivy Ito" });
        context.BookAuthors.AddRange(
            new BookAuthorRow { BookId = A, AuthorId = 1 },
            new BookAuthorRow { BookId = C, AuthorId = 2 },
            new BookAuthorRow { BookId = D, AuthorId = 3 },
            new BookAuthorRow { BookId = E, AuthorId = 2 },
            new BookAuthorRow { BookId = I, AuthorId = 4 });

        context.ReadingProgress.AddRange(
            new ReadingProgressRow { BookId = A, LastReadUtc = Day(3, 1), CompletionPct = 10 },
            new ReadingProgressRow { BookId = E, LastReadUtc = Day(4, 1), CompletionPct = 50 });

        context.VisualAssetManifests.AddRange(Cover(A), Cover(D));
        await context.SaveChangesAsync();
        return context;

        BookFileRow File(string bookId, string path, int fileStatus = 0, int validity = 0) => new()
        {
            BookId = bookId,
            RelativePath = path,
            FileStatus = fileStatus,
            FileValidity = validity,
            LastSeenUtc = seen,
        };

        VisualAssetManifestRow Cover(string bookId) => new()
        {
            BookId = bookId,
            Kind = (int)VisualAssetKind.Cover,
            Variant = "full",
            RelativePath = $"covers/{bookId}.webp",
            Source = "embedded",
            Status = (int)VisualAssetStatus.Ready,
            Format = "webp",
            WidthPx = 300,
            HeightPx = 450,
            GenerationVersion = 1,
            CreatedUtc = seen,
            UpdatedUtc = seen,
        };
    }

    private sealed class WorkGrouping(params IdentityGroupBookMembership[] memberships) : IIdentityGroupingService
    {
        public Task<IdentityGroupDescriptor> CreateAsync(
            IdentityGroupKind kind,
            IReadOnlyList<string> occurrenceIds,
            string actor,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IdentityGroupDescriptor> MergeAsync(
            string groupId,
            IReadOnlyList<string> occurrenceIds,
            string actor,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IdentityGroupDescriptor> SplitAsync(
            string groupId,
            IReadOnlyList<string> occurrenceIds,
            string actor,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IdentityGroupDescriptor> UndoLastAsync(
            string groupId,
            string actor,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IdentityGroupDescriptor?> FindByOccurrenceAsync(
            string occurrenceId,
            CancellationToken cancellationToken = default) => Task.FromResult<IdentityGroupDescriptor?>(null);

        public Task<IdentityGroupDescriptor?> FindByIdAsync(
            string groupId,
            CancellationToken cancellationToken = default) => Task.FromResult<IdentityGroupDescriptor?>(null);

        public Task<IReadOnlyList<IdentityGroupBookMembership>> FindBookMembershipsAsync(
            IReadOnlyList<string> bookIds,
            bool includeWorkGroups,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IdentityGroupBookMembership>>(
                memberships
                    .Where(item => bookIds.Contains(item.BookId, StringComparer.Ordinal))
                    .Where(item => includeWorkGroups || item.Kind == IdentityGroupKind.Edition)
                    .ToArray());
    }
}
