using OgmaLibrary.Application.Ocr;
using Tesseract;

namespace OgmaLibrary.Infrastructure.Ocr;

/// <summary>Local Tesseract-backed OCR provider for scanned PDFs.</summary>
internal sealed class TesseractOcrProvider : IOcrProvider
{
    private readonly string _tessdataPath;

    /// <summary>Initializes a provider using the app-local tessdata directory.</summary>
    public TesseractOcrProvider()
        : this(Path.Combine(AppContext.BaseDirectory, "tessdata"))
    {
    }

    /// <summary>Initializes a provider with an explicit tessdata path.</summary>
    /// <param name="tessdataPath">Directory containing Tesseract traineddata files.</param>
    public TesseractOcrProvider(string tessdataPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tessdataPath);
        _tessdataPath = tessdataPath;
    }

    /// <inheritdoc />
    public async Task<OcrPageResult> RecognizeAsync(
        Stream pageImage,
        string languageHint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pageImage);
        ArgumentException.ThrowIfNullOrWhiteSpace(languageHint);
        string? normalizedLanguage = OcrLanguagePolicy.Normalize(languageHint);
        if (normalizedLanguage is null)
        {
            // Sept-23 Phase 17: a language this build does not ship is a typed, localised
            // failure, not an argument error swallowed by the worker.
            throw new OcrFailureException(OcrFailureCodes.MissingLanguageData);
        }

        byte[] imageBytes;
        using (var buffer = new MemoryStream())
        {
            await pageImage.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            imageBytes = buffer.ToArray();
        }

        return await Task.Run(() => Recognize(imageBytes, normalizedLanguage), cancellationToken)
            .ConfigureAwait(false);
    }

    private OcrPageResult Recognize(byte[] imageBytes, string language)
    {
        TesseractTrainingDataVerification verification =
            TesseractTrainingDataVerifier.Verify(_tessdataPath, language);
        if (!verification.IsValid)
        {
            throw new OcrFailureException(
                OcrFailureCodes.MissingLanguageData,
                new InvalidOperationException(
                    $"OCR training data integrity check failed for '{verification.Language}': {verification.Code}."));
        }

        try
        {
            using var engine = new TesseractEngine(_tessdataPath, language, EngineMode.Default);
            using Pix pix = Pix.LoadFromMemory(imageBytes);
            using Page page = engine.Process(pix);
            string text = page.GetText() ?? string.Empty;
            return new OcrPageResult(text, page.GetMeanConfidence());
        }
        catch (Exception exception) when (exception is TesseractException or IOException or InvalidOperationException)
        {
            throw new OcrFailureException(OcrFailureCodes.UnreadablePage, exception);
        }
    }
}
