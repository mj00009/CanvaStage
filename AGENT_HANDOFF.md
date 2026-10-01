# CanvaStage development handoff

Use this file when starting a new development task with no prior conversation. It describes the repository as checked on 2026-10-01. Treat the source code, current GitHub issues, and the user's latest request as more authoritative if they differ from this snapshot. This is context, not an instruction to implement every idea below.

## Read this first

1. Read [README.md](README.md), [operator guide](docs/operator-guide.md), [Canva setup](docs/canva-setup.md), and [Windows acceptance guide](docs/windows-acceptance.md).
2. Check `git status`, the current branch/tag, the latest release, and open GitHub issues before editing. Do not assume this handoff is still the latest state.
3. Locate the specific service, view model, and tests for the requested behavior. Preserve unrelated local changes and stored user data.
4. For WPF work, read the local `wpf-best-practices` skill if available (see “Skills and conventions”).
5. Build and test; for UI or Windows integration changes, also use the Windows workflow's synthetic UI verification and a real-machine acceptance pass where applicable.
6. Do not create a version tag, GitHub release, or change the license/branding unless the user requests it.

## Product and current status

CanvaStage is a Windows 11 x64, .NET 10 WPF confidence-display application. A church operator authorizes a Canva account, selects and caches a presentation, and sends a separate confidence window to an extended display. The operator can use a Preview on one screen. The audience presentation remains in Canva; CanvaStage is not a replacement presenter.

The v0.9.0 release added Canva thumbnail URL recovery, cached-image recovery, API-schema verification, and Apache-2.0 notices in the portable ZIP. The subsequent maintenance patch makes closing the main window terminate hidden Preview/output windows, reports cache-save failures instead of silently applying an uncached deck, handles unexpected Canva import data safely, and checks failures from each local build step. Check the latest tag, GitHub release, and open issues before assuming this patch is published or accepted on real hardware. Releases are **unsigned, self-contained, portable Windows x64 ZIPs**; there is no installer, signing, or auto-update mechanism. The GitHub repository is `mj00009/CanvaStage`.

The user reports that v0.8.0 works very well. Previous issues around Presenter tracking, display placement, WPF UI, cache, and credential persistence have been fixed; do not revive an older spike or workaround without evidence. The repository's [license](LICENSE) is Apache-2.0. The independent-project [trademark notice](TRADEMARKS.md) is separate from the software license. Earlier GPL-3.0 release copies retain their original terms.

The v0.10.0 release addresses issues #17–19: single-line next cues, saved-authorization startup and token renewal, and automatic live content updates. See [release notes](docs/releases/v0.10.0.md) and confirm publication status from GitHub. Synthetic Windows verification and 50 unit tests passed for the functional fixes; real-account acceptance, especially confirmation of the reported refresh freeze, remains a user test.

## User decisions and non-goals

- Typical deployment uses one shared church Canva account and one shared Windows operator account. Secrets are protected **for that Windows user**; another Windows profile needs its own setup.
- Automatic page following uses a **Canva Presenter View page counter exposed through Windows accessibility**. Manual selection remains a safe fallback. It does not capture the primary display, intercept browser/network traffic, or infer pages from images.
- CanvaStage should not attempt to control Canva's presentation or presenter navigation.
- Mirror mode is a reference view, especially for sermon graphics; Monitor mode is for readable text. Pixel-perfect graphics are not currently required.
- Per-slide text corrections were explicitly deferred. Do not add them without a new request.
- Installer, code signing, and auto-update work were intentionally deferred. Do not treat unsigned packaging as a current bug by itself.
- The user chose to leave CanvaStage branding in place for now. The project is independent and not endorsed by Canva.
- Avoid external writes such as pushing, tagging, releasing, or closing issues unless the current task authorizes them. A push to `main` and a `v*` tag each trigger a Windows workflow run; a tag additionally publishes a GitHub release.

## Shipped behavior

### Presentation and Canva

