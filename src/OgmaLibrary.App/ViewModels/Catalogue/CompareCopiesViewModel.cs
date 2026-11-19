using System.Globalization;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Ingestion;

namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>
/// Compare copies and editions (Sept-23 Phase 10, task 10.9, CAT-007): the files of the selected
/// books side by side, one column per file, with path, size, content hash prefix, validity,
/// presence and dates. Read-only; it never deletes, moves or renames a file.
/// </summary>
public sealed class CompareCopiesViewModel
{
    private readonly ILocalizationService _localization;

    /// <summary>Initializes a new instance of the <see cref="CompareCopiesViewModel"/> class.</summary>
    /// <param name="files">The files to compare.</param>
    /// <param name="localization">The localisation service.</param>
    public CompareCopiesViewModel(IReadOnlyList<BookFileFacts> files, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(localization);
        _localization = localization;
        Columns = files.Select((file, index) => new CompareCopyColumn(file, index + 1, localization)).ToArray();
        IsIdenticalContent = files.Count > 1 &&
                             files.All(file => file.Sha256Prefix is not null) &&
                             files.Select(file => file.Sha256Prefix).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1;
    }

    /// <summary>One column per file.</summary>
    public IReadOnlyList<CompareCopyColumn> Columns { get; }

    /// <summary>Whether every file has the same content hash.</summary>
    public bool IsIdenticalContent { get; }

    /// <summary>The window title and heading.</summary>
    public string Title => string.Format(CultureInfo.CurrentCulture, _localization["Compare.Title"], Columns.Count);

    /// <summary>What the comparison shows and that nothing is deleted.</summary>
    public string IntroText => _localization["Compare.Intro"];

    /// <summary>"The files have identical content", or that they differ.</summary>
    public string VerdictText => _localization[IsIdenticalContent ? "Compare.Verdict.Identical" : "Compare.Verdict.Different"];

    /// <summary>Label of the close button.</summary>
    public string CloseText => _localization["Compare.Close"];

    /// <summary>Row label: path.</summary>
    public string PathLabel => _localization["Compare.Field.Path"];

    /// <summary>Row label: size.</summary>
    public string SizeLabel => _localization["Compare.Field.Size"];

    /// <summary>Row label: content hash prefix.</summary>
    public string HashLabel => _localization["Compare.Field.Hash"];

    /// <summary>Row label: validity.</summary>
    public string ValidityLabel => _localization["Compare.Field.Validity"];

    /// <summary>Row label: added.</summary>
    public string AddedLabel => _localization["Compare.Field.Added"];

    /// <summary>Row label: last seen.</summary>
    public string LastSeenLabel => _localization["Compare.Field.LastSeen"];
}

/// <summary>One file in the comparison.</summary>
public sealed class CompareCopyColumn
{
    /// <summary>Initializes a new instance of the <see cref="CompareCopyColumn"/> class.</summary>
    /// <param name="file">The file facts.</param>
    /// <param name="number">The 1-based column number.</param>
    /// <param name="localization">The localisation service.</param>
    public CompareCopyColumn(BookFileFacts file, int number, ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(localization);
        CultureInfo culture = localization.CurrentCulture;
        string unknown = localization["Compare.Unknown"];
        BookId = file.BookId;
        Title = string.IsNullOrWhiteSpace(file.Title) ? localization["Search.Result.Untitled"] : file.Title.Trim();
        Heading = string.Format(culture, localization["Compare.Column"], number);
        PathText = file.RelativePath;
        SizeText = file.SizeBytes is long size
            ? string.Format(culture, localization["Compare.SizeFormat"], size)
            : unknown;
        HashText = file.Sha256Prefix ?? unknown;
        string validity = localization[$"Compare.Validity.{file.Validity}"];
        ValidityText = file.IsPresent
            ? validity
            : string.Format(culture, localization["Compare.MissingFormat"], validity);
        AddedText = file.AddedUtc?.ToLocalTime().ToString("d", culture) ?? unknown;
        LastSeenText = file.LastSeenUtc.ToLocalTime().ToString("g", culture);
        AccessibleName = string.Format(culture, localization["Compare.ColumnName"], Heading, Title, PathText, SizeText, ValidityText);
        AutomationId = $"Compare.Column.{number}";
    }

    /// <summary>The book the file belongs to.</summary>
    public string BookId { get; }

    /// <summary>The book title.</summary>
    public string Title { get; }

    /// <summary>"File 1".</summary>
    public string Heading { get; }

    /// <summary>Library-relative path.</summary>
    public string PathText { get; }

    /// <summary>Size in bytes.</summary>
    public string SizeText { get; }

    /// <summary>Content hash prefix.</summary>
    public string HashText { get; }

    /// <summary>Validity, and whether the file is missing.</summary>
    public string ValidityText { get; }

    /// <summary>When the book was added.</summary>
    public string AddedText { get; }

    /// <summary>When the file was last seen.</summary>
    public string LastSeenText { get; }

    /// <summary>Screen-reader summary of the column.</summary>
    public string AccessibleName { get; }

    /// <summary>Stable automation id.</summary>
    public string AutomationId { get; }
}
