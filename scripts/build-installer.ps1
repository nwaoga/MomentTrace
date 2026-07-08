param(
    [string]$Version = "0.1.0",
    [string]$AssemblyVersion = "",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$PublishDir = "",
    [string]$OutputDir = "",
    [string]$CertificateThumbprint = "",
    [string]$TimestampUrl = "http://timestamp.digicert.com",
    [ValidateSet("CurrentUser", "LocalMachine")]
    [string]$CertificateStore = "CurrentUser",
    [string]$SignToolPath = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "MomentTrace.App\MomentTrace.App.csproj"
$installerScript = Join-Path $repoRoot "installer\MomentTrace.iss"
$signScript = Join-Path $repoRoot "scripts\sign-windows-artifacts.ps1"
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) "MomentTraceInstaller"
$stagingOutputDir = Join-Path $tempRoot $Version
$installerFileName = "MomentTrace-$Version-win-x64-setup.exe"

if ([string]::IsNullOrWhiteSpace($AssemblyVersion)) {
    if ($Version -match '^(\d+\.\d+\.\d+)') {
        $AssemblyVersion = $Matches[1]
    }
    else {
        throw "Version must start with MAJOR.MINOR.PATCH when AssemblyVersion is not supplied."
    }
}

if ($AssemblyVersion -notmatch '^\d+\.\d+\.\d+$') {
    throw "AssemblyVersion must use MAJOR.MINOR.PATCH format."
}

$assemblyVersionValue = "$AssemblyVersion.0"

function Resolve-InstallerPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Path))
}

if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $PublishDir = Join-Path $repoRoot "publish\$Runtime"
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot "dist"
}

$PublishDir = Resolve-InstallerPath -Path $PublishDir
$OutputDir = Resolve-InstallerPath -Path $OutputDir

New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

if (Test-Path $stagingOutputDir) {
    $resolvedTempRoot = [System.IO.Path]::GetFullPath($tempRoot)
    $resolvedStagingOutputDir = [System.IO.Path]::GetFullPath($stagingOutputDir)
    if (-not $resolvedStagingOutputDir.StartsWith($resolvedTempRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean installer staging directory outside the temp root: $resolvedStagingOutputDir"
    }

    Remove-Item -LiteralPath $stagingOutputDir -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $stagingOutputDir | Out-Null

& dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:Version=$AssemblyVersion `
    -p:AssemblyVersion=$assemblyVersionValue `
    -p:FileVersion=$assemblyVersionValue `
    -p:InformationalVersion="v$Version" `
    --output $PublishDir `
    -m:1 `
    -nr:false

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$publishedExePath = Join-Path $PublishDir "MomentTrace.App.exe"
if (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    & $signScript `
        -Path $publishedExePath `
        -CertificateThumbprint $CertificateThumbprint `
        -TimestampUrl $TimestampUrl `
        -CertificateStore $CertificateStore `
        -SignToolPath $SignToolPath
}

$iscc = Get-Command iscc -ErrorAction SilentlyContinue
$isccPath = if ($null -ne $iscc) { $iscc.Source } else { $null }
if ($null -eq $isccPath) {
    $candidatePaths = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )

    foreach ($candidatePath in $candidatePaths) {
        if (Test-Path $candidatePath) {
            $isccPath = $candidatePath
            break
        }
    }
}

if ($null -eq $isccPath) {
    throw "Inno Setup compiler was not found. Install Inno Setup 6, then rerun this script."
}

& $isccPath `
    "/DAppVersion=$Version" `
    "/DSourceDir=$PublishDir" `
    "/O$stagingOutputDir" `
    $installerScript

if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup failed with exit code $LASTEXITCODE."
}

$stagedInstallerPath = Join-Path $stagingOutputDir $installerFileName
if (-not (Test-Path $stagedInstallerPath)) {
    throw "Installer build completed but expected output was not found: $stagedInstallerPath"
}

$finalInstallerPath = Join-Path $OutputDir $installerFileName
Copy-Item -LiteralPath $stagedInstallerPath -Destination $finalInstallerPath -Force

if (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    & $signScript `
        -Path $finalInstallerPath `
        -CertificateThumbprint $CertificateThumbprint `
        -TimestampUrl $TimestampUrl `
        -CertificateStore $CertificateStore `
        -SignToolPath $SignToolPath
}

Write-Host "Installer created: $finalInstallerPath"
