using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using OgmaLibrary.Application.ClassroomClient;

namespace OgmaLibrary.Infrastructure.ClassroomClient;

internal interface IClassroomSecretStore
{
    Task SaveAsync(string key, string value, CancellationToken cancellationToken);

    Task<string?> GetAsync(string key, CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);
}

/// <summary>Classroom credential store backed by the active platform secret store.</summary>
internal sealed class PlatformClassroomCredentialStore : IClassroomCredentialStore
{
    private const string TargetPrefix = "Ogma:Classroom:";
    private readonly IClassroomSecretStore _secretStore;

    public PlatformClassroomCredentialStore(IClassroomSecretStore secretStore)
    {
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
    }

    public Task SaveSecretAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();
        return _secretStore.SaveAsync(CreateTarget(key), value, cancellationToken);
    }

    public Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();
        return _secretStore.GetAsync(CreateTarget(key), cancellationToken);
    }

    public Task DeleteSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();
        return _secretStore.DeleteAsync(CreateTarget(key), cancellationToken);
    }

    internal static string CreateTarget(string key) => TargetPrefix + key.Trim();
}

internal static class ClassroomSecretStoreFactory
{
    public static IClassroomSecretStore Create(string dataDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        if (OperatingSystem.IsWindows())
        {
            return new WindowsCredentialManagerClassroomSecretStore(new WindowsCredentialManager());
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new MacOsKeychainClassroomSecretStore(new DefaultClassroomMacOsSecurityTool());
        }

        if (OperatingSystem.IsLinux() && File.Exists(DefaultLinuxSecretTool.SecretToolPath))
        {
            return new LinuxSecretServiceClassroomSecretStore(new DefaultLinuxSecretTool());
        }

        return new FileClassroomSecretStore(
            Path.Combine(dataDirectory, "classroom", "credentials", "secrets.json"));
    }
}

internal sealed class WindowsCredentialManagerClassroomSecretStore : IClassroomSecretStore
{
    private const int CredTypeGeneric = 1;
    private readonly IWindowsCredentialManager _manager;

    public WindowsCredentialManagerClassroomSecretStore(IWindowsCredentialManager manager)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
    }

    public Task SaveAsync(string key, string value, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_manager.Write(key, value))
        {
            throw new InvalidOperationException("Windows Credential Manager rejected the classroom credential.");
        }

        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_manager.Read(key));
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();
        _manager.Delete(key);
        return Task.CompletedTask;
    }
}

internal interface IWindowsCredentialManager
{
    bool Write(string target, string secret);

    string? Read(string target);

    bool Delete(string target);
}

internal sealed class WindowsCredentialManager : IWindowsCredentialManager
{
    private const int CredTypeGeneric = 1;
    private const int CredPersistLocalMachine = 2;

    public bool Write(string target, string secret)
    {
        byte[] secretBytes = Encoding.Unicode.GetBytes(secret);
        try
        {
            var credential = new Credential
            {
                Type = CredTypeGeneric,
                TargetName = target,
                CredentialBlobSize = secretBytes.Length,
                Persist = CredPersistLocalMachine,
                UserName = Environment.UserName,
            };

            credential.CredentialBlob = Marshal.AllocCoTaskMem(secretBytes.Length);
            try
            {
                Marshal.Copy(secretBytes, 0, credential.CredentialBlob, secretBytes.Length);
                return CredWrite(ref credential, 0);
            }
            finally
            {
                Marshal.FreeCoTaskMem(credential.CredentialBlob);
            }
        }
        finally
        {
            Array.Clear(secretBytes);
        }
    }

    public string? Read(string target)
    {
        if (!CredRead(target, CredTypeGeneric, 0, out IntPtr credentialPtr))
        {
            return null;
        }

        try
        {
            var credential = Marshal.PtrToStructure<Credential>(credentialPtr);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize <= 0)
            {
                return null;
            }

            byte[] bytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
            try
            {
                return Encoding.Unicode.GetString(bytes).TrimEnd('\0');
            }
            finally
            {
                Array.Clear(bytes);
            }
        }
        finally
        {
            CredFree(credentialPtr);
        }
    }

    public bool Delete(string target) => CredDelete(target, CredTypeGeneric, 0);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public int Flags;
        public int Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref Credential credential, int flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);
}

internal sealed class MacOsKeychainClassroomSecretStore : IClassroomSecretStore
{
    internal const string ServiceName = "OgmaLibrary.Classroom";
    private readonly IClassroomMacOsSecurityTool _securityTool;

    public MacOsKeychainClassroomSecretStore(IClassroomMacOsSecurityTool securityTool)
    {
        _securityTool = securityTool ?? throw new ArgumentNullException(nameof(securityTool));
    }

