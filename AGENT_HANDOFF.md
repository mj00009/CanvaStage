# CanvaStage development handoff

Use this file when starting a new development task with no prior conversation. It describes the repository as checked on 2026-10-03. Treat the source code, current GitHub issues, and the user's latest request as more authoritative if they differ from this snapshot. This is context, not an instruction to implement every idea below.

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

The v0.10.1 update addresses issue #20 (stale Mirror content): it uses fresh JPEG exports instead of page-preview thumbnails and forces a manual import for **Check for updates** when automatic updates are enabled. The operator confirmed the stale-image problem was resolved, but later reported inefficient checks and updates seeming dependent on opening Presentation; #20 remains open for this follow-up. Issue #21 (missing word spaces) was confirmed resolved and closed by the operator. XML now loads with `LoadOptions.PreserveWhitespace` to preserve whitespace-only `a:t` runs. Tests cover standalone spaces, nonbreaking spaces, XML indentation, and words split across runs. All 56 unit tests and published Windows verification passed for the v0.10.1 functional commits. See [release notes](docs/releases/v0.10.1.md).

The v0.10.2 update implements only the scheduling/refresh-health follow-up requested on October 2. A re-armed one-shot dispatcher timer uses `AutomaticContentRefreshSchedule` (monotonic elapsed time) to avoid the old fixed-tick/due-time double gate that could skip a check. Live control has refresh state, last check/load, next check/retry, progress and safe errors. All 59 unit tests pass locally; Windows verification additionally exercises actual timer ticks in Live control and captures refresh-health states. macOS cannot execute those Windows checks; inspect their CI results and screenshots and confirm publication status from GitHub. Selective image refresh was explicitly deferred; changed decks still get full PPTX/JPEG exports. No output or Presenter restart is required for a live deck swap. See [release notes](docs/releases/v0.10.2.md).

The v0.10.3 update addresses #22 and #23. Both Monitor surfaces use `Controls/FittedTextHost.cs`, a Decorator that measures its real TextBlock at unbounded height and selects the fitting font size during layout, before painting. Do not restore the old requested-size reset/deferred Render-priority fitting; that caused a visible flash on slide changes. Current/Next share the selected family and default semibold weight, with independent cue size and an optional Bold override. `MonitorTypographyVerification.cs` observes first rendered frames through `CompositionTarget.Rendering` after layout; it checks transitions, resizing, layouts, four fonts and cue styling. Verify Windows CI and inspect its screenshots before publishing. Issues #22/#23 remain open pending real-machine confirmation. Issue #20 was confirmed resolved with v0.10.2 and closed by the operator on October 2. See [release notes](docs/releases/v0.10.3.md).

The v0.10.4 follow-up for issue #24 (October 3): the user confirmed that page/status and the in-app preview update while the separate output stays frozen, including manual navigation. They authorized restoring **only the separate output's v0.10.2 fitter** while retaining the v0.10.3 in-app fitter and Next font matching, then requested pushing and releasing the patch. This overrides the earlier no-rollback recommendation above. `OutputWindow` is a normal Grid/TextBlock again, with `OutputWindowViewModel.FittedFontSize` and a deferred fit callback; the known output sizing flash is an accepted temporary tradeoff. Tracking/content-refresh logic is unchanged. The exact cause of the real-machine freeze has not been reproduced or proven. `OutputPageUpdateVerification.cs` compares rendered output drawing pixels through 40 synthetic Presenter pages plus pause/manual/resume and backward jumps without forcing redraw before inspection. Expanded logs trace observations, selected pages, output snapshots, queued revisions and one-shot render-stage callbacks without logging slide text. `DiagnosticTimestamp` normalizes new entry/embedded timestamps to ISO UTC `Z`; older appended log entries keep their offsets and UI times stay local. The local cross-build and all 63 Core tests pass; check GitHub for Windows verification and publication status rather than assuming this release is published. Issue #24 remains open for real-machine confirmation. See [release notes](docs/releases/v0.10.4.md).

Additional issue #24 evidence: the operator used Windows wireless casting to a Samsung TV. After one failure, secondary output stayed blank across a CanvaStage restart and recovered after disconnecting/reconnecting the display. The mouse remained visible, but unrelated applications were **not tested**, so do not claim other apps were affected. A subsequent attempt tracked normally. The fitter is therefore not a confirmed root cause; a graphics or wireless-display path problem remains possible. Added read-only placement/display-change/render-capability telemetry and wireless acceptance steps; no render-mode changes, GPU resets or connection control. Synthetic drawing-pixel tests cannot validate a physical receiver.

