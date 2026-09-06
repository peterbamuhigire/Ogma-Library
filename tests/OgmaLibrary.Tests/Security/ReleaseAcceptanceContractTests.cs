using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace OgmaLibrary.Tests.Security;

/// <summary>Runs the release-acceptance PowerShell contract on every CI platform.</summary>
public sealed class ReleaseAcceptanceContractTests
{
    [Fact]
    public async Task ReleaseAcceptance_ValidFixturePassesAndStaleSchemaFreezeFails()
    {
        string repositoryRoot = FindRepositoryRoot();
        string scriptPath = Path.Combine(repositoryRoot, "scripts", "Test-ReleaseAcceptance.ps1");
        string validFixture = Path.Combine(
            repositoryRoot,
            "tests",
            "fixtures",
            "release-acceptance-contract-valid.json");
        string invalidFixture = Path.Combine(
            repositoryRoot,
            "tests",
            "fixtures",
            "release-acceptance-contract-invalid-schema-freeze.json");
        string sourceEvidence = Path.Combine(
            repositoryRoot,
            "tests",
            "fixtures",
            "release-acceptance-contract-evidence.txt");
        string fixtureDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ogma-release-acceptance-{Guid.NewGuid():N}");
        Directory.CreateDirectory(fixtureDirectory);
        string isolatedEvidence = Path.Combine(fixtureDirectory, Path.GetFileName(sourceEvidence));
        string isolatedValidFixture = Path.Combine(fixtureDirectory, Path.GetFileName(validFixture));
        string isolatedInvalidFixture = Path.Combine(fixtureDirectory, Path.GetFileName(invalidFixture));
        await File.WriteAllBytesAsync(isolatedEvidence, await File.ReadAllBytesAsync(sourceEvidence));
        string evidenceDigest = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(isolatedEvidence)));
        await WriteFixtureWithDigestAsync(validFixture, isolatedValidFixture, evidenceDigest);
        await WriteFixtureWithDigestAsync(invalidFixture, isolatedInvalidFixture, evidenceDigest);

        ProcessResult valid = await RunValidatorAsync(scriptPath, isolatedValidFixture);
        ProcessResult invalid = await RunValidatorAsync(scriptPath, isolatedInvalidFixture);
        ProcessResult wrongCommit = await RunValidatorAsync(scriptPath, isolatedValidFixture, new string('f', 40));
        string unsupportedPropertyFixture = Path.Combine(
            fixtureDirectory,
            $"ogma-release-acceptance-unsupported-{Guid.NewGuid():N}.json");
        ProcessResult unsupported;
        ProcessResult tamperedEvidence;
        try
        {
            string validJson = await File.ReadAllTextAsync(isolatedValidFixture);
            string withUnsupportedProperty = validJson.Replace(
                "\"schema\": \"ogma-release-acceptance-v1\",",
                "\"schema\": \"ogma-release-acceptance-v1\",\n  \"unexpected\": true,",
                StringComparison.Ordinal);
            await File.WriteAllTextAsync(unsupportedPropertyFixture, withUnsupportedProperty);
            unsupported = await RunValidatorAsync(scriptPath, unsupportedPropertyFixture);

            string tamperedJson = validJson.Replace(
                evidenceDigest,
                new string('f', 64),
                StringComparison.Ordinal);
            await File.WriteAllTextAsync(unsupportedPropertyFixture, tamperedJson);
            tamperedEvidence = await RunValidatorAsync(scriptPath, unsupportedPropertyFixture);
        }
        finally
        {
            File.Delete(unsupportedPropertyFixture);
            File.Delete(isolatedValidFixture);
            File.Delete(isolatedInvalidFixture);
            File.Delete(isolatedEvidence);
            Directory.Delete(fixtureDirectory);
        }

        Assert.True(
            valid.ExitCode == 0,
            $"Validator failed. stdout: {valid.StandardOutput} stderr: {valid.StandardError}");
        Assert.Contains("contract-fixture-do-not-release", valid.StandardOutput, StringComparison.Ordinal);
        Assert.NotEqual(0, invalid.ExitCode);
        Assert.Contains(
            "Acceptance migration count does not match the frozen baseline.",
            invalid.StandardError + invalid.StandardOutput,
            StringComparison.Ordinal);
        Assert.NotEqual(0, unsupported.ExitCode);
        Assert.Contains(
            "Acceptance record contains unsupported property 'unexpected'.",
            unsupported.StandardError + unsupported.StandardOutput,
            StringComparison.Ordinal);
        Assert.NotEqual(0, wrongCommit.ExitCode);
        Assert.Contains(
            "Acceptance record commit does not match the expected release commit.",
            wrongCommit.StandardError + wrongCommit.StandardOutput,
            StringComparison.Ordinal);
        Assert.NotEqual(0, tamperedEvidence.ExitCode);
        Assert.Contains(
            "Acceptance evidence digest for 'test-only-evidence' does not match.",
            tamperedEvidence.StandardError + tamperedEvidence.StandardOutput,
            StringComparison.Ordinal);
    }

    private static async Task WriteFixtureWithDigestAsync(
        string sourcePath,
        string destinationPath,
        string evidenceDigest)
    {
        string json = await File.ReadAllTextAsync(sourcePath);
        string isolatedJson = Regex.Replace(
            json,
            "\\\"sha256\\\": \\\"[0-9a-fA-F]{64}\\\"",
            $"\"sha256\": \"{evidenceDigest}\"",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
        await File.WriteAllTextAsync(destinationPath, isolatedJson);
    }

    private static async Task<ProcessResult> RunValidatorAsync(
        string scriptPath,
        string recordPath,
        string? expectedCommitSha = null)
    {
        string executable = OperatingSystem.IsWindows() ? "powershell.exe" : "pwsh";
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        if (OperatingSystem.IsWindows())
        {
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
        }

        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("-RecordPath");
        startInfo.ArgumentList.Add(recordPath);
        startInfo.ArgumentList.Add("-ExpectedCommitSha");
        startInfo.ArgumentList.Add(expectedCommitSha ?? new string('0', 40));

        using Process process = Process.Start(startInfo) ??
            throw new InvalidOperationException("Could not start the PowerShell acceptance validator.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(timeout.Token);
        return new ProcessResult(
            process.ExitCode,
            await standardOutput,
            await standardError);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "OgmaLibrary.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the Ogma Library repository root.");
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
