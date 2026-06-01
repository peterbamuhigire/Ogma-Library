using System.Text.Json.Serialization;

namespace OgmaLibrary.Bookshelf3D.Messages;

/// <summary>Book metadata sent to the Three.js shelf scene.</summary>
public sealed record BookSceneItem(
    [property: JsonPropertyName("bookId")] string BookId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("author")] string Author,
    [property: JsonPropertyName("spineUri")] string SpineUri,
    [property: JsonPropertyName("coverUri")] string? CoverUri);
