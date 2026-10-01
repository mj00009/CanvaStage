# Windows acceptance guide

Use this checklist before relying on a new release during a service. Automated verification covers deterministic behavior, but it cannot replace a real Canva account, physical monitors, and the actual presentation computer.

## Before a service

1. Extract the complete release ZIP and run CanvaStage as the usual Windows operator. Administrator elevation is not required.
2. Confirm **Saved securely** appears under Client Secret.
3. Refresh and apply the service presentation. Re-download it after Canva edits.
4. Open one Canva Presenter View and confirm **Following Canva**.
5. Identify the displays, select the Extended confidence monitor, check content in Preview, then start output to confirm physical display placement.
6. Start output and verify page changes, Mirror, Monitor, Blank output, and Restore output.
7. Test manual Previous or Next, confirm sync pauses, and then resume it.

## Real-machine checklist

- [ ] Launch the self-contained package without a separately installed .NET runtime.
- [ ] Test the Canva desktop application or supported browser used during services.
- [ ] Navigate forward, backward, and to distant pages. Confirm CanvaStage follows within an acceptable delay.
- [ ] Confirm hidden Canva slides are skipped and the visible page total matches Presenter View.
- [ ] Confirm main text excludes smaller headers, footers, and copyright notices on representative slides.
- [ ] Close Presenter View. Confirm output holds the last page, then reopen Presenter View and confirm following resumes.
- [ ] Open a different deck with another visible-page count. Confirm CanvaStage reports a mismatch and holds the last valid page.
- [ ] Disable automatic sync, restart, and confirm it remains disabled. Temporary pauses should not persist.
- [ ] Test one-screen Preview and at least one physical Extended display.
- [ ] Use **Identify displays** and confirm names, numbers, resolution, and Primary/Extended status are understandable.
- [ ] Test a 1152 × 864 output and mixed monitor scaling at 100%, 125%, 150%, and 200%. Confirm borderless output fills exactly the selected display.
- [ ] Move and resize the main window at each scale. Confirm its title bar remains reachable.
- [ ] Test every Monitor layout, maximum font sizes, long text, image fallback, colors, next-line styling, and the clock.
- [ ] Reapply the same deck while an image is displayed. Confirm refresh completes without a file-in-use error.
- [ ] Close and reopen CanvaStage. Confirm the cached presentation and Canva connection remain available.
- [ ] Confirm reopening loads the presentation list without a browser authorization prompt. Repeat after the access token has expired; token renewal should be automatic.
- [ ] Edit the applied deck while output is live. During the download, navigate Presenter, change mode and appearance, and blank/restore output. Confirm the refreshed content appears on the current page and tracking continues.
- [ ] Edit an image-only page while Mirror output is live. Confirm the newly exported image appears after refresh, not the old preview thumbnail. With automatic updates enabled, select **Check for updates** and verify it reloads even when Canva still reports the old modification time. With automatic updates disabled, verify that button only checks metadata and **Re-download** still forces a reload.
- [ ] Reopen an older thumbnail-based cache: automatic updates should migrate it once to rendered images; with automatic updates disabled it should stay usable and prompt for Re-download. Test failed image exports/downloads and verify the previous complete deck remains live.
- [ ] Add or remove a visible page, then confirm a temporary count mismatch resolves when the refreshed deck arrives. Repeat with sync paused and verify the pause and blank state are preserved.
- [ ] Disconnect the network during a refresh. Confirm the previous deck remains usable, an error is shown, and automatic refresh recovers after connectivity returns. Turning automatic updates off must stop future checks.
- [ ] Use a long paragraph on the next slide. Confirm the next cue is one line with an ellipsis in both previews and the confidence window; authored soft line breaks must be preserved in the main text.
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

The generated screenshots require visual review. Synthetic DPI rendering does not change the runner’s physical display configuration, so mixed-DPI and hotplug behavior remain real-machine checks.

## Reporting a problem

Include:

- CanvaStage version;
- Windows version;
- Canva desktop app or browser name and version;
- monitor resolution and scaling;
- the exact on-screen status;
- reproduction steps;
- the sanitized diagnostic ZIP from **Setup & diagnostics**.

Do not include the Client Secret, tokens, authorization codes, or screenshots of credential fields.
