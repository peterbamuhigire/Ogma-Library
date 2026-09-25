using OgmaLibrary.Application;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Application.Navigation;

namespace OgmaLibrary.App.Navigation;

/// <summary>
/// Environment overrides for the user-switchable capabilities. A null value means "not set":
/// the user's Settings choice decides.
/// </summary>
/// <param name="MetadataProviders">The <c>OGMA_ENABLE_METADATA_PROVIDERS</c> value, if set.</param>
/// <param name="ThreeDimensionalShelf">The <c>OGMA_ENABLE_3D_SHELF</c> value, if set.</param>
/// <param name="ClassroomHost">The <c>OGMA_ENABLE_CLASSROOM_HOST</c> value, if set.</param>
public sealed record CapabilityOverrides(
    bool? MetadataProviders = null,
    bool? ThreeDimensionalShelf = null,
    bool? ClassroomHost = null)
{
    /// <summary>No overrides.</summary>
    public static CapabilityOverrides None { get; } = new();

    /// <summary>The override for a capability.</summary>
    /// <param name="flag">The capability.</param>
    /// <returns>The override, or null when unset.</returns>
    public bool? For(UserCapability flag) => flag switch
    {
        UserCapability.MetadataProviders => MetadataProviders,
        UserCapability.ThreeDimensionalShelf => ThreeDimensionalShelf,
        UserCapability.ClassroomHost => ClassroomHost,
        _ => null,
    };
}

/// <summary>
/// The desktop <see cref="ICapabilityState"/> and <see cref="ICapabilitySettings"/> (Sept-23
/// Phases 07 and 08). The three user-switchable capabilities resolve as environment override,
/// then user preference, then off; the AI state is read live; the classroom client state is
/// pushed by the shell when connectivity changes. It also gates online metadata lookups.
/// </summary>
public sealed class RuntimeCapabilityState : ICapabilitySettings, IMetadataProviderPolicy
{
    private readonly Func<bool> _isAiConfigured;
    private readonly CapabilityOverrides _overrides;
    private bool _isClassroomClientConnected;
    private bool _lastAiConfigured;
    private volatile bool _preferMetadata;
    private volatile bool _preferShelf3D;
    private volatile bool _preferClassroomHost;

    /// <summary>Initializes a new instance of the <see cref="RuntimeCapabilityState"/> class.</summary>
    /// <param name="classroomHostEnabled">The initial user preference for the classroom Host.</param>
    /// <param name="shelf3DAvailable">The initial user preference for the 3D shelf.</param>
    /// <param name="metadataProvidersEnabled">The initial user preference for online metadata providers.</param>
    /// <param name="isAiConfigured">Reads whether an AI provider is configured.</param>
    /// <param name="overrides">Environment overrides, which win over preferences.</param>
    public RuntimeCapabilityState(
        bool classroomHostEnabled = false,
        bool shelf3DAvailable = false,
        bool metadataProvidersEnabled = false,
        Func<bool>? isAiConfigured = null,
        CapabilityOverrides? overrides = null)
    {
        _preferClassroomHost = classroomHostEnabled;
        _preferShelf3D = shelf3DAvailable;
        _preferMetadata = metadataProvidersEnabled;
        _overrides = overrides ?? CapabilityOverrides.None;
        _isAiConfigured = isAiConfigured ?? (() => false);
        _lastAiConfigured = ReadAi();
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public bool IsAiConfigured => _lastAiConfigured;

    /// <inheritdoc />
    public bool IsClassroomHostEnabled => IsEnabled(UserCapability.ClassroomHost);

    /// <inheritdoc />
    public bool IsClassroomClientConnected => _isClassroomClientConnected;

    /// <inheritdoc />
    public bool IsShelf3DAvailable => IsEnabled(UserCapability.ThreeDimensionalShelf);

    /// <inheritdoc />
    public bool AreMetadataProvidersEnabled => IsEnabled(UserCapability.MetadataProviders);

    /// <inheritdoc />
    bool IMetadataProviderPolicy.AreOnlineProvidersEnabled => AreMetadataProvidersEnabled;

    /// <summary>Resolves an effective value: the override when set, otherwise the preference.</summary>
    /// <param name="environmentOverride">The environment override, or null.</param>
    /// <param name="preference">The user preference.</param>
    /// <returns>The effective value.</returns>
    public static bool Resolve(bool? environmentOverride, bool preference) => environmentOverride ?? preference;

    /// <inheritdoc />
    public bool IsEnabled(UserCapability flag) => Resolve(_overrides.For(flag), Preference(flag));

    /// <inheritdoc />
    public bool IsManagedByEnvironment(UserCapability flag) => _overrides.For(flag).HasValue;

    /// <inheritdoc />
    public void ApplyPreferences(UserPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        int before = Snapshot();
        _preferMetadata = preferences.EnableMetadataProviders;
        _preferShelf3D = preferences.EnableThreeDimensionalShelf;
        _preferClassroomHost = preferences.EnableClassroomHost;
        if (Snapshot() != before)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Records whether this device is connected to a classroom Host.</summary>
    /// <param name="connected">The connection state.</param>
    public void SetClassroomClientConnected(bool connected)
    {
        if (_isClassroomClientConnected != connected)
        {
            _isClassroomClientConnected = connected;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Re-reads live capabilities (for example after AI settings change).</summary>
    public void Refresh()
    {
        bool ai = ReadAi();
        if (ai != _lastAiConfigured)
        {
            _lastAiConfigured = ai;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private bool Preference(UserCapability flag) => flag switch
    {
        UserCapability.MetadataProviders => _preferMetadata,
        UserCapability.ThreeDimensionalShelf => _preferShelf3D,
        UserCapability.ClassroomHost => _preferClassroomHost,
        _ => false,
    };

    // A compact comparison key: the three effective values packed into bits.
    private int Snapshot() =>
        (IsEnabled(UserCapability.MetadataProviders) ? 1 : 0) |
        (IsEnabled(UserCapability.ThreeDimensionalShelf) ? 2 : 0) |
        (IsEnabled(UserCapability.ClassroomHost) ? 4 : 0);

    private bool ReadAi()
    {
        try
        {
            return _isAiConfigured();
        }
        catch (InvalidOperationException)
        {
            // Intentionally ignored: an unreadable AI setting means "not configured".
            return false;
        }
    }
}
