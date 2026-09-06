# Canva Stage

Canva Stage is a Windows desktop confidence-monitor app for churches using Canva presentations. It gives people on stage a separate, readable output while the congregation continues to see the regular Canva presentation.

> **Project status:** `v0.5.1` adds automatic Canva Presenter tracking and a compact operator view to the completed Windows Phase 2 workflow. Manual page controls remain available as a dependable fallback.

## What it will do

- Connect to a church-managed Canva integration and choose a presentation.
- Open a separate confidence-output window, including a laptop-friendly Preview mode.
- Send the output to a selected confidence display when a second screen is available.
- Switch immediately between **Mirror** mode (the retrieved Canva page image) and **Monitor** mode (a simplified, high-visibility view of slide text).
- Automatically follow the visible page number in Canva Presenter View through Windows accessibility, with manual page controls always available.
- Collapse to a compact, pinnable operator view with a live mode-accurate preview for single-screen operation.

## Who it is for

Canva Stage is intended for a church operator, worship leader, musician, presenter, or speaker who needs a dependable on-stage confidence screen. It is designed for a calm Sunday workflow:

1. Configure Canva.
2. Choose the presentation and displays.
3. Test the output in Preview.
4. Start the confidence output.

You can set up and test the app with one display. For an independent on-stage display, Windows must see the screens as **extended** displays.

## How the planned app works

The first release is a native Windows 11 WPF app. CanvaStage watches for a Canva Presenter page counter exposed through Windows accessibility and follows it only when its total matches the applied presentation. It does not capture the screen or inspect browser traffic.

### Output modes

- **Mirror:** Shows the selected presentation page content.
- **Monitor:** Shows large, high-contrast current-slide text, with optional next-line and clock information. If usable text is not available, it falls back to the slide image.

## Canva account and privacy

Each church supplies and authorizes its own Canva integration. The app stores the Client Secret, access token, and refresh token locally in Windows DPAPI storage for the shared Windows operator account. They are never committed to this repository, logs, GitHub Actions artifacts, or release downloads. The operator UI shows only the Client ID after initial setup. Canva diagnostics redact credentials, tokens, OAuth codes, and authorization headers.

The app’s client-only model is a deliberate church-managed approach. Canva recommends a secure backend for client-secret handling and token exchange, so do not describe this desktop-only approach as Canva’s preferred production security model.

### Canva setup

1. Enable MFA on the church Canva account and create an integration in the [Canva Developer Portal](https://www.canva.dev/docs/connect/creating-integrations/).
2. Enable only `design:meta:read` and `design:content:read`.
3. Copy CanvaStage’s displayed loopback redirect URL into Canva’s **Authorized redirects**. The current value is `http://127.0.0.1:27843/callback/`; Canva permits `127.0.0.1` for local development, not `localhost`.
4. Enter the Client ID and Client Secret in CanvaStage, then select **Connect Canva** and finish authorization in the browser.
5. Refresh presentations, select one, and choose **Apply**. CanvaStage exports a temporary PPTX to read native text. It downloads a page thumbnail only when a page has no usable text.

CanvaStage does not capture the primary display. It follows Canva Presenter only when Windows exposes a matching page counter; otherwise the operator uses the manual page controls. Hidden slides are removed from the local presentation during import so visible-page numbering matches Presenter mode. Canva exports and page images are retrieved only as needed for the selected presentation; export URLs are short-lived.

### Closing the output window

Press `Escape`, or hover over the top-right corner of the confidence-output window to reveal the close button. Closing the output blanks it and returns to the operator controls.

### If the app does not open

CanvaStage records startup and unexpected-error details in `%LOCALAPPDATA%\CanvaStage\canvastage.log`. The log deliberately redacts Canva credentials, tokens, authorization codes, and authorization headers. Send that log with the Windows version and the release version when reporting a problem; do not send screenshots of Canva setup fields.

## Development

The implementation target is a self-contained Windows x64 release, so volunteers should not need a separately installed .NET runtime.

On Windows, double-click `Build Windows App.cmd`, or use:

```powershell
dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build
dotnet publish src/CanvaStage.Windows/CanvaStage.Windows.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

The GitHub Actions workflow automatically discovers the solution and WPF application project. It builds and tests pull requests and `main`, launches the published executable in a short smoke-test mode, uploads a self-contained Windows x64 artifact, and attaches that artifact to a GitHub Release for version tags such as `v0.2.1`.

The portable release ZIP includes a SHA-256 checksum. The app runs without requiring administrator elevation and stores mutable state under the shared Windows operator's local app-data directory.

## Repository contents

- `CanvaStage-Codex-Handoff-Updated.md` — full product requirements and phased delivery plan.
- `assets/` — Canva Stage logos and interface mockups.
- `.github/workflows/windows-release.yml` — Windows build, test, artifact, and tagged-release automation.

## Planned release process

1. Merge a tested change to `main`.
2. Create and push an annotated version tag, for example `v0.1.0`.
3. GitHub builds, tests, and publishes a self-contained Windows x64 package.
4. The workflow creates or updates the matching GitHub Release and attaches the package.

Code signing, an installer, and automatic updates are planned future enhancements.
