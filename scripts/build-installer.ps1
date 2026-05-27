param(
    [string]$Version = "0.1.0",
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$PublishDir = "",
    [string]$OutputDir = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "MomentTrace.App\MomentTrace.App.csproj"
$installerScript = Join-Path $repoRoot "installer\MomentTrace.iss"

if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $PublishDir = Join-Path $repoRoot "publish\$Runtime"
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $repoRoot "dist"
}

New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:Version=$Version `
    -p:AssemblyVersion="$Version.0" `
    -p:FileVersion="$Version.0" `
    -p:InformationalVersion="v$Version" `
    --output $PublishDir

$iscc = Get-Command iscc -ErrorAction SilentlyContinue
$isccPath = if ($null -ne $iscc) { $iscc.Source } else { $null }
if ($null -eq $isccPath) {
    $defaultIscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    if (Test-Path $defaultIscc) {
        $isccPath = $defaultIscc
    }
}

if ($null -eq $isccPath) {
    throw "Inno Setup compiler was not found. Install Inno Setup 6, then rerun this script."
}

& $isccPath `
    "/DAppVersion=$Version" `
    "/DSourceDir=$PublishDir" `
    "/O$OutputDir" `
    $installerScript
