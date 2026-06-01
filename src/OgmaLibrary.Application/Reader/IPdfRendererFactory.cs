namespace OgmaLibrary.Application.Reader;

/// <summary>
/// Creates document-scoped <see cref="IPdfRenderer"/> instances for a specific PDF file.
/// The factory is registered as a singleton; callers must dispose the returned renderer
/// when the document is closed to release the native PDFium handle.
/// </summary>
/// <remarks>
/// The production implementation selects PDFtoImage on Windows and macOS (ADR-0004).
/// A mock implementation is registered in the test host.
/// </remarks>
public interface IPdfRendererFactory
{
    /// <summary>
    /// Opens the PDF file at the specified path and returns a renderer for it.
    /// </summary>
    /// <param name="filePath">The absolute path to the PDF file.</param>
    /// <returns>A document-scoped renderer. The caller must dispose it.</returns>
    IPdfRenderer Open(string filePath);

    /// <summary>
    /// Opens a password-protected PDF file at the specified path and returns a renderer for it.
    /// </summary>
    /// <param name="filePath">The absolute path to the PDF file.</param>
    /// <param name="password">The password characters. Implementations must not persist this value.</param>
    /// <returns>A document-scoped renderer. The caller must dispose it.</returns>
    IPdfRenderer Open(string filePath, char[] password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(password);
        throw new NotSupportedException("This PDF renderer does not support password-protected documents.");
    }
}
