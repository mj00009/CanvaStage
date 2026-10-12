# Public help maintenance

The public operator help lives in this repository under `docs/help/` and is hosted at [CanvaStage Help](https://mj00009.github.io/CanvaStage/). The current guide covers v0.12.1. Its Help button and F1 shortcut open that version's guide; F1 also works in Compact view. The earlier v0.12.0 guide remains available, but that app version does not have Help/F1.

[The v0.12.1 deployment](https://github.com/mj00009/CanvaStage/actions/runs/38289356641) passed. The app's versioned help URL, latest guide, F1 instructions, and tagged screenshots were checked in the live browser. Its screenshot provenance is tag run `38288646297`, commit `c904a8b112b27afd94354a61076c55a0efac8a24`; the manifest records the reviewed image hashes. Both the v0.12.0 and v0.12.1 quick starts cover Full and Compact views, using original screenshots from their matching tagged builds.

## Enable hosting once

Hosting was enabled by the owner on October 12, 2026. The [initial deployment](https://github.com/mj00009/CanvaStage/actions/runs/38254507700) and [current-portal documentation update](https://github.com/mj00009/CanvaStage/actions/runs/38254957162) both passed. Root and archived routes, demo images, and current portal instructions were verified in the live browser. The setup below is retained for recovery or a future move.

The repository owner must open **Settings → Pages → Build and deployment → Source → GitHub Actions**. The current collaborator can push but cannot administer Pages. No custom domain or cross-repository token is needed. In **Settings → Environments → github-pages**, allow deployment from `main` and release tags (`v*`) if branch restrictions are enabled.

After enabling Pages, run **Actions → Public help documentation → Run workflow** on `main`, or rerun its deployment job. Verify both the root and `/v0.12.0/` routes in a browser. A passing build/upload alone does not mean the site is live. The workflow will not enable Pages by silently escalating permissions.

## Author and preview

Use the `humanizer` skill when writing or revising help documentation, as requested by the user. Write plain instructions for church operators. Keep every factual claim, safety warning, button name, version detail, and link target. Remove sales language and filler without changing how the app works. Keep technical and legal notices neutral, and leave code, metadata, and screenshot files unchanged during a prose edit.

- `docs/help/manifest.json` selects the latest guide and lists retained versions, screenshot hashes, reviewed tag commit, and successful Windows run.
- Each version directory contains four HTML content fragments: `index.html`, `quick-start.html`, `advanced.html`, and `troubleshooting.html`. Shared navigation, version labels, privacy/trademark footer, and stylesheet are supplied by the builder. No JavaScript, analytics, external fonts, or hosted third-party media are needed.
- `scripts/build-help.mjs` uses only Node's standard library. It publishes an explicit page/screenshot allowlist, never all of `docs/`, CI artifacts, or diagnostic storage. It checks links/anchors, image hashes, publication paths, and release version matching. The output must be a new or empty directory to prevent stale files leaking into a deployment.

```sh
node --test scripts/build-help.test.mjs
node scripts/build-help.mjs --output /tmp/canvastage-help-preview-UNIQUE
python3 -m http.server 8765 --bind 127.0.0.1 --directory /tmp/canvastage-help-preview-UNIQUE
```

Use an unused output directory name. Open `http://127.0.0.1:8765/`; inspect desktop/mobile layouts, keyboard navigation, all guides, expandable screenshots, and the archived version route. Node 24 is used in CI. Generated `_site/` is ignored by Git.

## Screenshot rules

Use only **synthetic demo screenshots from a successful Windows verification run of the corresponding release tag**. Review each image yourself before committing it. Never publish real presentation text, signed image links, account information, credentials, raw diagnostics, or the isolated test-storage folder. A fake identifier such as `verification-client` is not a credential to give an operator.

Use basic, neutral placeholder slide text such as "Sample slide 1" and "This is sample text" in screenshot fixtures. Do not combine religious wording with organization or product names. Case-preservation tests can use generic text such as "ABC and SampleText" without changing their test coverage. Review the words shown in every screenshot, not just its layout and privacy.

Copy selected PNGs into the version's `images/` directory, use clear names/captions and descriptive alt text, and record their SHA-256 hashes in the manifest. Hashes enforce that a replacement cannot publish without updating the review record; they do not automatically prove the image is private-data-free. Keep screenshots original, without recoloring or manufacturing UI state. Caption synthetic display choices honestly: CI's single physical display does not verify a real Extended screen.

Initial provenance: tag `v0.12.0`, commit `082d8906d656ea2ae1bbd49baea024f96583e790`, successful Windows run [38190044919](https://github.com/mj00009/CanvaStage/actions/runs/38190044919). The five reviewed images are committed so expiring Actions artifacts do not break published help.

## Release workflow

1. Keep the guide for the current published app accurate. Documentation-only corrections can publish on `main` without a new app release; set its updated date.
2. When preparing an app release, copy the previous version directory to the new stable version, review every page against its behavior, and update release/download links and Help/F1 instructions. Preserve earlier versions and their screenshots.
3. Run local tests and the Windows workflow. Prepare the version entry, but do not set it as `latest` until the release tag's Windows checks and screenshots pass. The site refuses to deploy unpublished/draft/prerelease guides as stable documentation.
4. After the tag's Windows build publishes the app, its dependent help job checks `manifest.latest` against the tag. The **first** run for a new version normally defers deployment with a notice, because its final tagged screenshots were not available before the tag was built. Current published help remains unchanged and the app release stays successful. The guide builder also rejects an explicit mismatching `--release-tag`.
5. Review/copy screenshots from that successful tag build, add their run/commit/hashes, update the new guide's manifest entry and `latest`, and push the documentation commit to `main`. This runs the help workflow and deploys the matching guide. Do not move an existing tag or rerun its app packaging merely to update help. Until this follow-up is published, the new app's versioned Help URL may show the fallback page; do not declare the release handoff complete without checking it.
6. Confirm the public root shows the new version, its archived route works, old routes still work, and the installed app's Help/F1 opens its versioned guide. Verify Windows UI screenshots for the Help button at narrow layouts and high DPI.

The workflow's successful tag-run/provenance check applies to the latest guide; archived image hashes remain checked even if older run metadata is removed. All referenced stable app releases must still exist. Pages artifacts expire after one day; committed guides/images provide the durable source. Failed documentation publication must be investigated separately from the Windows package result.

## In-app links

`HelpLinkService` opens a fixed HTTPS address under this project's Pages site, using the installed stable version. Prerelease/unknown/malformed versions open the latest guide. No deck name, page number, credentials, diagnostic data, or system details are appended to the URL. Opening Help changes no output/sync settings. Browser-launch failure displays a safe copyable address in a dialog; the website requires internet access. HTTP/network failure after the browser opens is handled by the browser, not detected by the app.

The Windows screenshot fixture injects a no-op launcher so CI does not open an external browser. Unit tests verify version routing and safe failures; Windows checks verify the visible Help button, F1 binding, and unchanged output session. To change the site's base URL, update this service, builder 404 link, authoring/README links, and tests together.
