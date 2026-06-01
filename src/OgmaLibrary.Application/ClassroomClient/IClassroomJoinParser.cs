namespace OgmaLibrary.Application.ClassroomClient;

/// <summary>Parses QR/manual Host join payloads into the Client-mode TOFU contract.</summary>
public interface IClassroomJoinParser
{
    /// <summary>Parses and validates a Host join URI.</summary>
    ClassroomJoinRequest Parse(string payload);

    /// <summary>Attempts to parse and validate a Host join URI.</summary>
    bool TryParse(string payload, out ClassroomJoinRequest? request, out string? errorMessage);
}