Publication update: v0.10.4 was released from commit `09baf9b`. Main run `37158270337` and tag run `37158454234` both passed Windows build, 63 Core tests and published runtime/UI verification, including 40-page output drawing checks. Representative CI screenshots were reviewed. This does not confirm real-account or wireless-TV recovery; keep issue #24 open for operator acceptance.

Unreleased follow-up after v0.10.4 (October 3): the operator reported another freeze and could not display other windows on the secondary monitor, suspected casting and explicitly requested restoring the text sizing fix. Current source therefore restores `FittedTextHost` on the separate output and removes deferred fitting/VM font-size state again, retaining v0.10.4 logs and the 40-page pixel regression. First-frame fitting assertions now cover both surfaces without forced redraw. This supersedes the output rollback above; it is not yet pushed, Windows-verified or released. Do not claim a confirmed Windows root cause. The only GitHub log supplied so far is the older v0.10.3 log; the latest v0.10.4 diagnostics were requested. The user also reported unexpected uppercase text that Canva case toggling fixes after refresh. Repository inspection found no case conversion on the Monitor path: the PPTX extractor copies raw `a:t` text. New Core tests preserve mixed case/legitimate capitals across split runs and capitalization formatting; synthetic Windows checks assert mixed-case strings in both view models/output bindings. An affected, unedited Canva-exported PPTX and expected text/screenshot were requested; do not implement guessed lowercase/title-case correction or claim this issue fixed.

The operator confirmed #22 fixed in v0.10.3 and closed it on October 3. As of the latest GitHub read, #23 and #24 remain open. All 66 Core tests and the local cross-build pass for the unreleased sizing restoration/casing regression work; Windows runtime/UI checks have not run for these new changes.

## User decisions and non-goals

Verification result: Windows run `37229085904` passed for commit `01ac496`, including all 66 Core tests, published runtime/UI checks, offline text/navigation and cached Mirror/textless-image constructors, pixel parity, 40-page drawing checks, binding checks and normal shutdown. Offline preview and Settings/compact screenshots were reviewed. One sidebar label clipped in the narrow 200%-scale screenshot; it now wraps in a constrained Grid and has an automated visibility check. Verify the follow-up run before calling that final adjustment checked. No tag or release was created.

Operator UI verification follow-up: commit `0496d65` was pushed to main and Windows run `37228352474` built, passed all 66 Core tests, and passed live preview pixel/40-page drawing checks before failing the new offline Preview check. The coordinator intentionally exposes `CurrentPage = null` while output is off. The approved fix resolves the selected page from `Snapshot.Document` only in the Windows renderer while offline, including initial image loading and page changes; Core live-state behavior remains unchanged. Offline text/navigation and Mirror/textless-image constructor checks were added. Two operator status strings were updated to Setup/Settings & Diagnostics. A fresh Windows run is required; no release is requested.

Unreleased operator simplification (October 4): the user requested ignoring freeze investigation while simplifying the Sunday workflow. Navigation now has Setup (deck download, confidence display, preview/go-live), Live control and Settings & Diagnostics. Canva credentials are in a collapsed one-time configuration section; Monitor appearance/layout and advanced cache/refresh settings moved out of Live control/Setup. Compact view intentionally has no Pause/Resume sync button; Full view retains those controls and keyboard shortcuts. Existing DPAPI storage and preference schema are unchanged.

Preview implementation changed: `OutputWindow.PreviewSurface` excludes window chrome/hover-close controls and is the single rendering source for in-app, compact and separate previews, copied with a uniformly scaled `VisualBrush`. Do not reintroduce independent preview TextBlocks or font/cue scaling; that causes different wrapping. `OutputWindowManager` now owns independent live and preview windows. Opening/closing Preview must not move or stop live output, and offline Preview must not start a live session. For offline layout only, the manager schedules a hidden surface measure/arrange using the selected display's physical dimensions and the hidden HWND's `GetDpiForWindow` value. Live layout remains controlled by the actual output window. Source rebinding after output close and shutdown cleanup are required. `OperatorWorkflowVerification` adds Windows ownership/compact controls, lifecycle, shared visual, pixel parity and scaled preview checks. Local cross-build and 66 Core tests pass; Windows runtime/visual verification is pending. The user authorized committing and pushing these changes for Windows verification; no new tag or release has been requested.

