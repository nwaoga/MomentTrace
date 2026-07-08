param(
    [Parameter(Mandatory = $true)]
    [string[]]$Path,

    [Parameter(Mandatory = $true)]
    [string]$CertificateThumbprint,

    [string]$TimestampUrl = "http://timestamp.digicert.com",
    [ValidateSet("CurrentUser", "LocalMachine")]
    [string]$CertificateStore = "CurrentUser",
    [string]$SignToolPath = ""
)

$ErrorActionPreference = "Stop"

function Find-SignTool {
    param([string]$ExplicitPath)

    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath)) {
        if (-not (Test-Path -LiteralPath $ExplicitPath)) {
            throw "SignToolPath was provided but not found: $ExplicitPath"
        }

        return $ExplicitPath
    }

    $command = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($null -ne $command) {
        return $command.Source
    }

    $candidateRoots = @(
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin",
        "$env:ProgramFiles\Windows Kits\10\bin"
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and (Test-Path -LiteralPath $_) }

    foreach ($root in $candidateRoots) {
        $candidate = Get-ChildItem -LiteralPath $root -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
            Sort-Object FullName -Descending |
            Select-Object -First 1

        if ($null -ne $candidate) {
            return $candidate.FullName
        }
    }

    throw "signtool.exe was not found. Install the Windows SDK or provide -SignToolPath."
}

if ([string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    throw "CertificateThumbprint is required for signing."
}

$signTool = Find-SignTool -ExplicitPath $SignToolPath

foreach ($artifactPath in $Path) {
    if (-not (Test-Path -LiteralPath $artifactPath)) {
        throw "Artifact to sign was not found: $artifactPath"
    }

    $signArgs = @(
        "sign",
        "/fd", "SHA256",
        "/td", "SHA256",
        "/tr", $TimestampUrl,
        "/sha1", $CertificateThumbprint
    )

    if ($CertificateStore -eq "LocalMachine") {
        $signArgs += "/sm"
    }

    $signArgs += $artifactPath

    & $signTool @signArgs

    if ($LASTEXITCODE -ne 0) {
        throw "signtool failed for $artifactPath with exit code $LASTEXITCODE."
    }

    & $signTool verify /pa /v $artifactPath
    if ($LASTEXITCODE -ne 0) {
        throw "signature verification failed for $artifactPath with exit code $LASTEXITCODE."
    }
}
