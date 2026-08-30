namespace OgmaLibrary.Domain;

/// <summary>Immutable identity of an approved library root.</summary>
public readonly record struct LibraryRootId
{
    /// <summary>Initializes an opaque stable root identity.</summary>
    public LibraryRootId(string value) => Value = StableIdentity.Validate(value, nameof(value));

    /// <summary>The opaque identity value.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Health state of a configured library root.</summary>
public enum LibraryRootStatus
{
    /// <summary>The root is reachable and the application can inspect it.</summary>
    Available = 0,

    /// <summary>The root is not currently reachable.</summary>
    Unavailable = 1,

    /// <summary>The root exists but access was denied.</summary>
    PermissionDenied = 2,

    /// <summary>The root needs a user-selected replacement location.</summary>
    NeedsRelink = 3,
}

/// <summary>Permission probe state for a configured library root.</summary>
public enum LibraryRootPermissionStatus
{
    /// <summary>The permission state has not been probed.</summary>
    Unknown = 0,

    /// <summary>The application can inspect the root.</summary>
    Granted = 1,

    /// <summary>The operating system denied inspection.</summary>
    Denied = 2,
}

/// <summary>Immutable identity of one physical file occurrence within a root.</summary>
public readonly record struct FileOccurrenceId
{
    /// <summary>Initializes an opaque stable file-occurrence identity.</summary>
    public FileOccurrenceId(string value) => Value = StableIdentity.Validate(value, nameof(value));

    /// <summary>The opaque identity value.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Immutable identity of one exact sequence of source-file bytes.</summary>
public readonly record struct ContentAssetId
{
    /// <summary>Initializes an opaque stable content-asset identity.</summary>
    public ContentAssetId(string value) => Value = StableIdentity.Validate(value, nameof(value));

    /// <summary>The opaque identity value.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Immutable identity of a specific bibliographic publication.</summary>
public readonly record struct EditionId
{
    /// <summary>Initializes an opaque stable edition identity.</summary>
    public EditionId(string value) => Value = StableIdentity.Validate(value, nameof(value));

    /// <summary>The opaque identity value.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Immutable identity of an intellectual work.</summary>
public readonly record struct WorkId
{
    /// <summary>Initializes an opaque stable work identity.</summary>
    public WorkId(string value) => Value = StableIdentity.Validate(value, nameof(value));

    /// <summary>The opaque identity value.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Immutable identity consumed by catalogue presentation clients.</summary>
public readonly record struct CatalogueItemId
{
    /// <summary>Initializes an opaque stable catalogue-presentation identity.</summary>
    public CatalogueItemId(string value) => Value = StableIdentity.Validate(value, nameof(value));

    /// <summary>The opaque identity value.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Immutable identity of a persisted identity decision.</summary>
public readonly record struct IdentityDecisionId
{
    /// <summary>Initializes an opaque stable decision identity.</summary>
    public IdentityDecisionId(string value) => Value = StableIdentity.Validate(value, nameof(value));

    /// <summary>The opaque identity value.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>
/// One observed physical occurrence. A null <see cref="ContentAssetId"/> means
/// fingerprinting has not yet established exact byte identity.
/// </summary>
public sealed class FileOccurrence
{
    /// <summary>Initializes a path-independent file occurrence.</summary>
    public FileOccurrence(
        FileOccurrenceId id,
        LibraryRootId libraryRootId,
        ContentAssetId? contentAssetId,
        AvailabilityStatus availability)
    {
        StableIdentity.EnsureDefined(id.Value, nameof(id));
        StableIdentity.EnsureDefined(libraryRootId.Value, nameof(libraryRootId));
        if (contentAssetId is ContentAssetId assetId)
        {
            StableIdentity.EnsureDefined(assetId.Value, nameof(contentAssetId));
        }

        if (!Enum.IsDefined(availability))
        {
            throw new ArgumentOutOfRangeException(nameof(availability));
        }

        Id = id;
        LibraryRootId = libraryRootId;
        ContentAssetId = contentAssetId;
        Availability = availability;
    }

    /// <summary>The occurrence identity.</summary>
    public FileOccurrenceId Id { get; }

    /// <summary>The root that owns the locator for this occurrence.</summary>
    public LibraryRootId LibraryRootId { get; }

    /// <summary>The exact-byte asset, or null while content identity is unknown.</summary>
    public ContentAssetId? ContentAssetId { get; }

    /// <summary>The observed availability without implying deletion.</summary>
    public AvailabilityStatus Availability { get; }
}

/// <summary>
/// An exact source-file byte sequence. A content asset cannot exist without a
/// genuine validated content hash; an unfingerprinted file remains an occurrence
/// with unknown asset identity.
/// </summary>
public sealed class ContentAsset
{
    /// <summary>Initializes a content asset from verified fingerprint output.</summary>
    public ContentAsset(
        ContentAssetId id,
        ContentHash sha256,
        int fingerprintVersion,
        long sizeBytes)
    {
        StableIdentity.EnsureDefined(id.Value, nameof(id));
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256.Hex, nameof(sha256));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fingerprintVersion);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeBytes);
        Id = id;
        Sha256 = sha256;
        FingerprintVersion = fingerprintVersion;
        SizeBytes = sizeBytes;
    }

    /// <summary>The content-asset identity.</summary>
    public ContentAssetId Id { get; }

    /// <summary>The verified SHA-256 identity of the complete source file.</summary>
    public ContentHash Sha256 { get; }

    /// <summary>The fingerprint contract version that produced the digest.</summary>
    public int FingerprintVersion { get; }

    /// <summary>The byte length covered by the digest.</summary>
    public long SizeBytes { get; }
}

/// <summary>Resolution state of a bibliographic work or edition.</summary>
public enum BibliographicResolutionState
{
    /// <summary>No bibliographic identity has been established.</summary>
    Unknown = 0,

    /// <summary>A migration or ingestion placeholder exists but is not verified.</summary>
    Provisional = 1,

    /// <summary>Strong evidence or an explicit user decision established identity.</summary>
    Identified = 2,

    /// <summary>Competing candidates require explicit review.</summary>
    Ambiguous = 3,
}

/// <summary>A specific publication that may be represented by several assets.</summary>
public sealed class Edition
{
    private readonly HashSet<ContentAssetId> _contentAssetIds = [];
    private readonly HashSet<BibliographicIdentifier> _identifiers = [];

    /// <summary>Initializes an edition under one intellectual work.</summary>
    public Edition(
        EditionId id,
        WorkId workId,
        BibliographicResolutionState resolutionState)
    {
        StableIdentity.EnsureDefined(id.Value, nameof(id));
        StableIdentity.EnsureDefined(workId.Value, nameof(workId));
        StableIdentity.EnsureDefined(resolutionState, nameof(resolutionState));
        Id = id;
        WorkId = workId;
        ResolutionState = resolutionState;
    }

    /// <summary>The edition identity.</summary>
    public EditionId Id { get; }

    /// <summary>The parent intellectual work.</summary>
    public WorkId WorkId { get; }

    /// <summary>Whether this edition is unknown, provisional, identified or ambiguous.</summary>
    public BibliographicResolutionState ResolutionState { get; }

    /// <summary>Exact-byte assets currently associated with the edition.</summary>
    public IReadOnlySet<ContentAssetId> ContentAssetIds => _contentAssetIds;

    /// <summary>Scoped external or standard identifiers attached to the edition.</summary>
    public IReadOnlySet<BibliographicIdentifier> Identifiers => _identifiers;

    /// <summary>Adds an asset without duplicating the relationship.</summary>
    public bool AddContentAsset(ContentAssetId assetId)
    {
        StableIdentity.EnsureDefined(assetId.Value, nameof(assetId));
        return _contentAssetIds.Add(assetId);
    }

    /// <summary>Adds an edition-scoped identifier.</summary>
    public bool AddIdentifier(BibliographicIdentifier identifier)
    {
        if (identifier.Scope != BibliographicIdentityScope.Edition)
        {
            throw new ArgumentException(
                "An edition can contain only edition-scoped identifiers.",
                nameof(identifier));
        }

        return _identifiers.Add(identifier);
    }
}

/// <summary>An intellectual work that can contain several distinct editions.</summary>
public sealed class Work
{
    private readonly HashSet<EditionId> _editionIds = [];
    private readonly HashSet<BibliographicIdentifier> _identifiers = [];

    /// <summary>Initializes an intellectual work.</summary>
    public Work(WorkId id, BibliographicResolutionState resolutionState)
    {
        StableIdentity.EnsureDefined(id.Value, nameof(id));
        StableIdentity.EnsureDefined(resolutionState, nameof(resolutionState));
        Id = id;
        ResolutionState = resolutionState;
    }

    /// <summary>The work identity.</summary>
    public WorkId Id { get; }

    /// <summary>Whether this work is unknown, provisional, identified or ambiguous.</summary>
    public BibliographicResolutionState ResolutionState { get; }

    /// <summary>Edition identities grouped under this work.</summary>
    public IReadOnlySet<EditionId> EditionIds => _editionIds;

    /// <summary>Scoped external identifiers attached to the work.</summary>
    public IReadOnlySet<BibliographicIdentifier> Identifiers => _identifiers;

    /// <summary>Adds an edition only when it belongs to this work.</summary>
    public bool AddEdition(Edition edition)
    {
        ArgumentNullException.ThrowIfNull(edition);
        if (edition.WorkId != Id)
        {
            throw new ArgumentException(
                "The edition belongs to a different intellectual work.",
                nameof(edition));
        }

        return _editionIds.Add(edition.Id);
    }

    /// <summary>Adds a work-scoped identifier.</summary>
    public bool AddIdentifier(BibliographicIdentifier identifier)
    {
        if (identifier.Scope != BibliographicIdentityScope.Work)
        {
            throw new ArgumentException(
                "A work can contain only work-scoped identifiers.",
                nameof(identifier));
        }

        return _identifiers.Add(identifier);
    }

}

/// <summary>
/// Stable identity handed to grid, list, search, advisor and 3D presentation
/// clients. It selects an edition and optional preferred local occurrence without
/// exposing a filesystem path.
/// </summary>
public sealed record CataloguePresentationIdentity
{
    /// <summary>Initializes a path-independent presentation identity.</summary>
    public CataloguePresentationIdentity(
        CatalogueItemId catalogueItemId,
        WorkId workId,
        EditionId editionId,
        FileOccurrenceId? preferredOccurrenceId)
    {
        StableIdentity.EnsureDefined(catalogueItemId.Value, nameof(catalogueItemId));
        StableIdentity.EnsureDefined(workId.Value, nameof(workId));
        StableIdentity.EnsureDefined(editionId.Value, nameof(editionId));
        if (preferredOccurrenceId is FileOccurrenceId occurrenceId)
        {
            StableIdentity.EnsureDefined(occurrenceId.Value, nameof(preferredOccurrenceId));
        }

        CatalogueItemId = catalogueItemId;
        WorkId = workId;
        EditionId = editionId;
        PreferredOccurrenceId = preferredOccurrenceId;
    }

    /// <summary>The stable identity used by catalogue presentation clients.</summary>
    public CatalogueItemId CatalogueItemId { get; }

    /// <summary>The intellectual work selected for presentation.</summary>
    public WorkId WorkId { get; }

    /// <summary>The publication selected for presentation.</summary>
    public EditionId EditionId { get; }

    /// <summary>The preferred local occurrence, or null if none is available.</summary>
    public FileOccurrenceId? PreferredOccurrenceId { get; }
}

/// <summary>Whether an external identifier names a work or an edition.</summary>
public enum BibliographicIdentityScope
{
    /// <summary>The identifier names an intellectual work.</summary>
    Work = 0,

    /// <summary>The identifier names a particular publication.</summary>
    Edition = 1,
}

/// <summary>Supported identity evidence types.</summary>
public enum BibliographicIdentifierKind
{
    /// <summary>A validated ISBN-10.</summary>
    Isbn10 = 0,

    /// <summary>A validated ISBN-13.</summary>
    Isbn13 = 1,

    /// <summary>A normalized Digital Object Identifier.</summary>
    Doi = 2,

    /// <summary>A provider-specific edition identifier.</summary>
    ProviderEditionId = 3,

    /// <summary>A provider-specific work identifier.</summary>
    ProviderWorkId = 4,
}

/// <summary>A source-attributed, explicitly scoped bibliographic identifier.</summary>
public readonly record struct BibliographicIdentifier
{
    /// <summary>Initializes a normalized identifier.</summary>
    public BibliographicIdentifier(
        string source,
        BibliographicIdentifierKind kind,
        BibliographicIdentityScope scope,
        string value)
    {
        Source = StableIdentity.Validate(source, nameof(source)).ToLowerInvariant();
        Value = StableIdentity.Validate(value, nameof(value));
        ValidateScope(kind, scope);
        Kind = kind;
        Scope = scope;
    }

    /// <summary>The provider or standard namespace.</summary>
    public string Source { get; }

    /// <summary>The identifier type.</summary>
    public BibliographicIdentifierKind Kind { get; }

    /// <summary>Whether the identifier names a work or edition.</summary>
    public BibliographicIdentityScope Scope { get; }

    /// <summary>The normalized identifier value.</summary>
    public string Value { get; }

    /// <summary>
    /// Determines whether two identifiers refer to the same scoped identity.
    /// Standard identifiers are provider-independent; provider IDs are not.
    /// </summary>
    public bool RefersToSameIdentityAs(BibliographicIdentifier other)
    {
        if (Kind != other.Kind || Scope != other.Scope ||
            !string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Kind is BibliographicIdentifierKind.Isbn10 or
                       BibliographicIdentifierKind.Isbn13 or
                       BibliographicIdentifierKind.Doi ||
               string.Equals(Source, other.Source, StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateScope(
        BibliographicIdentifierKind kind,
        BibliographicIdentityScope scope)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (!Enum.IsDefined(scope))
        {
            throw new ArgumentOutOfRangeException(nameof(scope));
        }

        bool invalid = kind switch
        {
            BibliographicIdentifierKind.Isbn10 or
            BibliographicIdentifierKind.Isbn13 or
            BibliographicIdentifierKind.Doi or
            BibliographicIdentifierKind.ProviderEditionId =>
                scope != BibliographicIdentityScope.Edition,
            BibliographicIdentifierKind.ProviderWorkId =>
                scope != BibliographicIdentityScope.Work,
            _ => false,
        };

        if (invalid)
        {
            throw new ArgumentException("The identifier kind is not valid for the supplied scope.");
        }
    }
}

internal static class StableIdentity
{
    private const int MaxLength = 128;

    public static string Validate(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > MaxLength || value.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            throw new ArgumentException(
                "A stable identity must contain 1 to 128 non-whitespace, non-control characters.",
                parameterName);
        }

        return value;
    }

    public static void EnsureDefined(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "A default or empty stable identity is not a defined identity.",
                parameterName);
        }
    }

    public static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}

/// <summary>Path-free occurrence projection returned by canonical repositories.</summary>
public sealed record CanonicalFileOccurrenceProjection(
    FileOccurrenceId FileOccurrenceId,
    LibraryRootId LibraryRootId,
    ContentAssetId? ContentAssetId,
    AvailabilityStatus Availability);

/// <summary>
/// Explicit canonical identity projection for catalogue, search, advisor and 3D
/// consumers during the legacy-ID compatibility window.
/// </summary>
public sealed record CanonicalIdentityProjection(
    CataloguePresentationIdentity PresentationIdentity,
    BibliographicResolutionState WorkResolutionState,
    BibliographicResolutionState EditionResolutionState,
    IReadOnlyList<CanonicalFileOccurrenceProjection> Occurrences,
    bool RequiresSemanticReindex);
