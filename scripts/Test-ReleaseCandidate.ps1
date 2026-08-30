[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $DescriptorPath,
    [Parameter(Mandatory = $true)] [string] $ArtifactPath,
    [string] $SignaturePath,
    [switch] $RequireSignature
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $DescriptorPath -PathType Leaf)) { throw 'Descriptor file does not exist.' }
if (-not (Test-Path -LiteralPath $ArtifactPath -PathType Leaf)) { throw 'Artifact file does not exist.' }

$descriptor = Get-Content -LiteralPath $DescriptorPath -Raw | ConvertFrom-Json
if ($descriptor.schema -ne 'ogma-release-v1') { throw 'Unsupported release descriptor schema.' }
if ($descriptor.signatureAlgorithm -ne 'RSA-PSS-SHA256') { throw 'Unsupported descriptor signature algorithm.' }
if ($descriptor.artifactName -ne (Split-Path -Leaf $ArtifactPath)) { throw 'Descriptor artifactName does not match the artifact.' }
if ($descriptor.artifactSha256 -notmatch '^[0-9a-fA-F]{64}$') { throw 'Descriptor artifactSha256 is not a SHA-256 value.' }

$actualHash = (Get-FileHash -LiteralPath $ArtifactPath -Algorithm SHA256).Hash
if (-not [string]::Equals($actualHash, $descriptor.artifactSha256, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Artifact digest does not match the release descriptor.'
}

if ($RequireSignature) {
    if ([string]::IsNullOrWhiteSpace($SignaturePath) -or -not (Test-Path -LiteralPath $SignaturePath -PathType Leaf)) {
        throw 'A detached descriptor signature is required.'
    }
    $signature = Get-Content -LiteralPath $SignaturePath -Raw
    if ([string]::IsNullOrWhiteSpace($signature)) { throw 'Detached descriptor signature is empty.' }
    try { [Convert]::FromBase64String($signature.Trim()) | Out-Null } catch { throw 'Detached descriptor signature is not base64.' }
}

Write-Output "Release candidate integrity passed: $ArtifactPath"
