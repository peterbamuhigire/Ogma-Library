using System.Text.Json.Serialization;

namespace OgmaLibrary.Bookshelf3D.Messages;

/// <summary>Serializable camera state shared between C# and the Three.js scene.</summary>
public sealed record CameraState(
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y,
    [property: JsonPropertyName("z")] double Z,
    [property: JsonPropertyName("targetX")] double TargetX,
    [property: JsonPropertyName("targetY")] double TargetY,
    [property: JsonPropertyName("targetZ")] double TargetZ,
    [property: JsonPropertyName("fov")] double Fov);
