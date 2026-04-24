# Architecture

> **Implementation status (summary).**
> - ✅ **Wired:** Shell (WinUI 3), Agent broker process, JSON-over-pipe IPC, handshake, lazy UAC promotion with Medium-IL pipe-label handling, 8 operations (`registry-set`, `registry-delete`, `process-kill`, `external-process`, `builtin-tool`, `powershell`, `command`, `delay`), end-to-end execute flow, Job Object for child-process cleanup (`JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`), severity-adapted confirmation dialog for action warnings (info/warning/danger/critical with typed-phrase challenge), shared string bundles in `resources/Strings.{en,fr}.json` with `{token}` interpolation, deterministic agent teardown on window close.
> - 🚧 **Partial:** manifest parse (lenient, no JSON Schema validation), footer running-count (observes the currently-selected Detail VM only — switching selection mid-execution drops the subscription).
> - 🔲 **Not yet implemented:** Service mode, service install/uninstall, Tray ↔ Agent IPC, gRPC transport, streaming progress events, undo, dry-run, restore points, execution audit log, Authenticode client verification, code signing, Sysinternals tool resolver, remaining operations (`registry-read`, `service-*`, `file-*`, `sysinternals-tool`, `system-restore-point`), sub-action step execution, template functions (`drive()` / `basename()` / `dirname()`), type-specific parameter pickers in the UI.
>
> The narrative below describes the **full architecture** (including unimplemented pieces). Companion docs mark target-vs-current per feature: [ipc-contract.md](ipc-contract.md), [action-authoring.md](action-authoring.md), [security-model.md](security-model.md).

## Processes

WinEvo runs as **up to four processes**, split by privilege level and UI needs.

```
┌───────────────────────────────┐         ┌───────────────────────────────┐
│           WinEvo.exe          │         │        WinEvo.Tray.exe        │
│         (WinUI 3, user)       │         │        (WinForms, user)       │
│     main UI, launched on      │◄──────► │     tray icon, autostart      │
│     demand, fully exits on    │   IPC   │     when background is on     │
│     close                     │         │                               │
└──────────────┬────────────────┘         └──────────────┬────────────────┘
               │                                         │
               │        named pipes (JSON today)         │
               ▼                                         ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                         WinEvo.Agent.exe                                │
│   --service  → runs as a Windows Service (LocalSystem), persistent      │
│   --broker   → runs as UAC-elevated user session process, ephemeral     │
│                                                                         │
│   Same binary; same IPC surface. Mode selected at install / launch time │
│   based on the user's "allow background execution" setting.             │
└─────────────────────────────────────────────────────────────────────────┘
```

### Why split Shell and Tray into different processes

WinUI 3 has a significant working-set cost (~150–300 MB). When the user closes the main window we want the process to **actually exit** — not linger in the background. A separate ~25 MB WinForms tray process owns the tray icon, relaunches the Shell on demand, and relays running-task status from the agent. This costs ~25 MB resident instead of ~250 MB.

### Why one agent binary in two modes

Users who enable background execution get a persistent Windows Service that can run long actions while the UI is closed. Users who opt out get an on-demand broker. Both modes reuse the exact same execution code — there is no "service version" and "broker version" of each action. The binary reads its startup mode from command-line args; everything above that is identical.

**Current implementation:** only the broker mode is wired. The broker is spawned when the Shell starts, runs as the current user, and is replaced with an elevated instance on demand when an action declares `elevation: required`. Service mode + its install/uninstall entry points are TODO.

## IPC

Named pipe transport. Current runtime uses length-prefixed JSON framing; the
gRPC contract is defined in [`WinEvo.Contracts/Protos/agent-service.proto`](../src/WinEvo.Contracts/Protos/agent-service.proto) as the target transport for a later swap. See [ipc-contract.md](ipc-contract.md) for the full protocol and security model.

- **Service mode pipe:** `\\.\pipe\WinEvo.Agent.System`, ACL grants LocalSystem + the interactive user SID. *(service mode not implemented yet)*
- **Broker mode pipe:** `\\.\pipe\WinEvo.Agent.User`, ACL grants the interactive user SID. When the broker is elevated, the pipe's mandatory integrity label is lowered to Medium so the unelevated Shell can write to it.
- **Client verification:** agent will resolve the client PID via `GetNamedPipeClientProcessId` and verify the client executable's Authenticode signature before accepting commands. *(not implemented yet)*

## Action model

Actions are declarative JSON manifests composed from a closed set of **operations** (registry, external-process, service control, etc.) and/or **sub-action** references to other manifests. See [action-authoring.md](action-authoring.md) for the schema and examples.

Community extensibility lives at the **manifest** level — anyone can write a JSON action and drop it into `%LOCALAPPDATA%\WinEvo\Actions\`. Extending the operation set (adding fundamentally new trusted-code operations to the agent) requires a code-level PR and review; this is the security boundary.

## Distribution

Two distribution channels, both driven by the same source tree:

1. **Microsoft Store** — MSIX package containing `WinEvo.exe` and `WinEvo.Tray.exe`. The agent MSI is bundled inside the MSIX and launched via UAC on first elevated action.
2. **Unpackaged / portable** — a zip containing Shell, Tray, agent MSI, and the WinUI 3 bootstrapper. First-run flow is identical to the Store path.

The agent is **always** installed via MSI (never as part of the MSIX payload), because Microsoft Store certification does not allow packaged apps to silently register privileged Windows Services.

## Safety model

See [security-model.md](security-model.md) for the full treatment. In brief:

- Elevation is opt-in per action. Non-elevated actions never touch the agent.
- Every destructive operation supports **undo** with state backup in `%ProgramData%\WinEvo\UndoStore\`.
- Restore points are opt-in per action manifest, not a default.
- Dry-run preview is opt-in per action manifest.
- All executions are logged to `%ProgramData%\WinEvo\Logs\executions.jsonl` for audit + undo discovery.

## Project layout

See the top-level [README.md](../README.md) and solution file for the canonical project structure. Dependency flow is strictly acyclic: `Contracts` → `ActionModel` → `Actions.Abstractions` → `Actions.Operations` → `Agent.Core` → `Agent`, with `Ipc` sitting beside `Contracts`, and `Shell.Core` / `Shell` / `Tray` consuming `Contracts` + `Ipc` only (no direct reference to agent internals).
