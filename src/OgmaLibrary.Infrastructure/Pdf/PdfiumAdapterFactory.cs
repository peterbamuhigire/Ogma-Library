using OgmaLibrary.Application.Reader;

namespace OgmaLibrary.Infrastructure.Pdf;

/// <summary>
/// Production <see cref="IPdfRendererFactory"/> that creates <see cref="PdfiumAdapter"/>
/// instances. Registered as a singleton; the returned adapters are document-scoped
/// and must be disposed by the caller (ADR-0004).
/// </summary>
public sealed class PdfiumAdapterFactory : IPdfRendererFactory
{
    /// <inheritdoc />
    public IPdfRenderer Open(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return new PdfiumAdapter(filePath);
    }
}
