param(
    [string]$OutputPath,
    [switch]$SkipVerification,
    [switch]$AllowDirtyWorktree,
    [string]$SameSubnetPeerAddress = '',
    [string]$MdnsObservation = 'Pending',
    [string]$HttpsHealthObservation = 'Pending',
    [string]$KeychainObservation = 'Pending',
    [string]$Notes = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-LoggedCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Command,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $started = Get-Date
    $output = & $Command @Arguments 2>&1
    $exitCode = $LASTEXITCODE
    $finished = Get-Date

    [PSCustomObject]@{
        Command = "$Command $($Arguments -join ' ')"
        ExitCode = $exitCode
        Started = $started
        Finished = $finished
        DurationSeconds = [Math]::Round(($finished - $started).TotalSeconds, 1)
        Output = @($output | ForEach-Object { $_.ToString() })
    }
}

function Add-CodeBlock {
    param(
        [System.Collections.Generic.List[string]]$Lines,
        [string]$Language,
        [string[]]$Content
    )

    $items = @($Content)
    $Lines.Add("``````$Language")
    if ($items.Count -eq 0) {
        $Lines.Add("(no output)")
    }
    else {
        foreach ($line in $items) {
            $Lines.Add($line)
        }
    }
    $Lines.Add("``````")
}

function Escape-TableCell {
    param([string]$Value)

    if ($null -eq $Value) {
        return ''
    }

    return ($Value -replace '\|', '\|').Replace("`r", ' ').Replace("`n", ' ')
}

function Test-IsWindows {
    return [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [System.Runtime.InteropServices.OSPlatform]::Windows)
}

function Test-IsMacOs {
    return [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
        [System.Runtime.InteropServices.OSPlatform]::OSX)
}

