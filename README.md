# WinEvo

An open-source optimizer and tweaker for Windows 11, built as a WinUI 3 application with an elevated agent for privileged operations. Community-extensible via JSON action manifests.

## Status

🚧 **Pre-alpha.** End-to-end works: Shell launches, spawns the agent broker, elevates on demand via UAC, and executes actions. Three operations are wired (`registry-set`, `process-kill`, `external-process`); the rest of the schema's operation catalog is still TODO. IPC is length-prefixed JSON over a named pipe; gRPC (`.proto` already defined) is the target transport.

## Goals

- Consolidate the tweaks, cleanups, and scripts Windows power users run manually into one polished app.
- Let the community add new actions via JSON/YAML manifests without recompiling.
- Run the UI unprivileged; elevate only when an action requires it, via an on-demand broker or an installed service (user choice).
- Ship to the Microsoft Store (MSIX) **and** as a portable/unpackaged distribution.

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

Agent diagnostic log: `%TEMP%\winevo-agent.log` (startup events, pipe-security outcomes, unhandled exceptions — essential for elevated runs where the agent has no visible stdio).

## Contributing actions

Drop a JSON manifest in the appropriate `actions/<category>/` folder. Schema at [actions/schemas/action.schema.json](actions/schemas/action.schema.json). Authoring guide: [docs/action-authoring.md](docs/action-authoring.md). New operation types require a code-level PR and review.

## License

[GPLv3](LICENSE).

## Author

Hillpro — [github.com/Hillpro/WinEvo](https://github.com/Hillpro/WinEvo)
