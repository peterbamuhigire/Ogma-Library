using System.Text.Json;
using System.Text.Json.Serialization;

namespace OgmaLibrary.Bookshelf3D.Messages;

/// <summary>JSON helpers for the typed C# and JavaScript bridge.</summary>
public static class OutboundMessageJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Serializes a C# to JavaScript bridge message using its runtime type.</summary>
    public static string Serialize(OutboundMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return JsonSerializer.Serialize(message, message.GetType(), Options);
    }
}

/// <summary>Parser for raw JavaScript to C# bridge messages.</summary>
public static class InboundMessageParser
{
    /// <summary>Attempts to parse raw JSON into an inbound bridge message.</summary>
    public static bool TryParse(string json, out InboundMessage? message, out string? error)
    {
        message = null;
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "Inbound bridge message is empty.";
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.TryGetProperty("version", out JsonElement versionElement) &&
                !string.Equals(versionElement.GetString(), BridgeProtocol.CurrentVersion, StringComparison.Ordinal))
            {
                error = $"Unsupported bookshelf bridge version '{versionElement.GetString()}'.";
                return false;
            }

            if (!root.TryGetProperty("type", out JsonElement typeElement))
            {
                error = "Inbound bridge message is missing type.";
                return false;
            }

            string? type = typeElement.GetString();
            message = type switch
            {
                "BookClicked" => new BookClickedMessage(ReadRequiredString(root, "bookId")),
                "BookDoubleClicked" => new BookDoubleClickedMessage(ReadRequiredString(root, "bookId")),
                "BookHovered" => new BookHoveredMessage(ReadRequiredString(root, "bookId")),
                "CameraChanged" => new CameraChangedMessage(ReadCamera(root.GetProperty("camera"))),
                "WebGl2Status" => new WebGl2StatusMessage(root.GetProperty("supported").GetBoolean()),
                "PerformanceWarning" => new PerformanceWarningMessage(root.GetProperty("averageFps").GetDouble()),
                "PerformanceMetrics" => new PerformanceMetricsMessage(
                    root.GetProperty("averageFps").GetDouble(),
                    root.GetProperty("frameTimeMs").GetDouble(),
                    root.GetProperty("drawCalls").GetInt32(),
                    root.GetProperty("sceneBookCount").GetInt32(),
                    root.GetProperty("residentBookCount").GetInt32(),
                    root.GetProperty("reducedMotion").GetBoolean()),
                _ => new UnknownInboundMessage(type ?? string.Empty),
            };
            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
        catch (InvalidOperationException ex)
        {
            error = ex.Message;
            return false;
        }
        catch (KeyNotFoundException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string ReadRequiredString(JsonElement root, string propertyName)
    {
        string? value = root.GetProperty(propertyName).GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Inbound bridge message property '{propertyName}' is required.");
        }

        return value;
    }

    private static CameraState ReadCamera(JsonElement camera) =>
        new(
            camera.GetProperty("x").GetDouble(),
            camera.GetProperty("y").GetDouble(),
            camera.GetProperty("z").GetDouble(),
            camera.GetProperty("targetX").GetDouble(),
            camera.GetProperty("targetY").GetDouble(),
            camera.GetProperty("targetZ").GetDouble(),
            camera.GetProperty("fov").GetDouble());
}
