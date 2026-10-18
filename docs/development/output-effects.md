# Faster following, slide fades and clocks (unreleased)

This development build addresses [#41](https://github.com/mj00009/CanvaStage/issues/41), [#42](https://github.com/mj00009/CanvaStage/issues/42) and [#45](https://github.com/mj00009/CanvaStage/issues/45). The published v0.12.2 download and its versioned help are unchanged.

## Operator controls

Open **Settings & Diagnostics → Output clocks and slide changes**.

- Enable **Show current time in Mirror** to place a white clock over the slide, with a small black shadow. It starts at the bottom right. Select **Place Mirror clock at bottom left (otherwise bottom right)** to move it.
- **Show current time in Monitor** controls the existing top-right Monitor clock. Monitor and Mirror have separate size sliders from 16 to 96. **Bold clocks (clear for regular weight)** changes both clocks between a slightly bold font and regular weight. The clock uses the computer's local time and updates once a second.
- Enable **Fade between slides** for a crossfade in Mirror and Monitor. Set **Fade duration (100–1000 ms)** to choose its length. Fades are off by default; a longer fade takes longer to reveal the new slide. Rapid navigation replaces the current fade with the newest page. Blank, Stop, mode changes and deck replacement discard the old frame immediately.

These settings survive reopening CanvaStage. Existing preferences retain the Monitor clock setting and size 28, with Mirror's clock and fades off. Every preview still copies the same output visual; it does not render a separate clock or transition.

## Following and verification

The accessibility worker waits 200 ms after a successful scan instead of 750 ms. Scans remain sequential and publish the first matching counter. Scan time and Canva's accessibility updates also affect the visible delay; this does not promise frame-synchronous following. Failure backoff, cancellation, stale-observation handling, manual pause and the visible-page-count guard remain in place.

Unit coverage checks preference migration, persistence, bounds and the healthy/failure polling delays. Windows synthetic checks exercise both clock placements over cached images, independent sizes, weight and shadow, fades in both modes, rapid page changes, immediate Blank/Stop, disabling fades, deck replacement and completed-frame cleanup. Existing output drawing, typography, preview parity, cache/auth, reconnect and shutdown checks still run.

Regression review also found that long Mirror placeholder text could overflow when no cached image was available. The whole placeholder now scales down to fit its viewport. A deck replacement followed immediately by Presenter resync could retain an old fade frame; capture now requires a rendered frame from the same deck, layout and appearance.

Before service use, compare following speed and CPU use on the actual Canva computer with one Presenter View open. Test long and short fades during fast forward/backward navigation, including Mirror pages with cached images. Confirm clocks remain readable over light and dark slide content at the actual display scaling. Test Blank, Stop and display loss during a fade on the confidence display. Synthetic checks cannot establish wireless receiver behavior or real-account timing.