- The offline demo deck has five pages (`DemoPresentationProvider`) and works without Canva credentials.
- Canva connection uses a local OAuth 2.0 Authorization Code + PKCE flow with the exact loopback redirect `http://127.0.0.1:27843/callback/`. Required scopes are `design:meta:read` and `design:content:read`. Operators provide their own Canva Client ID and Client Secret; setup is in [docs/canva-setup.md](docs/canva-setup.md).
- Operators can save/reuse the secret, test/connect/disconnect, refresh accessible presentations, select/apply one, re-download it, and check whether Canva's modification time is newer than the cache.
- Current source restores the saved connection and presentation list at startup. Expired access tokens are renewed under a semaphore because Canva refresh tokens are single-use; a protected API 401 gets one renewal/retry. Only an explicit Connect action can open browser authorization. Revoked authorization retains protected setup and the working cached deck.
- Automatic content checks are enabled by default and persisted in `AppPreferences.IsAutomaticContentRefreshEnabled`. A 30-second dispatcher timer checks only the applied design's metadata; a newer modification time downloads and saves a new immutable deck before swapping it into output. Failures keep the working deck and back off up to five minutes. No refresh stops Presenter scanning or resets live/blank/manual-pause state. Re-download targets the applied deck independently of dropdown selection.
- Progress is throttled at the service and coalesced at the dispatcher, while preview text fitting reacts only to text/layout changes. Imports and cache saves run away from the UI thread. `OutputCoordinator` queues reentrant notifications so a Presenter page selected during a deck swap cannot be overwritten by a late old snapshot. Same-deck refresh uses a stable external page ID if supplied, otherwise a valid page number; fresh matching Presenter counters take priority when following.
- Next cues preserve authored PPTX soft breaks, select the first nonempty line, and use a single visual line with word ellipsis for paragraphs that wrap. Main text remains complete.
- Applying a deck requests a PPTX export, extracts native text and hidden-slide state from its XML, omits hidden pages from the local page order, then gets Canva page metadata and downloads **separate Canva page thumbnails** for Mirror mode and textless-page fallback. Fetching short-lived thumbnail URLs after PPTX preparation, periodic metadata refresh, and one retry on image failure reduce stale-URL failures. Mirror images do **not** come from the PPTX. The export wait is five minutes, with progress for download bytes when Canva supplies content length and per-page caching progress.
- The text extractor joins PowerPoint runs without inserting spaces, then chooses one dominant text block using font size and shape area (65%/35% weighting). Smaller headings, footers, and copyright text are usually excluded. Some atypical slide designs may still need manual review.
- A cached presentation, selected page, output preferences, Client Secret, and OAuth authorization persist across normal exits. Missing cached image files no longer discard the deck's text and cause a warning to re-download before relying on Mirror. `Clear cache` removes downloaded content but retains credentials/preferences; `Disconnect` removes protected credentials; `Clean old downloads` prunes obsolete immutable image generations.

### Presenter tracking