function Get-OperatingSystemSummary {
    if (Test-IsWindows) {
        try {
            $os = Get-CimInstance Win32_OperatingSystem
            return "$($os.Caption) $($os.Version) build $($os.BuildNumber)"
        }
        catch {
            return [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
        }
    }

    return [System.Runtime.InteropServices.RuntimeInformation]::OSDescription
}

function Get-NetworkInterfaceSummary {
    $rows = [System.Collections.Generic.List[object]]::new()
    $interfaces = [System.Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces()
    foreach ($adapter in $interfaces) {
        $properties = $adapter.GetIPProperties()
        $addresses = @($properties.UnicastAddresses |
            Where-Object { $_.Address.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetwork } |
            ForEach-Object { $_.Address.ToString() })
        if ($addresses.Count -eq 0) {
            continue
        }

        $rows.Add([PSCustomObject]@{
            Name = $adapter.Name
            Type = $adapter.NetworkInterfaceType.ToString()
            Status = $adapter.OperationalStatus.ToString()
            Addresses = ($addresses -join ', ')
        })
    }

    return @($rows)
}

function Get-MacOsKeychainStatus {
    if (-not (Test-IsMacOs)) {
        return 'Not applicable on this OS'
    }

    $output = & /usr/bin/security find-generic-password -s 'OgmaLibrary.LanHost.HostCA' 2>&1
    if ($LASTEXITCODE -eq 0) {
        return 'Found service OgmaLibrary.LanHost.HostCA in macOS Keychain'
    }

    return "Pending: Keychain service not found or inaccessible ($($output -join ' '))"
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot

$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $evidenceDir = Join-Path $repoRoot 'docs/qa/evidence'
    New-Item -ItemType Directory -Force -Path $evidenceDir | Out-Null
    $OutputPath = Join-Path $evidenceDir "phase16-lan-verification-$timestamp.md"
}

$gitStatus = @((& git status --short) | ForEach-Object { $_.ToString() })
if (-not $AllowDirtyWorktree -and $gitStatus.Count -gt 0) {
    throw "Working tree is not clean. Commit or stash changes, or pass -AllowDirtyWorktree for a draft verification record."
}

$commit = (& git rev-parse HEAD).Trim()
$branch = (& git branch --show-current).Trim()
$remote = (& git remote get-url origin).Trim()
$dotnetVersion = (& dotnet --version).Trim()
$osSummary = Get-OperatingSystemSummary
$networkInterfaces = @(Get-NetworkInterfaceSummary)
$keychainProbe = Get-MacOsKeychainStatus

$results = @()
if (-not $SkipVerification) {
    $lanFilter = 'FullyQualifiedName~LanHostScaffoldTests|FullyQualifiedName~LanHostPersistenceTests|FullyQualifiedName~LanHostCertificateProvisionerTests|FullyQualifiedName~MdnsAdvertiserTests|FullyQualifiedName~LanBindAddressSelectorTests|FullyQualifiedName~LanClientAddressPolicyTests|FullyQualifiedName~LanBookFileResolverTests|FullyQualifiedName~LanPageRenderLimiterTests|FullyQualifiedName~LanHostEndpointTests|FullyQualifiedName~LanHostLoadSmokeTests|FullyQualifiedName~HostSharingViewModelTests'
    $architectureFilter = 'FullyQualifiedName~ArchTests_LanHost|FullyQualifiedName~ArchTests_StandaloneMode'

    $results += Invoke-LoggedCommand -Command 'dotnet' -Arguments @(
        'format',
        'OgmaLibrary.sln',
        '--verify-no-changes',
        '--no-restore')
    $results += Invoke-LoggedCommand -Command 'dotnet' -Arguments @(
        'build',
        'OgmaLibrary.sln',
        '--configuration',
        'Release',
        '--no-restore')
    $results += Invoke-LoggedCommand -Command 'dotnet' -Arguments @(
        'test',
        'tests/OgmaLibrary.Tests/OgmaLibrary.Tests.csproj',
        '--configuration',
        'Release',
        '--no-build',
        '--filter',
        $lanFilter,
        '--logger',
        'console;verbosity=minimal')
    $results += Invoke-LoggedCommand -Command 'dotnet' -Arguments @(
        'test',
        'tests/OgmaLibrary.Tests.Architecture/OgmaLibrary.Tests.Architecture.csproj',
        '--configuration',
        'Release',
        '--no-build',
        '--filter',
        $architectureFilter,
        '--logger',
        'console;verbosity=minimal')
}

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# Phase 16 LAN Verification Evidence')
$lines.Add('')
$lines.Add('| Field | Value |')
$lines.Add('| --- | --- |')
$lines.Add("| Generated UTC | $((Get-Date).ToUniversalTime().ToString('yyyy-MM-dd HH:mm:ss')) |")
$lines.Add("| Repository | $remote |")
$lines.Add("| Branch | $branch |")
$lines.Add("| Commit | $commit |")
$lines.Add("| OS | $osSummary |")
$lines.Add("| .NET SDK | $dotnetVersion |")
$lines.Add("| Verification skipped | $($SkipVerification.IsPresent) |")
$lines.Add('')
$lines.Add('## Working Tree')
$lines.Add('')
Add-CodeBlock -Lines $lines -Language 'text' -Content $gitStatus
$lines.Add('')
$lines.Add('## Network Interfaces')
$lines.Add('')
if ($networkInterfaces.Count -eq 0) {
    $lines.Add('No IPv4 network interfaces were detected.')
}
else {
    $lines.Add('| Name | Type | Status | IPv4 addresses |')
    $lines.Add('| --- | --- | --- | --- |')
    foreach ($adapter in $networkInterfaces) {
        $lines.Add("| $(Escape-TableCell $adapter.Name) | $($adapter.Type) | $($adapter.Status) | $(Escape-TableCell $adapter.Addresses) |")
    }
}
$lines.Add('')
$lines.Add('## Platform Probes')
$lines.Add('')
$lines.Add('| Probe | Result |')
$lines.Add('| --- | --- |')
$lines.Add("| macOS Keychain service | $(Escape-TableCell $keychainProbe) |")
$lines.Add('')
$lines.Add('## Automated Verification')
$lines.Add('')
if ($SkipVerification) {
    $lines.Add('Automated verification commands were skipped for this draft evidence record.')
}
else {
    $lines.Add('| Command | Exit code | Duration seconds |')
    $lines.Add('| --- | ---: | ---: |')
    foreach ($result in $results) {
        $lines.Add("| ``$($result.Command)`` | $($result.ExitCode) | $($result.DurationSeconds) |")
    }
    foreach ($result in $results) {
        $lines.Add('')
        $lines.Add("### $($result.Command)")
        $lines.Add('')
        Add-CodeBlock -Lines $lines -Language 'text' -Content $result.Output
    }
}
$lines.Add('')
$lines.Add('## Same-Subnet Verification')
$lines.Add('')
$lines.Add('| Check | Evidence |')
$lines.Add('| --- | --- |')
$lines.Add("| Peer address/device | $(Escape-TableCell $SameSubnetPeerAddress) |")
$lines.Add("| mDNS discovery from peer | $(Escape-TableCell $MdnsObservation) |")
$lines.Add("| HTTPS health from peer | $(Escape-TableCell $HttpsHealthObservation) |")
$lines.Add("| Host CA Keychain verification | $(Escape-TableCell $KeychainObservation) |")
$lines.Add("| Notes | $(Escape-TableCell $Notes) |")
$lines.Add('')
$lines.Add('## Closeout Criteria')
$lines.Add('')
$lines.Add('- Windows evidence must show the LAN Host automated tests, architecture guards, and same-subnet mDNS/HTTPS observations.')
$lines.Add('- macOS evidence must show the same LAN observations plus Host CA Keychain service evidence.')
$lines.Add('- If mDNS is blocked by a school firewall, record the failure and verify manual `ogma-lan://` join details against the same peer.')

Set-Content -Path $OutputPath -Value $lines -Encoding UTF8
Write-Host "Phase 16 LAN verification evidence written to $OutputPath"

if (-not $SkipVerification) {
    $failed = @($results | Where-Object { $_.ExitCode -ne 0 })
    if ($failed.Count -gt 0) {
        exit 1
    }
}
