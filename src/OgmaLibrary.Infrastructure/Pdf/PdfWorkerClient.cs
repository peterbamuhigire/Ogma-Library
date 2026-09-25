using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using OgmaLibrary.Application.Metadata;
using OgmaLibrary.Application.Reader;
using OgmaLibrary.Infrastructure.Pathing;
using SkiaSharp;

namespace OgmaLibrary.Infrastructure.Pdf;

/// <summary>
/// Launches the external PDF worker process and exchanges sandboxed render results
/// with the main application process.
/// </summary>
public sealed class PdfWorkerClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly PdfWorkerOptions _options;
    private long _maxPeakWorkingSetBytes;
    private long _maxPrivateMemoryBytes;

    /// <summary>
    /// Initializes a new instance of <see cref="PdfWorkerClient"/>.
    /// </summary>
    /// <param name="options">Optional worker launch settings.</param>
    public PdfWorkerClient(PdfWorkerOptions? options = null)
    {
        _options = options ?? new PdfWorkerOptions();
        if (_options.Timeout <= TimeSpan.Zero ||
            _options.CpuTimeLimit <= TimeSpan.Zero ||
            _options.MaxMemoryBytes <= 0 ||
            _options.MaxOutputBytes <= 0 ||
            _options.Session is null ||
            _options.Session.RequestTimeout <= TimeSpan.Zero ||
            _options.Session.StartupTimeout <= TimeSpan.Zero ||
            _options.Session.IdleTimeout <= TimeSpan.Zero ||
            _options.Session.MaxRespawns < 0 ||
            _options.Session.RespawnWindow <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Worker resource limits must be positive.");
        }
    }

    /// <summary>
    /// Checks whether the configured worker command can be resolved without
    /// launching it or opening a PDF.
    /// </summary>
    /// <returns>A redacted availability result that never includes a filesystem path.</returns>
    public PdfWorkerAvailability GetAvailability()
    {
        try
        {
            WorkerCommand command = ResolveWorkerCommand();
            bool exists = string.Equals(command.FileName, "dotnet", StringComparison.Ordinal) &&
                          command.PrefixArguments.Count > 0
                ? File.Exists(command.PrefixArguments[0])
                : File.Exists(command.FileName);
            return exists
                ? new PdfWorkerAvailability(true, "ready")
                : new PdfWorkerAvailability(false, "worker_file_unavailable");
        }
        catch (FileNotFoundException)
        {
            return new PdfWorkerAvailability(false, "worker_file_unavailable");
        }
        catch (UnauthorizedAccessException)
        {
            return new PdfWorkerAvailability(false, "worker_access_denied");
        }
        catch (ArgumentException)
        {
            return new PdfWorkerAvailability(false, "worker_path_unusable");
        }
    }

    /// <summary>
    /// Gets the page count for a PDF by invoking the worker process.
    /// </summary>
    /// <param name="filePath">The absolute PDF path.</param>
    /// <param name="password">Optional password characters.</param>
    /// <returns>The detected page count, or zero for malformed PDFs.</returns>
    public int GetPageCount(string filePath, char[]? password = null)
    {
        WorkerEnvelope<PageCountResponse> envelope = RunJson<PageCountResponse>(
            ["page-count", "--input", RequireAbsoluteFile(filePath)],
            password);
        return envelope.Payload?.PageCount ?? 0;
    }

    /// <summary>Opens a worker-backed document session for repeated reader operations.</summary>
    /// <param name="filePath">The absolute PDF path.</param>
    /// <param name="password">Optional password characters copied for the session lifetime.</param>
    /// <returns>A disposable session that keeps the document identity and password bounded.</returns>
    public PdfWorkerSession OpenSession(string filePath, char[]? password = null) =>
        new(this, RequireAbsoluteFile(filePath), password);

    /// <summary>Gets the resource policy applied to persistent reader sessions.</summary>
    internal PdfWorkerSessionLimits SessionLimits => _options.Session;

    /// <summary>
    /// Gets the largest worker peak working-set observation recorded by this
    /// client. Sessions record the value before they are disposed.
    /// </summary>
    public long MaxPeakWorkingSetBytes => Interlocked.Read(ref _maxPeakWorkingSetBytes);

    /// <summary>
    /// Gets the largest worker private-memory observation recorded by this
    /// client. Sessions record the value before they are disposed.
    /// </summary>
    public long MaxPrivateMemoryBytes => Interlocked.Read(ref _maxPrivateMemoryBytes);

    /// <summary>
    /// Renders a single PDF page by invoking the worker process.
    /// </summary>
    /// <param name="filePath">The absolute PDF path.</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <param name="request">The render request.</param>
    /// <param name="password">Optional password characters.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The rendered page result.</returns>
    public async Task<RenderResult> RenderPageAsync(
        string filePath,
        int pageIndex,
        RenderRequest request,
        char[]? password,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentNullException.ThrowIfNull(request);

        using PdfWorkerSandbox sandbox = CreateSandbox();
        string outputPath = Path.Combine(sandbox.Path, "page.png");
        WorkerEnvelope<RenderPageResponse> envelope = await RunJsonAsync<RenderPageResponse>(
                [
                    "render-page",
                    "--input",
                    RequireAbsoluteFile(filePath),
                    "--page",
                    pageIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "--width",
                    request.WidthPx.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "--height",
                    request.HeightPx.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "--scale",
                    request.Scale.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "--low-res",
                    request.IsLowResPreview ? "true" : "false",
                    "--page-box",
                    request.PageBox.ToString(),
                    "--annotation-mode",
                    request.AnnotationMode.ToString(),
                    "--include-form-values",
                    request.IncludeFormValues ? "true" : "false",
                    "--optional-content",
                    request.OptionalContentMode.ToString(),
                    "--rotation",
                    request.RotationDegrees?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "pdf",
                    "--output",
                    outputPath,
                ],
                password,
                sandbox,
                cancellationToken)
            .ConfigureAwait(false);

        (byte[] pngBytes, _) = await ReadVerifiedOutputAsync(
            outputPath,
            sandbox.Path,
            _options.MaxOutputBytes,
            cancellationToken).ConfigureAwait(false);
        RenderPageResponse payload = envelope.Payload ?? new RenderPageResponse(595, 842);
        return new RenderResult(pngBytes, payload.PageWidthPoints, payload.PageHeightPoints, pageIndex);
    }

    /// <summary>
    /// Gets the normalized PDF rotation for a page by invoking the worker process.
    /// </summary>
    /// <param name="filePath">The absolute PDF path.</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <param name="password">Optional password characters.</param>
    /// <returns>The clockwise page rotation in degrees.</returns>
    public int GetPageRotationDegrees(string filePath, int pageIndex, char[]? password = null)
    {
        WorkerEnvelope<RotationResponse> envelope = RunJson<RotationResponse>(
            [
                "rotation",
                "--input",
                RequireAbsoluteFile(filePath),
                "--page",
                pageIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ],
            password);
        return envelope.Payload?.RotationDegrees ?? 0;
    }

    /// <summary>
    /// Extracts a PDF text layer by invoking the worker process.
    /// </summary>
    /// <param name="filePath">The absolute PDF path.</param>
    /// <param name="pageIndex">The zero-based page index.</param>
    /// <param name="password">Optional password characters.</param>
    /// <returns>The extracted text layer, or an empty layer when extraction fails.</returns>
    public TextLayer ExtractTextLayer(string filePath, int pageIndex, char[]? password = null)
    {
        WorkerEnvelope<TextLayer> envelope = RunJson<TextLayer>(
            [
                "text-layer",
                "--input",
                RequireAbsoluteFile(filePath),
                "--page",
                pageIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ],
            password);
        return envelope.Payload ?? new TextLayer(pageIndex, [], ExtractionQuality.Empty);
    }

    /// <summary>
    /// Applies accepted document-information fields inside the isolated worker and
    /// copies the verified worker artifact to the caller-provided temporary path.
    /// </summary>
    /// <param name="filePath">The absolute source PDF path.</param>
    /// <param name="outputPath">The absolute temporary output path.</param>
    /// <param name="proposals">The user-accepted metadata fields.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public async Task WriteMetadataAsync(
        string filePath,
        string outputPath,
        IReadOnlyList<AcceptedFieldProposal> proposals,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(proposals);

        string fullOutputPath = Path.GetFullPath(outputPath);
        string? outputDirectory = Path.GetDirectoryName(fullOutputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new ArgumentException("The output path must include a directory.", nameof(outputPath));
        }

        byte[] proposalBytes = JsonSerializer.SerializeToUtf8Bytes(proposals, JsonOptions);
        if (proposalBytes.Length > 16 * 1024)
        {
            throw new ArgumentException("The metadata proposal payload is too large.", nameof(proposals));
        }

        string proposalBase64 = Convert.ToBase64String(proposalBytes);
        using PdfWorkerSandbox sandbox = CreateSandbox();
        string workerOutputPath = Path.Combine(sandbox.Path, "metadata.pdf");
        await RunJsonAsync<AssetResponse>(
                [
                    "write-metadata",
                    "--input",
                    RequireAbsoluteFile(filePath),
                    "--output",
                    workerOutputPath,
                    "--proposals",
                    proposalBase64,
                ],
                password: null,
                sandbox,
                cancellationToken)
            .ConfigureAwait(false);

        (byte[] bytes, _) = await ReadVerifiedOutputAsync(
                workerOutputPath,
                sandbox.Path,
                _options.MaxOutputBytes,
                cancellationToken)
            .ConfigureAwait(false);

        FileStream output = new(
            fullOutputPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using (output.ConfigureAwait(false))
        {
            await output.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            output.Flush(flushToDisk: true);
        }
    }

    /// <summary>
    /// Generates a cover asset in the worker sandbox and copies the completed file
    /// to the requested output path after the worker exits successfully.
    /// </summary>
    /// <param name="filePath">The absolute PDF path.</param>
    /// <param name="outputPath">The final sidecar output path.</param>
    public void GenerateCover(string filePath, string outputPath) =>
        GenerateCover(filePath, outputPath, 200, 300);

    /// <summary>Generates a cover at an explicitly bounded pixel size.</summary>
    public void GenerateCover(string filePath, string outputPath, int widthPx, int heightPx) =>
        GenerateAsset("cover", filePath, outputPath, widthPx, heightPx);

    /// <summary>
    /// Generates a cover from a bounded decodable image embedded on the first
    /// PDF page. Callers should fall back to <see cref="GenerateCover(string,
    /// string, int, int)"/> when no embedded image is available.
    /// </summary>
    public void GenerateEmbeddedCover(string filePath, string outputPath, int widthPx, int heightPx) =>
        GenerateAsset("embedded-cover", filePath, outputPath, widthPx, heightPx);

    /// <summary>
    /// Generates a spine asset in the worker sandbox and copies the completed file
    /// to the requested output path after the worker exits successfully.
    /// </summary>
    /// <param name="filePath">The absolute PDF path.</param>
    /// <param name="outputPath">The final sidecar output path.</param>
    public void GenerateSpine(string filePath, string outputPath) =>
        GenerateSpine(filePath, outputPath, 7, 100);

    /// <summary>Generates a spine at an explicitly bounded pixel size.</summary>
    public void GenerateSpine(string filePath, string outputPath, int widthPx, int heightPx) =>
        GenerateAsset("spine", filePath, outputPath, widthPx, heightPx);

    /// <summary>
    /// Runs a worker diagnostic used by security tests.
    /// </summary>
    /// <param name="diagnosticName">The diagnostic name.</param>
    /// <returns>The diagnostic result.</returns>
    public PdfWorkerDiagnosticResult RunDiagnostic(string diagnosticName)
    {
        WorkerEnvelope<PdfWorkerDiagnosticResult> envelope = RunJson<PdfWorkerDiagnosticResult>(
            ["diagnose", "--kind", diagnosticName],
            password: null);
        return envelope.Payload ?? new PdfWorkerDiagnosticResult("failed", "Worker returned no diagnostic payload.");
    }

    private void GenerateAsset(string command, string filePath, string outputPath, int widthPx, int heightPx)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        if (widthPx is <= 0 or > 4096 || heightPx is <= 0 or > 4096)
        {
            throw new ArgumentOutOfRangeException(nameof(widthPx), "Asset dimensions must be between 1 and 4096 pixels.");
        }

        string fullOutputPath = Path.GetFullPath(outputPath);
        string? directory = Path.GetDirectoryName(fullOutputPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("The output path must include a directory.", nameof(outputPath));
        }

        Directory.CreateDirectory(directory);

        using PdfWorkerSandbox sandbox = CreateSandbox();
        string workerOutputPath = Path.Combine(sandbox.Path, $"{command}.jpg");
        RunJson<AssetResponse>(
            [
                $"asset-{command}",
                "--input",
                RequireAbsoluteFile(filePath),
                "--output",
                workerOutputPath,
                "--width",
                widthPx.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "--height",
                heightPx.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ],
            password: null,
            sandbox);
        _ = VerifyOutput(
            workerOutputPath,
            sandbox.Path,
            _options.MaxOutputBytes,
            widthPx,
            heightPx);
        File.Copy(workerOutputPath, fullOutputPath, overwrite: true);
    }

    private WorkerEnvelope<T> RunJson<T>(IReadOnlyList<string> args, char[]? password, PdfWorkerSandbox? sandbox = null)
    {
        using var cts = new CancellationTokenSource(_options.Timeout);
        return RunJsonAsync<T>(args, password, sandbox, cts.Token).GetAwaiter().GetResult();
    }

    private async Task<WorkerEnvelope<T>> RunJsonAsync<T>(
        IReadOnlyList<string> args,
        char[]? password,
        PdfWorkerSandbox? sandbox,
        CancellationToken cancellationToken)
    {
        bool ownsSandbox = sandbox is null;
        sandbox ??= CreateSandbox();
        try
        {
            WorkerCommand command = ResolveWorkerCommand();
            List<string> workerArguments = PrepareWorkerArguments(args, sandbox.Path);
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo(command.FileName)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = sandbox.Path,
            };

            foreach (string prefixArg in command.PrefixArguments)
            {
                process.StartInfo.ArgumentList.Add(prefixArg);
            }

            process.StartInfo.ArgumentList.Add("pdf-worker");
            process.StartInfo.ArgumentList.Add("--sandbox");
            process.StartInfo.ArgumentList.Add(sandbox.Path);
            foreach (string arg in workerArguments)
            {
                process.StartInfo.ArgumentList.Add(arg);
            }

            SetSandboxEnvironment(process.StartInfo, sandbox.Path);

            process.Start();
            await SendPasswordAsync(process.StandardInput, password, closeAfterWrite: true)
                .ConfigureAwait(false);
            using WindowsChildProcessLimit? childProcessLimit = RequireWindowsProcessLimit(process, _options.CpuTimeLimit);
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            Task<string> stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(_options.Timeout);
                await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                KillProcessTree(process);
                throw new TimeoutException("The PDF worker process exceeded its execution timeout.");
            }

            string stdout = await stdoutTask.ConfigureAwait(false);
            string stderr = await stderrTask.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                ThrowWorkerFailure(stdout, stderr);
            }

            WorkerEnvelope<T>? envelope = JsonSerializer.Deserialize<WorkerEnvelope<T>>(stdout, JsonOptions);
            if (envelope is null)
            {
                throw new InvalidOperationException("The PDF worker returned an empty or invalid response.");
            }

            if (!string.Equals(envelope.Status, "ok", StringComparison.OrdinalIgnoreCase))
            {
                ThrowWorkerFailure(envelope);
            }

            return envelope;
        }
        finally
        {
            if (ownsSandbox)
            {
                sandbox.Dispose();
            }
        }
    }

    private WindowsChildProcessLimit? RequireWindowsProcessLimit(Process process, TimeSpan? cpuTimeLimit)
    {
        WindowsChildProcessLimit? limit = WindowsChildProcessLimit.TryAssign(
            process,
            _options.MaxMemoryBytes,
            cpuTimeLimit);
        if (OperatingSystem.IsWindows() && limit is null)
        {
            KillProcessTree(process);
            throw new InvalidOperationException(
                "The PDF worker could not be assigned a Windows resource-limiting Job Object.");
        }

        return limit;
    }

    private static void SetSandboxEnvironment(ProcessStartInfo startInfo, string sandboxPath)
    {
        startInfo.Environment["TMP"] = sandboxPath;
        startInfo.Environment["TEMP"] = sandboxPath;
        startInfo.Environment["TMPDIR"] = sandboxPath;
        startInfo.Environment["OGMA_PDF_WORKER_NETWORK"] = "disabled";
        startInfo.Environment["OGMA_PDF_WORKER_CHILD_PROCESSES"] = "disabled";
    }

    private static List<string> PrepareWorkerArguments(
        IReadOnlyList<string> args,
        string sandboxPath)
    {
        var prepared = args.ToList();
        for (int index = 0; index < prepared.Count - 1; index++)
        {
            if (!string.Equals(prepared[index], "--input", StringComparison.Ordinal))
            {
                continue;
            }

            string inputPath = RequireAbsoluteFile(prepared[index + 1]);
            prepared[index + 1] = CopyInputToSandbox(inputPath, sandboxPath);
            break;
        }

        return prepared;
    }

    private static string CopyInputToSandbox(string inputPath, string sandboxPath)
    {
        FileInfo before = new(inputPath);
        string beforeHash = ComputeFileHash(inputPath);
        string destination = Path.Combine(sandboxPath, "input.pdf");
        File.Copy(inputPath, destination, overwrite: false);

        FileInfo after = new(inputPath);
        string afterHash = ComputeFileHash(inputPath);
        if (before.Length != after.Length ||
            before.LastWriteTimeUtc != after.LastWriteTimeUtc ||
            !string.Equals(beforeHash, afterHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("The PDF source changed while it was being copied into the worker sandbox.");
        }

        string copiedHash = ComputeFileHash(destination);
        if (!string.Equals(afterHash, copiedHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("The worker sandbox copy did not match the PDF source fingerprint.");
        }

        return destination;
    }

    private static string ComputeFileHash(string path)
    {
        using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            options: FileOptions.SequentialScan);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static async Task<(byte[] Bytes, PdfWorkerOutputManifest Manifest)> ReadVerifiedOutputAsync(
        string outputPath,
        string sandboxPath,
        long maxOutputBytes,
        CancellationToken cancellationToken)
    {
        string boundedPath = VerifyOutput(outputPath, sandboxPath, maxOutputBytes);
        byte[] bytes = await File.ReadAllBytesAsync(boundedPath, cancellationToken).ConfigureAwait(false);
        return (
            bytes,
            new PdfWorkerOutputManifest(
                Path.GetFileName(boundedPath),
                bytes.LongLength,
                Convert.ToHexStringLower(SHA256.HashData(bytes))));
    }

    private static string VerifyOutput(
        string outputPath,
        string sandboxPath,
        long maxOutputBytes,
        int? expectedWidth = null,
        int? expectedHeight = null)
    {
        string boundedPath = PathGuard.EnsureWithinRoot(outputPath, sandboxPath);
        if (!File.Exists(boundedPath))
        {
            throw new InvalidOperationException("The PDF worker did not produce the expected output.");
        }

        long length = new FileInfo(boundedPath).Length;
        if (length <= 0 || length > maxOutputBytes)
        {
            throw new InvalidOperationException("The PDF worker output exceeded its bounded manifest policy.");
        }

        if (expectedWidth is not null || expectedHeight is not null)
        {
            using SKBitmap? bitmap = SKBitmap.Decode(boundedPath);
            if (bitmap is null ||
                (expectedWidth is not null && bitmap.Width != expectedWidth) ||
                (expectedHeight is not null && bitmap.Height != expectedHeight))
            {
                throw new InvalidOperationException("The PDF worker produced an invalid or unexpected asset image.");
            }
        }

        return boundedPath;
    }

    private static async Task SendPasswordAsync(
        StreamWriter writer,
        char[]? password,
        bool closeAfterWrite)
    {
        string encoded;
        if (password is null)
        {
            encoded = string.Empty;
        }
        else
        {
            byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(password);
            try
            {
                encoded = Convert.ToBase64String(passwordBytes);
            }
            finally
            {
                Array.Clear(passwordBytes);
            }
        }
        await writer.WriteLineAsync(encoded).ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);

        if (closeAfterWrite)
        {
            writer.Close();
        }
    }

    private static void ThrowWorkerFailure(string stdout, string stderr)
    {
        try
        {
            WorkerEnvelope<JsonElement>? error = JsonSerializer.Deserialize<WorkerEnvelope<JsonElement>>(stdout, JsonOptions);
            if (error is not null)
            {
                ThrowWorkerFailure(error);
            }
        }
        catch (JsonException)
        {
            // Fall through to the generic stderr message.
        }

        throw new InvalidOperationException("The PDF worker process failed.");
    }

    private static void ThrowWorkerFailure<T>(WorkerEnvelope<T> envelope)
    {
        string message = string.IsNullOrWhiteSpace(envelope.Error)
            ? "The PDF worker process failed."
            : envelope.Error;
        throw envelope.ErrorType switch
        {
            nameof(PdfPasswordRequiredException) => new PdfPasswordRequiredException(message),
            nameof(PdfPasswordIncorrectException) => new PdfPasswordIncorrectException(message),
            nameof(PdfEmbeddedCoverNotFoundException) => new PdfEmbeddedCoverNotFoundException(message),
            _ => new InvalidOperationException(message),
        };
    }

    private PdfWorkerSandbox CreateSandbox()
    {
        string root = string.IsNullOrWhiteSpace(_options.SandboxRoot)
            ? Path.Combine(Path.GetTempPath(), "OgmaLibraryPdfWorker")
            : _options.SandboxRoot;
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return new PdfWorkerSandbox(path);
    }

    private WorkerCommand ResolveWorkerCommand()
    {
        string? configuredPath = _options.WorkerPath;
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return ToWorkerCommand(configuredPath);
        }

        string? envPath = Environment.GetEnvironmentVariable("OGMA_PDF_WORKER_PATH");
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            return ToWorkerCommand(envPath);
        }

        string baseDirectory = AppContext.BaseDirectory;
        foreach (string candidate in GetWorkerPathCandidates(baseDirectory))
        {
            if (File.Exists(candidate))
            {
                return ToWorkerCommand(candidate);
            }
        }

        throw new FileNotFoundException("Could not locate the OgmaLibrary.Workers PDF worker executable.");
    }

    private static IEnumerable<string> GetWorkerPathCandidates(string baseDirectory)
    {
        if (OperatingSystem.IsWindows())
        {
            yield return Path.Combine(baseDirectory, "OgmaLibrary.Workers.exe");
        }

        yield return Path.Combine(baseDirectory, "OgmaLibrary.Workers");
        yield return Path.Combine(baseDirectory, "OgmaLibrary.Workers.dll");
        yield return Path.GetFullPath(Path.Combine(
            baseDirectory,
            "..",
            "..",
            "..",
            "..",
            "src",
            "OgmaLibrary.Workers",
            "bin",
            "Release",
            "net10.0",
            OperatingSystem.IsWindows() ? "OgmaLibrary.Workers.exe" : "OgmaLibrary.Workers"));
        yield return Path.GetFullPath(Path.Combine(
            baseDirectory,
            "..",
            "..",
            "..",
            "..",
            "src",
            "OgmaLibrary.Workers",
            "bin",
            "Release",
            "net10.0",
            "OgmaLibrary.Workers.dll"));
    }

    private static WorkerCommand ToWorkerCommand(string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (string.Equals(Path.GetExtension(fullPath), ".dll", StringComparison.OrdinalIgnoreCase))
        {
            return new WorkerCommand("dotnet", [fullPath]);
        }

        return new WorkerCommand(fullPath, []);
    }

    private static string RequireAbsoluteFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        string fullPath = Path.GetFullPath(filePath);
        if (!Path.IsPathFullyQualified(fullPath))
        {
            throw new ArgumentException("The PDF path must be absolute.", nameof(filePath));
        }

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The PDF file does not exist.", fullPath);
        }

        return fullPath;
    }

    private static void KillProcessTree(Process process, int waitMilliseconds = ProcessExitWaitMilliseconds)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            // Kill is asynchronous. Wait (bounded) so the worker's handles on sandbox
            // files are released before the caller deletes the sandbox.
            process.WaitForExit(waitMilliseconds);
        }
        catch (InvalidOperationException)
        {
            // Process already exited or was never started.
        }
    }

    private const int ProcessExitWaitMilliseconds = 5_000;

    private sealed record WorkerCommand(string FileName, IReadOnlyList<string> PrefixArguments);

    private sealed record PageCountResponse(int PageCount);

    private sealed record RenderPageResponse(double PageWidthPoints, double PageHeightPoints);

    private sealed record RotationResponse(int RotationDegrees);

    private sealed record AssetResponse(string OutputPath);

    private void RecordResourceUsage(long peakWorkingSetBytes, long privateMemoryBytes)
    {
        UpdateMaximum(ref _maxPeakWorkingSetBytes, peakWorkingSetBytes);
        UpdateMaximum(ref _maxPrivateMemoryBytes, privateMemoryBytes);
    }

    private static void UpdateMaximum(ref long target, long value)
    {
        long current = Interlocked.Read(ref target);
        while (value > current)
        {
            long observed = Interlocked.CompareExchange(ref target, value, current);
            if (observed == current)
            {
                return;
            }

            current = observed;
        }
    }

    /// <summary>
    /// Persistent worker-backed operations for one validated PDF document. Requests are
    /// correlated by id, scheduled by priority and bounded by a per-request wall clock;
    /// the session never blocks a caller's thread on worker IPC.
    /// </summary>
    public sealed class PdfWorkerSession : IDisposable
    {
        /// <summary>Protocol version echoed by the worker's ready line.</summary>
        internal const int ProtocolVersion = 2;

        private const int WorkerExitWaitMilliseconds = 2_000;
        private const int MaxRetainedDiagnosticLines = 16;
        private const int MaxDiagnosticLineLength = 256;

        private readonly PdfWorkerClient _client;
        private readonly PdfWorkerSessionLimits _limits;
        private readonly PdfWorkerSandbox _sandbox;
        private readonly Process _process;
        private readonly WindowsChildProcessLimit? _childProcessLimit;
        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;
        private readonly PriorityRequestGate _requestGate = new();
        private readonly WorkerResponseRouter<ServerResponse> _router = new();
        private readonly Queue<string> _diagnosticLines = new();
        private readonly Lock _diagnosticSync = new();
        private readonly char[]? _password;
        private long _stderrLineCount;
        private long _lastActivityTicks;
        private int _activeRequests;
        private string? _faultReason;
        private int _disposed;

        internal PdfWorkerSession(PdfWorkerClient client, string filePath, char[]? password)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _limits = client._options.Session;
            _password = password?.ToArray();
            _sandbox = client.CreateSandbox();
            Touch();

            try
            {
                WorkerCommand command = client.ResolveWorkerCommand();
                var startInfo = new ProcessStartInfo(command.FileName)
                {
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = _sandbox.Path,
                };

                foreach (string prefixArg in command.PrefixArguments)
                {
                    startInfo.ArgumentList.Add(prefixArg);
                }

                string sandboxInput = CopyInputToSandbox(filePath, _sandbox.Path);
                startInfo.ArgumentList.Add("pdf-worker");
                startInfo.ArgumentList.Add("--sandbox");
                startInfo.ArgumentList.Add(_sandbox.Path);
                startInfo.ArgumentList.Add("server");
                startInfo.ArgumentList.Add("--input");
                startInfo.ArgumentList.Add(sandboxInput);
                SetSandboxEnvironment(startInfo, _sandbox.Path);

                _process = new Process { StartInfo = startInfo };
                _process.Start();

                // Interactive session policy: memory, active-process and kill-on-close
                // containment, but no cumulative CPU cap (K30).
                _childProcessLimit = _client.RequireWindowsProcessLimit(_process, cpuTimeLimit: null);
                _reader = _process.StandardOutput;
                _writer = _process.StandardInput;

                // The worker's stderr was never read before; a chatty worker could fill
                // the pipe and stall. Drain it for bounded diagnostics.
                _ = Task.Run(DrainStandardErrorAsync);

                SendPasswordAsync(_writer, _password, closeAfterWrite: false)
                    .WaitAsync(_limits.StartupTimeout)
                    .GetAwaiter()
                    .GetResult();

                ServerResponse ready = ReadReadyAsync()
                    .WaitAsync(_limits.StartupTimeout)
                    .GetAwaiter()
                    .GetResult();
                ThrowIfError(ready);
                PageCount = ready.PageCount;
                _ = Task.Run(ReadLoopAsync);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>The page count reported by the persistent isolated worker.</summary>
        public int PageCount { get; }

        /// <summary>Gets the worker process identifier for bounded diagnostics and tests.</summary>
        internal int ProcessId => _process.Id;

        /// <summary>Gets whether the session can no longer serve requests.</summary>
        public bool IsFaulted => Volatile.Read(ref _faultReason) is not null || Volatile.Read(ref _disposed) != 0;

        /// <summary>Gets the stable reason the session faulted, if it has.</summary>
        public string? FaultReason => Volatile.Read(ref _faultReason);

        /// <summary>Gets the UTC time of the last request activity.</summary>
        internal DateTime LastActivityUtc => new(Interlocked.Read(ref _lastActivityTicks), DateTimeKind.Utc);

        /// <summary>Gets whether a request is queued or in flight.</summary>
        internal bool IsBusy => Volatile.Read(ref _activeRequests) > 0;

        /// <summary>Gets the number of requests waiting for the worker.</summary>
        internal int QueueDepth => _requestGate.QueueDepth;

        /// <summary>Gets the number of late responses discarded after timeout or cancellation.</summary>
        internal long DiscardedResponses => _router.DiscardedCount;

        /// <summary>Gets the number of stderr lines drained from the worker.</summary>
        internal long StandardErrorLines => Interlocked.Read(ref _stderrLineCount);

        /// <summary>Gets the Job Object limits applied to this session's worker.</summary>
        internal JobLimitSnapshot? AppliedLimits => _childProcessLimit?.QueryLimits();

        /// <summary>Gets the most recent bounded worker diagnostic lines.</summary>
        internal IReadOnlyList<string> RecentDiagnosticLines
        {
            get
            {
                lock (_diagnosticSync)
                {
                    return [.. _diagnosticLines];
                }
            }
        }

        /// <summary>
        /// Gets the peak resident working set observed for this worker process.
        /// The value is intended for bounded diagnostics and must be read before
        /// disposing the session.
        /// </summary>
        public long PeakWorkingSetBytes
        {
            get
            {
                try
                {
                    return _process.PeakWorkingSet64;
                }
                catch (InvalidOperationException)
                {
                    return 0;
                }
            }
        }

        /// <summary>
        /// Gets the current private memory size observed for this worker process.
        /// The value is intended for bounded diagnostics and must be read before
        /// disposing the session.
        /// </summary>
        public long PrivateMemoryBytes
        {
            get
            {
                try
                {
                    return _process.PrivateMemorySize64;
                }
                catch (InvalidOperationException)
                {
                    return 0;
                }
            }
        }

        /// <summary>Renders a page through the persistent isolated worker.</summary>
        /// <param name="pageIndex">The zero-based page index.</param>
        /// <param name="request">The render request.</param>
        /// <param name="cancellationToken">
        /// Cancels the request while it is queued. A render already running in the worker
        /// finishes (it is bounded by the per-request wall clock) and its output is discarded.
        /// </param>
        /// <returns>The rendered page.</returns>
        public async Task<RenderResult> RenderPageAsync(
            int pageIndex,
            RenderRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            string outputName = $"page-{Guid.NewGuid():N}.png";
            ServerResponse response = await SendAsync(
                    new ServerRequest(
                        "render-page",
                        pageIndex,
                        request.WidthPx,
                        request.HeightPx,
                        request.Scale,
                        request.IsLowResPreview,
                        outputName,
                        request.PageBox,
                        request.AnnotationMode,
                        request.IncludeFormValues,
                        request.OptionalContentMode,
                        request.RotationDegrees),
                    request.IsLowResPreview ? WorkerRequestPriority.Preview : WorkerRequestPriority.Render,
                    cancellationToken)
                .ConfigureAwait(false);
            string outputPath = Path.Combine(_sandbox.Path, outputName);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                (byte[] bytes, _) = await ReadVerifiedOutputAsync(
                        outputPath,
                        _sandbox.Path,
                        _client._options.MaxOutputBytes,
                        cancellationToken)
                    .ConfigureAwait(false);
                return new RenderResult(
                    bytes,
                    response.PageWidthPoints,
                    response.PageHeightPoints,
                    pageIndex);
            }
            finally
            {
                TryDeleteOutput(outputPath);
            }
        }

        /// <summary>Reads a page rotation through the persistent isolated worker.</summary>
        /// <param name="pageIndex">The zero-based page index.</param>
        /// <param name="cancellationToken">Cancels the request while it is queued.</param>
        /// <returns>The clockwise rotation in degrees.</returns>
        public async Task<int> GetPageRotationDegreesAsync(int pageIndex, CancellationToken cancellationToken) =>
            (await SendAsync(new ServerRequest("rotation", pageIndex), WorkerRequestPriority.Control, cancellationToken)
                .ConfigureAwait(false)).RotationDegrees;

        /// <summary>Reads effective page geometry through the persistent worker.</summary>
        /// <param name="pageIndex">The zero-based page index.</param>
        /// <param name="cancellationToken">Cancels the request while it is queued.</param>
        /// <returns>The page geometry, or a safe fallback.</returns>
        public async Task<PdfPageGeometry> GetPageGeometryAsync(int pageIndex, CancellationToken cancellationToken) =>
            (await SendAsync(new ServerRequest("geometry", pageIndex), WorkerRequestPriority.Control, cancellationToken)
                .ConfigureAwait(false)).PageGeometry
            ?? PdfPageGeometry.Fallback(pageIndex);

        /// <summary>Reads document information through the persistent worker.</summary>
        /// <param name="cancellationToken">Cancels the request while it is queued.</param>
        /// <returns>The document metadata.</returns>
        public async Task<PdfDocumentMetadata> ReadDocumentMetadataAsync(CancellationToken cancellationToken) =>
            (await SendAsync(new ServerRequest("metadata", -1), WorkerRequestPriority.Control, cancellationToken)
                .ConfigureAwait(false)).DocumentMetadata
            ?? new PdfDocumentMetadata();

        /// <summary>Reads sanitized outline entries through the persistent worker.</summary>
        /// <param name="cancellationToken">Cancels the request while it is queued.</param>
        /// <returns>The outline entries.</returns>
        public async Task<IReadOnlyList<PdfOutlineEntry>> ReadOutlineAsync(CancellationToken cancellationToken) =>
            (await SendAsync(new ServerRequest("outline", -1), WorkerRequestPriority.Control, cancellationToken)
                .ConfigureAwait(false)).Outline ?? [];

        /// <summary>Extracts a page text layer through the persistent isolated worker.</summary>
        /// <param name="pageIndex">The zero-based page index.</param>
        /// <param name="cancellationToken">Cancels the request while it is queued.</param>
        /// <returns>The text layer.</returns>
        public async Task<TextLayer> ExtractTextLayerAsync(int pageIndex, CancellationToken cancellationToken) =>
            (await SendAsync(new ServerRequest("text-layer", pageIndex), WorkerRequestPriority.Control, cancellationToken)
                .ConfigureAwait(false)).TextLayer
            ?? new TextLayer(pageIndex, [], ExtractionQuality.Empty);

        /// <summary>Stops the worker (bounded wait) and deletes its private sandbox.</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            Interlocked.CompareExchange(ref _faultReason, "disposed", null);

            // Construction can fail before Process.Start (for example when a packaged
            // worker is missing). Preserve the original failure rather than masking it
            // with a null-process cleanup failure.
            if (_process is not null)
            {
                _client.RecordResourceUsage(PeakWorkingSetBytes, PrivateMemoryBytes);
                KillProcessTree(_process, WorkerExitWaitMilliseconds);
            }

            _router.FailAll(new ObjectDisposedException(nameof(PdfWorkerSession)));
            _requestGate.Dispose();
            _childProcessLimit?.Dispose();
            if (_password is not null)
            {
                Array.Clear(_password);
            }

            _sandbox.Dispose();
            _process?.Dispose();
        }

        private async Task<ServerResponse> SendAsync(
            ServerRequest request,
            WorkerRequestPriority priority,
            CancellationToken cancellationToken)
        {
            ThrowIfUnusable();
            Interlocked.Increment(ref _activeRequests);
            Touch();
            try
            {
                try
                {
                    await _requestGate.WaitAsync(priority, cancellationToken).ConfigureAwait(false);
                }
                catch (ObjectDisposedException exception)
                {
                    throw new PdfWorkerSessionLostException(FaultReason ?? "disposed", exception);
                }

                try
                {
                    ThrowIfUnusable();
                    long requestId = _router.NextRequestId();
                    Task<ServerResponse> responseTask = _router.Register(requestId);
                    try
                    {
                        await _writer.WriteLineAsync(
                                JsonSerializer.Serialize(request with { RequestId = requestId }))
                            .ConfigureAwait(false);
                        await _writer.FlushAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (Exception exception) when (exception is IOException or ObjectDisposedException or InvalidOperationException)
                    {
                        _router.Abandon(requestId);
                        Fault("pipe_broken");
                        throw new PdfWorkerSessionLostException("pipe_broken", exception);
                    }

                    ServerResponse response;
                    try
                    {
                        response = await responseTask
                            .WaitAsync(_limits.RequestTimeout, CancellationToken.None)
                            .ConfigureAwait(false);
                    }
                    catch (TimeoutException exception)
                    {
                        // Runaway protection that replaces the cumulative CPU cap: a request
                        // that exceeds its wall clock kills the worker. The request id means a
                        // late reply could never be consumed by another request anyway.
                        _router.Abandon(requestId);
                        Fault("request_timeout");
                        throw new PdfWorkerSessionLostException("request_timeout", exception);
                    }

                    ThrowIfError(response);
                    return response;
                }
                finally
                {
                    _requestGate.Release();
                }
            }
            finally
            {
                Touch();
                Interlocked.Decrement(ref _activeRequests);
            }
        }

        private async Task ReadLoopAsync()
        {
            string reason = "worker_exited";
            Exception? failure = null;
            try
            {
                while (await _reader.ReadLineAsync().ConfigureAwait(false) is { } line)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    ServerResponse? response;
                    try
                    {
                        response = JsonSerializer.Deserialize<ServerResponse>(line, JsonOptions);
                    }
                    catch (JsonException exception)
                    {
                        reason = "protocol_violation";
                        failure = exception;
                        break;
                    }

                    if (response is null || response.RequestId <= 0)
                    {
                        reason = "protocol_violation";
                        break;
                    }

                    _router.TryComplete(response.RequestId, response);
                }
            }
            catch (Exception exception) when (exception is IOException or ObjectDisposedException or InvalidOperationException)
            {
                reason = "pipe_broken";
                failure = exception;
            }

            Fault(reason, failure);
        }

        private async Task DrainStandardErrorAsync()
        {
            try
            {
                StreamReader stderr = _process.StandardError;
                while (await stderr.ReadLineAsync().ConfigureAwait(false) is { } line)
                {
                    Interlocked.Increment(ref _stderrLineCount);
                    string bounded = line.Length > MaxDiagnosticLineLength
                        ? line[..MaxDiagnosticLineLength]
                        : line;
                    lock (_diagnosticSync)
                    {
                        _diagnosticLines.Enqueue(bounded);
                        while (_diagnosticLines.Count > MaxRetainedDiagnosticLines)
                        {
                            _diagnosticLines.Dequeue();
                        }
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or ObjectDisposedException or InvalidOperationException)
            {
                // Intentionally ignored: the worker exited or the session was disposed; the read loop reports the fault.
            }
        }

        private async Task<ServerResponse> ReadReadyAsync()
        {
            string? line = await _reader.ReadLineAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line))
            {
                throw new InvalidOperationException("The PDF worker session ended without a response.");
            }

            ServerResponse ready = JsonSerializer.Deserialize<ServerResponse>(line, JsonOptions)
                ?? throw new InvalidOperationException("The PDF worker session returned invalid JSON.");
            if (string.Equals(ready.Status, "ok", StringComparison.OrdinalIgnoreCase) &&
                ready.ProtocolVersion != ProtocolVersion)
            {
                throw new InvalidOperationException("The PDF worker protocol version does not match the application.");
            }

            return ready;
        }

        private void Fault(string reason, Exception? cause = null)
        {
            if (Interlocked.CompareExchange(ref _faultReason, reason, null) is not null)
            {
                return;
            }

            _router.FailAll(cause is null
                ? new PdfWorkerSessionLostException(reason)
                : new PdfWorkerSessionLostException(reason, cause));
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
            {
                // Intentionally ignored: the worker already exited; the fault is recorded either way.
            }
        }

        private void ThrowIfUnusable()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                throw new PdfWorkerSessionLostException("disposed");
            }

            if (Volatile.Read(ref _faultReason) is { } reason)
            {
                throw new PdfWorkerSessionLostException(reason);
            }
        }

        private void Touch() => Interlocked.Exchange(ref _lastActivityTicks, DateTime.UtcNow.Ticks);

        private static void TryDeleteOutput(string outputPath)
        {
            try
            {
                File.Delete(outputPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Intentionally ignored: best effort; the sandbox is deleted when the session is disposed.
            }
        }

        private static void ThrowIfError(ServerResponse response)
        {
            if (string.Equals(response.Status, "ok", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string message = string.IsNullOrWhiteSpace(response.Error)
                ? "The PDF worker session failed."
                : response.Error;
            throw response.ErrorType switch
            {
                nameof(PdfPasswordRequiredException) => new PdfPasswordRequiredException(message),
                nameof(PdfPasswordIncorrectException) => new PdfPasswordIncorrectException(message),
                nameof(PdfEmbeddedCoverNotFoundException) => new PdfEmbeddedCoverNotFoundException(message),
                _ => new InvalidOperationException(message),
            };
        }

        private sealed record ServerRequest(
            string Command,
            int PageIndex,
            int WidthPx = 0,
            int HeightPx = 0,
            double Scale = 1.0,
            bool IsLowResPreview = false,
            string? OutputName = null,
            PdfPageBox PageBox = PdfPageBox.CropBox,
            PdfAnnotationRenderMode AnnotationMode = PdfAnnotationRenderMode.Exclude,
            bool IncludeFormValues = false,
            PdfOptionalContentMode OptionalContentMode = PdfOptionalContentMode.Default,
            int? RotationDegrees = null,
            long RequestId = 0);

        private sealed record ServerResponse(
            string Status,
            string? ErrorType = null,
            string? Error = null,
            int PageCount = 0,
            int RotationDegrees = 0,
            double PageWidthPoints = 595,
            double PageHeightPoints = 842,
            TextLayer? TextLayer = null,
            PdfPageGeometry? PageGeometry = null,
            PdfDocumentMetadata? DocumentMetadata = null,
            IReadOnlyList<PdfOutlineEntry>? Outline = null,
            long RequestId = 0,
            int ProtocolVersion = 0);
    }
}

/// <summary>Redacted PDF worker prerequisite status.</summary>
/// <param name="IsAvailable">Whether the worker file can be resolved.</param>
/// <param name="Code">Stable diagnostic code with no path or secret value.</param>
public sealed record PdfWorkerAvailability(bool IsAvailable, string Code);

/// <summary>Signals that a PDF has no bounded decodable embedded cover image.</summary>
public sealed class PdfEmbeddedCoverNotFoundException : Exception
{
    /// <summary>Initializes the exception with the worker's diagnostic message.</summary>
    public PdfEmbeddedCoverNotFoundException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Options controlling PDF worker process launch.
/// </summary>
public sealed class PdfWorkerOptions
{
    /// <summary>
    /// Gets or sets the explicit worker executable or assembly path.
    /// </summary>
    public string? WorkerPath { get; set; }

    /// <summary>
    /// Gets or sets the root directory used for per-operation worker sandboxes.
    /// </summary>
    public string? SandboxRoot { get; set; }

    /// <summary>
    /// Gets or sets the one-shot worker operation timeout (ingestion, covers,
    /// write-back). Reader sessions use <see cref="Session"/> instead.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Maximum worker process memory enforced by Windows Job Objects. Applies to
    /// one-shot jobs and reader sessions alike.
    /// </summary>
    public long MaxMemoryBytes { get; set; } = 768L * 1024L * 1024L;

    /// <summary>
    /// Maximum cumulative CPU time for one-shot worker jobs, enforced by Windows Job
    /// Objects. Persistent reader sessions deliberately have no cumulative CPU cap
    /// (Sept-23 Kaizen K30); see <see cref="Session"/>.
    /// </summary>
    public TimeSpan CpuTimeLimit { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Resource policy for persistent interactive reader sessions.</summary>
    public PdfWorkerSessionLimits Session { get; set; } = new();

    /// <summary>Maximum size of one sandbox output artifact.</summary>
    public long MaxOutputBytes { get; set; } = 64L * 1024L * 1024L;
}

/// <summary>Verified output metadata for one bounded worker artifact.</summary>
public sealed record PdfWorkerOutputManifest(string RelativeName, long LengthBytes, string Sha256Hash);

/// <summary>
/// A diagnostic result returned by the PDF worker process.
/// </summary>
/// <param name="Status">The diagnostic status.</param>
/// <param name="Detail">The diagnostic detail.</param>
public sealed record PdfWorkerDiagnosticResult(string Status, string Detail);

internal sealed record WorkerEnvelope<T>(string Status, T? Payload, string? ErrorType = null, string? Error = null);

internal sealed class PdfWorkerSandbox : IDisposable
{
    public PdfWorkerSandbox(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public void Dispose()
    {
        // The OS can release a just-exited worker's file handles slightly after the
        // process ends, so retry briefly before giving up.
        for (int attempt = 1; attempt <= DeleteAttempts; attempt++)
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }

                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (attempt == DeleteAttempts)
                {
                    // Best-effort cleanup; stale sandboxes are under the controlled temp root.
                    return;
                }

                Thread.Sleep(DeleteRetryDelayMilliseconds * attempt);
            }
        }
    }

    private const int DeleteAttempts = 4;
    private const int DeleteRetryDelayMilliseconds = 50;
}
