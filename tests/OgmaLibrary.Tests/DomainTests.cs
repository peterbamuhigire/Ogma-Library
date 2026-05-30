using OgmaLibrary.Domain;
using Xunit;

namespace OgmaLibrary.Tests;

/// <summary>Unit tests for the Domain value objects (the pure-logic core).</summary>
public sealed class DomainTests
{
    [Theory]
    [InlineData("9780262033848", true)]   // valid ISBN-13
    [InlineData("0262033844", true)]       // valid ISBN-10
    [InlineData("978-0-262-03384-8", true)] // hyphens tolerated
    [InlineData("9780262033849", false)]   // bad check digit
    [InlineData("notanisbn", false)]
    [InlineData("", false)]
    public void Isbn_TryParse_ValidatesCheckDigit(string candidate, bool expected)
    {
        bool parsed = Isbn.TryParse(candidate, out _);
        Assert.Equal(expected, parsed);
    }

    [Fact]
    public void ContentHash_Compute_IsStableAndLowerHex()
    {
        byte[] content = "Ogma Library"u8.ToArray();

        ContentHash a = ContentHash.Compute(content);
        ContentHash b = ContentHash.Compute(content);

        Assert.Equal(a, b);
        Assert.Equal(64, a.Hex.Length);
        Assert.Equal(a.Hex, a.Hex.ToLowerInvariant());
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void ConfidenceScore_RejectsOutOfRange(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConfidenceScore(value));
    }

    [Fact]
    public void ConfidenceScore_AcceptsInRange()
    {
        var score = new ConfidenceScore(0.82);
        Assert.Equal(0.82, score.Value, precision: 5);
    }
}
