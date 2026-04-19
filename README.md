# WinEvo

An open-source optimizer and tweaker for Windows 11, built as a WinUI 3 application with an elevated agent for privileged operations. Community-extensible via JSON action manifests.

## Status

🚧 **Pre-alpha.** Scaffolding phase. Nothing is runnable yet.

## Goals

- Consolidate the tweaks, cleanups, and scripts Windows power users run manually into one polished app.
- Let the community add new actions via JSON/YAML manifests without recompiling.
- Run the UI unprivileged; elevate only when an action requires it, via an on-demand broker or an installed service (user choice).
- Ship to the Microsoft Store (MSIX) **and** as a portable/unpackaged distribution.

## Requirements

- **Windows 11 21H2** (build 22000) or later, x64 or ARM64.
- **.NET 10 SDK** (currently 10.0.202) to build.
- **Windows App SDK** (latest stable 1.x).
- **WiX Toolset v5** for MSI authoring.

## Architecture at a glance

Four processes, one agent binary with two modes:

| Process | Tech | Elevation | Purpose |
|---|---|---|---|
| `WinEvo.Shell.exe` | WinUI 3 | user | Main UI. Fully exits when closed. |
| `WinEvo.Tray.exe` | WinForms | user | Tray icon; persists when background is enabled. |
| `WinEvo.Agent.exe --service` | .NET console | LocalSystem | Persistent elevated actions (opt-in). |
| `WinEvo.Agent.exe --broker` | *same binary* | UAC-elevated | Ephemeral fallback when service is off. |

Shell ⇄ Tray ⇄ Agent over named pipes + gRPC. See [docs/architecture.md](docs/architecture.md).

## Layout

- `src/` — all C#/WiX projects.
- `tests/` — unit test projects.
- `actions/` — built-in JSON action manifests + JSON Schema.
- `docs/` — architecture, action-authoring guide, IPC contract, security model.
- `build/` — MSIX/MSI/signing scripts (later).

## Building

*(Phase 2 of scaffolding will introduce the solution file.)*

Once the solution exists:

```bash
dotnet restore
dotnet build -c Release
```

## Contributing actions

Drop a JSON manifest in the appropriate `actions/<category>/` folder. Schema at [actions/schemas/action.schema.json](actions/schemas/action.schema.json). Authoring guide: [docs/action-authoring.md](docs/action-authoring.md). New operation types require a code-level PR and review.

## License

[GPLv3](LICENSE).

## Author

Hillpro — [github.com/Hillpro/WinEvo](https://github.com/Hillpro/WinEvo)
