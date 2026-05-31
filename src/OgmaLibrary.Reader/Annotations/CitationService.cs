using System.Security.Cryptography;
using System.Text;
using OgmaLibrary.Application;
using OgmaLibrary.Application.Catalogue;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Domain;

namespace OgmaLibrary.Reader.Annotations;

/// <summary>
/// Captures citation cards from text selected in the reader (FR-READ-011, V1).
/// Reads book title and author from the catalogue read model.
/// </summary>
public sealed class CitationService : ICitationService
{
    private readonly ICatalogueReadModel _catalogue;
    private readonly ISidecarService? _sidecar;
    private readonly ILocalizationService? _localization;

    /// <summary>
    /// Initializes a new instance of <see cref="CitationService"/>.
    /// </summary>
    /// <param name="catalogue">The catalogue read model for resolving book metadata.</param>
    /// <param name="sidecar">Optional sidecar path resolver for plain-text exports.</param>
    /// <param name="localization">Optional localization service for fallback export strings.</param>
    public CitationService(
        ICatalogueReadModel catalogue,
        ISidecarService? sidecar = null,
        ILocalizationService? localization = null)
    {
        ArgumentNullException.ThrowIfNull(catalogue);
        _catalogue = catalogue;
        _sidecar = sidecar;
        _localization = localization;
    }

    /// <inheritdoc />
    public async Task<CitationCard> CaptureAsync(
        string bookId,
        int pageIndex,
        string selectedText,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedText);

        // Attempt to resolve title and author from the catalogue.
        string? title = null;
        string? author = null;

        try
        {
            BookDetailProjection? book = await _catalogue
                .GetBookDetailAsync(bookId, cancellationToken)
                .ConfigureAwait(false);

            if (book is not null)
            {
                title = book.Title;
                author = book.Authors.Count > 0 ? book.Authors[0] : null;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Catalogue lookup failure should not block citation capture.
        catch (Exception)
        {
            // Catalogue metadata unavailable — cite with available data.
        }
#pragma warning restore CA1031

        return new CitationCard(
            BookId: bookId,
            Title: title,
            Author: author,
            PageNumber: pageIndex + 1,
            SelectedText: selectedText);
    }

    /// <inheritdoc />
    public async Task<string> ExportAsync(CitationCard card, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (_sidecar is null)
        {
            throw new InvalidOperationException("Citation export requires a sidecar service.");
        }

        string sidecarKey = await ResolveSidecarKeyAsync(card.BookId, cancellationToken)
            .ConfigureAwait(false);
        string plainText = ToLocalizedPlainText(card);
        string digest = ShortDigest(plainText);
        string variant = FormattableString.Invariant($"_p{card.PageNumber:000}_{digest}");
        string path = _sidecar.Resolve(sidecarKey, SidecarClass.CitationExports, variant);

        await File.WriteAllTextAsync(path, plainText, Encoding.UTF8, cancellationToken)
            .ConfigureAwait(false);

        return path;
    }

    private string ToLocalizedPlainText(CitationCard card) =>
        _localization is null
            ? card.ToPlainText()
            : card.ToPlainText(
                _localization["Citation.UnknownAuthor"],
                _localization["Citation.UnknownTitle"],
                _localization["Citation.PageFormat"]);

    private async Task<string> ResolveSidecarKeyAsync(string bookId, CancellationToken cancellationToken)
    {
        try
        {
            BookDetailProjection? book = await _catalogue
                .GetBookDetailAsync(bookId, cancellationToken)
                .ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(book?.Sha256Hash))
            {
                return book.Sha256Hash;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Export can still use the stable book id when metadata is unavailable.
        catch (Exception)
        {
            // Fall back to the stable book id below.
        }
#pragma warning restore CA1031

        return SanitizeSidecarKey(bookId);
    }

    private static string ShortDigest(string value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash)[..12].ToLowerInvariant();
    }

    private static string SanitizeSidecarKey(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            builder.Append(char.IsLetterOrDigit(c) ? c : '_');
        }

        return builder.Length > 0 ? builder.ToString() : "citation";
    }
}
