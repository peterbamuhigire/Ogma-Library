namespace OgmaLibrary.Infrastructure.Catalogue.Entities;

/// <summary>Singleton EF row storing LAN Host-mode settings.</summary>
public sealed class HostModeSettingsRow
{
    /// <summary>Stable singleton key.</summary>
    public string SettingsId { get; set; } = "default";

    /// <summary>Whether Host mode should be enabled by administrator action.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Configured LAN HTTPS listener port.</summary>
    public int Port { get; set; }

    /// <summary>Content delivery mode.</summary>
    public int ContentMode { get; set; }

    /// <summary>Human-readable Host display name advertised to clients.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Last updated timestamp.</summary>
    public DateTimeOffset UpdatedUtc { get; set; }
}

