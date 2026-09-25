using OgmaLibrary.Application.Ingestion;
using OgmaLibrary.Infrastructure.Pdf;
using PdfSharp.Pdf;

namespace OgmaLibrary.Tests.Ingestion;

/// <summary>Sept-23 Phase 05 (K21): discovery-time PDF validity classes.</summary>
public sealed class FileValidityClassifierTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ogma-validity-{Guid.NewGuid():N}");
    private readonly PdfFileValidityClassifier _classifier = new();

    public FileValidityClassifierTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task FileValidity_EmptyFile_IsEmpty()
    {
        string path = Write("empty.pdf", []);
        Assert.Equal(FileValidity.Empty, await _classifier.ClassifyAsync(path));
    }

    [Fact]
    public async Task FileValidity_HtmlNamedPdf_IsNotAPdf()
    {
        string path = Write("not-really-a.pdf", "<html>this is not a pdf</html>"u8.ToArray());
        Assert.Equal(FileValidity.NotAPdf, await _classifier.ClassifyAsync(path));
    }

    [Fact]
    public async Task FileValidity_TruncatedDownload_IsDamaged()
    {
        string full = Path.Combine(_root, "full.pdf");
        IngestionTestFixture.WriteSyntheticPdf(full, "Truncated", "Author");
        byte[] bytes = await File.ReadAllBytesAsync(full);
        string path = Write("truncated.pdf", bytes[..(bytes.Length / 3)]);

        Assert.Equal(FileValidity.Valid, await _classifier.ClassifyAsync(full));
        Assert.Equal(FileValidity.Damaged, await _classifier.ClassifyAsync(path));
    }

    [Fact]
    public async Task FileValidity_PasswordProtectedPdf_IsLocked()
    {
        string path = Path.Combine(_root, "locked.pdf");
        using (var document = new PdfDocument())
        {
            document.AddPage();
            document.SecuritySettings.UserPassword = "secret";
            document.SecuritySettings.OwnerPassword = "owner";
            document.Save(path);
        }

        Assert.Equal(FileValidity.Locked, await _classifier.ClassifyAsync(path));
    }

    [Fact]
    public async Task FileValidity_ValidPdfWithSignatureAfterBinaryPreamble_IsValid()
    {
        // Some producers write a few bytes before %PDF-; the check reads 1,024 bytes.
        string full = Path.Combine(_root, "valid.pdf");
        IngestionTestFixture.WriteSyntheticPdf(full, "Valid", "Author");
        byte[] bytes = await File.ReadAllBytesAsync(full);
        string path = Write("preamble.pdf", [.. "\r\n\r\n"u8.ToArray(), .. bytes]);

        Assert.Equal(FileValidity.Valid, await _classifier.ClassifyAsync(path));
    }

    [Fact]
    public void FileValidity_OnlyValidAndLockedAreCataloguable()
    {
        Assert.True(FileValidity.Valid.IsCataloguable());
        Assert.True(FileValidity.Locked.IsCataloguable());
        Assert.False(FileValidity.Empty.IsCataloguable());
        Assert.False(FileValidity.NotAPdf.IsCataloguable());
        Assert.False(FileValidity.Damaged.IsCataloguable());
    }

    private string Write(string name, byte[] content)
    {
        string path = Path.Combine(_root, name);
        File.WriteAllBytes(path, content);
        return path;
    }
}
