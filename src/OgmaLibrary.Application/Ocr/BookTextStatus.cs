using OgmaLibrary.Application.Search;

namespace OgmaLibrary.Application.Ocr;

/// <summary>
/// Honest, user-facing statement of whether a book's text can be searched
/// (Sept-23 Phase 17, K21). It is derived from page classification, selected OCR
/// text and the OCR job state, never from index-job completion alone.
/// The numeric values are persisted in <c>Books.TextStatus</c>; do not renumber.
/// </summary>
public enum BookTextStatus
{
    /// <summary>Text has not been extracted yet.</summary>
    Unknown = 0,

    /// <summary>Every page with content has a usable text layer.</summary>
    Searchable = 1,

    /// <summary>Some pages are scanned images without text; the rest is searchable.</summary>
    PartlySearchable = 2,

    /// <summary>The book is (almost) entirely scanned images; it needs OCR to be searchable.</summary>
    ImageOnly = 3,

    /// <summary>OCR is queued or running for this book.</summary>
    OcrInProgress = 4,

    /// <summary>Searchable text for scanned pages came from OCR.</summary>
    OcrText = 5,

    /// <summary>No text could be read from the book, even after OCR where it applied.</summary>
    NoText = 6,

    /// <summary>OCR was attempted and failed; the failure code says why.</summary>
    OcrFailed = 7,
}

/// <summary>Per-page evidence used to derive a <see cref="BookTextStatus"/>.</summary>
/// <param name="PageIndex">Zero-based page index.</param>
/// <param name="NativeQuality">Classification of the PDF's own text layer.</param>
/// <param name="NativeWordCount">Words in the PDF's own text layer.</param>
/// <param name="HasOcrRow">Whether OCR has processed the page (even with empty output).</param>
/// <param name="OcrSelected">Whether the OCR text is the selected text for the page.</param>
/// <param name="OcrConfidence">OCR confidence in [0, 1], when OCR ran.</param>
public sealed record BookPageTextEvidence(
    int PageIndex,
    SearchExtractionQuality NativeQuality,
    int NativeWordCount,
    bool HasOcrRow = false,
    bool OcrSelected = false,
    double? OcrConfidence = null)
{
    /// <summary>Whether the page's own text layer is missing or too thin (<see cref="OcrPageQualityPolicy"/>).</summary>
    public bool NeedsOcr => NativeQuality != SearchExtractionQuality.Failed &&
        OcrPageQualityPolicy.ShouldProcess((Reader.ExtractionQuality)(int)NativeQuality, NativeWordCount);

    /// <summary>Whether the page's own text layer is usable for search.</summary>
    public bool HasUsableNativeText => !NeedsOcr &&
        NativeQuality != SearchExtractionQuality.Failed &&
        NativeWordCount > 0;
}

/// <summary>State of the latest OCR job for a book, as far as text status is concerned.</summary>
public enum BookOcrJobState
{
    /// <summary>No OCR job exists.</summary>
    None = 0,

    /// <summary>Queued, running, paused or waiting for a retry.</summary>
    Active = 1,

    /// <summary>The job completed.</summary>
    Completed = 2,

    /// <summary>The job failed terminally or was dead-lettered.</summary>
    Failed = 3,

    /// <summary>The job was cancelled by the user.</summary>
    Cancelled = 4,
}

/// <summary>Result of deriving a book's text status.</summary>
/// <param name="Status">The honest text status.</param>
/// <param name="TextQuality">Share of pages with usable text in [0, 1], OCR pages weighted by confidence.</param>
/// <param name="OcrConfidence">Mean confidence of the selected OCR pages, when any.</param>
/// <param name="TotalPages">Pages considered.</param>
/// <param name="PagesNeedingOcr">Pages whose own text layer is missing or too thin.</param>
/// <param name="PagesWithoutText">Pages that still have no usable text.</param>
public sealed record BookTextAssessment(
    BookTextStatus Status,
    double TextQuality,
    double? OcrConfidence,
    int TotalPages,
    int PagesNeedingOcr,
    int PagesWithoutText);

