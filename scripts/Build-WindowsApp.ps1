[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$logDirectory = Join-Path $projectRoot 'artifacts\logs'
$publishDirectory = Join-Path $projectRoot 'artifacts\CanvaStage-win-x64'
$zipPath = Join-Path $projectRoot "artifacts\CanvaStage-win-x64-$timestamp.zip"
$checksumPath = "$zipPath.sha256"

New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
$logPath = Join-Path $logDirectory "build-$timestamp.log"
Start-Transcript -Path $logPath -Force | Out-Null

try {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        Write-Host 'The .NET 10 SDK is required but was not found.' -ForegroundColor Yellow
        $install = Read-Host 'Open the official .NET download page now? (Y/N)'
        if ($install -match '^[Yy]') {
            Start-Process 'https://dotnet.microsoft.com/download/dotnet/10.0'
        }
        throw 'Install the .NET 10 SDK, then run this script again.'
    }

    Set-Location $projectRoot
    dotnet restore CanvaStage.sln
    if ($LASTEXITCODE -ne 0) { throw "Restore failed (exit code $LASTEXITCODE)." }
    dotnet build CanvaStage.sln --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Build failed (exit code $LASTEXITCODE)." }
    dotnet test CanvaStage.sln --configuration Release --no-build
    if ($LASTEXITCODE -ne 0) { throw "Tests failed (exit code $LASTEXITCODE)." }

    Remove-Item -Recurse -Force $publishDirectory -ErrorAction SilentlyContinue
    dotnet publish src\CanvaStage.Windows\CanvaStage.Windows.csproj `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        --output $publishDirectory
    if ($LASTEXITCODE -ne 0) { throw "Publish failed (exit code $LASTEXITCODE)." }

    Copy-Item (Join-Path $projectRoot 'LICENSE'), (Join-Path $projectRoot 'TRADEMARKS.md') -Destination $publishDirectory -ErrorAction Stop
    if (-not (Select-String -Path (Join-Path $publishDirectory 'LICENSE') -Pattern 'Apache License' -Quiet)) {
        throw 'The portable package is missing the Apache license text.'
    }

    Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
    Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $zipPath -Force
    $archive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        foreach ($requiredEntry in @('CanvaStage.Windows.exe', 'LICENSE', 'TRADEMARKS.md')) {
            if (-not $archive.GetEntry($requiredEntry)) {
                throw "The portable ZIP is missing $requiredEntry."
            }
        }
    }
    finally {
        $archive.Dispose()
    }
    (Get-FileHash -Path $zipPath -Algorithm SHA256).Hash | Set-Content -Path $checksumPath -NoNewline

    Write-Host "Build succeeded. Portable package: $zipPath" -ForegroundColor Green
    Write-Host "SHA-256 checksum: $checksumPath" -ForegroundColor Green
}
catch {
    Write-Error $_
    exit 1
}
finally {
    Stop-Transcript | Out-Null
    Write-Host "Log: $logPath"
}
