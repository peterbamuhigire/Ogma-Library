using Microsoft.EntityFrameworkCore;
using OgmaLibrary.App.ViewModels.Catalogue;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Navigation;
using OgmaLibrary.Infrastructure.Catalogue;
using OgmaLibrary.Infrastructure.Catalogue.Entities;
using OgmaLibrary.Infrastructure.Catalogue.Repositories;
using OgmaLibrary.Infrastructure.Commands;
using OgmaLibrary.Infrastructure.Localization;
using Xunit;

namespace OgmaLibrary.Tests.Catalogue.Phase11;

/// <summary>
/// Sept-23 Phase 11 slice 3 (task 11.4): inline metadata editing in the inspector, with
/// per-field provenance and undo. The first group runs the real SQLite write path; the second
/// drives <see cref="DetailFieldViewModel"/> through <see cref="BookDetailViewModel"/>.
/// </summary>
public sealed class DetailFieldEditTests : IDisposable
{
    private const string BookId = "book-p11-edit";
    private readonly CatalogueDbContext _context;
    private readonly string _dbPath;
    private readonly CatalogueWriteService _writeService;
    private readonly CatalogueReadModel _readModel;

    /// <summary>Creates a fresh temporary SQLite catalogue.</summary>
    public DetailFieldEditTests()
    {
        (_context, _dbPath) = CatalogueTestHelper.CreateTempFileContext();
        _context.Database.EnsureCreated();
        _writeService = new CatalogueWriteService(_context, new AuditRepository(_context));
        _readModel = new CatalogueReadModel(_context);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _context.Dispose();
        CatalogueTestHelper.DeleteTempDb(_dbPath);
    }

    // ── SQLite write path ──────────────────────────────────────────────────────

    [Fact]
    public async Task TitleEdit_ThroughHistory_UpdatesDetail_KeepsProviderRow_AndUndoRestores()
    {
        await AddBookAsync();
        var history = new CommandHistory();
        var edit = new MetadataFieldEditUndoableCommand(_writeService, BookId, EditableMetadataFields.Title, "Edited Title");

        await history.ExecuteAsync(edit);
        _context.ChangeTracker.Clear();

        BookDetailProjection after = (await _readModel.GetBookDetailAsync(BookId))!;
        Assert.Equal("Edited Title", after.Title);
        MetadataFieldProjection user = Assert.Single(after.MetadataFields, f => f.Source == "User");
        Assert.True(user.IsOverridden);
        Assert.Equal("Edited Title", user.Value);
        MetadataFieldProjection provider = Assert.Single(after.MetadataFields, f => f.Source == "GoogleBooks");
        Assert.False(provider.IsOverridden);
        Assert.Equal("Provider Title", provider.Value);
        Assert.True(edit.CanUndo);

        await history.UndoAsync();
        _context.ChangeTracker.Clear();

        BookDetailProjection undone = (await _readModel.GetBookDetailAsync(BookId))!;
        Assert.Equal("Original Title", undone.Title);
        Assert.DoesNotContain(undone.MetadataFields, f => f.Source == "User");
        Assert.False(edit.CanUndo);
        Assert.Equal(1, await _context.AuditEvents.CountAsync(a => a.EventType == "MetadataEditUndo"));
    }

    [Fact]
    public async Task YearIsbnDoiAndAuthorEdits_MirrorToCanonicalFields_AndUndoRestoresEach()
    {
        await AddBookAsync();
        var history = new CommandHistory();
        MetadataFieldEditUndoableCommand[] edits =
        [
            new(_writeService, BookId, EditableMetadataFields.Year, "1843"),
            new(_writeService, BookId, EditableMetadataFields.Isbn, "9780306406157"),
            new(_writeService, BookId, EditableMetadataFields.Doi, "10.1000/xyz123"),
            new(_writeService, BookId, EditableMetadataFields.Author, "Ada Lovelace; Charles Babbage"),
        ];
        foreach (MetadataFieldEditUndoableCommand edit in edits)
        {
            await history.ExecuteAsync(edit);
        }

        _context.ChangeTracker.Clear();
        BookDetailProjection after = (await _readModel.GetBookDetailAsync(BookId))!;
        Assert.Equal(1843, after.Year);
        Assert.Equal("9780306406157", after.Isbn);
        Assert.Equal("10.1000/xyz123", after.Doi);
        Assert.Equal(["Ada Lovelace", "Charles Babbage"], after.Authors);

        for (int i = 0; i < edits.Length; i++)
        {
            await history.UndoAsync();
        }

        _context.ChangeTracker.Clear();
        BookDetailProjection undone = (await _readModel.GetBookDetailAsync(BookId))!;
        Assert.Equal(1999, undone.Year);
        Assert.Null(undone.Isbn);
        Assert.Null(undone.Doi);
        Assert.Equal(["Old Writer"], undone.Authors);
        Assert.DoesNotContain(undone.MetadataFields, f => f.Source == "User");
    }

