namespace OgmaLibrary.App.ViewModels.Catalogue;

/// <summary>
/// The single state-driven primary action of the book-detail inspector
/// (Sept-23 Phase 11, task 11.2).
/// </summary>
public enum PrimaryActionKind
{
    /// <summary>No book is loaded; there is no primary action.</summary>
    None = 0,

    /// <summary>The book is available and has not been started.</summary>
    Read,

    /// <summary>The book is available and has saved reading progress.</summary>
    ContinueReading,

    /// <summary>The catalogue cannot find the book's PDF on disk.</summary>
    LocateFile,

    /// <summary>The book is password-protected.</summary>
    Unlock,
}
