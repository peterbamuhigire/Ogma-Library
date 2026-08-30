using System.Text.Json.Serialization;

namespace OgmaLibrary.Bookshelf3D.Messages;

/// <summary>Base type for JavaScript to C# bridge messages.</summary>
public abstract record InboundMessage([property: JsonPropertyName("type")] string Type);

/// <summary>Raised when a book spine is clicked.</summary>
public sealed record BookClickedMessage(
    [property: JsonPropertyName("bookId")] string BookId) : InboundMessage("BookClicked");

/// <summary>Raised when a book spine is double-clicked.</summary>
public sealed record BookDoubleClickedMessage(
    [property: JsonPropertyName("bookId")] string BookId) : InboundMessage("BookDoubleClicked");

/// <summary>Raised when hover focus moves to a book spine.</summary>
public sealed record BookHoveredMessage(
    [property: JsonPropertyName("bookId")] string BookId) : InboundMessage("BookHovered");

/// <summary>Raised when the JavaScript scene camera changes materially.</summary>
public sealed record CameraChangedMessage(
    [property: JsonPropertyName("camera")] CameraState Camera) : InboundMessage("CameraChanged");

/// <summary>Raised when the scene reports whether WebGL2 is available.</summary>
public sealed record WebGl2StatusMessage(
    [property: JsonPropertyName("supported")] bool Supported) : InboundMessage("WebGl2Status");

/// <summary>Raised when the scene detects sustained low frame rate.</summary>
public sealed record PerformanceWarningMessage(
    [property: JsonPropertyName("averageFps")] double AverageFps) : InboundMessage("PerformanceWarning");

/// <summary>Reports bounded runtime metrics from the local Three.js renderer.</summary>
public sealed record PerformanceMetricsMessage(
    [property: JsonPropertyName("averageFps")] double AverageFps,
    [property: JsonPropertyName("frameTimeMs")] double FrameTimeMs,
    [property: JsonPropertyName("drawCalls")] int DrawCalls,
    [property: JsonPropertyName("sceneBookCount")] int SceneBookCount,
    [property: JsonPropertyName("residentBookCount")] int ResidentBookCount,
    [property: JsonPropertyName("reducedMotion")] bool ReducedMotion) : InboundMessage("PerformanceMetrics");

/// <summary>Represents an unrecognized inbound message type that must be discarded.</summary>
public sealed record UnknownInboundMessage(string UnknownType) : InboundMessage(UnknownType);
