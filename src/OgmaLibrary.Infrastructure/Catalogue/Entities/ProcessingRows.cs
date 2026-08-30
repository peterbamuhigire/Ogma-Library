namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>Durable lifecycle record for one root scan.</summary>
public sealed class ScanSessionRow
{
    /// <summary>Database identifier.</summary>
    public long ScanSessionId { get; set; }

    /// <summary>The root scanned by this session.</summary>
    public string LibraryRootId { get; set; } = string.Empty;

    /// <summary>Session lifecycle value.</summary>
    public int Status { get; set; }

    /// <summary>UTC session start.</summary>
    public DateTimeOffset StartedUtc { get; set; }

    /// <summary>UTC session completion, when closed.</summary>
    public DateTimeOffset? CompletedUtc { get; set; }
}

/// <summary>Durable, idempotent and leaseable stage execution record.</summary>
public sealed class StageExecutionRow
{
    /// <summary>Database identifier.</summary>
    public long StageExecutionId { get; set; }

    /// <summary>Owning scan session.</summary>
    public long ScanSessionId { get; set; }

    /// <summary>Stable stage name, such as Discovery or PdfValidation.</summary>
    public string StageName { get; set; } = string.Empty;

    /// <summary>Opaque subject key; must not contain document text or secrets.</summary>
    public string SubjectKey { get; set; } = string.Empty;

    /// <summary>Stage lifecycle value.</summary>
    public int Status { get; set; }

    /// <summary>Number of claims attempted.</summary>
    public int Attempt { get; set; }

    /// <summary>Current worker lease owner.</summary>
    public string? LeaseOwner { get; set; }

    /// <summary>UTC lease expiry.</summary>
    public DateTimeOffset? LeaseExpiresUtc { get; set; }

    /// <summary>Earliest UTC time at which retry may be claimed.</summary>
    public DateTimeOffset? NextAttemptUtc { get; set; }

    /// <summary>Stable machine-readable failure code.</summary>
    public string? ErrorCode { get; set; }

    /// <summary>Redacted failure detail safe for local diagnostics.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>UTC creation time.</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>UTC terminal/completed time.</summary>
    public DateTimeOffset? CompletedUtc { get; set; }
}
