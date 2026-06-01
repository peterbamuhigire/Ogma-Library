namespace OgmaLibrary.Application.Ocr;

/// <summary>OCR provider boundary for Phase 15 scanned-PDF text recognition.</summary>
public interface IOcrProvider
{
    /// <summary>Recognizes text from a rendered page image.</summary>
    /// <param name="pageImage">Rendered page image stream, usually PNG.</param>
    /// <param name="languageHint">Tesseract language key such as <c>eng</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recognized text and confidence.</returns>
    Task<OcrPageResult> RecognizeAsync(
        Stream pageImage,
        string languageHint,
        CancellationToken cancellationToken = default);
}

/// <summary>OCR result for one rendered PDF page.</summary>
/// <param name="Text">Recognized plain text.</param>
/// <param name="Confidence">Provider confidence in [0.0, 1.0].</param>
public sealed record OcrPageResult(string Text, double Confidence);
