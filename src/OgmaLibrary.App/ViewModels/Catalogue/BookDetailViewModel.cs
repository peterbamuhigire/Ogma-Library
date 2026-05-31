using System.ComponentModel;
using System.Runtime.CompilerServices;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Navigation;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>
/// View model for the book-detail panel (FR-CAT-004). Exposes all five metadata
/// field groups from <see cref="BookDetailProjection"/> and the "Read" / "Enrich"
/// action commands. The "Enrich" action is disabled until Phase 07.
/// </summary>
public sealed class BookDetailViewModel : INotifyPropertyChanged
{
    private readonly ICatalogueReadModel _readModel;
    private readonly IReaderNavigationService _reader;
    private readonly ILocalizationService _localization;

    private BookDetailProjection? _book;
    private bool _isLoading;
    private bool _isVisible;

    /// <summary>
    /// Initializes a new instance of <see cref="BookDetailViewModel"/>.
    /// </summary>
    /// <param name="readModel">The catalogue read model.</param>
    /// <param name="reader">The reader navigation service.</param>
    /// <param name="localization">The localization service.</param>
    public BookDetailViewModel(
        ICatalogueReadModel readModel,
        IReaderNavigationService reader,
        ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(readModel);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(localization);

        _readModel = readModel;
        _reader = reader;
        _localization = localization;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    // ── Visibility & loading ────────────────────────────────────────────────────

    /// <summary>True while the detail is loading.</summary>
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading != value)
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>True when the detail panel should be shown.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible != value)
            {
                _isVisible = value;
                OnPropertyChanged();
            }
        }
    }

    // ── Core identity ───────────────────────────────────────────────────────────

    /// <summary>The loaded book detail projection, or <see langword="null"/> if not loaded.</summary>
    public BookDetailProjection? Book
    {
        get => _book;
        private set
        {
            _book = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(AuthorsDisplay));
            OnPropertyChanged(nameof(Year));
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(Rating));
            OnPropertyChanged(nameof(Isbn));
            OnPropertyChanged(nameof(Doi));
            OnPropertyChanged(nameof(RelativePath));
            OnPropertyChanged(nameof(SizeBytes));
            OnPropertyChanged(nameof(Sha256Hash));
            OnPropertyChanged(nameof(ReadingStatus));
            OnPropertyChanged(nameof(ReadingProgressPct));
            OnPropertyChanged(nameof(LastReadDisplay));
            OnPropertyChanged(nameof(AnnotationCount));
            OnPropertyChanged(nameof(FileFields));
            OnPropertyChanged(nameof(BiblioFields));
            OnPropertyChanged(nameof(ReadingFields));
            OnPropertyChanged(nameof(EnrichmentFields));
            OnPropertyChanged(nameof(AiFields));
        }
    }

    // ── Bibliographic group ────────────────────────────────────────────────────

    /// <summary>Book title.</summary>
    public string? Title => _book?.Title;

    /// <summary>Authors joined for display.</summary>
    public string? AuthorsDisplay => _book?.Authors.Count > 0
        ? string.Join("; ", _book.Authors)
        : null;

    /// <summary>Publication year as string.</summary>
    public string? Year => _book?.Year?.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>ISBN display string.</summary>
    public string? Isbn => _book?.Isbn;

    /// <summary>DOI display string.</summary>
    public string? Doi => _book?.Doi;

    // ── File group ────────────────────────────────────────────────────────────

    /// <summary>Library-relative file path.</summary>
    public string? RelativePath => _book?.RelativePath;

    /// <summary>File size in bytes.</summary>
    public long? SizeBytes => _book?.SizeBytes;

    /// <summary>SHA-256 hex digest.</summary>
    public string? Sha256Hash => _book?.Sha256Hash;

    // ── Reading group ─────────────────────────────────────────────────────────

    /// <summary>Lifecycle status.</summary>
    public int? Status => _book?.Status;

    /// <summary>Reader rating 1–5.</summary>
    public int? Rating => _book?.Rating;

    /// <summary>Reading status from progress record.</summary>
    public int? ReadingStatus => _book?.ReadingProgress?.Status;

    /// <summary>Reading completion percentage.</summary>
    public double? ReadingProgressPct => _book?.ReadingProgress?.CompletionPct;

    /// <summary>Last-read date formatted for display.</summary>
    public string? LastReadDisplay => _book?.ReadingProgress?.LastReadUtc
        ?.ToLocalTime()
        .ToString("d", System.Globalization.CultureInfo.CurrentCulture);

    /// <summary>Number of annotations.</summary>
    public int? AnnotationCount => _book?.Annotations;

    // ── Five field groups for the detail panel tabs ───────────────────────────

    /// <summary>File metadata fields (group 1).</summary>
    public IReadOnlyList<MetadataFieldProjection> FileFields =>
        _book?.MetadataFields
            .Where(f => FileFieldNames.Contains(f.FieldName))
            .ToList() ?? [];

    /// <summary>Bibliographic metadata fields (group 2).</summary>
    public IReadOnlyList<MetadataFieldProjection> BiblioFields =>
        _book?.MetadataFields
            .Where(f => BiblioFieldNames.Contains(f.FieldName))
            .ToList() ?? [];

    /// <summary>Reading state fields (group 3).</summary>
    public IReadOnlyList<MetadataFieldProjection> ReadingFields =>
        _book?.MetadataFields
            .Where(f => ReadingFieldNames.Contains(f.FieldName))
            .ToList() ?? [];

    /// <summary>Enrichment fields from metadata providers (group 4).</summary>
    public IReadOnlyList<MetadataFieldProjection> EnrichmentFields =>
        _book?.MetadataFields
            .Where(f => EnrichmentFieldNames.Contains(f.FieldName))
            .ToList() ?? [];

    /// <summary>AI-generated fields (group 5).</summary>
    public IReadOnlyList<MetadataFieldProjection> AiFields =>
        _book?.MetadataFields
            .Where(f => AiFieldNames.Contains(f.FieldName))
            .ToList() ?? [];

    // ── Actions ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens the PDF reader for the currently loaded book (routes to a
    /// "coming in Phase 08" placeholder until Phase 08 is implemented).
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task OpenReaderAsync(CancellationToken cancellationToken = default)
    {
        if (_book is null)
        {
            return;
        }

        await _reader.OpenReaderAsync(_book.BookId, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Closes the detail panel.</summary>
    public void Close()
    {
        IsVisible = false;
    }

    // ── Data loading ──────────────────────────────────────────────────────────

    /// <summary>
    /// Loads the book detail for the given book ID and makes the panel visible.
    /// </summary>
    /// <param name="bookId">The stable book identity.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task LoadBookAsync(string bookId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);

        IsLoading = true;
        IsVisible = true;

        try
        {
            BookDetailProjection? detail = await _readModel
                .GetBookDetailAsync(bookId, cancellationToken)
                .ConfigureAwait(false);

            UpdateOnUiThread(() =>
            {
                Book = detail;
                IsLoading = false;
            });
        }
        catch
        {
            UpdateOnUiThread(() => IsLoading = false);
            throw;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private static void UpdateOnUiThread(Action action)
    {
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(action);
        }
    }

    // ── Field group membership sets ───────────────────────────────────────────

    private static readonly HashSet<string> FileFieldNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "FileName", "RelativePath", "SizeBytes", "ModifiedUtc",
            "Format", "Pages", "PdfVersion", "IsEncrypted",
        };

    private static readonly HashSet<string> BiblioFieldNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Title", "Author", "Publisher", "Year", "Isbn", "Doi",
            "Language", "Categories", "Description",
        };

    private static readonly HashSet<string> ReadingFieldNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Status", "Rating", "Tags", "ReadingProgressPct", "LastReadDate",
        };

    private static readonly HashSet<string> EnrichmentFieldNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Provider", "Confidence", "LookupDate", "IsOverridden",
        };

    private static readonly HashSet<string> AiFieldNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "AiDescription", "RecommendedReadingLevel", "RelatedTitles",
        };
}