    [Theory]
    [InlineData("Year", "19x9")]
    [InlineData("ISBN", "978-0-306-40615-8")]
    [InlineData("DOI", "doi-without-prefix")]
    public async Task WriteService_RejectsInvalidValues_AndWritesNothing(string field, string value)
    {
        await AddBookAsync();
        int audits = await _context.AuditEvents.CountAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => _writeService.UpdateMetadataFieldAsync(BookId, field, value));

        _context.ChangeTracker.Clear();
        Assert.Equal(audits, await _context.AuditEvents.CountAsync());
        Assert.False(await _context.BookMetadataFields.AnyAsync(f => f.Source == "User"));
    }

    // ── Inspector view model ───────────────────────────────────────────────────

    [Fact]
    public async Task Edit_CallsUpdateMetadataField_AndProvenanceReadsEditedByYou()
    {
        (BookDetailViewModel vm, FakeWriteService writes, _) = await CreateVmAsync();
        DetailFieldViewModel title = Field(vm, EditableMetadataFields.Title);
        Assert.False(title.IsProvenanceLoaded);

        title.BeginEdit();
        Assert.Equal(DetailFieldState.Edit, title.State);
        title.EditText = "  A Better   Title ";
        await title.CommitAsync();

        Assert.Equal([("Title", (string?)"A Better Title")], writes.Updates);
        Assert.Equal(DetailFieldState.Display, title.State);
        Assert.Equal("A Better Title", title.Value);
        Assert.Equal("A Better Title", vm.Title);

        title.LoadProvenance();
        Assert.True(title.IsProvenanceLoaded);
        Assert.True(title.IsEditedByYou);
        Assert.Contains("Edited by you", title.ProvenanceText, StringComparison.Ordinal);
        Assert.True(vm.CanUndoFieldEdit);
        Assert.Equal("Undo", vm.UndoFieldEditText);
    }

    [Fact]
    public async Task Undo_RestoresThePriorValue()
    {
        (BookDetailViewModel vm, FakeWriteService writes, _) = await CreateVmAsync();
        DetailFieldViewModel year = Field(vm, EditableMetadataFields.Year);
        year.BeginEdit();
        year.EditText = "1843";
        await year.CommitAsync();
        Assert.Equal("1843", year.Value);

        await vm.UndoFieldEditAsync();

        Assert.Equal(1, writes.Restores);
        Assert.Equal("1999", year.Value);
        Assert.Equal(1999, vm.Book!.Year);
        Assert.False(vm.CanUndoFieldEdit);
    }

    [Fact]
    public async Task InvalidYear_IsRejectedInline_WithNoWrite()
    {
        (BookDetailViewModel vm, FakeWriteService writes, _) = await CreateVmAsync();
        DetailFieldViewModel year = Field(vm, EditableMetadataFields.Year);

        year.BeginEdit();
        year.EditText = "20x0";
        await year.CommitAsync();

        Assert.Empty(writes.Updates);
        Assert.Equal(DetailFieldState.Error, year.State);
        Assert.True(year.IsEditing);
        Assert.Equal("Enter a year from 1 to 9999.", year.ErrorText);
        Assert.Equal("1999", year.Value);
        Assert.Equal("20x0", year.EditText);
    }

    [Theory]
    [InlineData("ISBN", "12345", "Enter a valid 10- or 13-digit ISBN.")]
    [InlineData("DOI", "not a doi", "Enter a DOI that starts with 10., for example 10.1000/xyz123.")]
    public async Task InvalidIsbnOrDoi_IsRejectedInline_WithNoWrite(string fieldName, string text, string expected)
    {
        (BookDetailViewModel vm, FakeWriteService writes, _) = await CreateVmAsync();
        DetailFieldViewModel field = Field(vm, fieldName);

        field.BeginEdit();
        field.EditText = text;
        await field.CommitAsync();

        Assert.Empty(writes.Updates);
        Assert.Equal(expected, field.ErrorText);
    }

    [Fact]
    public async Task WriteFailure_ShowsLocalisedError_AndKeepsTheOldValue()
    {
        (BookDetailViewModel vm, FakeWriteService writes, _) = await CreateVmAsync();
        writes.Failure = new IOException(@"Disk full writing C:\Users\someone\secret.db");
        DetailFieldViewModel title = Field(vm, EditableMetadataFields.Title);

        title.BeginEdit();
        title.EditText = "Unsaved Title";
        await title.CommitAsync();

        Assert.Equal(DetailFieldState.Error, title.State);
        Assert.Equal("This change could not be saved. Your edit is still here; try again.", title.ErrorText);
        Assert.DoesNotContain("secret", title.ErrorText!, StringComparison.Ordinal);
        Assert.Equal("Original Title", title.Value);
        Assert.Equal("Original Title", vm.Title);
        Assert.Equal("Unsaved Title", title.EditText);
        Assert.False(vm.CanUndoFieldEdit);
    }

    [Fact]
    public async Task Cancel_RestoresDisplay_WithoutWriting()
    {
        (BookDetailViewModel vm, FakeWriteService writes, _) = await CreateVmAsync();
        DetailFieldViewModel title = Field(vm, EditableMetadataFields.Title);

        title.BeginEdit();
        title.EditText = "Discarded";
        title.CancelEdit();

        Assert.Equal(DetailFieldState.Display, title.State);
        Assert.Equal("Original Title", title.EditText);
        Assert.Empty(writes.Updates);
    }

    [Fact]
    public async Task UnchangedValue_ReturnsToDisplay_WithoutWriting()
    {
        (BookDetailViewModel vm, FakeWriteService writes, _) = await CreateVmAsync();
        DetailFieldViewModel title = Field(vm, EditableMetadataFields.Title);

        title.BeginEdit();
        await title.CommitAsync();

        Assert.Equal(DetailFieldState.Display, title.State);
        Assert.Empty(writes.Updates);
    }

    [Fact]
    public async Task Provenance_ShowsSourceAndConfidence_ForProviderValues()
    {
        (BookDetailViewModel vm, _, _) = await CreateVmAsync();
        DetailFieldViewModel title = Field(vm, EditableMetadataFields.Title);

        title.LoadProvenance();

        Assert.False(title.IsEditedByYou);
        Assert.Contains("GoogleBooks", title.ProvenanceText, StringComparison.Ordinal);
        Assert.Contains("90", title.ProvenanceText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FieldLabelsAndErrors_FollowCultureChange()
    {
        var localization = new InMemoryLocalizationService();
        (BookDetailViewModel vm, _, _) = await CreateVmAsync(localization);
        DetailFieldViewModel year = Field(vm, EditableMetadataFields.Year);
        year.BeginEdit();
        year.EditText = "0";
        await year.CommitAsync();

        localization.SetCulture("fr");

        Assert.Equal("Année", year.LabelText);
        Assert.Equal("Saisissez une année comprise entre 1 et 9999.", year.ErrorText);
    }

    [Fact]
    public async Task DisplayAccessibleName_CarriesLabelAndValue_InEnglishAndFrench()
    {
        // Slice 8 (K128): the display button's name hid the value from UIA and screen readers.
        var localization = new InMemoryLocalizationService();
        (BookDetailViewModel vm, _, _) = await CreateVmAsync(localization);
        DetailFieldViewModel title = Field(vm, EditableMetadataFields.Title);
        Assert.False(string.IsNullOrEmpty(title.Value));

        Assert.Equal($"{title.LabelText}: {title.DisplayText}. Edit", title.DisplayAccessibleText);

        localization.SetCulture("fr");

        Assert.Equal($"{title.LabelText} : {title.DisplayText}. Modifier", title.DisplayAccessibleText);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static DetailFieldViewModel Field(BookDetailViewModel vm, string name) =>
        Assert.Single(vm.EditableFields, f => f.FieldName == name);

    private static async Task<(BookDetailViewModel Vm, FakeWriteService Writes, FakeReadModel Reads)> CreateVmAsync(
        InMemoryLocalizationService? localization = null)
    {
        var reads = new FakeReadModel(Detail());
        var writes = new FakeWriteService(reads);
        var vm = new BookDetailViewModel(
            reads,
            new NoOpReader(),
            localization ?? new InMemoryLocalizationService(),
            catalogueWriteService: writes,
            commandHistory: new CommandHistory());
        await vm.LoadBookAsync(BookId);
        return (vm, writes, reads);
    }

    private static BookDetailProjection Detail() => new(
        BookId: BookId,
        Title: "Original Title",
        Authors: ["Old Writer"],
        Year: 1999,
        Isbn: null,
        Doi: null,
        Rating: null,
        Status: 0,
        CoverRelativePath: null,
        RelativePath: "books/synthetic.pdf",
        Sha256Hash: "abc123",
        SizeBytes: 2048,
        ReadingProgress: null,
        Annotations: 0,
        MetadataFields: [new MetadataFieldProjection("Title", "Provider Title", "GoogleBooks", 0.9, false)]);

    private async Task AddBookAsync()
    {
        var author = new AuthorRow { NormalizedName = "Old Writer", SortName = "Writer, Old" };
        var book = new BookRow { BookId = BookId, Title = "Original Title", Status = 0, Year = 1999 };
        book.BookAuthors.Add(new BookAuthorRow { BookId = BookId, Author = author, Role = "Author", DisplayOrder = 0 });
        _context.Books.Add(book);
        _context.BookMetadataFields.Add(new BookMetadataFieldRow
        {
            BookId = BookId,
            FieldName = "Title",
            Value = "Provider Title",
            Source = "GoogleBooks",
            Confidence = 0.9,
        });
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    private sealed class NoOpReader : IReaderNavigationService
    {
        public Task OpenReaderAsync(string bookId, int? pageHint = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeReadModel(BookDetailProjection detail) : ICatalogueReadModel
    {
        public BookDetailProjection Detail { get; set; } = detail;

        public async IAsyncEnumerable<BookSummaryProjection> GetBookSummariesAsync(
            CatalogueFilter filter,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<BookDetailProjection?> GetBookDetailAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.FromResult<BookDetailProjection?>(Detail);

        public async IAsyncEnumerable<ShelfProjection> GetShelvesAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<ReadingProgressProjection?> GetProgressAsync(string bookId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ReadingProgressProjection?>(null);
    }

    // Applies edits to the fake read model the way the SQLite write service does, synchronously.
    private sealed class FakeWriteService(FakeReadModel reads) : ICatalogueWriteService
    {
        public List<(string Field, string? Value)> Updates { get; } = [];

        public int Restores { get; private set; }

        public Exception? Failure { get; set; }

        public Task UpdateMetadataFieldAsync(string bookId, string fieldName, string? value, CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
            {
                return Task.FromException(Failure);
            }

            Updates.Add((fieldName, value));
            reads.Detail = Apply(reads.Detail, fieldName, value, user: true);
            return Task.CompletedTask;
        }

        public Task<MetadataFieldSnapshot?> CaptureMetadataFieldSnapshotAsync(string bookId, string fieldName, CancellationToken cancellationToken = default)
        {
            BookDetailProjection d = reads.Detail;
            string? canonical = fieldName switch
            {
                "Title" => d.Title,
                "Year" => d.Year?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "ISBN" => d.Isbn,
                "DOI" => d.Doi,
                _ => null,
            };
            MetadataFieldProjection? user = d.MetadataFields.FirstOrDefault(f => f.Source == "User" && f.FieldName == fieldName);
            return Task.FromResult<MetadataFieldSnapshot?>(
                new MetadataFieldSnapshot(bookId, fieldName, canonical, d.Authors, user is not null, user?.Value));
        }

        public Task RestoreMetadataFieldSnapshotAsync(MetadataFieldSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            Restores++;
            BookDetailProjection d = Apply(reads.Detail, snapshot.FieldName, snapshot.CanonicalValue, user: false);
            reads.Detail = d with
            {
                Authors = snapshot.Authors,
                MetadataFields = d.MetadataFields
                    .Where(f => !(f.Source == "User" && f.FieldName == snapshot.FieldName))
                    .ToArray(),
            };
            return Task.CompletedTask;
        }

        private static BookDetailProjection Apply(BookDetailProjection d, string field, string? value, bool user)
        {
            d = field switch
            {
                "Title" => d with { Title = value },
                "Year" => d with { Year = value is null ? null : int.Parse(value, System.Globalization.CultureInfo.InvariantCulture) },
                "ISBN" => d with { Isbn = value },
                "DOI" => d with { Doi = value },
                _ => d,
            };
            if (!user)
            {
                return d;
            }

            return d with
            {
                MetadataFields = d.MetadataFields
                    .Where(f => !(f.Source == "User" && f.FieldName == field))
                    .Append(new MetadataFieldProjection(field, value, "User", null, true))
                    .ToArray(),
            };
        }

        public Task<string> CreateShelfAsync(string name, bool isSmart = false, string? query = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RenameShelfAsync(string shelfId, string newName, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteShelfAsync(string shelfId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task AddBookToShelfAsync(string shelfId, string bookId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RemoveBookFromShelfAsync(string shelfId, string bookId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task BulkEditAsync(BulkEditCommand command, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<BulkEditSnapshot>> CaptureBulkEditSnapshotAsync(IReadOnlyList<string> bookIds, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task RestoreBulkEditSnapshotAsync(IReadOnlyList<BulkEditSnapshot> snapshots, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
