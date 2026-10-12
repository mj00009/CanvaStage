# Compatibility and release maintenance

CanvaStage relies on external behavior that can change independently of this repository. Review this guide when preparing a release or investigating a regression. Do not change a working Presenter-tracking approach solely because an alternative looks newer.

## Canva Connect API

- The app uses `design:meta:read` and `design:content:read`, lists presentations, reads page metadata, exports PPTX for native text and hidden-slide state, and exports JPEGs for Mirror images. Verify endpoint fields and limits in the [official Connect API docs](https://www.canva.dev/docs/connect/) and [changelog](https://www.canva.dev/docs/connect/changelog/) before API changes.
- Canva's [security recommendations](https://www.canva.dev/docs/apps/rest-apis/security-recommendations/) explicitly do not support desktop REST API integrations that store client credentials. Treat the current local deployment as self-managed when planning support or wider distribution.
- **Get design pages is a preview API.** It currently returns 1-based `page_number` and short-lived thumbnail URLs. A missing, duplicate, or out-of-range page number must fail safely rather than pair an image with the wrong slide. The Windows synthetic verification includes a changed-schema case.
- The PPTX export is for text and hidden-slide mapping; Mirror mode uses a separate [JPEG export job](https://www.canva.dev/docs/apps/rest-apis/reference/exports/create-design-export-job/), with visible source page numbers, quality 70, and `export_quality: regular`. When all requested page widths are known, width is the smaller of 1280 and the smallest source width; otherwise Canva's native dimensions are used. Export URLs are paired in page order only when their count matches the requested pages. Each image is validated before applying the complete new deck. A failed render/download retains the working deck instead of falling back to a stale preview thumbnail.
- `PresentationDocument.UsesRenderedPageImages` defaults to false for older cache files. Automatic refresh upgrades such a cache once even when the modification timestamp is unchanged. Manual **Check for updates** forces an import when automatic updates are enabled; with them disabled it remains a metadata-only check. Periodic checks still use `updated_at`, so real-account testing must cover delayed Canva timestamps and image-only edits.
- Content GET requests use `Cache-Control: no-cache` to request revalidation, without altering signed URL query strings. This is not a guarantee that Canva immediately publishes an edit. Privacy-safe logs record checks, import stages, timestamps, counts, and application of a new generation; never include signed links or page text.
- Do not log thumbnail/export URLs: they are short-lived signed links. Do not log or upload credentials, tokens, OAuth codes, authorization headers, or slide content. Keep synthetic CI fixtures isolated from uploaded artifacts.
- After Canva changes, test a real account with a short deck, a large deck, hidden pages, image-only pages, and pages with title/footer/copyright text. Confirm export progress, visible page count, cache reuse, Mirror, and Monitor.

## Canva Presenter and Windows

- Presenter following reads the first matching page counter exposed through Windows accessibility. Manual controls must remain usable whenever the counter is missing, stale, inaccessible, or reports the wrong visible-page total.
- Test the actual Canva desktop app or browser used for services, with only one Presenter View open and the same Windows permission level as CanvaStage. Check forward/backward/distant jumps, closing/reopening Presenter View, pause/resume, and restart with sync disabled.
- For Windows display changes, test one-screen Preview, an Extended monitor, mixed DPI, hotplug, and full physical-pixel coverage. CI's synthetic DPI screenshots do not replace real hardware tests. See [Windows acceptance](windows-acceptance.md).

## Local cache and recovery

- Protected credentials use Windows DPAPI `CurrentUser`; `preferences.json` and the presentation cache live under `%LOCALAPPDATA%\CanvaStage`. A different Windows account cannot reuse the protected secret.
- Keep old image generations immutable during re-download, because the output may still have an old image open. `Clean old downloads` removes obsolete generations only after a new one is active.
- On startup, a cached deck with missing image files keeps its text but removes broken image references. The operator is warned to re-download before relying on Mirror. Do not silently delete the whole cached presentation or the saved Canva connection.
- Verify `Clear cache`, `Disconnect`, restart persistence, and sanitized diagnostics separately; they have different deletion scopes.

## Before a release

Keep the public operator help aligned with each published app version. See [help maintenance](help-authoring.md) for reviewed demo screenshots, retained version routes, and the publication gate that runs after Windows verification. Do not publish unreleased UI as instructions for the current download.

1. Check open GitHub issues, Canva's Connect changelog, supported API fields, GitHub Actions runner requirements, and action release notes. Upgrade workflow actions only after checking compatibility; the workflow currently uses Node 24-capable action majors.
2. Restore, build, and run unit tests locally. On Windows, `Build Windows App.cmd` also publishes a ZIP with logs and a checksum.
3. Push the commit and confirm the Windows workflow builds the **published** app in synthetic verification mode. Inspect its verification summary and, when artifact storage permits, `verification.json` and UI screenshots—not only the green checkmark.
   If GitHub Actions artifact storage is full, uploads show warnings but cannot block a passing build or tag release. The tag job sends the ZIP and checksum directly to GitHub Releases and does not duplicate them as an Actions package artifact. New non-tag package artifacts expire after three days; test results, UI screenshots, and failure diagnostics expire after 14 days. These rules do not shorten retention of artifacts created before the workflow change. Restore artifact availability when the quota resets or through an explicitly approved cleanup; do not silently delete prior build artifacts.
4. Open the resulting versioned ZIP (for example, `CanvaStage-v0.11.1-win-x64.zip`) and verify `CanvaStage.Windows.exe`, the full Apache-2.0 `LICENSE`, and `TRADEMARKS.md` are present at ZIP root. Compare the SHA-256 checksum with the ZIP. The workflow reads the effective MSBuild Version and rejects release tags that do not match it. The local build script also includes the version, followed by its build timestamp.
5. Run the [real-machine acceptance checklist](windows-acceptance.md) with the church's normal Windows user, Canva account, deck, and external display. Do not report this complete based only on CI.
6. Only after verification, push an annotated `v*` tag if a release was requested. A `main` push and tag push each run the workflow; the tag job creates/updates the GitHub release.

The portable package remains unsigned. Signing, an installer, and auto-updates are deferred until explicitly requested.
