param(
    [string]$Version = "0.5.0-beta.1",
    [string]$Configuration = "Release",
    [string]$OutputRoot = "artifacts"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$artifactRoot = Join-Path $repositoryRoot $OutputRoot
$packageName = "JFlightShaker-CE-v$Version-win-x64"
$publishDirectory = Join-Path $artifactRoot $packageName
$zipPath = Join-Path $artifactRoot "$packageName.zip"
$checksumPath = Join-Path $artifactRoot "SHA256SUMS.txt"

if (Test-Path -LiteralPath $publishDirectory) {
    throw "Output already exists: $publishDirectory. Move or remove it before rebuilding."
}
if (Test-Path -LiteralPath $zipPath) {
    throw "Output already exists: $zipPath. Move or remove it before rebuilding."
}

New-Item -ItemType Directory -Path $artifactRoot -Force | Out-Null

Push-Location $repositoryRoot
try {
    dotnet restore JFlightShaker.sln
    dotnet test JFlightShaker.sln -c $Configuration --no-restore
    dotnet publish JFlightShaker.csproj -c $Configuration --no-restore -o $publishDirectory

    Copy-Item README.md, CHANGELOG.md, LICENSE, THIRD-PARTY-NOTICES.md $publishDirectory
    Compress-Archive -Path (Join-Path $publishDirectory "*") -DestinationPath $zipPath

    $hash = (Get-FileHash $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash *$packageName.zip" | Set-Content $checksumPath -Encoding ascii

    Write-Host "Release package: $zipPath"
    Write-Host "Checksums:      $checksumPath"
}
finally {
    Pop-Location
}
