namespace OgmaLibrary.Application.ClassroomClient;

/// <summary>Stores Client-mode secrets such as Host session tokens.</summary>
public interface IClassroomCredentialStore
{
    /// <summary>Saves or replaces a secret value.</summary>
    Task SaveSecretAsync(string key, string value, CancellationToken cancellationToken = default);

    /// <summary>Gets a secret value, or <see langword="null" /> when absent.</summary>
    Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Deletes a secret value when it exists.</summary>
    Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default);
}
