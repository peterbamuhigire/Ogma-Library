using System.ComponentModel;
using System.Runtime.CompilerServices;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Application.Navigation;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>
/// View model for the book-detail panel (FR-CAT-004). Exposes all five metadata
/// field groups from <see cref="BookDetailProjection"/> and the "Read" / "Enrich"
/// action commands.
/// </summary>
public sealed class BookDetailViewModel : INotifyPropertyChanged
{
    private readonly ICatalogueReadModel _readModel;
    private readonly IReaderNavigationService _reader;
    private readonly ILocalizationService _localization;
    private readonly IBookMetadataEnrichmentService? _metadataEnrichment;

    private BookDetailProjection? _book;
    private bool _isLoading;
    private bool _isEnriching;
    private bool _isVisible;
    private string? _enrichmentStatusText;

    /// <summary>
    /// Initializes a new instance of <see cref="BookDetailViewModel"/>.
    /// </summary>
    /// <param name="readModel">The catalogue read model.</param>
    /// <param name="reader">The reader navigation service.</param>
    /// <param name="localization">The localization service.</param>
    /// <param name="metadataEnrichment">The deterministic no-AI metadata enrichment service.</param>
    public BookDetailViewModel(
        ICatalogueReadModel readModel,
        IReaderNavigationService reader,
        ILocalizationService localization,
        IBookMetadataEnrichmentService? metadataEnrichment = null)
    {
        ArgumentNullException.ThrowIfNull(readModel);
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(localization);

        _readModel = readModel;
        _reader = reader;
        _localization = localization;
        _metadataEnrichment = metadataEnrichment;
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

    /// <summary>True while deterministic provider metadata enrichment is running.</summary>
    public bool IsEnriching
    {
        get => _isEnriching;
        private set
        {
            if (_isEnriching != value)
            {
                _isEnriching = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanEnrich));
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

    /// <summary>True when the selected book can run deterministic metadata enrichment.</summary>
    public bool CanEnrich => _book is not null && _metadataEnrichment is not null && !IsEnriching;

    /// <summary>Localized button label for deterministic metadata enrichment.</summary>
    public string EnrichText => _localization["Catalogue.BookDetail.Enrich"];

    /// <summary>Localized tooltip for deterministic metadata enrichment.</summary>
    public string EnrichTooltip => _localization["Catalogue.BookDetail.EnrichTooltip"];

    /// <summary>Current user-facing enrichment status, if any.</summary>
    public string? EnrichmentStatusText
    {
        get => _enrichmentStatusText;
        private set
        {
            if (_enrichmentStatusText != value)
            {
                _enrichmentStatusText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasEnrichmentStatus));
            }
        }
    }

    /// <summary>True when the detail panel has an enrichment status to display.</summary>
    public bool HasEnrichmentStatus => !string.IsNullOrWhiteSpace(EnrichmentStatusText);

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
            OnPropertyChanged(nameof(ReadingMemorySummaryLabel));
            OnPropertyChanged(nameof(ReadingMemoryKeyInsightLabel));
            OnPropertyChanged(nameof(ReadingMemoryDispositionLabel));
            OnPropertyChanged(nameof(ReadingMemoryKeyInsightExcerpt));
            OnPropertyChanged(nameof(ReadingMemoryDispositionDisplay));
            OnPropertyChanged(nameof(HasReadingMemorySummary));
            OnPropertyChanged(nameof(FileFields));
            OnPropertyChanged(nameof(BiblioFields));
            OnPropertyChanged(nameof(BiblioFieldDisplayRows));
            OnPropertyChanged(nameof(ReadingFields));
            OnPropertyChanged(nameof(EnrichmentFields));
            OnPropertyChanged(nameof(EnrichmentFieldDisplayRows));
            OnPropertyChanged(nameof(AiFields));
            OnPropertyChanged(nameof(CanEnrich));
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

    /// <summary>Localized reading-memory summary label.</summary>
    public string ReadingMemorySummaryLabel => _localization["Catalogue.BookDetail.ReadingMemory"];

    /// <summary>Localized reading-memory key-insight label.</summary>
    public string ReadingMemoryKeyInsightLabel => _localization["Catalogue.BookDetail.ReadingMemoryKeyInsight"];

    /// <summary>Localized reading-memory disposition label.</summary>
    public string ReadingMemoryDispositionLabel => _localization["Catalogue.BookDetail.ReadingMemoryDisposition"];

    /// <summary>True when the loaded book has memory content worth summarizing.</summary>
    public bool HasReadingMemorySummary =>
        _book?.ReadingMemory is { } memory &&
        (memory.Disposition is not null || !string.IsNullOrWhiteSpace(memory.KeyInsight));