/// <summary>
/// Deterministic derivation of <see cref="BookTextStatus"/> and the text-quality score
/// (Sept-23 Phase 17, tasks 1, 2 and 7).
/// </summary>
public static class BookTextStatusPolicy
{
    /// <summary>Share of pages needing OCR at or above which a book is "image only".</summary>
    public const double ImageOnlyPageShare = 0.8;

    /// <summary>Derives the text status from page evidence and the latest OCR job state.</summary>
    /// <param name="pages">One entry per page from the native extraction.</param>
    /// <param name="ocrJob">State of the latest OCR job for the book.</param>
    /// <returns>The assessment.</returns>
    public static BookTextAssessment Assess(
        IReadOnlyCollection<BookPageTextEvidence> pages,
        BookOcrJobState ocrJob = BookOcrJobState.None)
    {
        ArgumentNullException.ThrowIfNull(pages);
        int total = pages.Count;
        if (total == 0)
        {
            BookTextStatus empty = ocrJob == BookOcrJobState.Active ? BookTextStatus.OcrInProgress : BookTextStatus.Unknown;
            return new BookTextAssessment(empty, 0, null, 0, 0, 0);
        }

        int needing = 0;
        int scannedImages = 0;
        int usableNative = 0;
        int ocrCovered = 0;
        int ocrAttempted = 0;
        double confidenceSum = 0;
        foreach (BookPageTextEvidence page in pages)
        {
            if (page.HasUsableNativeText)
            {
                usableNative++;
                continue;
            }

            if (!page.NeedsOcr)
            {
                continue;
            }

            needing++;
            if (page.NativeQuality == SearchExtractionQuality.Scanned)
            {
                scannedImages++;
            }

            if (page.HasOcrRow)
            {
                ocrAttempted++;
            }

            if (page.OcrSelected)
            {
                ocrCovered++;
                confidenceSum += Math.Clamp(page.OcrConfidence ?? 0, 0, 1);
            }
        }

        int withoutText = total - usableNative - ocrCovered;
        double quality = Math.Clamp((usableNative + confidenceSum) / total, 0, 1);
        double? meanConfidence = ocrCovered > 0 ? confidenceSum / ocrCovered : null;
        bool ocrFinished = needing > 0 && ocrAttempted >= needing;
        double uncoveredShare = (needing - ocrCovered) / (double)total;

        BookTextStatus status;
        if (ocrJob == BookOcrJobState.Active && needing > ocrCovered)
        {
            status = BookTextStatus.OcrInProgress;
        }
        else if (ocrCovered > 0)
        {
            status = !ocrFinished && uncoveredShare > 0 ? BookTextStatus.PartlySearchable : BookTextStatus.OcrText;
        }
        else if (ocrJob == BookOcrJobState.Failed && needing > 0)
        {
            status = BookTextStatus.OcrFailed;
        }
        else if (usableNative == 0)
        {
            // Nothing readable: either it needs OCR, or OCR already ran and read nothing.
            status = needing > 0 && !ocrFinished ? BookTextStatus.ImageOnly : BookTextStatus.NoText;
        }
        else if (!ocrFinished && needing / (double)total >= ImageOnlyPageShare)
        {
            status = BookTextStatus.ImageOnly;
        }
        else if (!ocrFinished && scannedImages > 0)
        {
            // Blank or near-empty pages are normal in text books; only image pages without
            // text make a book "partly searchable".
            status = BookTextStatus.PartlySearchable;
        }
        else
        {
            status = BookTextStatus.Searchable;
        }

        return new BookTextAssessment(status, quality, meanConfidence, total, needing, withoutText);
    }

    /// <summary>Whether the status means the book would benefit from OCR.</summary>
    public static bool NeedsOcr(BookTextStatus status) =>
        status is BookTextStatus.ImageOnly or BookTextStatus.PartlySearchable;
}
