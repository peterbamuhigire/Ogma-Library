using System.Diagnostics;

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

        ProcessResult valid = await RunValidatorAsync(scriptPath, validFixture);
        ProcessResult invalid = await RunValidatorAsync(scriptPath, invalidFixture);
        string fixtureDirectory = Path.GetDirectoryName(validFixture)!;
        string unsupportedPropertyFixture = Path.Combine(
            fixtureDirectory,
            $"ogma-release-acceptance-unsupported-{Guid.NewGuid():N}.json");
        ProcessResult unsupported;
        ProcessResult tamperedEvidence;
        try
        {
            string validJson = await File.ReadAllTextAsync(validFixture);
            string withUnsupportedProperty = validJson.Replace(
                "\"schema\": \"ogma-release-acceptance-v1\",",
                "\"schema\": \"ogma-release-acceptance-v1\",\n  \"unexpected\": true,",
                StringComparison.Ordinal);
            await File.WriteAllTextAsync(unsupportedPropertyFixture, withUnsupportedProperty);
            unsupported = await RunValidatorAsync(scriptPath, unsupportedPropertyFixture);

            string tamperedJson = validJson.Replace(
                "7622f266371e99c061c5e00a3bf013633c3517bece5e764080db6ec237d02120",
                new string('f', 64),
                StringComparison.Ordinal);
            await File.WriteAllTextAsync(unsupportedPropertyFixture, tamperedJson);
            tamperedEvidence = await RunValidatorAsync(scriptPath, unsupportedPropertyFixture);
        }
        finally
        {
            File.Delete(unsupportedPropertyFixture);
        }

        Assert.Equal(0, valid.ExitCode);
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
        Assert.NotEqual(0, tamperedEvidence.ExitCode);
        Assert.Contains(
            "Acceptance evidence digest for 'test-only-evidence' does not match.",
            tamperedEvidence.StandardError + tamperedEvidence.StandardOutput,
            StringComparison.Ordinal);
    }

    private static async Task<ProcessResult> RunValidatorAsync(string scriptPath, string recordPath)
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