- Typical deployment uses one shared church Canva account and one shared Windows operator account. Secrets are protected **for that Windows user**; another Windows profile needs its own setup.
- Automatic page following uses a **Canva Presenter View page counter exposed through Windows accessibility**. Manual selection remains a safe fallback. It does not capture the primary display, intercept browser/network traffic, or infer pages from images.
- CanvaStage should not attempt to control Canva's presentation or presenter navigation.
- Mirror mode is a reference view, especially for sermon graphics; Monitor mode is for readable text. Pixel-perfect graphics are not currently required.
- Per-slide text corrections were explicitly deferred. Do not add them without a new request.
- Installer, code signing, and auto-update work were intentionally deferred. Do not treat unsigned packaging as a current bug by itself.
- The user chose to leave CanvaStage branding in place for now. The project is independent and not endorsed by Canva.
- Avoid external writes such as pushing, tagging, releasing, or closing issues unless the current task authorizes them. A push to `main` and a `v*` tag each trigger a Windows workflow run; a tag additionally publishes a GitHub release.

## Current source behavior

### Presentation and Canva

- The offline demo deck has five pages (`DemoPresentationProvider`) and works without Canva credentials.
- Canva connection uses a local OAuth 2.0 Authorization Code + PKCE flow with the exact loopback redirect `http://127.0.0.1:27843/callback/`. Required scopes are `design:meta:read` and `design:content:read`. Operators provide their own Canva Client ID and Client Secret; setup is in [docs/canva-setup.md](docs/canva-setup.md).
- Operators can save/reuse the secret, test/connect/disconnect, refresh accessible presentations, select/apply one, re-download it, and check whether Canva's modification time is newer than the cache.
- Current source restores the saved connection and presentation list at startup. Expired access tokens are renewed under a semaphore because Canva refresh tokens are single-use; a protected API 401 gets one renewal/retry. Only an explicit Connect action can open browser authorization. Revoked authorization retains protected setup and the working cached deck.
- Automatic content checks are enabled by default and persisted in `AppPreferences.IsAutomaticContentRefreshEnabled`. A one-shot dispatcher timer checks only the applied design's metadata, then schedules the next check 30 seconds after completion; a newer modification time downloads and saves a new immutable deck before swapping it into output. Busy actions defer checks without advancing the deadline. Failures keep the working deck and back off up to five minutes. Live control reports refresh health even when Setup is never opened. No refresh stops Presenter scanning or resets live/blank/manual-pause state. Re-download targets the applied deck independently of dropdown selection.
- `PresentationDocument.UsesRenderedPageImages` defaults to false for older cached JSON. Automatic checks migrate such a cache once even if Canva's timestamp is unchanged. Manual **Check for updates** forces an import when automatic updates are enabled, but remains metadata-only when disabled. Periodic checks after migration still depend on Canva's reported edit time.
- Progress is throttled at the service and coalesced at the dispatcher, while preview text fitting reacts only to text/layout changes. Imports and cache saves run away from the UI thread. `OutputCoordinator` queues reentrant notifications so a Presenter page selected during a deck swap cannot be overwritten by a late old snapshot. Same-deck refresh uses a stable external page ID if supplied, otherwise a valid page number; fresh matching Presenter counters take priority when following.
- Next cues preserve authored PPTX soft breaks, select the first nonempty line, and use a single visual line with word ellipsis for paragraphs that wrap. Main text remains complete.
- Applying a deck requests a PPTX export, extracts native text and hidden-slide state from its XML, omits hidden pages from the local page order, then reads Canva page IDs/dimensions and requests a **separate JPEG export** for the visible source page numbers. Mirror and textless-page fallback use these rendered images, not PPTX graphics or preview thumbnails. JPEG quality is 70, `export_quality` is `regular`, and width is capped at 1280 without upscaling when all page widths are known; otherwise native dimensions are used. Export preparation can take five minutes per job, with PPTX byte progress when Canva supplies content length and per-page image caching progress. URL counts must match requested pages, images must decode, and any render failure leaves the working deck active. GETs request cache revalidation without changing signed URLs.
- The text extractor preserves authored whitespace (including runs containing only a space), joins PowerPoint runs without inserting extra spaces, then chooses one dominant text block using font size and shape area (65%/35% weighting). XML indentation outside `a:t` is not slide text. Smaller headings, footers, and copyright text are usually excluded. Some atypical slide designs may still need manual review.
- A cached presentation, selected page, output preferences, Client Secret, and OAuth authorization persist across normal exits. Missing cached image files no longer discard the deck's text and cause a warning to re-download before relying on Mirror. `Clear cache` removes downloaded content but retains credentials/preferences; `Disconnect` removes protected credentials; `Clean old downloads` prunes obsolete immutable image generations.