    /// <summary>Key insight truncated for the compact book-detail reading card.</summary>
    public string ReadingMemoryKeyInsightExcerpt =>
        Truncate(_book?.ReadingMemory?.KeyInsight, 80) ??
        _localization["Catalogue.BookDetail.ReadingMemoryEmpty"];

    /// <summary>Disposition score formatted for the compact reading-memory summary.</summary>
    public string ReadingMemoryDispositionDisplay =>
        _book?.ReadingMemory?.Disposition is int disposition
            ? string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                _localization["Catalogue.BookDetail.ReadingMemoryDispositionFormat"],
                disposition)
            : _localization["Catalogue.BookDetail.ReadingMemoryEmpty"];

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

    /// <summary>Formatted bibliographic metadata rows for display.</summary>
    public IReadOnlyList<string> BiblioFieldDisplayRows =>
        BiblioFields
            .Where(f => !string.IsNullOrWhiteSpace(f.Value))
            .Select(FormatField)
            .ToList();

    /// <summary>Reading state fields (group 3).</summary>
    public IReadOnlyList<MetadataFieldProjection> ReadingFields =>
        _book?.MetadataFields
            .Where(f => ReadingFieldNames.Contains(f.FieldName))
            .ToList() ?? [];

    /// <summary>Enrichment fields from metadata providers (group 4).</summary>
    public IReadOnlyList<MetadataFieldProjection> EnrichmentFields =>
        _book?.MetadataFields
            .Where(IsProviderEnrichedField)
            .ToList() ?? [];

    /// <summary>Formatted provider-sourced metadata rows with provenance for display.</summary>
    public IReadOnlyList<string> EnrichmentFieldDisplayRows =>
        EnrichmentFields
            .Where(f => !string.IsNullOrWhiteSpace(f.Value))
            .Select(FormatField)
            .ToList();

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

    /// <summary>Runs deterministic provider metadata enrichment for the loaded book.</summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task EnrichMetadataAsync(CancellationToken cancellationToken = default)
    {
        if (_book is null)
        {
            return;
        }

        if (_metadataEnrichment is null)
        {
            EnrichmentStatusText = _localization["Catalogue.BookDetail.EnrichUnavailable"];
            return;
        }

        string bookId = _book.BookId;
        IsEnriching = true;
        EnrichmentStatusText = _localization["Catalogue.BookDetail.Enriching"];

        try
        {
            (bool success, string? errorMessage) = await _metadataEnrichment
                .EnrichAsync(bookId, absoluteFilePath: null, cancellationToken)
                .ConfigureAwait(false);

            if (!success)
            {
                UpdateOnUiThread(() =>
                {
                    EnrichmentStatusText = string.Format(
                        System.Globalization.CultureInfo.CurrentCulture,
                        _localization["Catalogue.BookDetail.EnrichFailedFormat"],
                        errorMessage ?? _localization["Catalogue.BookDetail.EnrichUnknownError"]);
                    IsEnriching = false;
                });
                return;
            }

            BookDetailProjection? detail = await _readModel
                .GetBookDetailAsync(bookId, cancellationToken)
                .ConfigureAwait(false);

            UpdateOnUiThread(() =>
            {
                Book = detail;
                EnrichmentStatusText = _localization["Catalogue.BookDetail.EnrichComplete"];
                IsEnriching = false;
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            UpdateOnUiThread(() =>
            {
                EnrichmentStatusText = string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    _localization["Catalogue.BookDetail.EnrichFailedFormat"],
                    ex.Message);
                IsEnriching = false;
            });
        }
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

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : string.Concat(trimmed.AsSpan(0, maxLength - 3), "...");
    }

    private static string FormatField(MetadataFieldProjection field)
    {
        string value = string.IsNullOrWhiteSpace(field.Value) ? "-" : field.Value.Trim();
        string provenance = FormatProvenance(field);
        return string.IsNullOrWhiteSpace(provenance)
            ? $"{field.FieldName}: {value}"
            : $"{field.FieldName}: {value} ({provenance})";
    }

    private static string FormatProvenance(MetadataFieldProjection field)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(field.Source))
        {
            parts.Add(field.Source.Trim());
        }

        if (field.Confidence is double confidence)
        {
            parts.Add(confidence.ToString("P0", System.Globalization.CultureInfo.CurrentCulture));
        }

        if (field.IsOverridden)
        {
            parts.Add("manual");
        }

        return string.Join(", ", parts);
    }

    private static bool IsProviderEnrichedField(MetadataFieldProjection field)
    {
        if (string.IsNullOrWhiteSpace(field.Source))
        {
            return false;
        }

        return !field.IsOverridden &&
            !string.Equals(field.Source, "System", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(field.Source, "User", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(field.Source, "Local", StringComparison.OrdinalIgnoreCase);
    }

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

    private static readonly HashSet<string> AiFieldNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "AiDescription", "RecommendedReadingLevel", "RelatedTitles",
        };
}
