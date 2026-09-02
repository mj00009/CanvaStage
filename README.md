# Canva Stage

Canva Stage is a Windows desktop confidence-monitor app for churches using Canva presentations. It gives people on stage a separate, readable output while the congregation continues to see the regular Canva presentation.

> **Project status:** Phase 1 implementation is underway. The app currently includes the local demo deck, manual page control, Mirror/Monitor output, Preview, display targeting, and safe output recovery. Canva OAuth and content retrieval are the next implementation slice.

## What it will do

- Connect to a church-managed Canva integration and choose a presentation.
- Open a separate confidence-output window, including a laptop-friendly Preview mode.
- Send the output to a selected confidence display when a second screen is available.
- Switch immediately between **Mirror** mode (the Canva page image) and **Monitor** mode (a simplified, high-visibility view of slide text).
- Provide manual page controls in Phases 1–2. Assisted live-slide recognition is deferred to a later phase.

## Who it is for

Canva Stage is intended for a church operator, worship leader, musician, presenter, or speaker who needs a dependable on-stage confidence screen. It is designed for a calm Sunday workflow:

1. Configure Canva.
2. Choose the presentation and displays.
3. Test the output in Preview.
4. Start the confidence output.

You can set up and test the app with one display. For an independent on-stage display, Windows must see the screens as **extended** displays.

## How the planned app works

The first release is a native Windows 11 WPF app. Phases 1–2 use explicit manual page control; display capture and slide inference are intentionally deferred. The app never claims automatic knowledge of Canva's current page.

### Output modes

- **Mirror:** Shows the selected Canva page image as faithfully as possible.
- **Monitor:** Shows large, high-contrast current-slide text, with optional next-line and clock information. If usable text is not available, it falls back to the slide image.

## Canva account and privacy

Each church supplies and authorizes its own Canva integration. The planned app will store connection credentials and tokens locally in protected Windows storage; they must never be committed to this repository, logs, GitHub Actions artifacts, or release downloads.

The app’s client-only model is a deliberate church-managed approach. Canva recommends a secure backend for client-secret handling and token exchange, so do not describe this desktop-only approach as Canva’s preferred production security model.

## Development

The implementation target is a self-contained Windows x64 release, so volunteers should not need a separately installed .NET runtime.

On Windows, double-click `Build Windows App.cmd`, or use:

```powershell
dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build
dotnet publish src/CanvaStage.Windows/CanvaStage.Windows.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

The GitHub Actions workflow automatically discovers the solution and WPF application project. It builds and tests pull requests and `main`, uploads a self-contained Windows x64 artifact, and attaches that artifact to a GitHub Release for version tags such as `v0.1.0`.

The portable release ZIP includes a SHA-256 checksum. The app runs without requiring administrator elevation and stores mutable state under the shared Windows operator's local app-data directory.

## Repository contents

- `CanvaStage-Codex-Handoff-Updated.md` — full product requirements and phased delivery plan.
- `assets/` — Canva Stage logos and interface mockups.
- `.github/workflows/windows-release.yml` — Windows build, test, artifact, and tagged-release automation.

## Planned release process

1. Merge a tested change to `main`.
2. Create and push an annotated version tag, for example `v0.1.0`.
3. GitHub builds, tests, and publishes a self-contained Windows x64 package.
4. The workflow creates or updates the matching GitHub Release and attaches the package.

Code signing, an installer, and automatic updates are planned future enhancements.