    public async Task SaveAsync(string key, string value, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();

        ClassroomMacOsSecurityToolResult result = await _securityTool.RunAsync(
                ["add-generic-password", "-a", key, "-s", ServiceName, "-w", value, "-U"],
                cancellationToken)
            .ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("macOS Keychain rejected the classroom credential: " + result.Error.Trim());
        }
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        ClassroomMacOsSecurityToolResult result = await _securityTool.RunAsync(
                ["find-generic-password", "-a", key, "-s", ServiceName, "-w"],
                cancellationToken)
            .ConfigureAwait(false);
        return result.ExitCode == 0
            ? result.Output.TrimEnd('\r', '\n')
            : null;
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        _ = await _securityTool.RunAsync(
                ["delete-generic-password", "-a", key, "-s", ServiceName],
                cancellationToken)
            .ConfigureAwait(false);
    }
}

internal interface IClassroomMacOsSecurityTool
{
    Task<ClassroomMacOsSecurityToolResult> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken);
}

internal sealed record ClassroomMacOsSecurityToolResult(int ExitCode, string Output, string Error);

internal sealed class DefaultClassroomMacOsSecurityTool : IClassroomMacOsSecurityTool
{
    private const string SecurityToolPath = "/usr/bin/security";

    public async Task<ClassroomMacOsSecurityToolResult> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(SecurityToolPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ??
            throw new InvalidOperationException("Could not start macOS security tool.");
        string output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        string error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return new ClassroomMacOsSecurityToolResult(process.ExitCode, output, error);
    }
}

internal sealed class LinuxSecretServiceClassroomSecretStore : IClassroomSecretStore
{
    internal const string ServiceName = "OgmaLibrary.Classroom";
    private readonly ILinuxSecretTool _secretTool;

    public LinuxSecretServiceClassroomSecretStore(ILinuxSecretTool secretTool)
    {
        _secretTool = secretTool ?? throw new ArgumentNullException(nameof(secretTool));
    }

    public async Task SaveAsync(string key, string value, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();

        LinuxSecretToolResult result = await _secretTool.RunAsync(
                ["store", "--label", ServiceName, "service", ServiceName, "key", key],
                value,
                cancellationToken)
            .ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("Linux Secret Service rejected the classroom credential: " + result.Error.Trim());
        }
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        LinuxSecretToolResult result = await _secretTool.RunAsync(
                ["lookup", "service", ServiceName, "key", key],
                standardInput: null,
                cancellationToken)
            .ConfigureAwait(false);
        return result.ExitCode == 0
            ? result.Output.TrimEnd('\r', '\n')
            : null;
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        _ = await _secretTool.RunAsync(
                ["clear", "service", ServiceName, "key", key],
                standardInput: null,
                cancellationToken)
            .ConfigureAwait(false);
    }
}

internal interface ILinuxSecretTool
{
    Task<LinuxSecretToolResult> RunAsync(
        IReadOnlyList<string> arguments,
        string? standardInput,
        CancellationToken cancellationToken);
}

internal sealed record LinuxSecretToolResult(int ExitCode, string Output, string Error);

internal sealed class DefaultLinuxSecretTool : ILinuxSecretTool
{
    internal const string SecretToolPath = "/usr/bin/secret-tool";

    public async Task<LinuxSecretToolResult> RunAsync(
        IReadOnlyList<string> arguments,
        string? standardInput,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(SecretToolPath)
        {
            RedirectStandardInput = standardInput is not null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ??
            throw new InvalidOperationException("Could not start Linux secret-tool.");
        if (standardInput is not null)
        {
            await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken).ConfigureAwait(false);
            await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
            process.StandardInput.Close();
        }

        string output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        string error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return new LinuxSecretToolResult(process.ExitCode, output, error);
    }
}

internal sealed class FileClassroomSecretStore : IClassroomSecretStore, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FileClassroomSecretStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    public async Task SaveAsync(string key, string value, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        cancellationToken.ThrowIfCancellationRequested();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Dictionary<string, string> secrets = await LoadAsync(cancellationToken).ConfigureAwait(false);
            secrets[key] = value;
            await SaveAsync(secrets, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Dictionary<string, string> secrets = await LoadAsync(cancellationToken).ConfigureAwait(false);
            secrets.TryGetValue(key, out string? value);
            return value;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Dictionary<string, string> secrets = await LoadAsync(cancellationToken).ConfigureAwait(false);
            if (secrets.Remove(key))
            {
                await SaveAsync(secrets, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Dictionary<string, string>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        using FileStream stream = File.OpenRead(_path);
        Dictionary<string, string>? secrets = await JsonSerializer
            .DeserializeAsync<Dictionary<string, string>>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return secrets is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(secrets, StringComparer.Ordinal);
    }

    private async Task SaveAsync(Dictionary<string, string> secrets, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        string tempPath = $"{_path}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (FileStream stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, secrets, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            }

            File.Move(tempPath, _path, overwrite: true);
            RestrictUnixFile(_path);
        }
        finally
        {
            DeleteTemporaryFile(tempPath);
        }
    }

    private static void DeleteTemporaryFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void RestrictUnixFile(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (PlatformNotSupportedException)
        {
        }
    }

    public void Dispose() => _gate.Dispose();
}
