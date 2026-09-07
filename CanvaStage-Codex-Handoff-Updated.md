# Canva Stage: Codex Development Handoff (Updated)

## Current delivery status — v0.6.0

Phases 1–2 are implemented. Assisted tracking now uses Windows accessibility, replacing the original image-matching implementation plan. v0.6.0 added design-ID/title matching, explicit Presenter-window confirmation, visible sync states, temporary pause/resume, automatic retry recovery, and expanded Windows verification. Subsequent work keeps a stable automatic Presenter-window choice and permits a single-window visible-page-count fallback when Canva exposes no usable identity; multiple plausible windows still require operator choice. Original acceptance checkboxes below are historical hardware/real-account sign-off items, not an unimplemented-feature list; use `docs/v0.6.0-acceptance.md` for the current checklist.

Saved presentation content and DPAPI-protected Canva credentials survive ordinary app closure. Disconnect deliberately removes credentials. Presentation imports use immutable image-cache generations so a refresh cannot overwrite an image being displayed. Smaller header/footer text blocks are excluded by selecting the dominant text block. These decisions supersede any earlier all-text or clear-on-close assumptions.

## Product summary

**Canva Stage** is a Windows desktop application for church operators using Canva presentations. It provides an independently controlled confidence-monitor output for people on stage, while the congregation continues to see the regular Canva presentation.

The first release is **Windows-first**. It is a native desktop app, not a web dashboard and not a hosted service. The codebase should be organized so that a future macOS version can reuse the core Canva, slide, and tracking logic while replacing platform-specific UI, display, capture, and credential-storage components.

## Product goals

1. Be simple and dependable for a volunteer to use on Sunday.
2. Let the operator connect a Canva account and select a Canva presentation as the content source.
3. Show a separate display window on the confidence monitor, controlled from the main app window.
4. Allow the confidence output to switch instantly between **Mirror** and **Monitor** modes.
5. Work for laptop-only setup and testing; a physical second monitor is not required to configure or preview the app.
6. Keep the interface dark, calm, high-contrast, and readable.
7. Make every failure actionable. Do not expose raw exceptions, credentials, or token values.

## Scope and non-goals

### In scope

- Windows 11 desktop application.
- Canva account authorization, presentation/page retrieval, and locally stored connection state.
- A main operator window and an independent confidence-output window.
- Display selection, maximize-to-selected-display behavior, Preview in a window, and recovery from display changes.
- Mirror and Monitor output modes.
- Accessibility-based Presenter following with explicit identity checks, sync status, and manual fallback. Display capture/image matching is an optional future fallback.
- GitHub Actions builds for self-contained Windows x64 releases.

### Out of scope for the first release

- Hosted backend, web dashboard, cloud token service, or remote configuration service.
- Automatic updating, installer signing, or a signed installer (may be added later).
- macOS delivery.
- Keyboard shortcuts unless later specified as an accessibility feature.
- A promise of perfect, guaranteed real-time knowledge of Canva's current page.

## Architecture

### Client-only model

The app is a client application. It does not require a hosted backend. Each church creates and supplies its own Canva integration credentials through the in-app setup flow.

- Never embed a Canva Client Secret, access token, refresh token, or church-specific configuration in source control, GitHub Actions, release assets, or the executable.
- Save credentials and tokens only in protected, per-user Windows credential storage (for example, Windows DPAPI/Credential Manager). Do not place them in plain-text settings files or logs.
- Provide a **Disconnect Canva** action that removes local authorization data and, where practical, revokes the active authorization.
- Clearly explain that the local computer is the credential-security boundary.

> Important: Canva's current documentation recommends a secure backend for handling Client Secrets and token exchange, and cautions against desktop-only credential storage. This project intentionally uses a client-only church-managed model. Preserve that limitation in setup documentation and do not claim that it is Canva's preferred production security design.

### Windows-first, macOS-ready structure

Keep platform-neutral business logic separate from Windows-specific code.

```text
Shared core
  Canva connection and presentation/page models
  Slide text extraction and next-line selection
  Slide matching/tracking state and manual fallback
  Monitor layout models and persisted user preferences

Windows app (first release)
  WPF UI
  Windows display enumeration, placement, capture, and topmost APIs
  Windows protected credential storage

macOS app (future)
  macOS UI
  macOS display enumeration, placement, capture, and permissions
  Keychain credential storage
```

Do not make the first release cross-platform merely for its own sake. External display handling, capture permissions, window management, and packaging differ substantially between Windows and macOS. Build a stable Windows implementation first, while maintaining these boundaries so macOS does not require rebuilding the full product.

## Canva setup and connection

The app must include a clear **Canva setup** screen. It should guide a church through creating its own Canva integration and connecting the app without requiring a command line or separate configuration file.

The screen must include:

