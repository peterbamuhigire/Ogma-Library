using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace OgmaLibrary.Infrastructure.LanHost;

internal interface IHostCaStore
{
    Task<byte[]?> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(byte[] pfxBytes, CancellationToken cancellationToken);
}

internal static class HostCaStoreFactory
{
    private const string ProtectedCertificateFileName = "host-ca.pfx.dpapi";
    private const string FallbackCertificateFileName = "host-ca.pfx";

    public static IHostCaStore Create(string certificateDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(certificateDirectory);
        string fallbackPath = Path.Combine(certificateDirectory, FallbackCertificateFileName);

        if (OperatingSystem.IsWindows())
        {
            string protectedPath = Path.Combine(certificateDirectory, ProtectedCertificateFileName);
            return new WindowsDpapiHostCaStore(protectedPath, fallbackPath);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new MacOsKeychainHostCaStore(
                new DefaultMacOsSecurityTool(),
                fallbackPath,
                MacOsKeychainHostCaStore.CreateAccountName(certificateDirectory));
        }

        return new FileHostCaStore(fallbackPath);
    }
}

internal sealed class WindowsDpapiHostCaStore : IHostCaStore
{
    private readonly string _protectedPath;
    private readonly string _fallbackPath;

    public WindowsDpapiHostCaStore(string protectedPath, string fallbackPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackPath);
        _protectedPath = protectedPath;
        _fallbackPath = fallbackPath;
    }

    public async Task<byte[]?> LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (File.Exists(_protectedPath))
        {
            byte[] protectedBytes = await File.ReadAllBytesAsync(_protectedPath, cancellationToken)
                .ConfigureAwait(false);
            return LocalCertificateProvisioner.WindowsDpapi.Unprotect(protectedBytes);
        }

        if (File.Exists(_fallbackPath))
        {
            return await File.ReadAllBytesAsync(_fallbackPath, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    public async Task SaveAsync(byte[] pfxBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pfxBytes);
        cancellationToken.ThrowIfCancellationRequested();

        byte[] protectedBytes = LocalCertificateProvisioner.WindowsDpapi.Protect(pfxBytes);
        await File.WriteAllBytesAsync(_protectedPath, protectedBytes, cancellationToken)
            .ConfigureAwait(false);
    }
}

internal sealed class MacOsKeychainHostCaStore : IHostCaStore
{
    internal const string ServiceName = "OgmaLibrary.LanHost.HostCA";
    private readonly IMacOsSecurityTool _securityTool;
    private readonly string _fallbackPath;
    private readonly string _accountName;

    public MacOsKeychainHostCaStore(
        IMacOsSecurityTool securityTool,
        string fallbackPath,
        string? accountName = null)
    {
        _securityTool = securityTool ?? throw new ArgumentNullException(nameof(securityTool));
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackPath);
        _fallbackPath = fallbackPath;
        _accountName = string.IsNullOrWhiteSpace(accountName)
            ? CreateAccountName(Path.GetDirectoryName(fallbackPath) ?? fallbackPath)
            : accountName;
    }

    public static string CreateAccountName(string certificateDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(certificateDirectory);
        string fullPath = Path.GetFullPath(certificateDirectory);
        string normalized = OperatingSystem.IsWindows()
            ? fullPath.ToUpperInvariant()
            : fullPath;
        byte[] digest = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalized));
        return Environment.UserName + ":" + Convert.ToHexStringLower(digest[..12]);
    }

    public async Task<byte[]?> LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        MacOsSecurityToolResult result = await _securityTool.RunAsync(
                ["find-generic-password", "-a", _accountName, "-s", ServiceName, "-w"],
                cancellationToken)
            .ConfigureAwait(false);

        if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Output))
        {
            try
            {
                return Convert.FromBase64String(result.Output.Trim());
            }
            catch (FormatException exception)
            {
                throw new CryptographicException("The stored macOS Keychain LAN Host CA is not valid.", exception);
            }
        }

        if (!File.Exists(_fallbackPath))
        {
            return null;
        }

        byte[] legacyPfx = await File.ReadAllBytesAsync(_fallbackPath, cancellationToken)
            .ConfigureAwait(false);
        await SaveAsync(legacyPfx, cancellationToken).ConfigureAwait(false);
        TryDeleteLegacyFallback();
        return legacyPfx;
    }

    public async Task SaveAsync(byte[] pfxBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pfxBytes);
        cancellationToken.ThrowIfCancellationRequested();

        string encodedPfx = Convert.ToBase64String(pfxBytes);
        MacOsSecurityToolResult result = await _securityTool.RunAsync(
                ["add-generic-password", "-a", _accountName, "-s", ServiceName, "-w", encodedPfx, "-U"],
                cancellationToken)
            .ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new CryptographicException("macOS Keychain rejected the LAN Host CA private key: " + result.Error.Trim());
        }
    }

    private void TryDeleteLegacyFallback()
    {
        try
        {
            File.Delete(_fallbackPath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

internal sealed class FileHostCaStore : IHostCaStore
{
    private readonly string _path;

    public FileHostCaStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    public async Task<byte[]?> LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return File.Exists(_path)
            ? await File.ReadAllBytesAsync(_path, cancellationToken).ConfigureAwait(false)
            : null;
    }

    public async Task SaveAsync(byte[] pfxBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pfxBytes);
        cancellationToken.ThrowIfCancellationRequested();

        await File.WriteAllBytesAsync(_path, pfxBytes, cancellationToken).ConfigureAwait(false);
        RestrictUnixFile(_path);
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
}

internal interface IMacOsSecurityTool
{
    Task<MacOsSecurityToolResult> RunAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken);
}

internal sealed record MacOsSecurityToolResult(int ExitCode, string Output, string Error);

internal sealed class DefaultMacOsSecurityTool : IMacOsSecurityTool
{
    private const string SecurityToolPath = "/usr/bin/security";

    public async Task<MacOsSecurityToolResult> RunAsync(
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
        return new MacOsSecurityToolResult(process.ExitCode, output, error);
    }
}
