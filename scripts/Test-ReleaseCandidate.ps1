[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string] $DescriptorPath,
    [Parameter(Mandatory = $true)] [string] $ArtifactPath,
    [string] $SignaturePath,
    [string] $PublicKeyPath,
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
    if ([string]::IsNullOrWhiteSpace($PublicKeyPath) -or -not (Test-Path -LiteralPath $PublicKeyPath -PathType Leaf)) {
        throw 'A protected public key is required to verify the descriptor signature.'
    }
    $signature = Get-Content -LiteralPath $SignaturePath -Raw
    if ([string]::IsNullOrWhiteSpace($signature)) { throw 'Detached descriptor signature is empty.' }
    try { $signatureBytes = [Convert]::FromBase64String($signature.Trim()) } catch { throw 'Detached descriptor signature is not base64.' }
    if ($signatureBytes.Length -eq 0) { throw 'Detached descriptor signature is empty.' }

    $openssl = Get-Command openssl -ErrorAction SilentlyContinue
    if (-not $openssl) { throw 'openssl is required to verify a detached descriptor signature.' }
    $rawSignaturePath = Join-Path ([IO.Path]::GetTempPath()) ("ogma-descriptor-signature-" + [guid]::NewGuid().ToString('N') + '.bin')
    try {
        [IO.File]::WriteAllBytes($rawSignaturePath, $signatureBytes)
        $verificationOutput = & $openssl.Source dgst -sha256 -verify $PublicKeyPath -sigopt rsa_padding_mode:pss -sigopt rsa_pss_saltlen:-1 -signature $rawSignaturePath $DescriptorPath 2>&1
        $verified = $LASTEXITCODE -eq 0 -and ($verificationOutput -match 'Verified OK')
    } finally {
        [Array]::Clear($signatureBytes, 0, $signatureBytes.Length)
        if (Test-Path -LiteralPath $rawSignaturePath) { Remove-Item -LiteralPath $rawSignaturePath -Force }
    }
    if (-not $verified) { throw 'Release descriptor signature verification failed.' }
}

Write-Output "Release candidate integrity passed: $ArtifactPath"
