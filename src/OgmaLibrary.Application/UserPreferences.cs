namespace OgmaLibrary.Application;

/// <summary>Supported application theme choices.</summary>
public enum UserTheme
{
    /// <summary>Use the light appearance.</summary>
    Light,
    /// <summary>Use the dark appearance.</summary>
    Dark,
    /// <summary>Follow the operating system appearance.</summary>
    System,
}

/// <summary>Supported application density choices.</summary>
public enum UserDensity
{
    /// <summary>Use the standard comfortable spacing and type scale.</summary>
    Comfortable,
    /// <summary>Use a reduced spacing and type scale.</summary>
    Compact,
}

/// <summary>Persisted appearance preferences for the desktop shell.</summary>
public sealed record UserPreferences(
    UserTheme Theme = UserTheme.Light,
    UserDensity Density = UserDensity.Comfortable);

/// <summary>Loads and saves non-sensitive desktop appearance preferences.</summary>
public interface IUserPreferencesService
{
    /// <summary>Returns validated preferences, falling back to safe defaults.</summary>
    Task<UserPreferences> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Atomically persists validated preferences.</summary>
    Task SaveAsync(UserPreferences preferences, CancellationToken cancellationToken = default);
}
