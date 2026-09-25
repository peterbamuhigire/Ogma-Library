using OgmaLibrary.Application.Navigation;

namespace OgmaLibrary.App.Navigation;

/// <summary>
/// The desktop <see cref="ICapabilityState"/> (Sept-23 Phase 07, T07.6). Installation-level
/// switches come from the runtime options; the AI state is read live; the classroom client
/// state is pushed by the shell when connectivity changes.
/// </summary>
public sealed class RuntimeCapabilityState : ICapabilityState
{
    private readonly Func<bool> _isAiConfigured;
    private bool _isClassroomClientConnected;
    private bool _lastAiConfigured;

    /// <summary>Initializes a new instance of the <see cref="RuntimeCapabilityState"/> class.</summary>
    /// <param name="classroomHostEnabled">Whether the classroom Host may be offered.</param>
    /// <param name="shelf3DAvailable">Whether the 3D shelf may be offered.</param>
    /// <param name="metadataProvidersEnabled">Whether online metadata providers are enabled.</param>
    /// <param name="isAiConfigured">Reads whether an AI provider is configured.</param>
    public RuntimeCapabilityState(
        bool classroomHostEnabled = false,
        bool shelf3DAvailable = false,
        bool metadataProvidersEnabled = false,
        Func<bool>? isAiConfigured = null)
    {
        IsClassroomHostEnabled = classroomHostEnabled;
        IsShelf3DAvailable = shelf3DAvailable;
        AreMetadataProvidersEnabled = metadataProvidersEnabled;
        _isAiConfigured = isAiConfigured ?? (() => false);
        _lastAiConfigured = ReadAi();
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public bool IsAiConfigured => _lastAiConfigured;

    /// <inheritdoc />
    public bool IsClassroomHostEnabled { get; }

    /// <inheritdoc />
    public bool IsClassroomClientConnected => _isClassroomClientConnected;

    /// <inheritdoc />
    public bool IsShelf3DAvailable { get; }

    /// <inheritdoc />
    public bool AreMetadataProvidersEnabled { get; }

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
