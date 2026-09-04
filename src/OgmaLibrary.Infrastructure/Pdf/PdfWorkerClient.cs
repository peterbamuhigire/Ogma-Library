using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
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
            _options.MaxOutputBytes <= 0)
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
            using WindowsChildProcessLimit? childProcessLimit = RequireWindowsProcessLimit(process);
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

    private WindowsChildProcessLimit? RequireWindowsProcessLimit(Process process)
    {
        WindowsChildProcessLimit? limit = WindowsChildProcessLimit.TryAssign(
            process,
            _options.MaxMemoryBytes,
            _options.CpuTimeLimit);
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
        string destination = Path.Combine(sandboxPath, "input.pdf");
        File.Copy(inputPath, destination, overwrite: false);
        return destination;
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

        throw new InvalidOperationException(
            string.IsNullOrWhiteSpace(stderr)
                ? "The PDF worker process failed."
                : $"The PDF worker process failed: {stderr.Trim()}");
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

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Process already exited.
        }
    }

    private sealed record WorkerCommand(string FileName, IReadOnlyList<string> PrefixArguments);

    private sealed record PageCountResponse(int PageCount);

    private sealed record RenderPageResponse(double PageWidthPoints, double PageHeightPoints);

    private sealed record RotationResponse(int RotationDegrees);

    private sealed record AssetResponse(string OutputPath);

    /// <summary>Persistent worker-backed operations for one validated PDF document.</summary>
    public sealed class PdfWorkerSession : IDisposable
    {
        private readonly PdfWorkerClient _client;
        private readonly PdfWorkerSandbox _sandbox;
        private readonly Process _process;
        private readonly WindowsChildProcessLimit? _childProcessLimit;
        private readonly StreamReader _reader;
        private readonly StreamWriter _writer;
        private readonly SemaphoreSlim _requestGate = new(1, 1);
        private readonly char[]? _password;
        private bool _disposed;

        internal PdfWorkerSession(PdfWorkerClient client, string filePath, char[]? password)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _password = password?.ToArray();
            _sandbox = client.CreateSandbox();

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
                _childProcessLimit = _client.RequireWindowsProcessLimit(_process);
                _reader = _process.StandardOutput;
                _writer = _process.StandardInput;
                SendPasswordAsync(_writer, _password, closeAfterWrite: false)
                    .GetAwaiter()
                    .GetResult();

                ServerResponse ready = ReadResponseAsync().GetAwaiter().GetResult();
                ThrowIfError(ready);
                PageCount = ready.PageCount;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>The page count reported by the persistent isolated worker.</summary>
        public int PageCount { get; }

        /// <summary>Renders a page through the persistent isolated worker.</summary>
        public async Task<RenderResult> RenderPageAsync(
            int pageIndex,
            RenderRequest request,
            CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            await _requestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                string outputName = $"page-{Guid.NewGuid():N}.png";
                ServerResponse response = await SendAsync(
                    new ServerRequest(
                        "render-page",
                        pageIndex,
                        request.WidthPx,
                        request.HeightPx,
                        request.Scale,
                        request.IsLowResPreview,
                        outputName),
                    cancellationToken).ConfigureAwait(false);
                string outputPath = Path.Combine(_sandbox.Path, outputName);
                (byte[] bytes, _) = await ReadVerifiedOutputAsync(
                    outputPath,
                    _sandbox.Path,
                    _client._options.MaxOutputBytes,
                    cancellationToken).ConfigureAwait(false);
                File.Delete(outputPath);
                return new RenderResult(
                    bytes,
                    response.PageWidthPoints,
                    response.PageHeightPoints,
                    pageIndex);
            }
            finally
            {
                _requestGate.Release();
            }
        }

        /// <summary>Reads a page rotation through the persistent isolated worker.</summary>
        public int GetPageRotationDegrees(int pageIndex) =>
            SendSynchronously(new ServerRequest("rotation", pageIndex)).RotationDegrees;

        /// <summary>Extracts a page text layer through the persistent isolated worker.</summary>
        public TextLayer ExtractTextLayer(int pageIndex) =>
            SendSynchronously(new ServerRequest("text-layer", pageIndex)).TextLayer
            ?? new TextLayer(pageIndex, [], ExtractionQuality.Empty);

        /// <summary>Stops the worker and deletes its private sandbox.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            KillProcessTree(_process);
            _childProcessLimit?.Dispose();
            _requestGate.Dispose();
            if (_password is not null)
            {
                Array.Clear(_password);
            }

            _sandbox.Dispose();
            _process.Dispose();
        }

        private ServerResponse SendSynchronously(ServerRequest request)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _requestGate.Wait();
            try
            {
                return SendAsync(request, CancellationToken.None).GetAwaiter().GetResult();
            }
            finally
            {
                _requestGate.Release();
            }
        }

        private async Task<ServerResponse> SendAsync(
            ServerRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _writer.WriteLineAsync(JsonSerializer.Serialize(request)).ConfigureAwait(false);
            await _writer.FlushAsync(CancellationToken.None).ConfigureAwait(false);
            ServerResponse response = await ReadResponseAsync()
                .WaitAsync(_client._options.Timeout, CancellationToken.None)
                .ConfigureAwait(false);
            ThrowIfError(response);
            return response;
        }

        private async Task<ServerResponse> ReadResponseAsync()
        {
            string? line = await _reader.ReadLineAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line))
            {
                throw new InvalidOperationException("The PDF worker session ended without a response.");
            }

            return JsonSerializer.Deserialize<ServerResponse>(line, JsonOptions)
                ?? throw new InvalidOperationException("The PDF worker session returned invalid JSON.");
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
    }
}

/// <summary>Redacted PDF worker prerequisite status.</summary>
/// <param name="IsAvailable">Whether the worker file can be resolved.</param>
/// <param name="Code">Stable diagnostic code with no path or secret value.</param>
public sealed record PdfWorkerAvailability(bool IsAvailable, string Code);

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
    /// Gets or sets the worker operation timeout.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>Maximum worker process memory enforced by Windows Job Objects.</summary>
    public long MaxMemoryBytes { get; set; } = 768L * 1024L * 1024L;

    /// <summary>Maximum worker CPU time enforced by Windows Job Objects.</summary>
    public TimeSpan CpuTimeLimit { get; set; } = TimeSpan.FromSeconds(15);

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
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup; stale sandboxes are under the controlled temp root.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup; stale sandboxes are under the controlled temp root.
        }
    }
}
