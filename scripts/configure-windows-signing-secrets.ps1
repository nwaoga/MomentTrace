param(
    [Parameter(Mandatory = $true)]
    [string]$PfxPath,

    [Parameter(Mandatory = $true)]
    [securestring]$PfxPassword,

    [string]$Repository = "nwaoga/MomentTrace",

    [switch]$SkipSecretUpload
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $PfxPath)) {
    throw "PFX file was not found: $PfxPath"
}

$resolvedPfxPath = (Resolve-Path -LiteralPath $PfxPath).Path
$cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2
$cert.Import(
    $resolvedPfxPath,
    $PfxPassword,
    [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet)

if (-not $cert.HasPrivateKey) {
    throw "PFX does not include a private key and cannot be used for code signing."
}

$codeSigningOid = "1.3.6.1.5.5.7.3.3"
$hasCodeSigningEku = $false
foreach ($extension in $cert.Extensions) {
    if ($extension -is [System.Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]) {
        foreach ($usage in $extension.EnhancedKeyUsages) {
            if ($usage.Value -eq $codeSigningOid) {
                $hasCodeSigningEku = $true
                break
            }
        }
    }
}

if (-not $hasCodeSigningEku) {
    throw "Certificate is missing the Code Signing enhanced key usage ($codeSigningOid)."
}

if ($cert.NotAfter -le (Get-Date)) {
    throw "Certificate is expired: $($cert.NotAfter.ToString('u'))"
}

$plainPassword = [System.Net.NetworkCredential]::new("", $PfxPassword).Password
try {
    $pfxBase64 = [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($resolvedPfxPath))

    if (-not $SkipSecretUpload) {
        $gh = Get-Command gh -ErrorAction SilentlyContinue
        if ($null -eq $gh) {
            throw "GitHub CLI was not found. Install gh or rerun with -SkipSecretUpload."
        }

        $pfxBase64 | gh secret set WINDOWS_SIGNING_PFX_BASE64 --repo $Repository --body-file -
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to set WINDOWS_SIGNING_PFX_BASE64."
        }

        $plainPassword | gh secret set WINDOWS_SIGNING_PFX_PASSWORD --repo $Repository --body-file -
        if ($LASTEXITCODE -ne 0) {
            throw "Failed to set WINDOWS_SIGNING_PFX_PASSWORD."
        }
    }

    [pscustomobject]@{
        Repository = $Repository
        Uploaded = -not $SkipSecretUpload
        Subject = $cert.Subject
        Thumbprint = $cert.Thumbprint
        NotAfter = $cert.NotAfter.ToString("u")
    }
}
finally {
    if ($null -ne $plainPassword) {
        $plainPassword = $null
    }

    if ($null -ne $pfxBase64) {
        $pfxBase64 = $null
    }

    $cert.Dispose()
}
