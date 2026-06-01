namespace OgmaLibrary.Workers.Ocr;

/// <summary>Serialized payload for an OCR background job.</summary>
/// <param name="FilePath">Absolute source PDF path.</param>
/// <param name="Language">OCR language key.</param>
/// <param name="TotalPages">Total pages discovered for the job.</param>
/// <param name="ProcessedPages">Pages successfully persisted as OCR text.</param>
public sealed record OcrJobPayload(
    string FilePath,
    string Language = "eng",
    int TotalPages = 0,
    int ProcessedPages = 0);
