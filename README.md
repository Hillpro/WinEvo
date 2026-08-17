# WinEvo

[![Status](https://img.shields.io/badge/status-alpha-orange.svg)](#status)
[![Version](https://img.shields.io/github/v/release/Hillpro/WinEvo?include_prereleases&sort=semver&label=version)](https://github.com/Hillpro/WinEvo/releases/latest)
[![License](https://img.shields.io/badge/license-GPLv3-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2011-0078D6.svg?logo=windows&logoColor=white)](#requirements)

An open-source optimizer and tweaker for Windows 11, built as a WinUI 3 application with an elevated agent for privileged operations. Community-extensible via JSON action manifests.

## Status

🚧 **Alpha (0.1.1, portable-only).** End-to-end works: Shell launches, spawns the agent broker, elevates on demand via UAC, and executes actions with a severity-gated confirmation dialog. Nine operations are wired (`registry-set`, `registry-delete`, `registry-read`, `process-kill`, `external-process`, `builtin-tool`, `powershell`, `command`, `delay`); the live schema is trimmed to those. Roadmap operations (`service-*`, `file-*`, `system-restore-point`, `sysinternals-tool`) and target manifest features (undo, dry-run, preconditions, sub-actions, restore points, progress streaming) are tracked in [docs/manifest-reference/](docs/manifest-reference/). Spawned children are kept under a Windows Job Object so they die with the agent. IPC is length-prefixed JSON over a named pipe; gRPC (`.proto` already defined) is the target transport.

## Goals

- Consolidate the tweaks, cleanups, and scripts Windows power users run manually into one polished app.
- Let the community add new actions via JSON/YAML manifests without recompiling.
- Run the UI unprivileged; elevate only when an action requires it, via an on-demand broker or an installed service (user choice).
- Ship as a portable/unpackaged zip, with a signed installer planned alongside it ([docs/distribution.md](docs/distribution.md)).

## Requirements

- **Windows 11 21H2** (build 22000) or later, x64 or ARM64.
- **.NET 10 SDK** (currently 10.0.202) to build.
- **Windows App SDK** (latest stable 1.8.x).
- **WiX Toolset v7** for MSI authoring.

## Prerequisites for Visual Studio users

Visual Studio 2022 does **not** recognize `.wixproj` (WiX v4+ SDK-style) projects out of the box. If you open `WinEvo.slnx` in VS without the extension below, the Installer project will fail to load.

**Install [HeatWave](https://www.firegiant.com/wix/heatwave/)** — FireGiant's (the WiX maintainers') Visual Studio extension for WiX 4/5/6/7.

From inside Visual Studio: *Extensions → Manage Extensions → Online → search "HeatWave" → Download → restart VS*. Or download the `.vsix` directly from firegiant.com.

**If you don't want HeatWave**, build from the CLI instead:

```bash
dotnet build WinEvo.slnx       # builds everything including the installer
dotnet test  WinEvo.slnx       # runs the test projects
```

## Architecture at a glance

Four processes, one agent binary with two modes:

| Process | Tech | Elevation | Purpose |
|---|---|---|---|
| `WinEvo.exe` | WinUI 3 | user | Main UI. Fully exits when closed. |
| `WinEvo.Tray.exe` | WinForms | user | Tray icon; persists when background is enabled. *(stub; not connected to the agent yet)* |
| `WinEvo.Agent.exe --service` | .NET Windows app | LocalSystem | Persistent elevated actions. *(not implemented yet)* |
| `WinEvo.Agent.exe --broker` | *same binary* | user, UAC-promoted on demand | Long-lived for the Shell's session; replaces itself with an elevated broker when an action needs it. |

Shell ⇄ Agent over a named pipe (length-prefixed JSON today; gRPC `.proto` defined for a later transport swap). See [docs/architecture.md](docs/architecture.md) and [docs/ipc-contract.md](docs/ipc-contract.md). Tray ↔ Agent integration is not wired yet.

## Layout

- `src/` — all C#/WiX projects.
- `tests/` — unit test projects.
- `actions/` — built-in JSON action manifests + JSON Schema.
- `docs/` — architecture, action-authoring guide, IPC contract, security model (each doc carries its own `*(not implemented yet)*` markers where relevant).
- `AUTHORS`, `LICENSE` — authorship and GPLv3 text.

## Building and running

```bash
dotnet restore WinEvo.slnx
dotnet build   WinEvo.slnx
dotnet test    WinEvo.slnx
dotnet run --project src/WinEvo.Shell     # or F5 in Visual Studio
```

Agent diagnostic log: `%LOCALAPPDATA%\WinEvo\agent.log` (startup events, pipe-security outcomes, unhandled exceptions — essential for elevated runs where the agent has no visible stdio). The Shell logs alongside it as `shell.log` in the same folder.

### Release publish

```bash
# Portable, self-contained: bundles .NET + WinAppSDK runtimes
# Output: src/WinEvo.Shell/bin/Release/net10.0-windows10.0.22000.0/win-x64/publish/
dotnet publish src/WinEvo.Shell -p:PublishProfile=Portable-win-x64
```

This uses the `Portable-win-x64` publish profile in [src/WinEvo.Shell/Properties/PublishProfiles/](src/WinEvo.Shell/Properties/PublishProfiles/), and the output runs on a clean Windows 11 22000+ machine without any prerequisite installs.

**Portable is the only build output today.** A signed installer is the next distribution milestone. MSIX/Store packaging was built and then abandoned — the Store refused the capability that lets a packaged build write real user settings; see [docs/distribution.md](docs/distribution.md).

Release `.zip` production is automated: [`.github/workflows/release-portable.yml`](.github/workflows/release-portable.yml) is tag-triggered (`v*`), publishes the portable zip, attests build provenance, and creates the GitHub Release.

## Portable distribution (end users)

The portable zip is the no-install path: download, unzip, run. Recommended unzip location is somewhere under your user profile (e.g. `%LOCALAPPDATA%\WinEvo\`) so it doesn't need elevation to lay down. `Program Files` works too but requires admin to write.

**SmartScreen.** The first launch will show *"Windows protected your PC"* because the build is not code-signed by a recognized CA. Click **More info → Run anyway**. The prompt goes away once distribution moves to a Store-signed package or a CA-issued code-signing cert.

**Downloaded copies.** Windows marks every file extracted from a downloaded zip, and that marker used to make elevated actions fail with a bare *"Elevation declined"* — SmartScreen was blocking the privileged helper without showing a prompt. WinEvo now clears the marker from its own helper on first elevated launch, so this should not happen. If you still see that message without a UAC prompt appearing, unblock the archive and re-extract: right-click the downloaded `.zip` → **Properties** → tick **Unblock** → **OK**, then extract again.

**Checksums.** Each release lists a SHA-256 next to the zip. Verify before running:

```powershell
Get-FileHash WinEvo-<version>-win-x64.zip -Algorithm SHA256
```

**What's inside.**

```
WinEvo/
├── WinEvo.exe              # the UI; launch this
├── agent/                  # privileged helper, spawned on demand
│   └── WinEvo.Agent.exe
├── actions/                # shipped JSON action manifests
├── resources/              # i18n string bundles
└── (.NET and WinAppSDK runtimes — bundled; no separate installs needed)
```

**Uninstall.** Delete the `WinEvo/` folder. Both processes write diagnostic logs to `%LOCALAPPDATA%\WinEvo\` (`shell.log` and `agent.log`) — clear that folder manually if you want a fully clean removal.

## Contributing actions

Drop a JSON manifest in the appropriate `actions/<category>/` folder. Live (alpha-trimmed) schema at [actions/schemas/action.schema.json](actions/schemas/action.schema.json); the long-term target shape — including features the engine doesn't consume yet — lives at [docs/manifest-reference/](docs/manifest-reference/). Authoring guide: [docs/action-authoring.md](docs/action-authoring.md). New operation types require a code-level PR and review.

## License

[GPLv3](LICENSE).

## Author

Hillpro — [github.com/Hillpro/WinEvo](https://github.com/Hillpro/WinEvo)