### Presenter tracking

- The current tracker scans candidate Canva desktop/browser windows via Windows UI Automation. It publishes the **first** exposed page counter found; do not reintroduce an exhaustive scan that delays updates (earlier tracking regressions were fixed by restoring the spike's direct behavior).
- The polling interval is approximately 750 ms, with retry backoff up to 15 seconds after failures. Each start/stop increments a generation so late updates cannot replace newer state. The view model considers observations stale after three seconds.
- Following changes the selected page only when a fresh page counter's visible-page total equals the applied deck's visible-page count. A mismatch, missing counter, or scan error holds the last page and leaves manual controls usable. Keep only one Presenter View open and run it at the same Windows elevation as CanvaStage.
- Previous/Next pauses following. `Resume sync` reuses a fresh observation or waits for the next one. Disabling automatic sync persists; a temporary pause does not.

### Output and operator UI

- Independent Preview and selected-display borderless output; loss of the selected display stops output safely. Displays have stable identity when available, friendly name, resolution, Primary/Extended status, and numbered identification overlays.
- Mirror shows the cached Canva render (older caches may still contain thumbnails until migrated). Monitor shows the extracted main text and optional next line, with four layouts, font/size/colors, contrast guidance, an optional clock, and a fallback image if text is unusable. Main text is measured and shrunk to avoid clipping.
- Appearance/mode changes apply immediately to in-app preview and output. The output can be blanked/restored without closing its session; `Escape` and a hover-revealed close button can close it. The operator has a compact view and Always-on-top toggle.
- Keyboard shortcuts with the main window focused: `Ctrl+Left/Right` previous/next; `Ctrl+Space` pause/resume sync; `Ctrl+B` blank/restore; `Ctrl+1/2` Mirror/Monitor.
- Settings & Diagnostics shows connection, presenter, output, and cache health. A privacy-safe log lives at `%LOCALAPPDATA%\CanvaStage\canvastage.log`; diagnostic ZIP exports omit presentation content and redact credentials/tokens/codes/authorization headers.

## Architecture and where to edit

| Area | Primary files / responsibility |
|---|---|
| Cross-platform state | `src/CanvaStage.Core/Models/*`, `Services/OutputCoordinator.cs`: deck, selected page, mode, layout, appearance, blank/live snapshot |
| Presentation providers | `Services/IPresentationProvider.cs`, `ICanvaPresentationProvider.cs`, `DemoPresentationProvider.cs`; Canva implementation is Windows-specific |
| Canva API and import | `src/CanvaStage.Windows/Services/CanvaConnectService.cs`: auth, list, page metadata, PPTX/JPEG exports, immutable image cache |
| PPTX text | `src/CanvaStage.Core/Utilities/PptxSlideTextExtractor.cs`: hidden slides, run joining, dominant block selection |
| Presenter sync | `Services/CanvaPresenterAccessibilityTracker.cs`, `ICanvaPresenterTracker.cs`, `Core/Utilities/PresenterPageCounterParser.cs`, `Core/Services/ResilientPollingLoop.cs`; policy/state lives in `MainViewModel.cs` |
| Local persistence | `CanvaCredentialsStore.cs` (Windows DPAPI CurrentUser), `JsonPreferencesStore.cs` (`preferences.json`), `PresentationCacheStore.cs` (`cache/presentation.json` plus immutable image generations) |
| Main UI | `Views/MainWindow.xaml(.cs)`, `ViewModels/MainViewModel.cs`, `Themes/Colors.xaml`, `Themes/Styles.xaml`; code-behind mainly handles window placement, responsiveness, and color dialog |
| Confidence output | `Services/OutputWindowManager.cs`, `Views/OutputWindow.xaml(.cs)`, `ViewModels/OutputWindowViewModel.cs`, `Views/ConfidencePreview.xaml`: display placement, live updates, text fitting, image loading |
| Display handling | `DisplayService.cs`, `DisplayIdentificationService.cs`; native placement reconciles physical screen bounds with WPF DPI |
| Startup and diagnostics | `App.xaml.cs` constructs services and installs crash handlers; `PrivacySafeLog.cs`, `DiagnosticsExportService.cs`, `Core/Utilities/SensitiveDataRedactor.cs` |
| Windows CI verification | `WindowsReleaseVerification.cs`, `LiveCanvaRefreshVerification.cs`, `MirrorImageExportVerification.cs`, `VerificationWindowSizing.cs`: synthetic delayed/failed refresh, changed Mirror pixels, hidden-page render mapping, token tests, screenshots, binding checks, text fit, DPI/layout scenarios |
| Unit tests | `tests/CanvaStage.Tests/*`: core state, text extraction, counter parsing, polling, preferences, contrast, redaction |

There is no DI container; `App.xaml.cs` constructs the service graph directly. The Windows application targets `net10.0-windows` with WPF and Windows Forms enabled; the Core library targets `net10.0`. Both project files advertise Apache-2.0. Avoid moving Windows UI Automation, DPAPI, WPF types, or display APIs into Core.

### Data flow

`CanvaConnectService` → `PresentationDocument` (native text + cached image paths) → `OutputCoordinator` (selected page, live/blank state, mode, layout, appearance) → `MainViewModel`/`OutputWindowViewModel` → in-app `ConfidencePreview` and separate `OutputWindow`.

The accessibility tracker supplies page-counter observations to `MainViewModel`; the view model validates deck length and pause state before calling `OutputCoordinator.SelectPage`. `OutputWindowViewModel` listens to coordinator snapshots; `OutputWindowManager` manages window placement and reacts to Windows display changes. Preferences are saved on state changes. This separation is important: the app does not know Canva's current page from the API or PPTX alone.

## Storage, security, and external dependencies

- Mutable app data lives under `%LOCALAPPDATA%\CanvaStage`. Protected credentials are in `canva.credentials.protected`; preferences in `preferences.json`; cached deck/images under `cache`.
- Canva credentials and OAuth tokens must never appear in source, test fixtures uploaded to CI, logs, screenshots, or diagnostic bundles. Keep errors/privacy-safe and test redaction when adding API flows.
- The local desktop secret model is a deliberate, self-managed compromise. Canva's [published security guidance](https://www.canva.dev/docs/apps/rest-apis/security-recommendations/) says desktop REST API integrations that store credentials are unsupported; do not claim this architecture is Canva-supported or suitable for broad public distribution. DPAPI reduces local exposure but does not change that boundary.
- Mirror uses bounded regular-quality JPEG exports for freshness, not a pixel-perfect presentation feed. Do not assume PPTX export quality controls the Mirror image. A configurable higher-quality option remains deferred.
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

1. **Optional sharper Mirror images** — current development source uses bounded JPEG exports to address stale previews, not to promise pixel-perfect graphics. A user-configurable quality/native-resolution option is not implemented. Weigh API/export time, large-deck download size, cache growth, Canva rate limits, and hidden-page mapping before adding one. Do not silently use stale thumbnails as a fallback during a content update.
2. **Per-slide text corrections** — explicitly deferred; the current one-dominant-block heuristic is accepted for now.
3. **Installer, signing, auto-updates** — intentionally skipped. Revisit only if distribution needs change; do not silently introduce signing cost or installer behavior.
4. **Richer live capture/recognition or Canva command interception** — out of current scope. The working accessibility-based Presenter counter is the accepted tracking method.
5. **Long-term robustness/maintenance** — v0.9.0 adds API and cache recovery checks plus a [maintenance checklist](docs/maintenance.md). Continue monitoring Canva APIs, Presenter accessibility, Windows DPI/display behavior, and release-action dependencies. File concrete issues for observed regressions.

If the user asks what to build next, propose a prioritized plan grounded in fresh issues and real usage feedback. Do not claim an older roadmap remains pending when the feature is already shipped.
