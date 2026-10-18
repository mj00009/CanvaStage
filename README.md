# CanvaStage

CanvaStage is an independent Windows confidence-monitor application for presentations created in Canva. It gives speakers, musicians, and service leaders a separate, readable output while the audience continues to see the normal presentation.

CanvaStage is not affiliated with, endorsed by, or sponsored by Canva Pty Ltd. See [Licensing and trademarks](docs/licensing-and-trademarks.md).

## Help

Choose **Help** in the app, or press **F1** while the main window has focus, to open [CanvaStage Help](https://mj00009.github.io/CanvaStage/). F1 also works in Compact view. The website has a Sunday quick start, first-time and advanced setup, troubleshooting, and demo screenshots. Each app release links to its own versioned guide. You need internet access to open the website. See [help maintenance](docs/help-authoring.md) for publishing and screenshot rules.

## Download

The latest release is [CanvaStage v0.13.0](https://github.com/mj00009/CanvaStage/releases/tag/v0.13.0). See the [release notes](docs/releases/v0.13.0.md) for faster Presenter following, optional slide fades, a customizable Mirror clock and regression fixes.

The operator workflow is Setup, Live control and Settings & Diagnostics. Simple Setup checks the last-used deck and saved Extended display, reuses unchanged cached content, and starts output with one action. Advanced retains the numbered controls for first-time setup or changing decks/displays. Compact has no Pause Sync button. Every preview copies the actual output visual, sharing text wrapping, fitted size, colors, clock and next-line truncation. Separate Preview does not relocate or start live output.

See the [operator guide](docs/operator-guide.md) for the current behavior.

Stop closes the fullscreen window, Compact scales smaller without scrollbars, touchpad wheel movement is smooth, and errors explain recovery steps. Live control and Compact preserve the output's aspect ratio. On top is Compact-only; connection/cache Settings cards remain always visible. Reconnecting the remembered Extended display can offer a Yes/No restart using the cached deck. No keeps output off, and an explicit Stop suppresses later offers until the operator requests output again.

**Hardware testing:** the freeze reported in [#24](https://github.com/mj00009/CanvaStage/issues/24) occurred with Windows wireless casting to a Samsung TV. The operator has closed the issue, but its root cause is not confirmed. Test on the actual presentation computer and receiver before service use; this release does not claim a casting fix. The audio source reported in [#35](https://github.com/mj00009/CanvaStage/issues/35) still needs confirmation; CanvaStage displays static images/text and does not play or automatically mute media.

Monitor text again fits during layout before drawing, retaining the v0.10.4 diagnostics and output drawing checks. A separate casing report remains unconfirmed; the extractor copies PPTX text without changing its case and needs an affected export to establish the cause.

Download `CanvaStage-v0.13.0-win-x64.zip`, extract the entire ZIP, and run `CanvaStage.Windows.exe`. The package includes the Apache-2.0 `LICENSE` and `TRADEMARKS.md` notice. It is self-contained, so the computer does not need a separate .NET installation.

The application and package are currently unsigned. Windows may show a SmartScreen warning on first launch. Administrator access is not required.

Saved Canva connection details, appearance, clock/fade controls, advanced content-refresh controls and support tools live in Settings & Diagnostics. Simple Setup also has the automatic-update checkbox. [Versioned Windows verification passed](https://github.com/mj00009/CanvaStage/actions/runs/39131248434): all 135 unit tests and 20 runtime/UI checks, including clocks, fades, immediate Blank/Stop, reconnects, Help/F1, Setup/cache reuse, typography, offline previews and pixel parity. The CI runner has one physical display and injects Yes/No answers; confirm native dialogs, actual Canva timing, touchpad and secondary-display behavior on the presentation computer before service use.

## Features

- Connects to a church-managed Canva account and downloads a selected presentation.
- Offers one-action Simple Setup using the last-applied deck and saved Extended display, with unchanged-cache reuse and safe recovery steps; Advanced retains manual setup controls.
- Automatically follows the current page in Canva Presenter View through Windows accessibility.
- Excludes hidden slides so CanvaStage numbering matches Presenter View.
- Provides manual Previous and Next controls when automatic following is paused or unavailable.
- Sends a borderless confidence window to a selected extended display.
- Identifies displays with friendly names, resolution, Primary/Extended status, and temporary numbered overlays.
- Offers Mirror mode using the retrieved slide image and Monitor mode using simplified high-contrast text.
- Chooses the largest primary text region and ignores smaller header or footer text where possible.
- Shrinks long Monitor text to keep it visible and falls back to the slide image when usable text is unavailable.
- Supports next-line styling, four Monitor layouts, custom colors, curated fonts, and an optional clock.
- Adds an optional Mirror clock at the bottom left or right with a small shadow, separate Monitor/Mirror sizes and regular or slightly bold weight.
- Offers optional slide fades from 100 to 1000 ms in both modes; Blank and Stop remain immediate.
- Keeps the downloaded presentation, preferences, Canva credentials, and authorization between launches.
- Provides cache controls, presentation freshness checks, tracking health, and sanitized diagnostic exports.
- Loads presentation choices using saved authorization and automatically renews expired or rejected access tokens.
- Checks the applied deck for edits every 30 seconds and downloads newer content without interrupting live output or page tracking; automatic content refresh can be disabled.
- Limits next-slide cues to one display line, with an ellipsis for long paragraphs.
- Provides a resizable, scrollbar-free compact operator view, smooth Full-view scrolling and Blank/Stop output safety controls.

## Requirements

- Windows 11 x64.
- A Canva account with multi-factor authentication enabled.
- A Canva Developer Portal integration with `design:meta:read` and `design:content:read`.
- One display for setup and Preview; two displays configured to Extend for independent confidence output.

Read [Canva setup](docs/canva-setup.md) for the current integration steps and security limitations.

## Typical Sunday workflow

1. Open CanvaStage using the same Windows account used during setup.
2. Open **Setup → Simple**, confirm the listed deck/display, and choose **Start setup**. An unchanged, verified cache is reused; otherwise wait for download and the synced/ready message.
3. For first-time setup or a different deck/display, use **Setup → Advanced** to Refresh/Apply the intended deck, Identify/select the Extended confidence display, and Preview/start output.
4. Open one matching Canva Presenter View and confirm CanvaStage reports **Following Canva**.
5. Confirm the intended confidence monitor and use **Live control** or **Compact view** during the service.
6. Use **Blank output** when the stage display should be black, **Restore output** to return content, or **Stop output** in Full view to close the fullscreen window.

Simple **Check slides for updates** only imports reported changes when automatic updates are enabled. Advanced **Check for updates** retains a forced import with automatic updates enabled; **Re-download** is available when Canva's modification metadata lags behind an edit. Simple Setup will not guess an unavailable deck or cover the Primary display; use Advanced to correct its defaults.

Manual page navigation pauses automatic following. Choose **Resume sync** to return to Canva Presenter’s current page.

See the [operator guide](docs/operator-guide.md) for modes, keyboard shortcuts, cache controls, and troubleshooting.

Set up credentials once in **Settings & Diagnostics → Canva connection**. Monitor appearance and other persistent preferences are also there. Return to **Full view** to resume or manage sync; compact view deliberately has no sync toggle.

## Privacy and local data

The Client Secret and OAuth tokens are protected for the current Windows user using Windows DPAPI. Non-secret preferences and downloaded presentation content are stored under `%LOCALAPPDATA%\CanvaStage`.

Closing CanvaStage keeps the connection and downloaded presentation. **Disconnect** removes the protected Canva credentials. **Clear cache** removes downloaded presentation content but keeps credentials and preferences.

Logs and exported diagnostic bundles redact credentials, tokens, OAuth codes, and authorization headers. Diagnostic bundles exclude presentation text and images.

## Development

v0.13.0 addresses [#41](https://github.com/mj00009/CanvaStage/issues/41), [#42](https://github.com/mj00009/CanvaStage/issues/42) and [#45](https://github.com/mj00009/CanvaStage/issues/45). Healthy Presenter scans wait 200 ms before the next scan; actual latency also depends on scan time and Canva's accessibility updates. See [development notes](docs/development/output-effects.md) for implementation and acceptance checks.

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