1. A brief explanation of the church-managed, local-only credential model.
2. An **Open Canva Developers** button that opens the official Canva developer portal.
3. The required loopback redirect URL in a read-only, copyable field. The exact URL must match the implementation.
4. Plain instructions to create/configure a Canva integration and add the redirect URL.
5. Fields for **Client ID** and **Client Secret**.
6. A note that secrets and tokens are stored only in protected local Windows storage and must not be shared.
7. Save, Cancel, Test connection, Connect Canva, and Disconnect Canva actions.
8. Safe, specific errors for missing credentials, redirect mismatch, denied authorization, invalid credentials, missing permissions, expired authorization, and network/API errors.

The authorization flow must use the current Canva-supported OAuth Authorization Code flow with PKCE. Confirm required scopes and API capabilities in official Canva documentation immediately before implementation; request only the least privilege needed to read the selected presentation's metadata, page content, and renderable page assets.

After authorization, the app must let the operator select a presentation/design and show its connection state, selected presentation title, and last successful refresh time.

## Windows and display behavior

### Main operator window

- Dark, polished, non-web Windows desktop UI.
- Can be pinned/unpinned as **Always on top** so controls remain usable while Canva is presenting.
- Provides clear controls for Canva connection, presentation selection, source display, confidence display, preview, output mode, layout, and diagnostics.
- Follows this simple Sunday flow: **1. Configure Canva, 2. Choose presentation and screens, 3. Test, 4. Start output.**

### Confidence-output window

- An independent window used only for the confidence display.
- Can be launched in a normal resizable Preview window on the main computer.
- Can be assigned to a selected display and maximized/full-screened there with an explicit action such as **Maximize on selected display**.
- Must recover safely if the chosen display is unplugged, changes resolution, or is no longer present. Fall back to Preview mode and show a clear status instead of crashing.
- Must not require a second monitor for initial setup, Canva connection, or Preview testing.

When Windows reports only one display, explain that a separate confidence screen requires Windows display mode **Extend**. Casting or mirroring can appear as one Windows display and does not guarantee independent audience and confidence outputs.

## Output modes

### Mirror mode

Mirror mode displays the selected/current Canva page image without confidence-specific reformatting. It should be pixel-faithful to the Canva page image the app has retrieved or rendered.

Mirror mode does **not** promise to reproduce transient operating-system or Canva presentation UI such as a mouse pointer, browser chrome, video playback state, animations, transition effects, or popovers.

### Monitor mode

Monitor mode creates a simplified, high-visibility confidence layout from the current slide's retrieved text content.

- Use large, high-contrast typography and generous spacing.
- Show the current slide's relevant text prominently.
- Optionally show the next slide's first usable line so musicians and presenters can anticipate what is coming.
- Make the next-line behavior configurable: **First non-empty line**, **Title only**, or **Hidden**.
- If usable text cannot be extracted (for example, text embedded in a raster image, video, or complex artwork), fall back to the slide image and clearly indicate that formatted text is unavailable.

Toggling Mirror/Monitor must update the confidence-output window immediately without closing or crashing it.

## Slide source and live tracking

The app retrieves Canva presentation/page information and page renderings after the user authorizes Canva access. It reads the page counter and available document identity from Canva Presenter through Windows accessibility.

### Tracking model

1. Retrieve ordered Canva pages, thumbnails, and dominant text content; remove hidden slides from local numbering.
2. Discover Presenter windows and their accessible page counters and document identity.
3. Match a Canva design ID when exposed, otherwise match the exact normalized presentation title. If matching is ambiguous or unavailable, require an operator-confirmed window. A known wrong design or visible-page count blocks following.
4. Follow only accepted pages; keep the last selected page when the source becomes unavailable. Use the following visible page as the next page.
5. Pause following on manual page navigation. Resume uses a fresh reading; disabled updates and previous scan generations cannot change the page. Retry scan exceptions with bounded backoff.

This is not a guaranteed Canva live-slide event feed. Present honest states in Live control and compact mode:

- **Following Canva** — the selected source passed identity/confirmation and page-count checks.
- **Choose Presenter window / Presentation mismatch** — identity is ambiguous, unavailable, or incompatible.
- **Waiting for Presenter / Reconnecting to Presenter** — no current acceptable signal or a retry is pending.
- **Paused — manual control / Manual control** — the operator has paused following or disabled automatic sync.

Provide a manual fallback that lets the operator select the current page and step backward/forward. Manual navigation pauses following so it cannot be immediately overwritten.

Real-machine acceptance must cover latency, browser/desktop provider support, wrong decks with equal page counts, multiple windows, restart recovery, and mixed-DPI displays. Image confidence thresholds and screen-content recognition belong to a future optional image-matching fallback.

## Styles and visual design

Monitor mode should offer a small, clear set of layouts:

- Current text + next line + clock
- Current text only
- Current text + next line, no clock
- Large speaker-focused current text

Changing a layout must apply immediately to Preview and the confidence output. Use near-black/charcoal surfaces, white main text, muted secondary text, subtle borders, and one restrained accent color. All dropdowns/menus must have dark backgrounds and readable white text. Dialogs must be opaque.

## Setup diagnostics

Provide a prominent **Test setup** panel rather than a single all-or-nothing modal. Each check must show Pass, Warning, or Fail, explain what happened, and provide one relevant next action.

