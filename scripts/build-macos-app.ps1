param(
    [string]$Version = "0.1.0",
    [string]$AssemblyVersion = "",
    [string]$Configuration = "Release",
    [string]$Runtime = "osx-arm64",
    [string]$Framework = "net8.0",
    [string]$PublishDir = "",
    [string]$OutputDir = "",
    [string]$CodeSignIdentity = "-",
    [string]$BuildNumber = "",
    [string]$CommitSha = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "MomentTrace.Desktop\MomentTrace.Desktop.csproj"
$appName = "MomentTrace"
$executableName = "MomentTrace.Desktop"

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

if ([string]::IsNullOrWhiteSpace($BuildNumber) -and -not [string]::IsNullOrWhiteSpace($env:GITHUB_RUN_NUMBER)) {
    $BuildNumber = $env:GITHUB_RUN_NUMBER
    if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_RUN_ATTEMPT)) {
        $BuildNumber = "$BuildNumber.$env:GITHUB_RUN_ATTEMPT"
    }
}

if ([string]::IsNullOrWhiteSpace($CommitSha) -and -not [string]::IsNullOrWhiteSpace($env:GITHUB_SHA)) {
    $CommitSha = $env:GITHUB_SHA
}

function Resolve-MacBuildPath {
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

$PublishDir = Resolve-MacBuildPath -Path $PublishDir
$OutputDir = Resolve-MacBuildPath -Path $OutputDir
$bundlePath = Join-Path $OutputDir "$appName.app"
$contentsPath = Join-Path $bundlePath "Contents"
$macOsPath = Join-Path $contentsPath "MacOS"
$resourcesPath = Join-Path $contentsPath "Resources"
$zipPath = Join-Path $OutputDir "MomentTrace-$Version-macos-arm64.zip"
$iconSourcePath = Join-Path $repoRoot "MomentTrace.App\Assets\MomentTrace.png"
$iconSetPath = Join-Path $OutputDir "MomentTrace.iconset"
$icnsPath = Join-Path $resourcesPath "MomentTrace.icns"

New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

if (Test-Path -LiteralPath $bundlePath) {
    Remove-Item -LiteralPath $bundlePath -Recurse -Force
}

if (Test-Path -LiteralPath $iconSetPath) {
    Remove-Item -LiteralPath $iconSetPath -Recurse -Force
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

& dotnet publish $projectPath `
    --configuration $Configuration `
    --framework $Framework `
    --runtime $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:Version=$AssemblyVersion `
    -p:AssemblyVersion=$assemblyVersionValue `
    -p:FileVersion=$assemblyVersionValue `
    -p:InformationalVersion="v$Version" `
    -p:MomentTraceBuildNumber=$BuildNumber `
    -p:MomentTraceCommitSha=$CommitSha `
    --output $PublishDir `
    -m:1 `
    -nr:false

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

New-Item -ItemType Directory -Force -Path $macOsPath | Out-Null
New-Item -ItemType Directory -Force -Path $resourcesPath | Out-Null
Copy-Item -Path (Join-Path $PublishDir "*") -Destination $macOsPath -Recurse -Force

if (-not (Test-Path -LiteralPath $iconSourcePath)) {
    throw "macOS icon source was not found: $iconSourcePath"
}

New-Item -ItemType Directory -Force -Path $iconSetPath | Out-Null
$iconSizes = @(
    @{ Size = 16; Name = "icon_16x16.png" },
    @{ Size = 32; Name = "icon_16x16@2x.png" },
    @{ Size = 32; Name = "icon_32x32.png" },
    @{ Size = 64; Name = "icon_32x32@2x.png" },
    @{ Size = 128; Name = "icon_128x128.png" },
    @{ Size = 256; Name = "icon_128x128@2x.png" },
    @{ Size = 256; Name = "icon_256x256.png" },
    @{ Size = 512; Name = "icon_256x256@2x.png" },
    @{ Size = 512; Name = "icon_512x512.png" },
    @{ Size = 1024; Name = "icon_512x512@2x.png" }
)

foreach ($iconSize in $iconSizes) {
    $iconDestination = Join-Path $iconSetPath $iconSize.Name
    & sips --resampleHeightWidth $iconSize.Size $iconSize.Size $iconSourcePath --out $iconDestination | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "sips failed while creating $iconDestination."
    }
}

& iconutil --convert icns $iconSetPath --output $icnsPath
if ($LASTEXITCODE -ne 0) {
    throw "iconutil failed while creating $icnsPath."
}

$publishedExecutable = Join-Path $macOsPath $executableName
if (-not (Test-Path -LiteralPath $publishedExecutable)) {
    throw "Published executable was not found: $publishedExecutable"
}

& chmod +x $publishedExecutable
if ($LASTEXITCODE -ne 0) {
    throw "chmod failed for $publishedExecutable."
}

$infoPlistPath = Join-Path $contentsPath "Info.plist"
$infoPlist = @"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>en</string>
  <key>CFBundleExecutable</key>
  <string>$executableName</string>
  <key>CFBundleIdentifier</key>
  <string>com.momenttrace.app</string>
  <key>CFBundleName</key>
  <string>$appName</string>
  <key>CFBundleDisplayName</key>
  <string>$appName</string>
  <key>CFBundleIconFile</key>
  <string>MomentTrace.icns</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>$AssemblyVersion</string>
  <key>CFBundleVersion</key>
  <string>$AssemblyVersion</string>
  <key>LSMinimumSystemVersion</key>
  <string>13.0</string>
  <key>NSAppleEventsUsageDescription</key>
  <string>MomentTrace uses Apple Events to capture supported browser URLs for your local timeline.</string>
  <key>NSHighResolutionCapable</key>
  <true/>
</dict>
</plist>
"@

Set-Content -LiteralPath $infoPlistPath -Value $infoPlist -Encoding UTF8

$appExecutableFile = Get-Item -LiteralPath $publishedExecutable
$macOsFiles = Get-ChildItem -LiteralPath $macOsPath -File |
    Where-Object { $_.FullName -ne $appExecutableFile.FullName } |
    Sort-Object FullName

foreach ($macOsFile in $macOsFiles + $appExecutableFile) {
    & codesign --force --sign $CodeSignIdentity $macOsFile.FullName
    if ($LASTEXITCODE -ne 0) {
        throw "codesign failed for $($macOsFile.FullName)."
    }
}

& codesign --force --deep --sign $CodeSignIdentity $bundlePath
if ($LASTEXITCODE -ne 0) {
    throw "codesign failed for $bundlePath."
}

& ditto -c -k --sequesterRsrc --keepParent $bundlePath $zipPath
if ($LASTEXITCODE -ne 0) {
    throw "ditto failed while creating $zipPath."
}

Write-Host "macOS app bundle created: $bundlePath"
Write-Host "macOS zip created: $zipPath"
