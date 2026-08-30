using OgmaLibrary.Application.Ocr;
using Tesseract;

namespace OgmaLibrary.Infrastructure.Ocr;

/// <summary>Local Tesseract-backed OCR provider for Phase 15 scanned PDFs.</summary>
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
            throw new ArgumentException("Unsupported OCR language pack.", nameof(languageHint));
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
        using var engine = new TesseractEngine(_tessdataPath, language, EngineMode.Default);
        using Pix pix = Pix.LoadFromMemory(imageBytes);
        using Page page = engine.Process(pix);
        string text = page.GetText() ?? string.Empty;
        return new OcrPageResult(text, page.GetMeanConfidence());
    }
}
