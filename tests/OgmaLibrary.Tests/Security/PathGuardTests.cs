using OgmaLibrary.Infrastructure.Security;

namespace OgmaLibrary.Tests.Security;

/// <summary>Adversarial coverage for all filesystem trust-root checks.</summary>
public sealed class PathGuardTests
{
    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("..\\outside.txt")]
    [InlineData("%2e%2e%2foutside.txt")]
    [InlineData("%2e%2e/outside.txt")]
    [InlineData("C:\\outside.txt")]
    [InlineData("\\\\server\\share\\outside.txt")]
    public void EnsureWithinRoot_RejectsTraversal(string candidate)
    {
        string root = Path.Combine(Path.GetTempPath(), "ogma-path-guard", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Assert.Throws<PathTraversalException>(() => PathGuard.EnsureWithinRoot(candidate, root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EnsureWithinRoot_AllowsCanonicalChild()
    {
        string root = Path.Combine(Path.GetTempPath(), "ogma-path-guard", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string nested = Path.Combine(root, "nested", "book.pdf");
            string actual = PathGuard.EnsureWithinRoot(nested, root);
            Assert.Equal(Path.GetFullPath(nested), actual);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EnsureWithinRoot_RejectsSymlinkEscapeWhenSupported()
    {
        string root = Path.Combine(Path.GetTempPath(), "ogma-path-guard", Guid.NewGuid().ToString("N"));
        string outside = Path.Combine(Path.GetTempPath(), "ogma-path-guard-outside", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);
        string link = Path.Combine(root, "linked");
        try
        {
            try
            {
                Directory.CreateSymbolicLink(link, outside);
            }
            catch (Exception error) when (error is UnauthorizedAccessException or PlatformNotSupportedException or IOException)
            {
                return;
            }

            Assert.Throws<PathTraversalException>(() => PathGuard.EnsureWithinRoot(Path.Combine(link, "secret.txt"), root));
        }
        finally
        {
            if (Directory.Exists(link))
            {
                Directory.Delete(link);
            }

            Directory.Delete(root, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }
}
