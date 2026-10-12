# Windows acceptance guide

Use this checklist before relying on a new release during a service. Automated verification covers deterministic behavior, but it cannot replace a real Canva account, physical monitors, and the actual presentation computer.

## Before a service

1. Extract the complete release ZIP and run CanvaStage as the usual Windows operator. Administrator elevation is not required.
2. Confirm **Saved securely** appears under Client Secret.
3. Use **Setup → Simple → Start setup** to check the last-used deck and saved Extended display. For a different deck/display or first-time setup, use Advanced's numbered steps. Force Re-download in Advanced if Canva's metadata has not caught up with an edit.
4. Open one Canva Presenter View and confirm **Following Canva**.
5. Identify the displays, select the Extended confidence monitor, check content in Preview, then start output to confirm physical display placement.
6. Start output and verify page changes, Mirror, Monitor, Blank output, and Restore output.
7. Test manual Previous or Next, confirm sync pauses, and then resume it.

## Real-machine checklist

- [ ] For v0.12.0 (#36–40), confirm Setup initially shows Simple; use Advanced once to Apply the deck/select the Extended display, then Start setup. Repeat with unchanged content (no exports), edited content (download progress), missing cached images, missing deck, disconnected target, and Primary-only Windows layout. Confirm every refusal explains the cause and recovery without guessing another target. Simple Check slides must not download unchanged content; Advanced force-refresh remains available for lagging Canva metadata.
- [ ] While Simple Setup downloads, navigate Presenter, then choose Stop/Cancel setup start in Full view. Completing the download must not restart output. Restart setup afterward and confirm it reuses the finished cache. Test revoked credentials, cancelled browser authorization, export failure and an interrupted download; cached content must remain available.
- [ ] Invoke Stop output from Advanced and Live control with separate Preview open, in Mirror and Monitor and after Blank. The borderless window must disappear and remain off through Presenter changes, mode/style changes and Preview reopening. Start again explicitly and confirm output returns to the remembered screen. Diagnostic stop entries should report live/visible false after completion.
- [ ] Resize Compact to a small 260 × 260 window using wide and portrait sources. Confirm the complete preview and all controls remain available, no scrollbar appears, Full view still restores normal controls, and On top remains Compact-only. Test gentle touchpad movement, fast direction changes, a normal mouse wheel, scrollbar drag and keyboard scrolling in Full view without jumps or animated snap-back.
- [ ] Trigger Presenter mismatch, no Presenter, disabled/paused sync and scan errors. Full view must explain actual page counts where available, why output is holding and what to do; Compact's status tooltip must contain the same detail. Confirm normal page following remains unchanged.
- [ ] Launch the self-contained package without a separately installed .NET runtime.
- [ ] Test the Canva desktop application or supported browser used during services.
- [ ] Navigate forward, backward, and to distant pages. Confirm CanvaStage follows within an acceptable delay.
- [ ] Confirm hidden Canva slides are skipped and the visible page total matches Presenter View.
- [ ] Confirm main text excludes smaller headers, footers, and copyright notices on representative slides.
- [ ] Close Presenter View. Confirm output holds the last page, then reopen Presenter View and confirm following resumes.
- [ ] Open a different deck with another visible-page count. Confirm CanvaStage reports a mismatch and holds the last valid page.
- [ ] Disable automatic sync, restart, and confirm it remains disabled. Temporary pauses should not persist.
- [ ] Test one-screen Preview and at least one physical Extended display.
- [ ] If using Windows wireless casting, repeat sustained Presenter navigation on the actual receiver (including the Samsung TV reported in issue #24). Compare with a wired display when available. Test disconnect/reconnect and confirm display changes stop output safely; reselect the display and start output after reconnecting.
- [ ] Use **Identify displays** and confirm names, numbers, resolution, and Primary/Extended status are understandable. Include 250% Windows scaling and an Extended display to the left/above the primary; each temporary label must stay inside its screen without stealing focus.
- [ ] For the issue #30 development fix, connect/disconnect a display while Setup is open. Confirm the dropdown updates automatically, refresh/reordering preserves the chosen monitor, disconnect does not silently select the primary, and a renamed reconnect restores the remembered target without restarting output. Choose **Start output** explicitly and verify the actual target screen. Repeat after restarting with the chosen screen disconnected.
- [ ] Test a 1152 × 864 output and mixed monitor scaling at 100%, 125%, 150%, and 200%. Confirm borderless output fills exactly the selected display.
- [ ] Move and resize the main window at each scale. Confirm its title bar remains reachable.
- [ ] Use Setup → Advanced's numbered steps to load the deck, identify/select the display and start output. Confirm Client ID/Secret and appearance controls are only in Settings & Diagnostics; saved values survive upgrading and reopening.
- [ ] In Compact view, confirm there is no Pause Sync button, the page/status remain visible, and Full view restores access to resume/manage sync.
- [ ] Confirm the retained #29/#31–34 UI behavior: Live control has a small readable gap below its preview label and no side bars within the preview frame. The whole frame must retain the output's aspect ratio at wide, standard and portrait dimensions, including after a display or window resize. Compact shows the complete preview, scaling rather than scrolling/cropping when resized smaller; Full view must not remain topmost, even with the saved Compact On top choice enabled. Settings connection/cache cards must be always visible and use normal-sized field/button text.
- [ ] For #35, play an audio-bearing Canva slide with CanvaStage closed and note whether the confidence TV still plays audio. Repeat with HDMI versus casting if available. Inspect the presenting app/browser's Windows Volume mixer output device; verify any operator-chosen rerouting preserves the audience sound system. CanvaStage must not play media or automatically mute other apps/devices.
- [ ] Confirm the version shown below the sidebar information and in the title bar matches the versioned download filename. In Live control, sync actions appear once beside the status and Mirror/Monitor appear once under Sunday controls. Check rounded preview corners and tighter spacing at each scale; live output must remain rectangular and unchanged.
- [ ] Compare in-app, compact and separate Preview with live output at different window sizes and display aspect ratios/scaling. Current text wrapping, next-line ellipsis, clock, colors, image fallback and blank state must match. Live control and Compact containers follow the source aspect ratio; the separate resizable Preview may letterbox. Black areas already present in the output or slide image remain. Opening/resizing/closing Preview must not relocate, stop or reflow the live output. Before output starts, Preview must show the cached deck using the selected display's viewport without starting a live session.
- [ ] Test every Monitor layout, maximum font sizes, long text, image fallback, colors, next-line styling, and the clock.
- [ ] Reapply the same deck while an image is displayed. Confirm refresh completes without a file-in-use error.
- [ ] Close and reopen CanvaStage. Confirm the cached presentation and Canva connection remain available.
- [ ] Confirm reopening loads the presentation list without a browser authorization prompt. Repeat after the access token has expired; token renewal should be automatic.
- [ ] Edit the applied deck while output is live. During the download, navigate Presenter, change mode and appearance, and blank/restore output. Confirm the refreshed content appears on the current page and tracking continues.
- [ ] Stay in Live control for several minutes without opening Setup. Verify the refresh card's last check advances about 30 seconds after each completed check, unchanged decks do not show repeated downloads, and next check/retry times are visible. Edit a slide and confirm download progress appears here and content swaps without restarting output or Presenter tracking. Inspect the card at 125% and 200% scaling for wrapped, legible text.
- [ ] Edit an image-only page while Mirror output is live. Confirm the newly exported image appears after refresh, not the old preview thumbnail. With automatic updates enabled, select **Check for updates** and verify it reloads even when Canva still reports the old modification time. With automatic updates disabled, verify that button only checks metadata and **Re-download** still forces a reload.
- [ ] Reopen an older thumbnail-based cache: automatic updates should migrate it once to rendered images; with automatic updates disabled it should stay usable and prompt for Re-download. Test failed image exports/downloads and verify the previous complete deck remains live.
- [ ] Re-download the slide reported in issue #21. Compare Monitor words and spaces with Canva and check the slide image in Mirror separately. Standalone space runs must remain spaces, while split runs inside a word must not gain extra spaces.
- [ ] Add or remove a visible page, then confirm a temporary count mismatch resolves when the refreshed deck arrives. Repeat with sync paused and verify the pause and blank state are preserved.
- [ ] Disconnect the network during a refresh. Confirm the previous deck remains usable, an error is shown, and automatic refresh recovers after connectivity returns. Turning automatic updates off must stop future checks.
- [ ] Confirm Live control shows a safe refresh error and retry time after failure, clears the error after successful retry or Re-download, and shows checks disabled or reconnection needed when appropriate.
- [ ] Use a long paragraph on the next slide. Confirm the next cue is one line with an ellipsis in both previews and the confidence window; authored soft line breaks must be preserved in the main text.
- [ ] Select maximum main font size, then navigate short → long → short slides in Monitor mode. Text must fit before drawing. Repeat while resizing output, changing layouts and toggling the clock. Preview resizing must scale the same visual without changing the output's text wrapping.
- [ ] With an unchanged deck, advance at least 40 Presenter pages and navigate backward, then pause/resume and use manual Previous/Next. Compare the operator page/status, in-app preview, separate Preview and physical output: text must keep changing on all surfaces, not just the operator view. If a surface freezes, export diagnostics before restarting. Check the page-change pipeline and explicitly UTC timestamps; older appended entries retain their original offsets.
- [ ] Try Aptos, Arial, Segoe UI and Verdana. Current and Next text must share the selected family and default semibold weight. Changing Next size must not change current size; Next Bold must remain an independent heavier-weight override.
- [ ] Compare mixed case, legitimate all-caps words and acronyms against Canva and an unedited PPTX export. Check both Monitor surfaces after Apply/Re-download and cache reopening. Do not toggle Canva casing before saving the failing example.
- [ ] Open Preview, stop output, then close the main window. Confirm no hidden CanvaStage process remains. Repeat while output is live and approve the close prompt; confirm the confidence window closes.
- [ ] Check for presentation updates, clean old downloads, and clear the cache.
- [ ] Reopen a cached deck after deleting one cached page image in a test profile. Confirm Monitor text remains available and the app warns that Mirror images need re-downloading.
- [ ] Unplug or reconfigure the confidence display. Confirm output stops safely.
- [ ] Export diagnostics and inspect the ZIP for credentials, tokens, authorization codes, presentation text, and slide images.
- [ ] Confirm the release ZIP contains `LICENSE` (Apache License 2.0) and `TRADEMARKS.md` beside the executable.

## Automated release verification

The GitHub workflow:

1. restores, builds, and tests the solution on Windows;
2. publishes a self-contained Windows x64 package;
3. launches the published executable with isolated synthetic data;
4. verifies Presenter following, pause/resume, output blanking, DPAPI reuse, saved-authorization startup, concurrent token renewal and rejected-token recovery, live deck refresh during navigation, refresh failures and cancellation, changed page counts, changed Mirror pixels despite stale thumbnails, unchanged-timestamp manual reloads, legacy cache migration, rendered-image validation and hidden-page mapping, Canva page-schema validation, damaged-cache recovery and cleanup, display bounds, contrast, WPF bindings, output text fitting, and single-line next cues;
5. renders every operator section and Monitor layout at representative 100%, 125%, 150%, and 200% effective sizes;
6. attempts to upload test results, the verification report, and UI screenshots; non-tag runs also upload a short-lived package artifact. GitHub Actions storage quota can block these artifacts; the verification summary remains in the workflow run, and a tagged release uploads its ZIP and checksum directly to GitHub Releases.

The simplified workflow also checks ownership of credentials/appearance settings, the absence of compact sync controls, shared preview/output visual identity, 1:1 pixel parity, uniform scaled previews, independent offline/live Preview window lifecycle and offline cached text/images. Windows run [37229598823](https://github.com/mj00009/CanvaStage/actions/runs/37229598823) passed these checks for commit `93b2618`, including narrow-sidebar label wrapping. Representative Settings & Diagnostics, compact and offline Preview screenshots were reviewed.

The current Windows checks observe first-frame fitting on both Monitor surfaces, verify default Current/Next typography across four curated fonts, independent cue sizes and the bold override. They also compare the separate output's rendered drawing pixels across 40 Presenter pages, pause/manual/resume transitions and backward jumps, including exact mixed-case text in output and in-app view models. That observation path does not force layout, fitting or redraw before checking pixels, so it cannot deliberately repair a stale visual before inspecting it. It does not replace physical-screen or real-Canva acceptance. The v0.10.4 release checked first-frame in-app fitting and settled output fitting instead.

The generated screenshots require visual review. Synthetic DPI rendering does not change the runner’s physical display configuration, so mixed-DPI and hotplug behavior remain real-machine checks.

## Reporting a problem

Include:

- CanvaStage version;
- Windows version;
- Canva desktop app or browser name and version;
- monitor resolution and scaling;
- the exact on-screen status;
- reproduction steps;
- the sanitized diagnostic ZIP from **Settings & Diagnostics**.

For frozen or blank secondary output, also record the connection type (HDMI/DisplayPort, dock/adapter, or Windows wireless casting), receiver model, whether the mouse remains visible, and whether an unrelated app such as Notepad displays there. Note whether restarting only CanvaStage or reconnecting the display restores content. Export diagnostics before recovery when possible. A visible mouse alone does not establish that app content is rendering correctly.

Do not include the Client Secret, tokens, authorization codes, or screenshots of credential fields.
