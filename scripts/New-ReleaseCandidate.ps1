[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('windows', 'macos')]
    [string] $Platform,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$')]
    [string] $Version,

    [ValidateSet('x64', 'arm64')]
    [string] $Architecture = 'x64',

    [string] $OutputDirectory,
    [string] $PublicKeyId = 'production-2026',
    [string] $SigningKeyPath,
    [switch] $RequireSignature
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$runtimeIdentifier = if ($Platform -eq 'windows') { "win-$Architecture" } else { "osx-$Architecture" }
$releaseId = "rc-$Version-$runtimeIdentifier-$(git -C $repoRoot rev-parse --short=12 HEAD)"
$defaultOutput = Join-Path $repoRoot "artifacts/release-candidates/$Version/$runtimeIdentifier"
$candidateRoot = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $defaultOutput } else { (Resolve-Path (Split-Path -Parent $OutputDirectory) -ErrorAction SilentlyContinue)?.Path + [IO.Path]::DirectorySeparatorChar + (Split-Path -Leaf $OutputDirectory) }

if ([string]::IsNullOrWhiteSpace($candidateRoot)) {
    throw 'OutputDirectory must resolve to a directory.'
}

$publishDirectory = Join-Path $candidateRoot 'publish'
$artifactName = "OgmaLibrary-$Version-$runtimeIdentifier.zip"
$artifactPath = Join-Path $candidateRoot $artifactName
$descriptorPath = Join-Path $candidateRoot 'release-descriptor.json'
$signaturePath = Join-Path $candidateRoot 'release-descriptor.sig'

New-Item -ItemType Directory -Force -Path $publishDirectory | Out-Null
& dotnet restore (Join-Path $repoRoot 'OgmaLibrary.sln') --locked-mode
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed with exit code $LASTEXITCODE." }

& dotnet publish (Join-Path $repoRoot 'src/OgmaLibrary.App/OgmaLibrary.App.csproj') `
    --configuration Release `
    --no-restore `
    --runtime $runtimeIdentifier `
    --self-contained true `
    --output $publishDirectory `
    -p:PublishTrimmed=false `
    -p:PublishSingleFile=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:Version=$Version `
    -p:InformationalVersion=$Version
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }

if (Test-Path $artifactPath) { Remove-Item -LiteralPath $artifactPath -Force }
Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $artifactPath -CompressionLevel Optimal
$artifactSha256 = (Get-FileHash -LiteralPath $artifactPath -Algorithm SHA256).Hash.ToLowerInvariant()

# Keep this JSON compact and property-ordered. The exact bytes are the signed
# payload; consumers must not parse and reserialise it before verification.
$descriptorJson = '{' +
    "`"schema`":`"ogma-release-v1`",`"releaseId`":`"$releaseId`",`"version`":`"$Version`",`"platform`":`"$Platform`",`"runtimeIdentifier`":`"$runtimeIdentifier`",`"artifactName`":`"$artifactName`",`"artifactSha256`":`"$artifactSha256`",`"signatureAlgorithm`":`"RSA-PSS-SHA256`",`"publicKeyId`":`"$PublicKeyId`"" +
    '}'
[IO.File]::WriteAllText($descriptorPath, $descriptorJson, [Text.UTF8Encoding]::new($false))

if ($SigningKeyPath) {
    if (-not (Test-Path -LiteralPath $SigningKeyPath -PathType Leaf)) { throw 'SigningKeyPath does not exist.' }
    $openssl = Get-Command openssl -ErrorAction SilentlyContinue
    if (-not $openssl) { throw 'openssl is required when SigningKeyPath is supplied.' }
    $rawSignaturePath = Join-Path $candidateRoot 'release-descriptor.sig.raw'
    & $openssl.Source dgst -sha256 -sign $SigningKeyPath -sigopt rsa_padding_mode:pss -sigopt rsa_pss_saltlen:-1 -out $rawSignaturePath $descriptorPath
    if ($LASTEXITCODE -ne 0) { throw "Descriptor signing failed with exit code $LASTEXITCODE." }
    [Convert]::ToBase64String([IO.File]::ReadAllBytes($rawSignaturePath)) | Set-Content -LiteralPath $signaturePath -NoNewline
    Remove-Item -LiteralPath $rawSignaturePath -Force
} elseif ($RequireSignature) {
    throw 'A signing key is required for this release candidate. No private key is stored in the repository.'
}

& (Join-Path $PSScriptRoot 'Test-ReleaseCandidate.ps1') `
    -DescriptorPath $descriptorPath `
    -ArtifactPath $artifactPath `
    -SignaturePath $(if (Test-Path $signaturePath) { $signaturePath } else { $null }) `
    -RequireSignature:$RequireSignature
if ($LASTEXITCODE -ne 0) { throw "Release-candidate verification failed with exit code $LASTEXITCODE." }

Write-Output "Release candidate created: $artifactPath"
Write-Output "Descriptor: $descriptorPath"
Write-Output "Artifact SHA-256: $artifactSha256"
