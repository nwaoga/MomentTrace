# macOS Packaging, Signing, And Notarization Plan

This plan covers the release path for `MomentTrace.Desktop` on macOS. The app should ship as a signed `.app` bundle inside a notarized DMG, with the macOS recorder hosted by `MomentTrace.Mac`.

## Target Artifacts

- `MomentTrace.app`: self-contained macOS app bundle for Apple Silicon and Intel builds.
- `MomentTrace-<version>-macos-arm64.zip`: ad-hoc signed Apple Silicon app bundle zip for beta releases.
- `MomentTrace-<version>-macos-arm64.dmg`: signed and notarized Apple Silicon installer image for the later public release path.
- `MomentTrace-<version>-macos-x64.dmg`: signed and notarized Intel installer image, if Intel support is kept.
- Checksums for each published artifact.

## Required Apple Assets

- Active Apple Developer Program membership.
- Developer ID Application certificate for signing `MomentTrace.app`.
- Developer ID Installer certificate only if a future `.pkg` installer is added.
- App Store Connect API key for notarization automation:
  - issuer ID
  - key ID
  - private key `.p8`
- Team ID.

Keep certificates and notarization credentials in the release system secret store. Do not commit certificates, exported private keys, API keys, provisioning material, or keychain passwords.

## Bundle Shape

Publish the Avalonia desktop app for each runtime:

```bash
dotnet publish ./MomentTrace.Desktop/MomentTrace.Desktop.csproj \
  -c Release \
  -f net8.0 \
  -r osx-arm64 \
  --self-contained true \
  -p:PublishSingleFile=false
```

Create `MomentTrace.app` with:

```text
MomentTrace.app/
  Contents/
    Info.plist
    MacOS/MomentTrace.Desktop
    Resources/MomentTrace.icns
    Resources/
```

The bundle identifier should be stable, for example `com.momenttrace.app`, and must match signing/notarization records.

## Info.plist Requirements

Include at minimum:

- `CFBundleIdentifier`
- `CFBundleName`
- `CFBundleDisplayName`
- `CFBundleExecutable`
- `CFBundleVersion`
- `CFBundleShortVersionString`
- `CFBundleIconFile`
- `LSMinimumSystemVersion`
- `NSAppleEventsUsageDescription`
- `NSAppleScriptEnabled`
- `NSCameraUsageDescription` only if future capture features require it
- `NSMicrophoneUsageDescription` only if future capture features require it

MomentTrace currently needs clear permission copy for Accessibility-guided metadata, Apple Events/browser URL capture, and Screen Recording screenshots. Accessibility and Screen Recording are largely user-approved in System Settings rather than controlled only by `Info.plist`, so first-run onboarding should point users to the right privacy panes.

## Signing

Beta builds should at least ad-hoc sign the published files inside the generated `.app` before zipping, sign the app executable after its sibling assemblies, then sign the app bundle so macOS sees a coherent bundle signature instead of a damaged app. Public builds should sign nested native/dylib content first if present, then sign the app bundle with hardened runtime enabled:

```bash
find "MomentTrace.app/Contents/MacOS" -maxdepth 1 -type f ! -name "MomentTrace.Desktop" -exec codesign --force --sign - {} \;
codesign --force --sign - "MomentTrace.app/Contents/MacOS/MomentTrace.Desktop"
codesign --force --deep --sign - "MomentTrace.app"
```

```bash
codesign --force --timestamp --options runtime \
  --sign "Developer ID Application: <Company Name> (<TEAMID>)" \
  "MomentTrace.app"
```

Verify:

```bash
codesign --verify --deep --strict --verbose=2 "MomentTrace.app"
spctl --assess --type execute --verbose=4 "MomentTrace.app"
```

Ad-hoc signed beta zips are still not Developer ID signed or notarized, so Gatekeeper can require right-click > Open. A failed `codesign --verify --deep --strict` check is not acceptable for release artifacts, even for unsigned/internal beta builds.

Start with no special entitlements. Add an entitlements file only when a concrete macOS API requires it.

## DMG Creation

Create a polished DMG containing:

- `MomentTrace.app`
- `/Applications` symlink
- optional background image and layout metadata later

Use a repeatable tool such as `create-dmg` or a scripted `hdiutil` flow. Sign the DMG after creation:

```bash
codesign --force --timestamp \
  --sign "Developer ID Application: <Company Name> (<TEAMID>)" \
  "MomentTrace-<version>-macos-arm64.dmg"
```

## Notarization

Submit the signed DMG:

```bash
xcrun notarytool submit "MomentTrace-<version>-macos-arm64.dmg" \
  --key "<path-to-api-key.p8>" \
  --key-id "<KEYID>" \
  --issuer "<ISSUERID>" \
  --wait
```

Staple and verify:

```bash
xcrun stapler staple "MomentTrace-<version>-macos-arm64.dmg"
xcrun stapler validate "MomentTrace-<version>-macos-arm64.dmg"
spctl --assess --type open --verbose=4 "MomentTrace-<version>-macos-arm64.dmg"
```

## Release Pipeline Outline

1. Run tests and build validation on Windows.
2. Run macOS publish on a macOS runner for `osx-arm64`.
3. Generate `MomentTrace.app` with versioned `Info.plist`.
4. Sign the app bundle with hardened runtime.
5. Verify the app signature locally on the runner.
6. Create and sign the DMG.
7. Submit the DMG for notarization.
8. Staple notarization ticket to the DMG.
9. Verify Gatekeeper assessment.
10. Generate checksums.
11. Publish artifacts to the release.

## Manual Acceptance Checklist

- Fresh Mac download opens without an unidentified-developer block.
- Gatekeeper shows the expected verified developer identity.
- The app launches from `/Applications`.
- First-run onboarding appears.
- macOS privacy prompts or setup instructions are understandable.
- Timeline rows are created under `~/Library/Application Support/MomentTrace/timeline.db`.
- Screenshot and thumbnail capture works after Screen Recording permission is granted.
- Browser URLs are captured for supported browsers after Automation permission is granted.

## Open Implementation Tasks

- Add Developer ID signing and notarized DMG generation once Apple Developer credentials are available.
- Add an `.icns` asset derived from the current app icon.
- Decide whether to ship universal, separate `arm64`/`x64`, or `arm64`-only artifacts.
- Add a macOS release workflow once Apple Developer credentials are available.
- Add first-run permission deep links or copy for Accessibility, Screen Recording, and Automation.
