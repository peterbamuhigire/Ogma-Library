namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>
/// The explicit state of the book-detail inspector (Sept-23 Phase 11, task 11.7). Exactly one
/// state applies; the view shows one template per state and the primary action follows it.
/// </summary>
public enum InspectorState
{
    /// <summary>No book is loaded.</summary>
    None = 0,

    /// <summary>The book detail is being read from the catalogue.</summary>
    Loading = 1,

    /// <summary>The book is loaded and has no blocking condition.</summary>
    Ready = 2,

    /// <summary>The catalogue, or a failed open, says the PDF is no longer where it was.</summary>
    MissingFile = 3,

    /// <summary>The PDF needs a password.</summary>
    PasswordProtected = 4,

    /// <summary>Text recognition is queued or running for the book.</summary>
    Processing = 5,

    /// <summary>The latest text recognition attempt failed.</summary>
    ProcessingFailed = 6,

    /// <summary>The book has no bibliographic metadata beyond its title.</summary>
    NoMetadata = 7,
}
