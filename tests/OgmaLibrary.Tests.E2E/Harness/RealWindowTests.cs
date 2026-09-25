namespace OgmaLibrary.Tests.E2E.Harness;

/// <summary>All real-window tests share one serial collection: one app on the desktop at a time.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RealWindowTests
{
    /// <summary>The collection name.</summary>
    public const string Name = "Real window (serial)";
}
