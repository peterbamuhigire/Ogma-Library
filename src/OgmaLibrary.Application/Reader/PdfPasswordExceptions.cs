namespace OgmaLibrary.Application.Reader;

/// <summary>Raised when a PDF cannot be opened without a password.</summary>
public sealed class PdfPasswordRequiredException : InvalidOperationException
{
    /// <summary>Initializes a new instance of <see cref="PdfPasswordRequiredException"/>.</summary>
    /// <param name="filePath">The protected PDF path, or empty when it is not known.</param>
    public PdfPasswordRequiredException(string filePath)
        : base(string.IsNullOrWhiteSpace(filePath)
            ? "The PDF file requires a password."
            : $"The PDF file '{filePath}' requires a password.")
    {
        FilePath = filePath;
    }

    /// <summary>The protected PDF path.</summary>
    public string FilePath { get; }
}

/// <summary>Raised when a supplied PDF password is missing or incorrect.</summary>
public sealed class PdfPasswordIncorrectException : InvalidOperationException
{
    /// <summary>Initializes a new instance of <see cref="PdfPasswordIncorrectException"/>.</summary>
    /// <param name="filePath">The protected PDF path, or empty when it is not known.</param>
    public PdfPasswordIncorrectException(string filePath)
        : base(string.IsNullOrWhiteSpace(filePath)
            ? "The supplied password did not unlock the PDF file."
            : $"The supplied password did not unlock PDF file '{filePath}'.")
    {
        FilePath = filePath;
    }

    /// <summary>The protected PDF path.</summary>
    public string FilePath { get; }
}
