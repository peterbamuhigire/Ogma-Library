using OgmaLibrary.Application.Reader;
using OgmaLibrary.Infrastructure.Pdf;
using PdfSharp.Pdf;

namespace OgmaLibrary.Tests.Pdf;

/// <summary>Phase 15 password-protected PDF integration tests.</summary>
public sealed class PdfiumAdapterPasswordTests
{
    private const string Password = "ogma-test-password";

    [Fact]
    public async Task PasswordPdf_UnlockViaPassword_OpensAndRendersPage()
    {
        string path = PasswordProtectedPdfFixture.Path;

        Assert.Throws<PdfPasswordRequiredException>(() => new PdfiumAdapter(path));

        using var renderer = new PdfiumAdapter(path, Password.ToCharArray());
        RenderResult result = await renderer.RenderPageAsync(
            0,
            new RenderRequest(800),
            CancellationToken.None);

        Assert.Equal(1, renderer.PageCount);
        Assert.True(result.PngBytes.Length > 0);
    }

    /// <summary>
    /// Sept-23 stabilisation: the isolated worker's password error used to be re-wrapped with the
    /// worker's message as the "path": "The PDF file 'A password is required to open the PDF.'
    /// requires a password."
    /// </summary>
    [Fact]
    public async Task WorkerSession_PasswordPdf_ThrowsTypedExceptionNamingTheFile()
    {
        string sandbox = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ogma-password-worker-{Guid.NewGuid():N}");
        try
        {
            var worker = new PdfWorkerClient(new PdfWorkerOptions { SandboxRoot = sandbox });

            PdfPasswordRequiredException exception = await Assert.ThrowsAsync<PdfPasswordRequiredException>(
                () => Task.Run(() => worker.OpenSession(PasswordProtectedPdfFixture.Path).Dispose()));

            Assert.Equal(PasswordProtectedPdfFixture.Path, exception.FilePath);
            Assert.DoesNotContain("A password is required", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(sandbox))
            {
                Directory.Delete(sandbox, recursive: true);
            }
        }
    }

    private static class PasswordProtectedPdfFixture
    {
        private static readonly Lazy<string> LazyPath = new(Create, isThreadSafe: true);

        public static string Path => LazyPath.Value;

        private static string Create()
        {
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ogma-password-pdf-tests");
            Directory.CreateDirectory(dir);
            string path = System.IO.Path.Combine(dir, "password-protected.pdf");
            if (File.Exists(path))
            {
                return path;
            }

            using var document = new PdfDocument();
            document.Info.Title = "Ogma password-protected fixture";
            document.AddPage();
            document.SecuritySettings.UserPassword = Password;
            document.SecuritySettings.OwnerPassword = "ogma-owner-password";
            document.Save(path);
            return path;
        }
    }
}
