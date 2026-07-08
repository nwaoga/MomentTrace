Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = "Stop"

function Get-WindowElement {
    param(
        [string]$Name,
        [int]$TimeoutSeconds = 20
    )

    $root = [System.Windows.Automation.AutomationElement]::RootElement
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $condition = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty,
            $Name)
        $window = $root.FindFirst([System.Windows.Automation.TreeScope]::Children, $condition)
        if ($window) {
            return $window
        }

        Start-Sleep -Milliseconds 300
    }

    throw "Window '$Name' not found."
}

function Find-DescendantByAutomationId {
    param(
        [System.Windows.Automation.AutomationElement]$Root,
        [string]$AutomationId
    )

    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        $AutomationId)
    return $Root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

function Invoke-Element {
    param([System.Windows.Automation.AutomationElement]$Element)

    if (-not $Element) {
        throw "Cannot invoke a null automation element."
    }

    $invokePattern = $Element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $invokePattern.Invoke()
    Start-Sleep -Milliseconds 500
}

function Capture-ElementScreenshot {
    param(
        [System.Windows.Automation.AutomationElement]$Element,
        [string]$Path
    )

    $bounds = $Element.Current.BoundingRectangle
    if ($bounds.Width -le 0 -or $bounds.Height -le 0) {
        throw "Element has invalid bounds for screenshot capture."
    }

    $width = [int][Math]::Ceiling($bounds.Width)
    $height = [int][Math]::Ceiling($bounds.Height)
    $bitmap = New-Object System.Drawing.Bitmap($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.CopyFromScreen([int]$bounds.Left, [int]$bounds.Top, 0, 0, $bitmap.Size)
    $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
}

function Stop-MomentTraceProcesses {
    Get-Process MomentTrace.Desktop -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 700
}

function Start-MomentTraceWithAppData {
    param(
        [string]$AppDataRoot,
        [string]$ExePath
    )

    $process = Start-Process -FilePath $ExePath -WorkingDirectory (Split-Path $ExePath) -PassThru -WindowStyle Normal -Environment @{
        APPDATA = $AppDataRoot
    }

    return $process
}

function Write-SettingsFile {
    param(
        [string]$SettingsPath,
        [string]$ThemeKey
    )

    $payload = [ordered]@{
        Opacity = 1.0
        TimelineViewMode = "Cards"
        Theme = $ThemeKey
        ExcludeAdultWebsites = $true
        AdultBlocklistDomains = @(
            "pornhub.com",
            "xvideos.com",
            "xnxx.com",
            "xhamster.com",
            "redtube.com",
            "youporn.com",
            "tube8.com",
            "spankbang.com",
            "beeg.com",
            "eporner.com"
        )
        CustomBlockedDomains = @()
        CustomAllowedDomains = @()
        CustomBlockedApps = @()
        AdultBlocklistUpdatedAt = $null
    }

    $json = $payload | ConvertTo-Json -Depth 4
    Set-Content -Path $SettingsPath -Value $json -Encoding UTF8
}

$themes = @(
    @{ Key = "Midnight"; Display = "Midnight" },
    @{ Key = "StarbucksReserve"; Display = "Starbucks Reserve" },
    @{ Key = "NeroRoast"; Display = "Nero Roast" },
    @{ Key = "CostaAfterglow"; Display = "Costa Afterglow" },
    @{ Key = "EspressoMono"; Display = "Espresso (Mono)" },
    @{ Key = "Light"; Display = "Hot White" }
)

$outputDirectory = Join-Path $PSScriptRoot "theme-qa-output"
$qaAppDataRoot = Join-Path $outputDirectory "appdata-root"
$qaMomentTraceRoot = Join-Path $qaAppDataRoot "MomentTrace"
$qaDatabasePath = Join-Path $qaMomentTraceRoot "timeline.db"
$settingsPath = Join-Path $qaMomentTraceRoot "settings.json"
$realDatabasePath = Join-Path (Join-Path $env:APPDATA "MomentTrace") "timeline.db"
$exePath = Join-Path $PSScriptRoot "..\\MomentTrace.Desktop\\bin\\Debug\\net8.0-windows\\MomentTrace.Desktop.exe"

New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $qaMomentTraceRoot -Force | Out-Null
Copy-Item -Path $realDatabasePath -Destination $qaDatabasePath -Force

$results = @()

foreach ($theme in $themes) {
    Write-SettingsFile -SettingsPath $settingsPath -ThemeKey $theme.Key
    Stop-MomentTraceProcesses
    $process = Start-MomentTraceWithAppData -AppDataRoot $qaAppDataRoot -ExePath $exePath
    $mainWindow = Get-WindowElement -Name "MomentTrace"
    Start-Sleep -Milliseconds 1500

    $timelineButton = Find-DescendantByAutomationId -Root $mainWindow -AutomationId "TimelineNavButton"
    $overviewButton = Find-DescendantByAutomationId -Root $mainWindow -AutomationId "OverviewNavButton"
    $savedButton = Find-DescendantByAutomationId -Root $mainWindow -AutomationId "SavedMomentsNavButton"

    Invoke-Element -Element $timelineButton
    $timelinePath = Join-Path $outputDirectory "$($theme.Key)-timeline.png"
    Capture-ElementScreenshot -Element $mainWindow -Path $timelinePath

    Invoke-Element -Element $overviewButton
    Start-Sleep -Milliseconds 900
    $overviewPath = Join-Path $outputDirectory "$($theme.Key)-overview.png"
    Capture-ElementScreenshot -Element $mainWindow -Path $overviewPath

    Invoke-Element -Element $savedButton
    Start-Sleep -Milliseconds 700
    $savedPath = Join-Path $outputDirectory "$($theme.Key)-saved.png"
    Capture-ElementScreenshot -Element $mainWindow -Path $savedPath

    $results += [pscustomobject]@{
        Theme = $theme.Display
        Timeline = $timelinePath
        Overview = $overviewPath
        Saved = $savedPath
    }

    if (-not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        Start-Sleep -Milliseconds 700
    }
}

Stop-MomentTraceProcesses
$results | ConvertTo-Json -Depth 4