| Check | Pass condition | Helpful response if unavailable |
|---|---|---|
| Preview output | Preview opens and renders | Explain that preview is sufficient for laptop-only testing. |
| Confidence display | Selected monitor exists | Offer Preview in a window or choosing another display. |
| Canva capture display | Selected source display exists | Explain how to select the display showing Canva. |
| Canva configuration | Client ID and Client Secret are present locally | Open Canva setup. |
| Canva authorization | Authorization and locally stored tokens are valid | Offer reconnect; report safe OAuth/API details. |
| Presentation source | A Canva design has been selected and its pages loaded | Offer Select presentation or Refresh. |
| Slide tracking | A recognizable current page is found | Explain Present mode, source-display selection, and manual fallback. |

Use status language that distinguishes:

- **Working without a second display**
- **Ready for live confidence output**
- **Canva connected but tracking unavailable**

## Reliability and persistence

- Persist the selected presentation, output display, capture display, chosen layout, output mode, next-line option, and always-on-top preference.
- Restore safe preferences when the app restarts, but never silently expose credentials or token values.
- Every output control (Start, Preview, Mirror, Monitor, maximize, display change) must be safe when no second display exists or when Canva is disconnected.
- Add clear, privacy-safe diagnostic logging for display/output lifecycle and tracking decisions. Never log Client Secrets, access tokens, refresh tokens, authorization codes, or raw authorization headers.

## Build, packaging, and release

The repository is hosted on GitHub. Add a GitHub Actions pipeline that:

1. Builds the Windows solution on a Windows runner.
2. Runs automated tests that can run in CI.
3. Publishes a Windows x64 **self-contained** release build, so end users do not need to install .NET.
4. Uploads the release artifact to the workflow run and, when a release/tag process is chosen, to the corresponding GitHub Release.
5. Saves build logs as artifacts when a build fails.

Initial distribution may be a self-contained executable/package with a simple launch guide. A signed installer, signing certificate, automatic updater, and private release feed are later enhancements, not MVP blockers.

For local Windows development/release packages, include an easy `Build Windows App.cmd` entry point that starts the PowerShell build process, asks before installing a missing .NET SDK, writes timestamped logs, prints the final artifact path, and remains open so non-developers can read the result.

## Delivery phases

### Phase 1 — Windows output MVP

- WPF main window and independent output/Preview window.
- Display selection, maximize-on-selected-display, output lifecycle, and laptop-only Preview.
- Always-on-top main window.
- Mirror/Monitor controls and layouts using test/local slide data.
- Multi-check diagnostics and safe logging.
- GitHub Actions Windows build artifact.

### Phase 2 — Canva connection and content

- In-app Canva setup and OAuth with PKCE.
- Protected local credential/token storage.
- Presentation selection and page/content retrieval.
- Mirror rendering from retrieved Canva page images.
- Monitor text extraction and next-line configuration.

### Phase 3 — Assisted live tracking

- Implemented: automatic accessibility discovery, design-ID/title matching, explicit source confirmation, manual pause/resume, and retry recovery.
- Implemented: visible Following/Waiting/Manual/Paused/Mismatch/Recovering states and deterministic regression checks.
- Remaining acceptance: real Canva/provider and physical display testing as documented in `docs/v0.6.0-acceptance.md`.
- Deferred alternative: selected-display capture and page-image matching if a required Presenter setup lacks accessible counters.

### Phase 4 — Production refinement and future macOS work

- Windows installer signing and installer experience, if desired.
- Sunday setup guide and Windows acceptance testing.
- Native macOS app that reuses the shared core while implementing macOS display capture, permissions, window placement, and Keychain storage.

## Acceptance criteria for the first Windows release

- [ ] A clean Windows build produces a self-contained Windows x64 release artifact through GitHub Actions.
- [ ] The app launches without Visual Studio or a preinstalled .NET runtime.
- [ ] The app opens and Preview works with one display connected.
- [ ] The main window can be set Always on top.
- [ ] The output window launches, can be previewed, and can be maximized on a selected second display.
- [ ] Mirror and Monitor toggles change the output without crashing or restarting it.
- [ ] The output recovers safely when a selected display is removed or reconfigured.
- [ ] Dropdowns are dark and readable; dialogs are opaque and readable.
- [ ] Test setup gives independent diagnostic results and does not reject laptop-only testing.
- [ ] Canva setup and connection require no command-line configuration.
- [ ] Credentials/tokens are not committed, logged, displayed, or stored in plain text.
- [ ] The app clearly distinguishes Tracking, Uncertain, and Not tracking.
- [ ] Manual current-slide controls provide a usable fallback whenever tracking is not reliable.
- [ ] Monitor mode accurately communicates when text formatting is unavailable instead of presenting an incorrect result as certain.

## Development reporting for each iteration

For every implementation iteration, report:

1. What changed.
2. How it was tested on Windows.
3. What remains a prototype, is limited by Canva/API capability, or requires a real Canva account.
4. Exactly how to obtain the updated Windows build artifact.
