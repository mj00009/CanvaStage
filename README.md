# CanvaStage

CanvaStage is an independent Windows confidence-monitor application for presentations created in Canva. It gives speakers, musicians, and service leaders a separate, readable output while the audience continues to see the normal presentation.

CanvaStage is not affiliated with, endorsed by, or sponsored by Canva Pty Ltd. See [Licensing and trademarks](docs/licensing-and-trademarks.md).

## Download

The latest release is [CanvaStage v0.11.3](https://github.com/mj00009/CanvaStage/releases/tag/v0.11.3). See the [release notes](docs/releases/v0.11.3.md) for changes and suggested acceptance checks.

This update simplifies the operator workflow into Setup, Live control and Settings & Diagnostics. Compact view has no Pause Sync button. Every preview copies the actual output visual, sharing text wrapping, fitted size, colors, clock and next-line truncation. Opening Preview no longer relocates or starts live output; cached text and images remain available before output starts.

See the [operator guide](docs/operator-guide.md) for the current behavior.

v0.11.3 makes Live control and Compact preview containers follow the actual output's aspect ratio, growing Compact view to show the complete frame. On top applies only in Compact view. Canva connection and content/cache Settings cards are always visible, with normal-sized controls. It retains the scaled display-identification and selection recovery fixes from v0.11.2. Reconnecting a display does not restart output automatically.

**Hardware testing:** the freeze reported in [#24](https://github.com/mj00009/CanvaStage/issues/24) occurred with Windows wireless casting to a Samsung TV. The operator has closed the issue, but its root cause is not confirmed. Test on the actual presentation computer and receiver before service use; this release does not claim a casting fix. The audio source reported in [#35](https://github.com/mj00009/CanvaStage/issues/35) still needs confirmation; CanvaStage displays static images/text and does not play or automatically mute media.

Monitor text again fits during layout before drawing, retaining the v0.10.4 diagnostics and output drawing checks. A separate casing report remains unconfirmed; the extractor copies PPTX text without changing its case and needs an affected export to establish the cause.

Download `CanvaStage-v0.11.3-win-x64.zip`, extract the entire ZIP, and run `CanvaStage.Windows.exe`. The package includes the Apache-2.0 `LICENSE` and `TRADEMARKS.md` notice. It is self-contained, so the computer does not need a separate .NET installation.

The application and package are currently unsigned. Windows may show a SmartScreen warning on first launch. Administrator access is not required.

Setup combines deck loading and display selection into three numbered steps. Saved Canva connection details, Monitor appearance, content-refresh preferences and support tools live in Settings & Diagnostics. [Windows release verification passed](https://github.com/mj00009/CanvaStage/actions/runs/37841551307), including all 89 unit tests and 16 runtime/UI checks for Setup displays, selection recovery, offline previews, pixel parity, aspect changes, Compact transitions and Settings text sizing. Representative versioned-build screenshots were reviewed. The CI runner has only one physical display; confirm secondary-display placement on your hardware.

## Features

- Connects to a church-managed Canva account and downloads a selected presentation.
- Automatically follows the current page in Canva Presenter View through Windows accessibility.
- Excludes hidden slides so CanvaStage numbering matches Presenter View.
- Provides manual Previous and Next controls when automatic following is paused or unavailable.
- Sends a borderless confidence window to a selected extended display.
- Identifies displays with friendly names, resolution, Primary/Extended status, and temporary numbered overlays.
- Offers Mirror mode using the retrieved slide image and Monitor mode using simplified high-contrast text.
- Chooses the largest primary text region and ignores smaller header or footer text where possible.
- Shrinks long Monitor text to keep it visible and falls back to the slide image when usable text is unavailable.
- Supports next-line styling, four Monitor layouts, custom colors, curated fonts, and an optional clock.
- Keeps the downloaded presentation, preferences, Canva credentials, and authorization between launches.
- Provides cache controls, presentation freshness checks, tracking health, and sanitized diagnostic exports.
- Loads presentation choices using saved authorization and automatically renews expired or rejected access tokens.
- Checks the applied deck for edits every 30 seconds and downloads newer content without interrupting live output or page tracking; automatic content refresh can be disabled.
- Limits next-slide cues to one display line, with an ellipsis for long paragraphs.
- Provides a compact operator view and a Blank output safety control.

## Requirements

- Windows 11 x64.
- A Canva account with multi-factor authentication enabled.
- A Canva Developer Portal integration with `design:meta:read` and `design:content:read`.
- One display for setup and Preview; two displays configured to Extend for independent confidence output.

Read [Canva setup](docs/canva-setup.md) for the current integration steps and security limitations.

## Typical Sunday workflow

1. Open CanvaStage using the same Windows account used during setup.
2. In **Setup → 1. Load the Canva deck**, confirm the saved connection, refresh the list, select the presentation, and choose **Apply**.
3. In **Setup → 2. Choose the confidence display**, choose **Identify displays** and select the Extended confidence display.
4. Choose **Open Preview** to check content and appearance in a resizable window.
5. Open one Canva Presenter View and confirm CanvaStage reports **Following Canva**.
6. In **Setup → 3. Preview and go live**, choose **Start output**, confirm the intended monitor, then use **Live control** or **Compact view**.
7. Use **Blank output** whenever the stage display should be black.

Manual page navigation pauses automatic following. Choose **Resume sync** to return to Canva Presenter’s current page.

See the [operator guide](docs/operator-guide.md) for modes, keyboard shortcuts, cache controls, and troubleshooting.

Set up credentials once in **Settings & Diagnostics → Canva connection**. Monitor appearance and other persistent preferences are also there. Return to **Full view** to resume or manage sync; compact view deliberately has no sync toggle.

## Privacy and local data

The Client Secret and OAuth tokens are protected for the current Windows user using Windows DPAPI. Non-secret preferences and downloaded presentation content are stored under `%LOCALAPPDATA%\CanvaStage`.

Closing CanvaStage keeps the connection and downloaded presentation. **Disconnect** removes the protected Canva credentials. **Clear cache** removes downloaded presentation content but keeps credentials and preferences.

Logs and exported diagnostic bundles redact credentials, tokens, OAuth codes, and authorization headers. Diagnostic bundles exclude presentation text and images.

## Development

CanvaStage is a .NET 10 WPF application. On Windows, run `Build Windows App.cmd`, or use:

```powershell
dotnet restore CanvaStage.sln
dotnet build CanvaStage.sln --configuration Release --no-restore
dotnet test CanvaStage.sln --configuration Release --no-build
dotnet publish src\CanvaStage.Windows\CanvaStage.Windows.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true
```

GitHub Actions builds and tests pull requests and `main`. It also launches the published Windows application with synthetic data, checks live content refresh and token renewal, checks WPF bindings and output text fitting, and renders screenshots at representative display scales. A `v*` tag creates a GitHub release containing the portable ZIP and SHA-256 checksum.

Real hardware and Canva account checks are documented in the [Windows acceptance guide](docs/windows-acceptance.md).
The [maintenance guide](docs/maintenance.md) records external compatibility boundaries and pre-release checks.

## Repository layout

- `src/CanvaStage.Core` — presentation models and platform-independent output behavior.
- `src/CanvaStage.Windows` — WPF interface, Canva connection, Windows display handling, credentials, cache, and diagnostics.
- `tests/CanvaStage.Tests` — automated tests for core behavior.
- `docs` — setup, operation, licensing, and real-machine acceptance guides.
- `AGENT_HANDOFF.md` — current architecture, decisions, validation, and future-work context for new development agents.
- `assets` — image and icon resources compiled into the application.
- `.github/workflows/windows-release.yml` — Windows build, verification, packaging, and release automation.

## License

CanvaStage is licensed under the [Apache License 2.0](LICENSE). It may be used, modified, and redistributed under that license. The license includes an explicit patent grant, excludes trademark rights, and provides the software on an “as is” basis without warranties or contributor liability except where applicable law requires otherwise.

See [Licensing and trademarks](docs/licensing-and-trademarks.md) before redistributing or rebranding the application.
