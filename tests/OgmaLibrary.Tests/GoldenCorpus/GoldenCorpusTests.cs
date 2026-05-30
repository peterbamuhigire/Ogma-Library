using Xunit;

namespace OgmaLibrary.Tests.GoldenCorpus;

/// <summary>Tests for the golden-corpus harness (Phase 02 WP6).</summary>
public sealed class GoldenCorpusTests
{
    [Fact]
    public void SyntheticCorpusGenerator_SameSeed_ProducesIdenticalOutput()
    {
        var a = SyntheticCorpusGenerator.Generate(count: 100, seed: 42);
        var b = SyntheticCorpusGenerator.Generate(count: 100, seed: 42);

        Assert.Equal(a.Count, b.Count);
        for (int i = 0; i < a.Count; i++)
        {
            Assert.Equal(a[i].Title, b[i].Title);
            Assert.Equal(a[i].Author, b[i].Author);
            Assert.Equal(a[i].Year, b[i].Year);
            Assert.Equal(a[i].PageText, b[i].PageText); // sequence equality on the page text
        }
    }

    [Fact]
    public void SyntheticCorpusGenerator_DifferentSeed_ProducesDifferentOutput()
    {
        var a = SyntheticCorpusGenerator.Generate(count: 100, seed: 42);
        var b = SyntheticCorpusGenerator.Generate(count: 100, seed: 43);

        Assert.NotEqual(a[0], b[0]);
    }

    [Fact]
    public void ManifestVerifier_DetectsMatchAndTamper()
    {
        string root = Path.Combine(Path.GetTempPath(), "ogma-gc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string file = Path.Combine(root, "sample.txt");
            File.WriteAllText(file, "ogma golden corpus");

            string hash = Convert.ToHexStringLower(
                System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file)));
            string manifest = Path.Combine(root, "MANIFEST.sha256");
            File.WriteAllText(manifest, $"{hash}  sample.txt\n");

            Assert.True(ManifestVerifier.AllMatch(root, manifest));

            File.WriteAllText(file, "tampered");
            Assert.False(ManifestVerifier.AllMatch(root, manifest));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
