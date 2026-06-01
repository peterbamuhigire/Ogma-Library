using System.Diagnostics;
using System.Runtime.InteropServices;
using OgmaLibrary.Application.Reader;

namespace OgmaLibrary.Infrastructure.Security;

/// <summary>macOS Keychain-backed password provider for protected PDFs.</summary>
public sealed class MacOsKeychainPasswordProvider : IPasswordProvider
{
    private const string SecurityToolPath = "/usr/bin/security";

    /// <inheritdoc />
    public async Task<PasswordResult> GetPasswordAsync(
        PasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return PasswordResult.Failed("macOS Keychain is only available on macOS.");
        }

        string target = PasswordCredentialKey.Create(request.ContentHash);
        ProcessResult result = await RunSecurityAsync(
                ["find-generic-password", "-a", Environment.UserName, "-s", target, "-w"],
                cancellationToken)
            .ConfigureAwait(false);
        if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Output))
        {
            return PasswordResult.Success(result.Output.TrimEnd('\r', '\n').ToCharArray(), wasStored: true);
        }

        return PasswordResult.Failed("No stored Keychain password was found for this PDF.");
    }

    /// <inheritdoc />
    public async Task ForgetPasswordAsync(PasswordRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return;
        }

        string target = PasswordCredentialKey.Create(request.ContentHash);
        _ = await RunSecurityAsync(
                ["delete-generic-password", "-a", Environment.UserName, "-s", target],
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<ProcessResult> RunSecurityAsync(
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
        return new ProcessResult(process.ExitCode, output, error);
    }

    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}
