namespace OgmaLibrary.Application.Reader;

/// <summary>Outcome of validating one untrusted PDF input at the I/O boundary.</summary>
public enum PdfInputValidationStatus
{
    /// <summary>Input passed all broker checks.</summary>
    Valid = 0,

    /// <summary>Input path does not resolve to a file.</summary>
    NotFound = 1,

    /// <summary>Input resolves outside the declared root.</summary>
    OutsideRoot = 2,

    /// <summary>Input does not have a PDF extension.</summary>
    InvalidExtension = 3,

    /// <summary>Input does not begin with the PDF magic header.</summary>
    InvalidMagic = 4,

    /// <summary>Input exceeds the configured size ceiling.</summary>
    TooLarge = 5,

    /// <summary>Input could not be read.</summary>
    Unreadable = 6,
}

/// <summary>Redacted validation result safe for UI and diagnostics.</summary>
public sealed record PdfInputValidationResult(
    PdfInputValidationStatus Status,
    long SizeBytes,
    string? CanonicalPath)
{
    /// <summary>Whether the file may be handed to a PDF parser or worker.</summary>
    public bool IsValid => Status == PdfInputValidationStatus.Valid;
}

/// <summary>Validates PDF inputs before any parser or renderer consumes them.</summary>
public interface IPdfInputBroker
{
    /// <summary>Validates one file under a declared root with a bounded header read.</summary>
    Task<PdfInputValidationResult> ValidateAsync(
        string filePath,
        string rootPath,
        CancellationToken cancellationToken = default);
}
