using System.Security.Cryptography;

namespace OgmaLibrary.Tests.GoldenCorpus;

/// <summary>The outcome of verifying one file against a manifest.</summary>
/// <param name="RelativePath">The file's path relative to the corpus root.</param>
/// <param name="Matches">Whether the on-disk content matches the manifest hash.</param>
public sealed record ManifestEntryResult(string RelativePath, bool Matches);

/// <summary>
/// Verifies golden-corpus fixtures against a <c>MANIFEST.sha256</c> file so that an
/// unintended fixture change fails the build (Test Strategy §2.3). The manifest format
/// is one entry per line: a 64-character lower-case SHA-256 hex digest, two spaces, and
/// the path relative to the corpus root (the same format <c>sha256sum</c> emits).
/// </summary>
public static class ManifestVerifier
{
    /// <summary>Verifies every entry in a manifest against the files on disk.</summary>
    /// <param name="corpusRoot">The directory the manifest paths are relative to.</param>
    /// <param name="manifestPath">The path to the <c>MANIFEST.sha256</c> file.</param>
    /// <returns>One result per manifest entry.</returns>
    /// <exception cref="FileNotFoundException">A listed file is missing.</exception>
    public static IReadOnlyList<ManifestEntryResult> Verify(string corpusRoot, string manifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(corpusRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);

        var results = new List<ManifestEntryResult>();
        foreach (string line in File.ReadLines(manifestPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            int split = line.IndexOf("  ", StringComparison.Ordinal);
            if (split < 0)
            {
                throw new FormatException($"Malformed manifest line: '{line}'.");
            }

            string expectedHash = line[..split].Trim().ToLowerInvariant();
            string relativePath = line[(split + 2)..].Trim();
            string fullPath = Path.Combine(corpusRoot, relativePath);

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Manifest references a missing file: {relativePath}", fullPath);
            }

            string actualHash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(fullPath)));
            results.Add(new ManifestEntryResult(relativePath, actualHash == expectedHash));
        }

        return results;
    }

    /// <summary>Returns <see langword="true"/> if every entry matches.</summary>
    /// <param name="corpusRoot">The directory the manifest paths are relative to.</param>
    /// <param name="manifestPath">The path to the manifest.</param>
    /// <returns>Whether all entries match.</returns>
    public static bool AllMatch(string corpusRoot, string manifestPath) =>
        Verify(corpusRoot, manifestPath).All(r => r.Matches);
}
