# Operator guide

This guide describes v0.11.1, with simplified navigation and shared-output previews. Earlier releases have different navigation and preview behavior.

## Prepare the presentation

1. Open CanvaStage using the Windows account used during Canva setup.
2. Select **Setup → 1. Load the Canva deck**.
3. The saved Canva connection loads the presentation list automatically. Select the presentation and choose **Apply**; use **Refresh** if needed.
4. Wait for the PowerPoint export, text extraction, separate Mirror image export, and image caching to finish.

The progress bar shows exact transferred bytes when Canva supplies the export size. Export preparation remains indeterminate because Canva does not publish a percentage for that stage.

Mirror and textless-page fallback use newly exported JPEG images, not Canva's page-preview thumbnails. Reference images use regular export quality, JPEG quality 70, and a width capped at 1280 pixels when page dimensions are available, without enlarging smaller designs. Preparing both exports can take several minutes for large decks. If any new image cannot be retrieved or validated, the previous deck remains active; CanvaStage does not silently substitute a potentially stale thumbnail.

Hidden slides are removed from CanvaStage’s local page order. Smaller header and footer text blocks are ignored when a larger main text block is available. If a page has no usable text, Monitor mode shows its retrieved page image.

**Automatically apply Canva content updates** is enabled by default. CanvaStage schedules its next check 30 seconds after the previous check or download finishes and downloads a new copy when Canva reports a newer modification time. Unchanged decks only receive a metadata check, not another export. Checks run in Live control without opening Setup; a busy Canva action or busy Windows UI can delay a check. This continues during a service: the existing deck remains live and follows Presenter while the new copy downloads, then the updated content replaces it. Export preparation and large downloads can take several minutes; edits are not instantaneous.

The **Content refresh** card at the top of **Live control** shows the current state, last completed metadata check, last content load, and next check or retry time. It also shows download progress and safe error details. A completed metadata check means the cache matches Canva's reported modification time, not a guarantee that Canva has published every recent edit. The card reports when automatic checks are off or reconnection is needed.

You do not need to stop and restart CanvaStage output or Presenter tracking to use newly downloaded content. The refresh preserves the current Presenter page when its counter matches the new deck, manual pauses, appearance, output placement, and blank state. If pages were added or removed, tracking can temporarily report a count mismatch until the new copy is ready. A failed refresh keeps the current deck and retries with a longer delay (up to five minutes). Updates still download the complete visible deck; selective image refresh is deferred.

Turn automatic updates off in **Settings & Diagnostics → Content refresh and local cache** to control updates manually. With automatic updates enabled, **Check for updates** reloads the applied deck even if Canva's reported modification time has not advanced. With automatic updates disabled, it only checks the modification time; use **Re-download** to obtain new content. **Re-download** always reloads the applied deck, even if a different presentation is highlighted in the dropdown. Changing to another deck still requires **Apply**.

Older cached decks that use preview thumbnails are upgraded once on the next automatic check. If automatic updates are disabled, choose **Re-download** to upgrade. Automatic checks still depend on Canva reporting a newer modification time after that upgrade; if an edit has not appeared, use **Check for updates** with automatic updates enabled or **Re-download**.

The next-line cue shows only the first authored line of the next page's main text. A paragraph without authored line breaks displays as one line, with an ellipsis when it is too long for the available width.

## Choose the confidence display

1. In Windows display settings, select **Extend these displays**.
2. In CanvaStage, open **Setup → 2. Choose the confidence display** and choose **Identify displays**.
3. Match the large temporary number on each screen with the numbered dropdown entry.
4. Choose the Extended display intended for the stage.
5. Use **Open Preview** to verify content and appearance in a resizable window. Use **Start output** to verify placement on the physical confidence monitor.

The dropdown includes the monitor name reported by Windows, resolution, and Primary or Extended status. CanvaStage remembers the monitor’s hardware identity when Windows provides one.

## Monitor typography

Open **Settings & Diagnostics → Monitor appearance** for font, maximum size, colors, contrast guidance and Next cue styling. Layout, next-line selection and the optional clock are on the same Settings page. Changes apply immediately and remain saved.

The current text automatically shrinks to fit its available area. Text fits once on the output surface, before drawing; all previews copy that same visual. Your selected font size is a maximum, not a guarantee that long slides will use that size. See the [v0.11.0 release notes](releases/v0.11.0.md) for changes and acceptance checks.

Current text and the Next cue use the same selected font family and, by default, the same semibold weight. The Next cue retains its independent size and color; its optional **Bold** setting makes that cue heavier without changing the current text. Every preview uniformly scales the entire output visual, without independently wrapping or resizing text. Letterboxing preserves the display's aspect ratio. Opening or closing the separate Preview does not move, stop or start live output. Before starting output, Preview uses the selected display's dimensions and Windows DPI; while live, it copies the actual output window. This is a content/layout reference, not proof that a physical or wireless display is receiving the frame. Next cues remain a single line with an ellipsis when necessary; current text remains complete.

## Start automatic Presenter following

1. Open the presentation in Canva.
2. Start the Canva Presenter View used during the service.
3. Keep only one Canva Presenter View open.
4. Run Canva and CanvaStage at the same Windows permission level. Normally, neither should run as administrator.
5. Confirm CanvaStage reports **Following Canva**.

CanvaStage reads the first Canva Presenter page counter exposed through Windows accessibility. It checks the visible-page total against the applied presentation before changing pages. It does not capture the screen or inspect browser traffic.

Manual Previous or Next navigation pauses automatic following. Choose **Resume sync** to return to the current Presenter page. If tracking becomes unavailable, CanvaStage holds the last page and manual controls remain usable.

## Start and control output

