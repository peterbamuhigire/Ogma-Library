namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>
/// The four segmented sections of the book-detail inspector (Sept-23 Phase 11, slice 2).
/// They replace the earlier seven tab groups.
/// </summary>
public enum InspectorSection
{
    /// <summary>Tags, reading progress, annotations and reading memory.</summary>
    Overview = 0,

    /// <summary>Bibliographic fields, write-back and the collapsed technical details.</summary>
    Details = 1,

    /// <summary>The table of contents.</summary>
    Contents = 2,

    /// <summary>History, enrichment, OCR, proposals, provenance and the AI card.</summary>
    Activity = 3,
}
