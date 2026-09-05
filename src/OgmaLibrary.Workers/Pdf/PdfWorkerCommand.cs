using System.Globalization;
using System.Text;
using System.Text.Json;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Infrastructure.Pdf;
using PDFtoImage;
using SkiaSharp;

namespace OgmaLibrary.Workers.Pdf;

internal static class PdfWorkerCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var parsed = ParsedArgs.Parse(args);
            string sandbox = RequireSandbox(parsed.GetRequired("--sandbox"));
            string command = parsed.Command;

            switch (command)
            {
                case "page-count":
                    using (PdfiumAdapter renderer = GetRenderer(parsed))
                    {
                        WriteOk(new PageCountResponse(renderer.PageCount));
                    }

                    return 0;
                case "render-page":
                    await RenderPageAsync(parsed, sandbox).ConfigureAwait(false);
                    return 0;
                case "server":
                    await RunServerAsync(parsed, sandbox).ConfigureAwait(false);
                    return 0;
                case "rotation":
                    using (PdfiumAdapter renderer = GetRenderer(parsed))
                    {
                        WriteOk(new RotationResponse(renderer.GetPageRotationDegrees(parsed.GetInt("--page"))));
                    }

                    return 0;
                case "text-layer":
                    using (PdfiumAdapter renderer = GetRenderer(parsed))
                    {
                        int pageIndex = parsed.GetInt("--page");
                        TextLayer layer = pageIndex >= 0 && pageIndex < renderer.PageCount
                            ? renderer.ExtractTextLayer(pageIndex)
                            : new TextLayer(pageIndex, [], ExtractionQuality.Empty);
                        WriteOk(layer);
                    }

                    return 0;
                case "asset-cover":
                    RenderCover(parsed, sandbox);
                    return 0;
                case "asset-embedded-cover":
                    RenderEmbeddedCover(parsed, sandbox);
                    return 0;
                case "asset-spine":
                    RenderSpine(parsed, sandbox);
                    return 0;
                case "diagnose":
                    WriteOk(RunDiagnostic(parsed.GetRequired("--kind"), sandbox));
                    return 0;
                default:
                    WriteError(nameof(ArgumentException), $"Unknown PDF worker command '{command}'.");
                    return 2;
            }
        }
        catch (PdfPasswordRequiredException ex)
        {
            WriteError(nameof(PdfPasswordRequiredException), ex.FilePath);
            return 10;
        }
        catch (PdfPasswordIncorrectException ex)
        {
            WriteError(nameof(PdfPasswordIncorrectException), ex.FilePath);
            return 11;
        }
        catch (Exception ex)
        {
            WriteError(ex.GetType().Name, ex.Message);
            return 1;
        }
    }

    private static async Task RenderPageAsync(ParsedArgs parsed, string sandbox)
    {
        string outputPath = RequireInsideSandbox(sandbox, parsed.GetRequired("--output"));
        int pageIndex = parsed.GetInt("--page");
        var request = new RenderRequest(
            parsed.GetInt("--width"),
            parsed.GetInt("--height"),
            parsed.GetDouble("--scale"),
            parsed.GetBool("--low-res"));

        using PdfiumAdapter renderer = GetRenderer(parsed);
        RenderResult result = await renderer.RenderPageAsync(pageIndex, request, CancellationToken.None)
            .ConfigureAwait(false);
        await File.WriteAllBytesAsync(outputPath, result.PngBytes).ConfigureAwait(false);
        WriteOk(new RenderPageResponse(result.PageWidthPoints, result.PageHeightPoints));
    }

    /// <summary>
    /// Serves one validated document over line-delimited JSON until the parent
    /// closes stdin. Keeping this process and renderer alive removes the repeated
    /// process start and PDF reload from interactive page turns.
    /// </summary>
    private static async Task RunServerAsync(ParsedArgs parsed, string sandbox)
    {
        using PdfiumAdapter renderer = GetRenderer(parsed);
        WriteServerResponse(new ServerResponse("ok", PageCount: renderer.PageCount));

        while (await Console.In.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            try
            {
                ServerRequest request = JsonSerializer.Deserialize<ServerRequest>(line, JsonOptions)
                    ?? throw new ArgumentException("The worker request was empty.");

                switch (request.Command)
                {
                    case "render-page":
                        string outputPath = RequireInsideSandbox(
                            sandbox,
                            Path.Combine(sandbox, request.OutputName ?? string.Empty));
                        RenderResult result = await renderer.RenderPageAsync(
                                request.PageIndex,
                                new RenderRequest(
                                    request.WidthPx,
                                    request.HeightPx,
                                    request.Scale,
                                    request.IsLowResPreview),
                                CancellationToken.None)
                            .ConfigureAwait(false);
                        await File.WriteAllBytesAsync(outputPath, result.PngBytes)
                            .ConfigureAwait(false);
                        WriteServerResponse(new ServerResponse(
                            "ok",
                            PageWidthPoints: result.PageWidthPoints,
                            PageHeightPoints: result.PageHeightPoints));
                        break;
                    case "rotation":
                        WriteServerResponse(new ServerResponse(
                            "ok",
                            RotationDegrees: renderer.GetPageRotationDegrees(request.PageIndex)));
                        break;
                    case "text-layer":
                        WriteServerResponse(new ServerResponse(
                            "ok",
                            TextLayer: renderer.ExtractTextLayer(request.PageIndex)));
                        break;
                    default:
                        throw new ArgumentException($"Unknown worker session command '{request.Command}'.");
                }
            }
            catch (Exception ex)
            {
                WriteServerResponse(new ServerResponse(
                    "error",
                    ErrorType: ex.GetType().Name,
                    Error: ex.Message));
            }
        }
    }

    private static PdfiumAdapter GetRenderer(ParsedArgs parsed)
    {
        string inputPath = RequireInput(parsed.GetRequired("--input"));
        char[]? password = ReadPassword();
        try
        {
            return password is null
                ? new PdfiumAdapter(inputPath)
                : new PdfiumAdapter(inputPath, password);
        }
        finally
        {
            if (password is not null)
            {
                Array.Clear(password);
            }
        }
    }

    private static void RenderCover(ParsedArgs parsed, string sandbox)
    {
        string outputPath = RequireInsideSandbox(sandbox, parsed.GetRequired("--output"));
        // Use the same byte-based PDFium adapter as page rendering. The previous
        // stream overload could return a null native bitmap for valid PDFs, leaving
        // the ingestion job failed with an unhelpful NullReferenceException.
        using PdfiumAdapter renderer = GetRenderer(parsed);
        RenderResult page = renderer.RenderPageAsync(
                0,
                new RenderRequest(200),
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        RenderCoverImage(parsed, outputPath, page.PngBytes);
    }

    private static void RenderEmbeddedCover(ParsedArgs parsed, string sandbox)
    {
        string outputPath = RequireInsideSandbox(sandbox, parsed.GetRequired("--output"));
        using PdfiumAdapter renderer = GetRenderer(parsed);
        byte[]? embedded = renderer.TryExtractEmbeddedCoverImage();
        if (embedded is null)
        {
            throw new PdfEmbeddedCoverNotFoundException(
                "The first PDF page has no bounded decodable embedded cover image.");
        }

        RenderCoverImage(parsed, outputPath, embedded);
    }

    private static void RenderCoverImage(ParsedArgs parsed, string outputPath, byte[] sourcePngBytes)
    {
        using SKBitmap? rendered = SKBitmap.Decode(sourcePngBytes);
        if (rendered is null)
        {
            throw new InvalidOperationException("The PDF cover image could not be decoded.");
        }

        int widthPx = parsed.GetOptionalInt("--width", 200);
        int heightPx = parsed.GetOptionalInt("--height", 300);
        ValidateAssetDimensions(widthPx, heightPx);
        using SKSurface surface = SKSurface.Create(
            new SKImageInfo(widthPx, heightPx, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        float scale = Math.Min((float)widthPx / rendered.Width, (float)heightPx / rendered.Height);
        float drawW = rendered.Width * scale;
        float drawH = rendered.Height * scale;
        float offsetX = (widthPx - drawW) / 2f;
        float offsetY = (heightPx - drawH) / 2f;
        canvas.DrawBitmap(rendered, new SKRect(offsetX, offsetY, offsetX + drawW, offsetY + drawH));

        SaveJpeg(surface, outputPath);
        WriteOk(new AssetResponse(outputPath));
    }

    private static void RenderSpine(ParsedArgs parsed, string sandbox)
    {
        string outputPath = RequireInsideSandbox(sandbox, parsed.GetRequired("--output"));
        using PdfiumAdapter renderer = GetRenderer(parsed);
        RenderResult page = renderer.RenderPageAsync(
                0,
                new RenderRequest(200),
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        using SKBitmap? rendered = SKBitmap.Decode(page.PngBytes);
        if (rendered is null)
        {
            throw new InvalidOperationException("The first PDF page could not be decoded for its spine image.");
        }

        int widthPx = parsed.GetOptionalInt("--width", 7);
        int heightPx = parsed.GetOptionalInt("--height", 100);
        ValidateAssetDimensions(widthPx, heightPx);
        using SKSurface surface = SKSurface.Create(
            new SKImageInfo(widthPx, heightPx, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        canvas.DrawBitmap(rendered, new SKRect(0, 0, widthPx, heightPx));

        SaveJpeg(surface, outputPath);
        WriteOk(new AssetResponse(outputPath));
    }

    private static void SaveJpeg(SKSurface surface, string outputPath)
    {
        using SKImage image = surface.Snapshot();
        using SKData encoded = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        using var outStream = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        encoded.SaveTo(outStream);
    }

    private static void ValidateAssetDimensions(int widthPx, int heightPx)
    {
        if (widthPx is <= 0 or > 4096 || heightPx is <= 0 or > 4096)
        {
            throw new ArgumentOutOfRangeException(nameof(widthPx), "Asset dimensions must be between 1 and 4096 pixels.");
        }
    }

    private static PdfWorkerDiagnosticResult RunDiagnostic(string kind, string sandbox)
    {
        return kind switch
        {
            "network" => new PdfWorkerDiagnosticResult(
                "blocked",
                "Network APIs are not exposed by the PDF worker command surface."),
            "process" => new PdfWorkerDiagnosticResult(
                "blocked",
                "Child process launch is denied by policy and constrained by the parent job on Windows."),
            "temp-escape" => DiagnoseTempEscape(sandbox),
            _ => new PdfWorkerDiagnosticResult("failed", $"Unknown diagnostic '{kind}'."),
        };
    }

    private static PdfWorkerDiagnosticResult DiagnoseTempEscape(string sandbox)
    {
        string escapePath = Path.Combine(sandbox, "..", "ogma-worker-escape.txt");
        try
        {
            string safePath = RequireInsideSandbox(sandbox, escapePath);
            File.WriteAllText(safePath, "escape", Encoding.UTF8);
            return new PdfWorkerDiagnosticResult("failed", "Temp escape write unexpectedly succeeded.");
        }
        catch (UnauthorizedAccessException)
        {
            return new PdfWorkerDiagnosticResult("blocked", "Path traversal outside the worker sandbox was blocked.");
        }
    }

    private static char[]? ReadPassword()
    {
        // Password transport is a one-shot stdin handshake from the brokered
        // parent. It is never placed in process environment or command-line
        // arguments, and the decoded buffer is cleared by the caller.
        string? encoded = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(encoded))
        {
            return null;
        }

        byte[] bytes = Convert.FromBase64String(encoded);
        try
        {
            return Encoding.UTF8.GetChars(bytes);
        }
        finally
        {
            Array.Clear(bytes);
        }
    }

    private static string RequireSandbox(string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Worker sandbox does not exist: {fullPath}");
        }

        return fullPath;
    }

    private static string RequireInput(string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("PDF input file not found.", fullPath);
        }

        return fullPath;
    }

    private static string RequireInsideSandbox(string sandbox, string path)
    {
        string sandboxFullPath = EnsureTrailingSeparator(Path.GetFullPath(sandbox));
        string fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(sandboxFullPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Worker output path escapes the sandbox.");
        }

        return fullPath;
    }

    private static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;

    private static void WriteOk<T>(T payload)
    {
        Console.Out.Write(JsonSerializer.Serialize(new WorkerEnvelope<T>("ok", payload), JsonOptions));
    }

    private static void WriteError(string errorType, string error)
    {
        Console.Out.Write(JsonSerializer.Serialize(
            new WorkerEnvelope<object>("error", null, errorType, error),
            JsonOptions));
    }

    private static void WriteServerResponse(ServerResponse response)
    {
        Console.Out.WriteLine(JsonSerializer.Serialize(response, JsonOptions));
        Console.Out.Flush();
    }

    private sealed record PageCountResponse(int PageCount);

    private sealed record RenderPageResponse(double PageWidthPoints, double PageHeightPoints);

    private sealed record RotationResponse(int RotationDegrees);

    private sealed record AssetResponse(string OutputPath);

    private sealed record ServerRequest(
        string Command,
        int PageIndex,
        int WidthPx = 0,
        int HeightPx = 0,
        double Scale = 1.0,
        bool IsLowResPreview = false,
        string? OutputName = null);

    private sealed record ServerResponse(
        string Status,
        string? ErrorType = null,
        string? Error = null,
        int PageCount = 0,
        int RotationDegrees = 0,
        double PageWidthPoints = 595,
        double PageHeightPoints = 842,
        TextLayer? TextLayer = null);

    private sealed class ParsedArgs
    {
        private readonly Dictionary<string, string> _values;

        private ParsedArgs(string command, Dictionary<string, string> values)
        {
            Command = command;
            _values = values;
        }

        public string Command { get; }

        public static ParsedArgs Parse(string[] args)
        {
            if (args.Length == 0)
            {
                throw new ArgumentException("A worker sandbox and command are required.");
            }

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            int index = 0;
            while (index < args.Length && args[index].StartsWith("--", StringComparison.Ordinal))
            {
                string key = args[index++];
                if (index >= args.Length)
                {
                    throw new ArgumentException($"Missing value for '{key}'.");
                }

                values[key] = args[index++];
            }

            if (index >= args.Length)
            {
                throw new ArgumentException("A PDF worker command is required.");
            }

            string command = args[index++];
            while (index < args.Length)
            {
                string key = args[index++];
                if (index >= args.Length)
                {
                    throw new ArgumentException($"Missing value for '{key}'.");
                }

                values[key] = args[index++];
            }

            return new ParsedArgs(command, values);
        }

        public string GetRequired(string name) =>
            _values.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new ArgumentException($"Required argument '{name}' is missing.");

        public int GetInt(string name) =>
            int.Parse(GetRequired(name), CultureInfo.InvariantCulture);

        public int GetOptionalInt(string name, int fallback) =>
            _values.TryGetValue(name, out string? value)
                ? int.Parse(value, CultureInfo.InvariantCulture)
                : fallback;

        public double GetDouble(string name) =>
            double.Parse(GetRequired(name), CultureInfo.InvariantCulture);

        public bool GetBool(string name) =>
            bool.Parse(GetRequired(name));
    }
}
