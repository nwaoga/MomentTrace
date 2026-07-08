# Windows Code Signing And Verified Publisher Plan

MomentTrace Windows releases should be signed so SmartScreen and installer prompts show a verified publisher instead of an unknown publisher warning.

## Signing Model

Use an OV, EV, or trusted cloud-backed code signing certificate issued for the publishing entity. The release flow signs:

- `MomentTrace.Desktop.exe` after `dotnet publish`
- `MomentTrace-<version>-win-x64-setup.exe` after Inno Setup packaging

Portable zip contents inherit the signed executable.

## Required Secrets

For GitHub Actions, configure these repository or environment secrets:

- `WINDOWS_SIGNING_PFX_BASE64`: base64-encoded `.pfx` certificate export
- `WINDOWS_SIGNING_PFX_PASSWORD`: password for the `.pfx`

The workflow imports the PFX into the Windows runner certificate store for the release job, signs artifacts by thumbprint, and deletes the temporary PFX file immediately after import. Do not commit certificates, exported private keys, passwords, or thumbprint-specific local scripts.

## Local Signing

Install the Windows SDK so `signtool.exe` is available. If the certificate is in the current user's personal certificate store, build a signed installer with:

```powershell
.\scripts\build-installer.ps1 `
  -Version 0.1.0 `
  -CertificateThumbprint "<certificate-thumbprint>"
```

If the certificate is in the local machine store:

```powershell
.\scripts\build-installer.ps1 `
  -Version 0.1.0 `
  -CertificateThumbprint "<certificate-thumbprint>" `
  -CertificateStore LocalMachine
```

To sign existing artifacts directly:

```powershell
.\scripts\sign-windows-artifacts.ps1 `
  -Path .\publish\win-x64\MomentTrace.Desktop.exe, .\dist\MomentTrace-0.1.0-win-x64-setup.exe `
  -CertificateThumbprint "<certificate-thumbprint>"
```

## Verification

The signing script verifies each signed artifact with `signtool verify /pa /v`. Manual release QA should also check:

- File Properties > Digital Signatures shows the expected publisher.
- The installer UAC prompt shows the verified publisher name.
- Fresh install succeeds on a clean Windows machine.
- The installed app launches and the Start Menu shortcut points at the signed executable.

## Release Workflow

`.github/workflows/release.yml` now imports the signing certificate when both signing secrets are configured. If secrets are absent, the workflow still produces unsigned artifacts for internal test releases and writes a clear log message.

The release job:

1. Resolves the semantic version tag.
2. Imports the signing certificate when secrets exist.
3. Publishes the Windows app.
4. Packages the portable zip.
5. Builds the Inno Setup installer.
6. Signs `MomentTrace.Desktop.exe` and the setup executable when a certificate is available.
7. Uploads release artifacts.

Prerelease tags such as `v0.1.1-beta.1` are supported. The release workflow keeps assembly and file versions numeric, names artifacts with the prerelease label, and marks the GitHub Release as a prerelease.

## Certificate Handling Notes

- Prefer GitHub environment protection for production signing secrets.
- Rotate the PFX password if it is shared outside the release system.
- Consider Azure Trusted Signing or another HSM-backed provider before a public launch to avoid long-lived PFX handling.
- Keep timestamping enabled so signatures remain valid after certificate expiry.
