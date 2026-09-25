namespace OgmaLibrary.Application.Navigation;

/// <summary>The optional capabilities a user can switch on in Settings (Sept-23 Phase 08, K16).</summary>
public enum UserCapability
{
    /// <summary>Online book-detail lookups (Google Books, Open Library).</summary>
    MetadataProviders,

    /// <summary>The 3D bookshelf preview.</summary>
    ThreeDimensionalShelf,

    /// <summary>This device may act as a classroom Host.</summary>
    ClassroomHost,
}

/// <summary>
/// Resolves each user-switchable capability (Sept-23 Phase 08, task 8.2). The effective value is
/// the environment override when one is set, otherwise the user's preference, otherwise off.
/// </summary>
public interface ICapabilitySettings : ICapabilityState
{
    /// <summary>Whether the capability is effectively on.</summary>
    /// <param name="flag">The capability.</param>
    /// <returns>The effective value.</returns>
    bool IsEnabled(UserCapability flag);

    /// <summary>
    /// Whether an administrator or test environment variable fixes the value, so the Settings
    /// control is shown disabled with an explanation.
    /// </summary>
    /// <param name="flag">The capability.</param>
    /// <returns>True when an environment override is active.</returns>
    bool IsManagedByEnvironment(UserCapability flag);

    /// <summary>The environment variable that overrides the capability.</summary>
    /// <param name="flag">The capability.</param>
    /// <returns>The variable name, for example <c>OGMA_ENABLE_3D_SHELF</c>.</returns>
    string EnvironmentVariableName(UserCapability flag) => flag switch
    {
        UserCapability.MetadataProviders => "OGMA_ENABLE_METADATA_PROVIDERS",
        UserCapability.ThreeDimensionalShelf => "OGMA_ENABLE_3D_SHELF",
        UserCapability.ClassroomHost => "OGMA_ENABLE_CLASSROOM_HOST",
        _ => throw new ArgumentOutOfRangeException(nameof(flag)),
    };

    /// <summary>Applies the persisted user preferences; raises <see cref="ICapabilityState.Changed"/> when an effective value changes.</summary>
    /// <param name="preferences">The current preferences.</param>
    void ApplyPreferences(UserPreferences preferences);
}
