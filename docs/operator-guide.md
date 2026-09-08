# Operator guide

## Prepare the presentation

1. Open CanvaStage using the Windows account used during Canva setup.
2. Select **Presentation**.
3. Choose **Refresh**, select the Canva presentation, and choose **Apply**.
4. Wait for the export, download, text extraction, and image caching to finish.

The progress bar shows exact transferred bytes when Canva supplies the export size. Export preparation remains indeterminate because Canva does not publish a percentage for that stage.

Hidden slides are removed from CanvaStage’s local page order. Smaller header and footer text blocks are ignored when a larger main text block is available. If a page has no usable text, Monitor mode shows its retrieved page image.

Use **Check for updates** to compare the cached copy with Canva’s current modification time. Choose **Re-download** after changing the Canva presentation.

## Choose the confidence display

1. In Windows display settings, select **Extend these displays**.
2. In CanvaStage, open **Displays** and choose **Identify displays**.
3. Match the large temporary number on each screen with the numbered dropdown entry.
4. Choose the Extended display intended for the stage.
5. Use **Open Preview** to verify content and appearance.

The dropdown includes the monitor name reported by Windows, resolution, and Primary or Extended status. CanvaStage remembers the monitor’s hardware identity when Windows provides one.

## Start automatic Presenter following

1. Open the presentation in Canva.
2. Start the Canva Presenter View used during the service.
3. Keep only one Canva Presenter View open.
4. Run Canva and CanvaStage at the same Windows permission level. Normally, neither should run as administrator.
5. Confirm CanvaStage reports **Following Canva**.

CanvaStage reads the first Canva Presenter page counter exposed through Windows accessibility. It checks the visible-page total against the applied presentation before changing pages. It does not capture the screen or inspect browser traffic.

Manual Previous or Next navigation pauses automatic following. Choose **Resume sync** to return to the current Presenter page. If tracking becomes unavailable, CanvaStage holds the last page and manual controls remain usable.

## Start and control output

- **Mirror** shows the retrieved Canva page image.
- **Monitor** shows simplified confidence text using the selected layout and appearance.
- **Start on selected display** opens borderless output on the chosen monitor.
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

## Cache and saved data

Closing the app keeps the downloaded presentation, preferences, Client Secret, and Canva authorization.

On the Presentation page:

- **Clean old downloads** removes previous immutable download generations while retaining the active presentation.
- **Clear cache** removes downloaded presentation content and returns to the demo deck. It keeps credentials, preferences, and the saved presentation selection.
- **Re-download** creates a fresh cache generation for the selected presentation.

Use **Disconnect** only when the local Canva credentials should be removed.

## Diagnostics

Open **Setup & diagnostics** to review:

- Presenter sync health;
- the last detected page, observation time, and scan duration;
- Canva connection and presentation readiness;
- confidence-display availability.

**Export diagnostics** creates a sanitized ZIP on the desktop. The bundle excludes credentials, tokens, authorization codes, presentation text, and slide images.

The regular log is stored at:

```text
%LOCALAPPDATA%\CanvaStage\canvastage.log
```

## Known boundaries

- CanvaStage requires Canva Presenter to expose a readable page counter through Windows accessibility.
- Only one Presenter View should be open.
- Videos, transitions, animations, browser controls, and pointer movement are not mirrored.
- The current package is unsigned, so Windows may display a SmartScreen warning.
- Canva integration credentials are protected locally, but Canva recommends a server-side integration architecture for production public integrations.
