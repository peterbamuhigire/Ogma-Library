using System.Text.Json.Serialization;

namespace OgmaLibrary.Bookshelf3D.Messages;

/// <summary>Base type for C# to JavaScript bridge messages.</summary>
public abstract record OutboundMessage([property: JsonPropertyName("type")] string Type);

/// <summary>Replaces the full Three.js scene.</summary>
public sealed record SetSceneMessage(
    [property: JsonPropertyName("books")] IReadOnlyList<BookSceneItem> Books,
    [property: JsonPropertyName("camera")] CameraState Camera) : OutboundMessage("SetScene");

/// <summary>Updates a single book already present in the scene.</summary>
public sealed record UpdateBookMessage(
    [property: JsonPropertyName("bookId")] string BookId,
    [property: JsonPropertyName("book")] BookSceneItem Book) : OutboundMessage("UpdateBook");

/// <summary>Removes one book from the scene.</summary>
public sealed record RemoveBookMessage(
    [property: JsonPropertyName("bookId")] string BookId) : OutboundMessage("RemoveBook");

/// <summary>Sets the Three.js camera state.</summary>
public sealed record SetCameraMessage(
    [property: JsonPropertyName("camera")] CameraState Camera) : OutboundMessage("SetCamera");

/// <summary>Synchronizes the app theme with the Three.js scene.</summary>
public sealed record SetThemeMessage(
    [property: JsonPropertyName("themeKey")] string ThemeKey) : OutboundMessage("SetTheme");

/// <summary>Switches between supported Three.js shelf layouts.</summary>
public sealed record SetLayoutMessage(
    [property: JsonPropertyName("layout")] string Layout) : OutboundMessage("SetLayout");
