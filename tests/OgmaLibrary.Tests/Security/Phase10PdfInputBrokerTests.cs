using System.Text;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Infrastructure.Pdf;
using OgmaLibrary.Infrastructure.Pathing;

namespace OgmaLibrary.Tests.Security;

/// <summary>Phase 10 acceptance tests for the root-bounded PDF input broker.</summary>
public sealed class Phase10PdfInputBrokerTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), $"ogma-phase10-{Guid.NewGuid():N}");
    private readonly PdfInputBroker _broker = new(maximumBytes: 16);

    public Phase10PdfInputBrokerTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task ValidPdfHeader_IsAcceptedAndCanonicalized()
    {
        string path = Path.Combine(_root, "book.pdf");
        await File.WriteAllTextAsync(path, "%PDF-1.7\nbody", Encoding.ASCII);

        PdfInputValidationResult result = await _broker.ValidateAsync(path, _root);

        Assert.True(result.IsValid);
        Assert.Equal(PdfInputValidationStatus.Valid, result.Status);
        Assert.Equal(PathGuard.CanonicalizeRoot(path), result.CanonicalPath);
        Assert.Equal(13, result.SizeBytes);
    }

    [Theory]
    [InlineData("missing.pdf", PdfInputValidationStatus.NotFound)]
    [InlineData("book.txt", PdfInputValidationStatus.InvalidExtension)]
    public async Task InvalidInputs_ReturnTypedRedactedStatus(
        string fileName,
        PdfInputValidationStatus expected)
    {
        if (fileName.EndsWith(".txt", StringComparison.Ordinal))
        {
            await File.WriteAllTextAsync(Path.Combine(_root, fileName), "not pdf");
        }

        PdfInputValidationResult result = await _broker.ValidateAsync(
            Path.Combine(_root, fileName), _root);

        Assert.Equal(expected, result.Status);
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task TraversalMagicAndSizeViolations_AreRejectedBeforeParserEntry()
    {
        string outside = Path.Combine(Path.GetDirectoryName(_root)!, "outside.pdf");
        await File.WriteAllTextAsync(outside, "%PDF-1.7");
        string invalid = Path.Combine(_root, "invalid.pdf");
        await File.WriteAllTextAsync(invalid, "hello");
        string large = Path.Combine(_root, "large.pdf");
        await File.WriteAllBytesAsync(large, [.. Encoding.ASCII.GetBytes("%PDF-1.7"), .. new byte[20]]);

        PdfInputValidationResult traversal = await _broker.ValidateAsync(
            Path.Combine(_root, "..", Path.GetFileName(outside)), _root);
        PdfInputValidationResult magic = await _broker.ValidateAsync(invalid, _root);
        PdfInputValidationResult size = await _broker.ValidateAsync(large, _root);

        Assert.Equal(PdfInputValidationStatus.OutsideRoot, traversal.Status);
        Assert.Equal(PdfInputValidationStatus.InvalidMagic, magic.Status);
        Assert.Equal(PdfInputValidationStatus.TooLarge, size.Status);
    }
}
