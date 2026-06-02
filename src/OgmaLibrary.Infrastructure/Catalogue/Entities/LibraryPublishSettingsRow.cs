namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>Host-local publishing policy for a classroom-visible library root.</summary>
public sealed class LibraryPublishSettingsRow
{
    /// <summary>Stable library root identifier.</summary>
    public string LibraryRootId { get; set; } = string.Empty;

    /// <summary>Administrator-facing display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Host-local source path. This is never exposed to classroom clients.</summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>Whether this library is visible to enrolled classroom clients.</summary>
    public bool IsPublished { get; set; }

    /// <summary>Configured AI privacy tier for this library.</summary>
    public int AiTier { get; set; }

    /// <summary>UTC update timestamp.</summary>
    public DateTimeOffset UpdatedUtc { get; set; }
}
