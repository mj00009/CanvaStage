# Display reconnect work for issues #43 and #44

This work ships in [v0.12.2](https://github.com/mj00009/CanvaStage/releases/tag/v0.12.2), tagged at `fe99335b1fd28ae7d09b59caf159d2955e2b92aa`. [Windows implementation run 38456793631](https://github.com/mj00009/CanvaStage/actions/runs/38456793631) and [the release-tag pipeline](https://github.com/mj00009/CanvaStage/actions/runs/38458961632) passed all 129 unit tests, all 19 published-app runtime checks, and packaging. Local cross-build had zero warnings/errors, and all nine help-site tests passed. Neutral demo screenshots were reviewed for the matching help guide. The native dialogs and physical/wireless display behavior still need operator acceptance; CI injects its confirmation answers. Additional manual EXE/package inspection is opt-in and has not been performed.

## Remembered-display prompt (#43)

CanvaStage offers to start output when Windows reports the remembered display again as an unambiguous Extended display. The offer uses the currently applied, cached default deck without repeating Canva authorization, Setup, or a download. The prompt names the screen and defaults to No.

The app waits one second before checking the current display list. It defers the offer during a Canva action, Simple Setup, or the display-loss warning. Repeated notifications for the same connection do not repeat a declined offer. A later disconnect/reconnect can produce a new offer.

Startup on an already connected display does not generate a prompt. Missing, unknown, Primary, or duplicate display identities do not qualify. A different saved choice does not count as a reconnect. Output must be off and the current deck must match the remembered applied deck. An explicit Stop suppresses offers until the operator requests output again.

After Yes, CanvaStage rechecks the display, output intent, deck, busy state, and shutdown state. A disconnect, replacement deck, or Stop during the native dialog invalidates the answer. Restart retains the current page, mode, layout, and tracking state. Like other output starts, it starts unblanked.

`DisplayReconnectPolicy` handles identity and availability transitions. `MainViewModel.DisplayReconnect` handles the timer, injected confirmation, consent revalidation, and existing output-start path. No new background Canva refresh loop or display-selection fallback is introduced.

## Casting disconnect investigation (#44)

The issue log shows Windows reporting only the laptop display before CanvaStage requests an output stop. The selected cast TV is absent from that display snapshot. `OutputWindowManager.RefreshDisplayConfiguration` stops the live output when its target is no longer available. The app warning comes from CanvaStage, not Windows.

The code inspected does not request a wireless receiver disconnect. These observations do not establish why Windows removed the screen, or whether opening Chrome caused it. The TV reset and wireless connection still require testing on the affected computer and receiver.

The warning now explains the observed condition, retained cache/tracking, and recovery steps. A dedicated log entry distinguishes a display-loss safety stop from the operator's Stop action and records the unavailable target and remaining display count. It does not log slide text, credentials, browser traffic, or other apps' contents.

## Verification and acceptance

Local unit tests cover first observation, attach/reconnect, repeated events, changed device names, target changes, duplicate identities, Primary/unknown choices, and legacy case-insensitive device names. Windows verification injects Yes/No answers and exercises the real view model/output manager, including declined offers, cache/page/mode retention without exports, and disconnect/deck/Stop races. The CI runner uses its one physical screen as a synthetic Extended adapter; this does not prove wireless or multiple-monitor behavior.

Before release, check the native Yes/No prompt and warning on Windows:

1. Load the intended cached deck and disconnect its remembered Extended screen. Reconnect using Extend. Confirm the prompt names that screen, No keeps output off, and Yes starts the current page without a download.
2. Leave output stopped manually, then reconnect the screen. Confirm output stays off and no reconnect offer appears. Use Start output or Start setup when ready.
3. Disconnect again while the prompt is open, then answer Yes. Confirm output stays off and does not move to the laptop. Test a duplicate identity or an unavailable saved monitor without an arbitrary fallback.
4. With output live, open Chrome and compare casting with wired HDMI. Record whether Windows still lists the TV, whether other apps can use it, and the time of the disconnect. Capture Windows/receiver diagnostics if casting drops. Keep #44 open until the cause is established.

Leave #41 (tracking latency) and #42 (transitions) unchanged in this work. Keep published help guides describing their existing releases until a new release is requested and verified.
