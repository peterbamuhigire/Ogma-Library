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
    [string] $PublicKeyPath,
    [switch] $RequireSignature,
    [switch] $RequirePlatformSigning,
    [string] $WindowsCertificateThumbprint,
    [string] $AppleSigningIdentity,
    [string] $NotaryProfile
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$runtimeIdentifier = if ($Platform -eq 'windows') { "win-$Architecture" } else { "osx-$Architecture" }
$releaseId = "rc-$Version-$runtimeIdentifier-$(git -C $repoRoot rev-parse --short=12 HEAD)"
$defaultOutput = Join-Path $repoRoot "artifacts/release-candidates/$Version/$runtimeIdentifier"
$candidateRoot = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $defaultOutput
} elseif ([IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory
} else {
    Join-Path $repoRoot $OutputDirectory
}

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

if ($RequirePlatformSigning -and $Platform -eq 'windows') {
    if ([string]::IsNullOrWhiteSpace($WindowsCertificateThumbprint)) {
        throw 'WindowsCertificateThumbprint is required for platform signing.'
    }
    $signtool = Get-Command signtool -ErrorAction SilentlyContinue
    if (-not $signtool) { throw 'signtool is required for Windows platform signing.' }
    $signTargets = @(Get-ChildItem -LiteralPath $publishDirectory -Recurse -File |
        Where-Object { $_.Extension -in '.exe', '.dll' })
    if ($signTargets.Count -eq 0) { throw 'No Windows PE files were found to sign.' }
    foreach ($target in $signTargets) {
        & $signtool.Source sign /sha1 $WindowsCertificateThumbprint /fd SHA256 /tr 'http://timestamp.digicert.com' /td SHA256 $target.FullName
        if ($LASTEXITCODE -ne 0) { throw "Authenticode signing failed for $($target.Name)." }
        & $signtool.Source verify /pa /all $target.FullName
        if ($LASTEXITCODE -ne 0) { throw "Authenticode verification failed for $($target.Name)." }
    }
} elseif ($RequirePlatformSigning -and $Platform -eq 'macos') {
    if ([string]::IsNullOrWhiteSpace($AppleSigningIdentity) -or [string]::IsNullOrWhiteSpace($NotaryProfile)) {
        throw 'AppleSigningIdentity and NotaryProfile are required for signed/notarized macOS candidates.'
    }
    $codesign = Get-Command codesign -ErrorAction SilentlyContinue
    $xcrun = Get-Command xcrun -ErrorAction SilentlyContinue
    if (-not $codesign -or -not $xcrun) { throw 'codesign and xcrun are required for macOS platform signing.' }
    $macExecutable = Get-ChildItem -LiteralPath $publishDirectory -File |
        Where-Object { $_.Extension -eq '' } |
        Select-Object -First 1
    if (-not $macExecutable) { throw 'No macOS application executable was found to bundle.' }
    $appBundle = Join-Path $publishDirectory 'OgmaLibrary.app'
    $contents = Join-Path $appBundle 'Contents'
    $macOsDirectory = Join-Path $contents 'MacOS'
    New-Item -ItemType Directory -Force -Path $macOsDirectory | Out-Null
    $entriesToBundle = @(Get-ChildItem -LiteralPath $publishDirectory)
    foreach ($entry in $entriesToBundle) { Move-Item -LiteralPath $entry.FullName -Destination (Join-Path $macOsDirectory $entry.Name) }
    $infoPlistPath = Join-Path $contents 'Info.plist'
    $infoPlist = Get-Content -LiteralPath (Join-Path $repoRoot 'packaging/macos/Info.plist') -Raw
    $infoPlist = $infoPlist.Replace('__OGMA_EXECUTABLE__', $macExecutable.Name, [StringComparison]::Ordinal)
    $infoPlist = $infoPlist.Replace('__OGMA_VERSION__', $Version, [StringComparison]::Ordinal)
    [IO.File]::WriteAllText($infoPlistPath, $infoPlist, [Text.UTF8Encoding]::new($false))
    & $codesign.Source --force --deep --options runtime --sign $AppleSigningIdentity $appBundle
    if ($LASTEXITCODE -ne 0) { throw 'Developer ID signing failed for the macOS application bundle.' }
    & $codesign.Source --verify --deep --strict $appBundle
    if ($LASTEXITCODE -ne 0) { throw 'Developer ID verification failed for the macOS application bundle.' }
    & $xcrun.Source notarytool submit $appBundle --keychain-profile $NotaryProfile --wait
    if ($LASTEXITCODE -ne 0) { throw 'Apple notarization failed.' }
    & $xcrun.Source stapler staple $appBundle
    if ($LASTEXITCODE -ne 0) { throw 'Apple notarization ticket stapling failed.' }
}

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

if ($RequireSignature -and [string]::IsNullOrWhiteSpace($PublicKeyPath)) {
    throw 'A protected public key is required when descriptor signature verification is required.'
}

& (Join-Path $PSScriptRoot 'Test-ReleaseCandidate.ps1') `
    -DescriptorPath $descriptorPath `
    -ArtifactPath $artifactPath `
    -SignaturePath $(if (Test-Path $signaturePath) { $signaturePath } else { $null }) `
    -PublicKeyPath $(if (Test-Path -LiteralPath $PublicKeyPath -PathType Leaf) { $PublicKeyPath } else { $null }) `
    -RequireSignature:$RequireSignature
if ($LASTEXITCODE -ne 0) { throw "Release-candidate verification failed with exit code $LASTEXITCODE." }

Write-Output "Release candidate created: $artifactPath"
Write-Output "Descriptor: $descriptorPath"
Write-Output "Artifact SHA-256: $artifactSha256"
