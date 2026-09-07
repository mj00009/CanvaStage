# Canva Stage

Canva Stage is a Windows desktop confidence-monitor app for churches using Canva presentations. It gives people on stage a separate, readable output while the congregation continues to see the regular Canva presentation.

> **Project status:** `v0.7.0` improves automatic Presenter matching, adds independent next-line styling, and gives the Windows application its CanvaStage icon. This release remains unsigned.

## What it will do

- Connect to a church-managed Canva integration and choose a presentation.
- Open a separate confidence-output window, including a laptop-friendly Preview mode.
- Send the output to a selected confidence display when a second screen is available.
- Switch immediately between **Mirror** mode (the retrieved Canva page image) and **Monitor** mode (a simplified, high-visibility view of slide text).
- Automatically follow the visible page number in Canva Presenter View through Windows accessibility, with sticky window selection and manual page controls always available.
- Collapse to a compact, pinnable operator view with a live mode-accurate preview for single-screen operation.

## Who it is for

Canva Stage is intended for a church operator, worship leader, musician, presenter, or speaker who needs a dependable on-stage confidence screen. It is designed for a calm Sunday workflow:

1. Configure Canva.
2. Choose the presentation and displays.
3. Test the output in Preview.
4. Start the confidence output.

You can set up and test the app with one display. For an independent on-stage display, Windows must see the screens as **extended** displays.

## How the planned app works

CanvaStage is a native Windows 11 WPF app. It reads the Canva Presenter page counter through Windows accessibility. Automatic matching uses the Canva design ID from the accessible document URL when available, otherwise an exact presentation title after removing known browser/Canva suffixes. If Canva exposes exactly one otherwise-unidentified window whose visible-page count matches the applied deck, CanvaStage follows that window as a compatibility fallback. It never uses page count to choose between multiple plausible windows. It does not capture the screen or inspect browser traffic.

CanvaStage keeps following the same native Presenter window if another matching Canva window appears during a service. If several plausible windows are already open and no automatic choice has been established, open **Setup & diagnostics**, select the Presenter window, and choose **Confirm selected window**. Confirm only the window showing the applied presentation. A known different design ID or visible-page count still blocks sync. The selection is session-only; a missing explicitly chosen window never causes an automatic switch to a different one. Title and page-count fallbacks are less definitive than a design ID, so give presentations distinct titles.

**Live control** and **Compact view** show the current sync state and a **Pause sync / Resume sync** button. Manual previous/next navigation pauses sync. Resume uses the latest fresh page reading, or waits for a new reading. Scanning retries automatically after errors, with delays up to 15 seconds; reopening Presenter View is detected without restarting CanvaStage. Turning off automatic sync in settings is saved across restarts; a temporary pause is not.

### Output modes

- **Mirror:** Shows the selected presentation page content.
- **Monitor:** Shows large, high-contrast current-slide text, with optional next-line and clock information. The next line has independent size, bold, and color controls. If usable text is not available, it falls back to the slide image.

## Canva account and privacy

Each church supplies and authorizes its own Canva integration. The app stores the Client Secret, access token, and refresh token locally in Windows DPAPI storage for the shared Windows operator account. They are never committed to this repository, logs, GitHub Actions artifacts, or release downloads. The operator UI shows only the Client ID after initial setup. Canva diagnostics redact credentials, tokens, OAuth codes, and authorization headers.

The app’s client-only model is a deliberate church-managed approach. Canva recommends a secure backend for client-secret handling and token exchange, so do not describe this desktop-only approach as Canva’s preferred production security model.

### Canva setup

1. Enable MFA on the church Canva account and create an integration in the [Canva Developer Portal](https://www.canva.dev/docs/connect/creating-integrations/).
2. Enable only `design:meta:read` and `design:content:read`.
3. Copy CanvaStage’s displayed loopback redirect URL into Canva’s **Authorized redirects**. The current value is `http://127.0.0.1:27843/callback/`; Canva permits `127.0.0.1` for local development, not `localhost`.
4. Enter the Client ID and Client Secret in CanvaStage and select **Save configuration**, then **Connect Canva** and finish authorization in the browser. On later launches, **Saved securely** confirms that the secret is already stored and reused. The masked indicator is not the secret itself. Use **Replace saved secret** only when changing credentials.
5. Refresh presentations, select one, and choose **Apply**. CanvaStage exports a temporary PPTX to read native text and downloads visible-page thumbnails for Mirror and image fallback. Each refresh uses a separate local cache folder so images currently on screen are never overwritten. Previous cache generations are retained; cache cleanup controls are future work.

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

The application now includes the CanvaStage icon in its Windows executable and windows. Public code signing still requires a validated publisher identity and signing service; see the release notes for whether a particular build is signed. An installer and automatic updates remain future enhancements.

## Release verification and Sunday testing

The Windows workflow runs an isolated, offline verification mode with synthetic content and credentials. It checks Presenter matching and pause/resume, DPAPI credential reuse, refreshing while an image is locked, cache restoration, core theme contrast, WPF binding errors, and measured output text fit. Screenshots cover all operator sections and Monitor layouts, compact controls, and effective layout sizes/raster DPI representing 100%, 125%, 150%, and 200% scaling.

These DPI scenarios simulate layout constraints and rendering; they do not change the runner's Windows display settings or test physical monitor transitions. Screenshots still need visual review. See [the Windows acceptance guide](docs/v0.6.0-acceptance.md) for real-machine checks and known limitations.
