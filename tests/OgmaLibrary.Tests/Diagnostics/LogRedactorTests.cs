using OgmaLibrary.Infrastructure.Diagnostics;

namespace OgmaLibrary.Tests.Diagnostics;

/// <summary>Sept-23 Phase 02 (T02.3): the log redaction policy.</summary>
public sealed class LogRedactorTests
{
    private const string Home = @"C:\Users\Reader";
    private const string Library = @"D:\Books\My Library";

    [Fact]
    public void Redact_LibraryPdfPath_CollapsesToHashedLibraryToken()
    {
        var redactor = new LogRedactor(Home, [Library]);

        string result = redactor.Redact(@"Opened D:\Books\My Library\Novels\The Secret Garden - Burnett.pdf for rendering");

        Assert.DoesNotContain("Secret Garden", result, StringComparison.Ordinal);
        Assert.DoesNotContain("Novels", result, StringComparison.Ordinal);
        Assert.DoesNotContain("My Library", result, StringComparison.Ordinal);
        Assert.Matches(@"<lib>/…/[0-9a-f]{8}\.pdf for rendering", result);
    }

    [Fact]
    public void Redact_SameLibraryPath_ProducesStableTokenForCorrelation()
    {
        var redactor = new LogRedactor(Home, [Library]);

        string first = redactor.Redact(@"D:\Books\My Library\a\Title.pdf");
        string second = redactor.Redact(@"d:\books\my library\A\TITLE.PDF");

        Assert.Equal(first.ToUpperInvariant(), second.ToUpperInvariant());
    }

    [Fact]
    public void Redact_SiblingFolderWithRootPrefix_IsNotTreatedAsInsideTheLibrary()
    {
        var redactor = new LogRedactor(Home, [@"D:\Books"]);

        string result = redactor.Redact(@"D:\Books2\catalogue.db");

        Assert.Equal(@"D:\Books2\catalogue.db", result);
    }

    [Fact]
    public void Redact_HomePath_BecomesTilde()
    {
        var redactor = new LogRedactor(Home);

        string result = redactor.Redact(@"Data folder C:\Users\Reader\AppData\Local\Ogma Library Data\catalogue.db");

        Assert.Equal(@"Data folder ~\AppData\Local\Ogma Library Data\catalogue.db", result);
    }

    [Fact]
    public void Redact_PdfOutsideLibrary_HashesTheFileNameOnly()
    {
        var redactor = new LogRedactor(Home);

        string result = redactor.Redact(@"Direct open of E:\Downloads\Private Diary 2026.pdf failed");

        Assert.DoesNotContain("Private Diary", result, StringComparison.Ordinal);
        Assert.Matches(@"E:\\Downloads\\[0-9a-f]{8}\.pdf failed", result);
    }

    [Theory]
    [InlineData("Authorization: Bearer abc.DEF-123_xyz", "abc.DEF-123_xyz")]
    [InlineData("api_key=SECRET12345", "SECRET12345")]
    [InlineData("GET https://books.example/v1?q=x&key=AIzaSyA1234567890abcdefghij", "AIzaSyA1234567890abcdefghij")]
    [InlineData("provider key sk-proj-abcdefghijklmnop123456 rejected", "sk-proj-abcdefghijklmnop123456")]
    [InlineData("{\"password\": \"hunter2\"}", "hunter2")]
    [InlineData("contact reader@example.org for access", "reader@example.org")]
    public void Redact_SecretsAndPersonalData_AreRemoved(string input, string secret)
    {
        var redactor = new LogRedactor(Home);

        string result = redactor.Redact(input);

        Assert.DoesNotContain(secret, result, StringComparison.Ordinal);
    }

    [Fact]
    public void Redact_Null_ReturnsEmpty() =>
        Assert.Equal(string.Empty, new LogRedactor(Home).Redact(null));
}
