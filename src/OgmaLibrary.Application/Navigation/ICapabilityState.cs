namespace OgmaLibrary.Application.Navigation;

/// <summary>
/// Which optional capabilities are available right now (Sept-23 Phase 07, T07.6, K17). Rail
/// items and toolbar actions bind to it, so a destination is shown only when it can work, or it
/// explains what is missing and routes to Settings.
/// </summary>
public interface ICapabilityState
{
    /// <summary>Whether an AI provider is configured (the Advisor can answer).</summary>
    bool IsAiConfigured { get; }

    /// <summary>Whether this installation may act as a classroom Host.</summary>
    bool IsClassroomHostEnabled { get; }

    /// <summary>Whether this device is connected to a classroom Host as a client.</summary>
    bool IsClassroomClientConnected { get; }

    /// <summary>Whether the 3D bookshelf may be offered on this device.</summary>
    bool IsShelf3DAvailable { get; }

    /// <summary>Whether online metadata providers are enabled.</summary>
    bool AreMetadataProvidersEnabled { get; }

    /// <summary>Whether any classroom surface is available (Host or connected client).</summary>
    bool IsClassroomAvailable => IsClassroomHostEnabled || IsClassroomClientConnected;

    /// <summary>Raised when any capability changes.</summary>
    event EventHandler? Changed;
}