In **Live control**, Mirror/Monitor are under **Sunday controls**. Pause/Resume sync and Sync details appear once, beside the status at the top. The app version is shown below the sidebar information and in the title bar, including when the sidebar is hidden. Operator previews have rounded corners and tighter spacing; live output retains its full rectangular frame.

- **Mirror** shows the retrieved Canva page image.
- **Monitor** shows simplified confidence text using the selected layout and appearance.
- **Start output** opens borderless output on the chosen monitor.
- **Blank output** makes the live confidence window black without closing it.
- **Restore output** returns the current page.
- **Stop output** closes the live session.

The output window can always be closed with `Escape`. Moving the pointer over its upper-right corner reveals a close button.

### Keyboard shortcuts

Shortcuts work while the main CanvaStage window has focus:

| Shortcut | Action |
|---|---|
| `Ctrl+Left` | Previous page and pause sync |
| `Ctrl+Right` | Next page and pause sync |
| `Ctrl+Space` | Pause or resume Presenter sync |
| `Ctrl+B` | Blank or restore output |
| `Ctrl+1` | Mirror mode |
| `Ctrl+2` | Monitor mode |

### Compact view

Compact view contains the output preview, Mirror/Monitor, blank/restore, Always on top and Full view. There is no Pause Sync button. To resume or manage following, return to **Full view**. The sync status remains visible; manual navigation and keyboard shortcuts still work in the main window.

## Cache and saved data

Closing the app keeps the downloaded presentation, preferences, Client Secret, and Canva authorization.
The app reuses and renews the saved authorization at startup without opening a browser. Reauthorization is needed if Canva revokes or rejects the saved refresh token, or the integration credentials change; choose **Connect Canva** when the app reports that condition. Content checks never open an authorization browser automatically.

In **Settings & Diagnostics → Content refresh and local cache**:

- **Clean old downloads** removes previous immutable download generations while retaining the active presentation.
- **Clear cache** removes downloaded presentation content and returns to the demo deck. It keeps credentials, preferences, and the saved presentation selection.
On **Setup**, **Re-download** creates a fresh cache generation for the applied presentation, independently of the highlighted dropdown selection.

Use **Disconnect** only when the local Canva credentials should be removed.

## Diagnostics

Open **Settings & Diagnostics** to review:

- Presenter sync health;
- the last detected page, observation time, and scan duration;
- Canva connection and presentation readiness;
- confidence-display availability.

**Export diagnostics** creates a sanitized ZIP on the desktop. The bundle excludes credentials, tokens, authorization codes, presentation text, and slide images.

The regular log is stored at:

```text
%LOCALAPPDATA%\CanvaStage\canvastage.log
```

New diagnostic entries and embedded observation/content timestamps use ISO 8601 UTC with a `Z` suffix. Existing older entries keep their original explicit timezone offsets; they are not rewritten. The app's operator-facing clock and status times remain Windows-local. The exported summary labels local status text separately from its UTC creation time.

Page-change entries distinguish **Presenter observation**, **Presenter page selected**, **Output snapshot received**, **Output update queued**, and **Output render stage**. Output entries include page, mode, visibility, revision, character counts, text-binding match, font size, viewport and DPI, but never slide text or credentials. Render-stage entries observe WPF's callback; they do not prove that physical screen pixels changed. If output freezes, record whether the page/status, in-app preview, separate Preview, and display output still change, then export diagnostics before restarting.

Placement and display-change entries record output bounds and available displays; **Output graphics state** records WPF rendering capability, the process render-mode setting and DPI when output is shown or its rendering tier changes. These are observations, not a GPU-health check, and do not reset graphics or change rendering mode. They cannot verify delivery to a wireless display. For blank secondary output, note the connection type, whether the mouse is visible, whether an unrelated app displays there, and whether recovery requires restarting CanvaStage or reconnecting the display.

When investigating a freeze, compare entries around its time. Advancing **Presenter page selected**, **Output snapshot received** and **Output render stage** entries with `text matches True` show that page selection, output binding and WPF render-stage callbacks continued; they do not prove successful physical presentation. Missing render callbacks are a clue, not proof of an app bug: visibility, graphics or other rendering conditions may also affect them. Exceptions or display-change entries may provide additional evidence. If unrelated apps also fail on the same receiver, investigate the Windows graphics/casting path alongside CanvaStage. Windows Event Viewer and Reliability Monitor may have corresponding application, display-driver or hardware errors. Compare timezone offsets carefully: new app logs are UTC while Windows views may show local time. See [Microsoft's freeze troubleshooting guidance](https://learn.microsoft.com/en-us/troubleshoot/windows-client/performance/windows-based-computer-freeze-troubleshooting) and [wireless display guidance](https://support.microsoft.com/en-us/windows/hardware/display-graphics/fix-connections-to-wireless-displays-or-docks-in-windows).

Monitor text preserves the case stored in Canva's exported PPTX; the extractor does not apply automatic upper/lower/title casing. For a casing mismatch, provide an affected PPTX export before editing its case, the expected text or screenshot, and whether **Re-download** without editing changes the result. A diagnostic ZIP deliberately excludes the slide text/PPTX and cannot establish how Canva encoded the affected words.

## Known boundaries

- CanvaStage requires Canva Presenter to expose a readable page counter through Windows accessibility.
- Only one Presenter View should be open.
- Videos, transitions, animations, browser controls, and pointer movement are not mirrored.
- The current package is unsigned, so Windows may display a SmartScreen warning.
- Canva's published REST API guidance does not support desktop apps that store integration credentials. CanvaStage uses local Windows protection for this self-managed deployment, but that does not make it a Canva-supported desktop integration. See [Canva setup](canva-setup.md).
