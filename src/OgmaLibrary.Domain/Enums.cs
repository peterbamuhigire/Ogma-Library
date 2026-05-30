namespace OgmaLibrary.Domain;

/// <summary>The availability of a catalogued file on disk (FR-LIB-004).</summary>
public enum AvailabilityStatus
{
    /// <summary>The file is present and readable at its recorded path.</summary>
    Available = 0,

    /// <summary>The file was catalogued previously but is no longer present at scan time.</summary>
    Unavailable = 1,
}

/// <summary>The reader's progress state for a book.</summary>
public enum ReadingStatus
{
    /// <summary>Not yet opened.</summary>
    Unread = 0,

    /// <summary>Currently being read.</summary>
    Reading = 1,

    /// <summary>Finished.</summary>
    Finished = 2,

    /// <summary>Started but set aside.</summary>
    Abandoned = 3,
}

/// <summary>The kind of annotation anchored to a page region (FR-READ-008).</summary>
public enum AnnotationKind
{
    /// <summary>A coloured highlight over a text region.</summary>
    Highlight = 0,

    /// <summary>A textual note anchored to a region.</summary>
    Note = 1,
}