- The current tracker scans candidate Canva desktop/browser windows via Windows UI Automation. It publishes the **first** exposed page counter found; do not reintroduce an exhaustive scan that delays updates (earlier tracking regressions were fixed by restoring the spike's direct behavior).
- The polling interval is approximately 750 ms, with retry backoff up to 15 seconds after failures. Each start/stop increments a generation so late updates cannot replace newer state. The view model considers observations stale after three seconds.
- Following changes the selected page only when a fresh page counter's visible-page total equals the applied deck's visible-page count. A mismatch, missing counter, or scan error holds the last page and leaves manual controls usable. Keep only one Presenter View open and run it at the same Windows elevation as CanvaStage.
- Previous/Next pauses following. `Resume sync` reuses a fresh observation or waits for the next one. Disabling automatic sync persists; a temporary pause does not.

### Output and operator UI

- Independent Preview and selected-display borderless output; loss of the selected display stops output safely. Displays have stable identity when available, friendly name, resolution, Primary/Extended status, and numbered identification overlays.
- Mirror shows the cached Canva thumbnail. Monitor shows the extracted main text and optional next line, with four layouts, font/size/colors, contrast guidance, an optional clock, and a fallback image if text is unusable. Main text is measured and shrunk to avoid clipping.
- Appearance/mode changes apply immediately to in-app preview and output. The output can be blanked/restored without closing its session; `Escape` and a hover-revealed close button can close it. The operator has a compact view and Always-on-top toggle.
- Keyboard shortcuts with the main window focused: `Ctrl+Left/Right` previous/next; `Ctrl+Space` pause/resume sync; `Ctrl+B` blank/restore; `Ctrl+1/2` Mirror/Monitor.
- Setup diagnostics show connection, presenter, output, and cache health. A privacy-safe log lives at `%LOCALAPPDATA%\CanvaStage\canvastage.log`; diagnostic ZIP exports omit presentation content and redact credentials/tokens/codes/authorization headers.

## Architecture and where to edit

| Area | Primary files / responsibility |
|---|---|
| Cross-platform state | `src/CanvaStage.Core/Models/*`, `Services/OutputCoordinator.cs`: deck, selected page, mode, layout, appearance, blank/live snapshot |
| Presentation providers | `Services/IPresentationProvider.cs`, `ICanvaPresentationProvider.cs`, `DemoPresentationProvider.cs`; Canva implementation is Windows-specific |
| Canva API and import | `src/CanvaStage.Windows/Services/CanvaConnectService.cs`: auth, list, page metadata, export/download, thumbnail cache |
| PPTX text | `src/CanvaStage.Core/Utilities/PptxSlideTextExtractor.cs`: hidden slides, run joining, dominant block selection |
| Presenter sync | `Services/CanvaPresenterAccessibilityTracker.cs`, `ICanvaPresenterTracker.cs`, `Core/Utilities/PresenterPageCounterParser.cs`, `Core/Services/ResilientPollingLoop.cs`; policy/state lives in `MainViewModel.cs` |
| Local persistence | `CanvaCredentialsStore.cs` (Windows DPAPI CurrentUser), `JsonPreferencesStore.cs` (`preferences.json`), `PresentationCacheStore.cs` (`cache/presentation.json` plus immutable image generations) |
| Main UI | `Views/MainWindow.xaml(.cs)`, `ViewModels/MainViewModel.cs`, `Themes/Colors.xaml`, `Themes/Styles.xaml`; code-behind mainly handles window placement, responsiveness, and color dialog |
| Confidence output | `Services/OutputWindowManager.cs`, `Views/OutputWindow.xaml(.cs)`, `ViewModels/OutputWindowViewModel.cs`, `Views/ConfidencePreview.xaml`: display placement, live updates, text fitting, image loading |
| Display handling | `DisplayService.cs`, `DisplayIdentificationService.cs`; native placement reconciles physical screen bounds with WPF DPI |
| Startup and diagnostics | `App.xaml.cs` constructs services and installs crash handlers; `PrivacySafeLog.cs`, `DiagnosticsExportService.cs`, `Core/Utilities/SensitiveDataRedactor.cs` |
| Windows CI verification | `WindowsReleaseVerification.cs`, `LiveCanvaRefreshVerification.cs`, `VerificationWindowSizing.cs`: synthetic delayed/failed live-refresh and token tests, screenshots, binding checks, text fit, DPI/layout scenarios |
| Unit tests | `tests/CanvaStage.Tests/*`: core state, text extraction, counter parsing, polling, preferences, contrast, redaction |

There is no DI container; `App.xaml.cs` constructs the service graph directly. The Windows application targets `net10.0-windows` with WPF and Windows Forms enabled; the Core library targets `net10.0`. Both project files advertise Apache-2.0. Avoid moving Windows UI Automation, DPAPI, WPF types, or display APIs into Core.

### Data flow

`CanvaConnectService` → `PresentationDocument` (native text + cached image paths) → `OutputCoordinator` (selected page, live/blank state, mode, layout, appearance) → `MainViewModel`/`OutputWindowViewModel` → in-app `ConfidencePreview` and separate `OutputWindow`.

The accessibility tracker supplies page-counter observations to `MainViewModel`; the view model validates deck length and pause state before calling `OutputCoordinator.SelectPage`. `OutputWindowViewModel` listens to coordinator snapshots; `OutputWindowManager` manages window placement and reacts to Windows display changes. Preferences are saved on state changes. This separation is important: the app does not know Canva's current page from the API or PPTX alone.

## Storage, security, and external dependencies

- Mutable app data lives under `%LOCALAPPDATA%\CanvaStage`. Protected credentials are in `canva.credentials.protected`; preferences in `preferences.json`; cached deck/images under `cache`.
- Canva credentials and OAuth tokens must never appear in source, test fixtures uploaded to CI, logs, screenshots, or diagnostic bundles. Keep errors/privacy-safe and test redaction when adding API flows.
- The local desktop secret model is a deliberate, self-managed compromise. Canva's [published security guidance](https://www.canva.dev/docs/apps/rest-apis/security-recommendations/) says desktop REST API integrations that store credentials are unsupported; do not claim this architecture is Canva-supported or suitable for broad public distribution. DPAPI reduces local exposure but does not change that boundary.
- Canva page metadata thumbnails have limited resolution. Direct JPEG/PNG exports support higher-resolution options, but no such quality upgrade is implemented. Do not assume PPTX export quality controls the Mirror image.
- Canva Connect API details, supported formats, scopes, limits, and developer terms can change. Verify the [official Canva Connect docs](https://www.canva.dev/docs/connect/) before implementing API changes.

## Build, test, and release

On Windows, `Build Windows App.cmd` calls `scripts/Build-WindowsApp.ps1`; it restores, builds, tests, publishes a self-contained single-file win-x64 package, and writes ZIP/checksum/build logs under ignored `artifacts/`. Equivalent development commands:

```powershell
dotnet restore CanvaStage.sln
dotnet build CanvaStage.sln --configuration Release --no-restore
dotnet test CanvaStage.sln --configuration Release --no-build
```

The macOS .NET SDK can build and run the cross-platform tests with Windows targeting enabled; it cannot launch/visually test WPF. The [GitHub Actions workflow](.github/workflows/windows-release.yml) runs on `windows-latest` for PRs, pushes to `main`, version tags, and manual dispatch. It restores, builds, tests, publishes a self-contained ZIP with SHA-256 checksum, launches the **published** app in isolated synthetic verification mode, and uploads test results, a verification report, and UI screenshots. Tag pushes matching `v*` also create/update a GitHub release. A main push and a tag push on the same commit deliberately run the workflow twice.

Windows verification is launched with `--ui-demo-screenshot`, related screenshot arguments, and `--ui-verification-directory`. It uses synthetic content/credentials in isolated storage and checks WPF binding errors, Presenter transitions, DPAPI persistence, image-refresh safety, cache-save failure propagation, normal close with a hidden Preview window, output placement, text fitting, contrast, and multiple effective DPI/layout sizes. These screenshots need visual review; synthetic DPI cannot prove actual mixed-DPI or monitor hotplug behavior. Use [docs/windows-acceptance.md](docs/windows-acceptance.md) with physical hardware and a real Canva account for those cases. A `--smoke-test` startup path also exists.

Releases are still unsigned; Windows SmartScreen may warn. Do not promise an installer/signature or call a successful CI smoke test a full real-account acceptance test. Keep version in `src/CanvaStage.Windows/CanvaStage.Windows.csproj` and tag/release naming aligned when the user requests a release. The publish workflow and local Windows build script explicitly copy `LICENSE` and `TRADEMARKS.md` into the ZIP and verify their presence; inspect the actual release ZIP as well.

The [maintenance guide](docs/maintenance.md) records external compatibility risks and pre-release checks.

## Skills and working conventions

- **`wpf-best-practices`**: used for WPF/C# implementation. In this workspace it is at `.agents/skills/wpf-best-practices/SKILL.md`; because `.agents/` is git-ignored, a fresh clone may not have it. If present, read it fully before WPF work. In particular: use nullable/file-scoped C#, keep UI work on the dispatcher, use async cancellation, dispose Windows resources, preserve MVVM boundaries, and visually verify XAML changes. Follow the actual repository architecture where it differs from the skill's examples (notably direct construction rather than a DI container).
- **`grill-me`**: a local, user-invoked requirements-interview skill at `.agents/skills/grill-me/SKILL.md`. It helped refine earlier product requirements; it is not an automatic development step and may be absent in a fresh clone. Use only if the current user explicitly invokes it and its invocation mechanism is available.
- Before changing a service or UI, read its tests and the corresponding acceptance section. Add regression coverage for fixed bugs. Use `rg`/`rg --files` for discovery, `apply_patch` for edits, and `git diff --check` before committing. Preserve local/user changes and avoid deleting caches/credentials outside an explicit request.
- For XAML changes, keep a consistent dark operator theme and readable contrast. The last several releases had visual regressions caught by screenshots; inspect generated UI images as well as test exit codes.
- For tracking changes, preserve direct first-counter publishing, page-count guard, stale-observation handling, and manual fallback. Test on the Canva desktop app and the actual browser/elevation configuration used in services.
- For a bug report, prefer a privacy-safe diagnostic ZIP, exact Windows/display scaling, Canva desktop/browser version, applied deck page count, and reproduction steps. Never ask for the Client Secret or token.

## Future work: distinguish commitments from ideas

After v0.9.0, the following are candidates or deliberately deferred areas, not permission to start them without a new request:

1. **Optional sharper Mirror images** — the user noticed low-resolution graphics but explicitly asked only to consider tradeoffs. Current Mirror uses Canva page thumbnails, not images rendered from PPTX. If later requested, consider direct JPEG export at presentation-native dimensions with moderate quality, cache it per deck revision, and retain thumbnails as fallback. Weigh API/export time, large-deck download size, cache growth, Canva rate limits, and hidden-page mapping. Since Mirror is only a graphic reference, this may not be worth the complexity.
2. **Per-slide text corrections** — explicitly deferred; the current one-dominant-block heuristic is accepted for now.
3. **Installer, signing, auto-updates** — intentionally skipped. Revisit only if distribution needs change; do not silently introduce signing cost or installer behavior.
4. **Richer live capture/recognition or Canva command interception** — out of current scope. The working accessibility-based Presenter counter is the accepted tracking method.
5. **Long-term robustness/maintenance** — v0.9.0 adds API and cache recovery checks plus a [maintenance checklist](docs/maintenance.md). Continue monitoring Canva APIs, Presenter accessibility, Windows DPI/display behavior, and release-action dependencies. File concrete issues for observed regressions.

If the user asks what to build next, propose a prioritized plan grounded in fresh issues and real usage feedback. Do not claim an older roadmap remains pending when the feature is already shipped.
