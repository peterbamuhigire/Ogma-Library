namespace OgmaLibrary.Application.Catalogue;

/// <summary>
/// Sort fields shared by the catalogue read model and the in-memory view (FR-CAT-002,
/// Sept-23 Phase 10). Values are appended only, because the name is persisted in view state.
/// </summary>
public enum CatalogueSortField
{
    /// <summary>Sort by bibliographic title.</summary>
    Title,

    /// <summary>Sort by primary author.</summary>
    Author,

    /// <summary>Sort by publication year.</summary>
    Year,

    /// <summary>Sort by reader rating.</summary>
    Rating,

    /// <summary>Sort by lifecycle status.</summary>
    Status,

    /// <summary>Sort by the time the book was added to the catalogue.</summary>
    DateAdded,

    /// <summary>Sort by the time the book was last opened in the reader.</summary>
    LastOpened,

    /// <summary>Sort by file size.</summary>
    FileSize,

    /// <summary>Sort by metadata quality score.</summary>
    Quality,
}

/// <summary>
/// Bibliographic fields a "missing field" filter can require to be absent (Sept-23 Phase 10).
/// Several flags are combined conjunctively: the book must miss every selected field.
/// </summary>
[Flags]
public enum CatalogueMissingFields
{
    /// <summary>No missing-field filter.</summary>
    None = 0,

    /// <summary>No author is recorded.</summary>
    Author = 1,

    /// <summary>No ISBN is recorded.</summary>
    Isbn = 2,

    /// <summary>No cover was extracted from the book or a provider.</summary>
    Cover = 4,

    /// <summary>No publication year is recorded.</summary>
    Year = 8,
}
