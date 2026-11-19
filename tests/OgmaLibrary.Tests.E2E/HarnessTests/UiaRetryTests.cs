using System.Runtime.InteropServices;
using OgmaLibrary.Tests.E2E.Harness;

namespace OgmaLibrary.Tests.E2E.HarnessTests;

/// <summary>
/// K102: list-item lookups retry transient UIA failures (a COMException or a stale-element
/// InvalidOperationException) a bounded number of times, and rethrow when they persist.
/// No window is needed; the retry helper is exercised with a scripted delegate.
/// </summary>
[Collection(RealWindowTests.Name)]
public sealed class UiaRetryTests
{
    private static readonly string[] ExpectedItems = ["a", "b"];

    /// <summary>Two transient failures followed by success return the result of the third attempt.</summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Journey", "Harness")]
    [Trait("Tag", "Harness")]
    public void Retry_TwoComExceptionsThenSuccess_ReturnsItems() => Journey.RunCheck("Harness", () =>
    {
        int calls = 0;
        string[] items = Uia.Retry(() =>
        {
            calls++;
            if (calls <= 2)
            {
#pragma warning disable CA2201 // The test simulates the COMException UIA raises when the tree changes.
                throw new COMException("UIA tree changed.");
#pragma warning restore CA2201
            }

            return ExpectedItems;
        }, gapMs: 1);

        Assert.Equal(3, calls);
        Assert.Equal(ExpectedItems, items);
    });

    /// <summary>A failure that persists through every attempt is rethrown after exactly three tries.</summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Journey", "Harness")]
    [Trait("Tag", "Harness")]
    public void Retry_PersistentStaleElement_RethrowsAfterThreeAttempts() => Journey.RunCheck("Harness", () =>
    {
        int calls = 0;
        var thrown = Assert.Throws<InvalidOperationException>(() => Uia.Retry<string[]>(() =>
        {
            calls++;
            throw new InvalidOperationException("Element is stale.");
        }, gapMs: 1));

        Assert.Equal(Uia.RetryAttempts, calls);
        Assert.Equal("Element is stale.", thrown.Message);
    });

    /// <summary>A non-transient failure is not retried.</summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Journey", "Harness")]
    [Trait("Tag", "Harness")]
    public void Retry_NonTransientFailure_IsNotRetried() => Journey.RunCheck("Harness", () =>
    {
        int calls = 0;
        Assert.Throws<ArgumentException>(() => Uia.Retry<string[]>(() =>
        {
            calls++;
            throw new ArgumentException("Wrong argument.");
        }, gapMs: 1));

        Assert.Equal(1, calls);
    });
}
