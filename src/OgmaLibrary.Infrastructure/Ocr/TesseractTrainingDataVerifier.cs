using System.Security.Cryptography;

namespace OgmaLibrary.Infrastructure.Ocr;

/// <summary>Result of validating one or more local Tesseract language packs.</summary>
internal sealed record TesseractTrainingDataVerification(
    bool IsValid,
    string Code,
    string? Language,
    string? Path,
    string? ActualSha256,
    string? ExpectedSha256);

/// <summary>Fail-closed integrity checks for packaged Tesseract training data.</summary>
internal static class TesseractTrainingDataVerifier
{
    // Tesseract.Data.English 4.0.0, eng.traineddata, verified from the restored
    // package during Phase 24 execution. Other language packs must be added to
    // this allow-list when they are intentionally packaged and independently
    // verified.
    private static readonly Dictionary<string, string> ExpectedSha256ByLanguage =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["eng"] = "DAA0C97D651C19FBA3B25E81317CD697E9908C8208090C94C3905381C23FC047",
        };

    public static TesseractTrainingDataVerification Verify(
        string tessdataPath,
        string languageSelector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tessdataPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(languageSelector);

        string[] languages = languageSelector.Split('+', StringSplitOptions.RemoveEmptyEntries);
        foreach (string language in languages)
        {
            if (!ExpectedSha256ByLanguage.TryGetValue(language, out string? expectedSha256))
            {
                return new(false, "MissingApprovedChecksum", language, null, null, null);
            }

            string trainedDataPath = Path.Combine(tessdataPath, $"{language}.traineddata");
            if (!File.Exists(trainedDataPath))
            {
                return new(false, "MissingTrainingData", language, trainedDataPath, null, expectedSha256);
            }

            string actualSha256;
            try
            {
                using FileStream stream = File.OpenRead(trainedDataPath);
                actualSha256 = Convert.ToHexString(SHA256.HashData(stream));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return new(false, "TrainingDataUnreadable", language, trainedDataPath, null, expectedSha256);
            }

            if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                return new(false, "TrainingDataChecksumMismatch", language, trainedDataPath, actualSha256, expectedSha256);
            }
        }

        return new(true, "Verified", null, null, null, null);
    }
}
