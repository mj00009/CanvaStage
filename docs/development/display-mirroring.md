# Audience mirroring fallback (unreleased)

This development change adds **Mirror the audience screen** at the top of **Setup → Advanced**. Source and public help remain v0.13.0 until a release is requested.

## Operator flow

Start with three connected, Extended screens: the primary operator screen, the audience screen, and the confidence screen. Select the confidence screen in step 2, then choose the audience screen in the fallback card. Display numbers can change; check the screen names rather than assuming they are always 2 and 3.

**Mirror audience screen** asks which named screens will be mirrored and explains that confidence output will stop. The confirmation ends with **Are you sure you want to do this?**, with **Yes** and **No**. No is the default. Choosing No leaves the layout and live output alone.

After Yes, CanvaStage stops its confidence output and asks Windows to mirror the audience screen onto the confidence screen. The primary operator screen stays independent. A second dialog asks whether the audience slides are visible on both screens. Choose Yes within 15 seconds to keep the layout. No, closing the dialog, or leaving it unanswered restores the previous layout. Keep Canva's audience presentation on the audience screen; CanvaStage does not move or start that presentation.

**Restore extended layout** also asks Yes/No, then restores the saved desktop. Confidence output stays stopped after either action. Check the confidence screen and start output when ready. Separate output and Simple Setup are blocked while a restore point remains, so the confidence window cannot cover the shared audience desktop.

## Windows integration and recovery

`WindowsDisplayTopology` uses Windows CCD APIs to query active paths and validate a supplied layout before applying it. Virtual-mode paths retain their source identities and copy only the audience source-mode geometry; legacy paths share the audience source on a compatible adapter. Other paths are retained. The policy rejects missing, primary, identical, ambiguous or already-cloned choices. Windows must accept the configuration; not every driver, dock or wireless receiver supports the requested pair.

The original layout is saved to `%LOCALAPPDATA%\CanvaStage\display-layout-restore.json` before switching. A failed save prevents the switch. The record survives an app restart. Before applying, the service checks that the confirmed layout has not changed. Afterwards it checks physical targets, geometry and the primary screen; unexpected changes trigger restoration.

If a screen disappears or restoration fails, the record is kept. Reconnect the same screens and try **Restore extended layout**. If Windows or the graphics adapter has changed, use Windows Display settings to restore Extend. The app refuses to guess a different hardware layout. If the saved record is no longer usable, close CanvaStage, verify the intended Extended layout in Windows, and remove only `display-layout-restore.json` before reopening. Keep credentials, preferences and cached decks.

Mirroring cannot reconnect a missing wireless screen or establish the cause of a casting failure.

The API approach follows Microsoft's [CCD example](https://learn.microsoft.com/en-us/windows-hardware/drivers/display/ccd-example-code) and [SetDisplayConfig contract](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setdisplayconfig).

## Related issues

[#46](https://github.com/mj00009/CanvaStage/issues/46): the sidebar Presenter card now shows the current sync status, a matching outline, and a short explanation or recovery step. Pause/Resume and Sync details sit beneath it. Narrow or short windows retain the header controls when the sidebar card is hidden.

[#47](https://github.com/mj00009/CanvaStage/issues/47): the attached screenshots show different casing between CanvaStage and the authored slide. They do not establish where the change occurs. The importer reads PPTX text runs without uppercase/lowercase conversion, and existing mixed-case import/render checks pass. Diagnosis still needs an unedited affected Canva PPTX export and a comparison after Re-download. Do not add blanket lowercase conversion; legitimate uppercase words and acronyms must remain intact. No fix is claimed for this issue.

## Verification boundary

Unit tests cover native structure sizes, virtual and legacy clone policy, unavailable source modes, rejected choices, stale consent, persistent restoration, save/apply/rollback failures, disconnected targets and unexpected operator-screen changes. Windows runtime checks perform a read-only native topology query, checking usable modes or safe rejection when a virtual/remote display omits them, then use an injected backend for every display mutation. They exercise No, stale consent, keep/revert, restore, output suppression, bindings and a real timed keep dialog. Initial Yes/No responses are injected in automation.

Before relying on this fallback in a service, test the real three-screen computer, its graphics driver and the intended connection types. Confirm actual Yes/No defaults, primary-screen preservation, both audience images, timeout rollback, restore after restarting CanvaStage, and failed/hotplug recovery. Automated checks do not prove that the church's hardware accepts a clone layout.
