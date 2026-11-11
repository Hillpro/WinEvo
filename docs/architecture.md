# Architecture

> **Implementation status (summary).**
> - ✅ **Wired:** Shell (WinUI 3), Agent broker process, JSON-over-pipe IPC, handshake, lazy UAC promotion with Medium-IL pipe-label handling, code-defined actions run by id, 8 operations (`registry-set`, `registry-delete`, `process-kill`, `external-process`, `builtin-tool`, `powershell`, `command`, `delay`), live state read from the agent when an action is selected, Job Object for child-process cleanup (`JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`), severity-adapted confirmation dialog for action warnings (info/warning/danger/critical with typed-phrase challenge), the whole window localized (English and French) in the Windows display language, deterministic agent teardown on window close.
> - ✅ **Wired (continued):** type-specific parameter inputs (string→TextBox, integer→NumberBox, boolean→ToggleSwitch, enum→ComboBox, drive→drive-picker), selected per `Parameter` subclass by `ParameterInputFactory` + `ParameterInputTemplateSelector`; running executions tracked for the whole session (`ExecutionTracker`), so the footer count, the close confirmation and a re-selected running action see executions started from any action.
> - 🔲 **Not yet implemented:** Service mode, service install/uninstall, Tray ↔ Agent IPC, gRPC transport, streaming progress events, undo, dry-run, restore points, execution audit log, Authenticode client verification, code signing, Sysinternals tool resolver, planned operations (`service-*`, `file-*`, `sysinternals-tool`, `system-restore-point`), pickers for a Wi-Fi profile / file / folder / service, actions supplied by third parties.
>
> The narrative below describes the **full architecture**, including pieces that are not built yet; those are called out where it matters. Companion docs mark target-vs-current per feature: [ipc-contract.md](ipc-contract.md), [action-authoring.md](action-authoring.md), [security-model.md](security-model.md).

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

An action is a C# class compiled into WinEvo. It has two halves: a **descriptor**, plain data from which the Shell lists the action and draws its inputs, and **behaviour**, which runs only in the agent and is composed from a closed set of **operations** (registry writes, process control, running a tool). See [action-authoring.md](action-authoring.md).

The Shell and the agent both load the same actions assembly. The Shell reads descriptors and never executes action code; it sends the agent an action id and the parameter values. The agent looks the id up in its own catalog, so a caller can only ask for an action the agent already contains.

Actions cannot be added without rebuilding WinEvo. Whether third-party actions return, as a simple data format or as a locally built assembly loaded in an explicit developer mode, is not decided. Extending the operation set is a code change with the strictest review; operations are what actually touch the system.

## Distribution

See [distribution.md](distribution.md) for the full picture, including why MSIX was abandoned.

1. ✅ **Unpackaged / portable** — self-contained zip containing `WinEvo.exe`, `agent/WinEvo.Agent.exe`, and the bundled .NET + WinAppSDK runtimes. Runs from any folder with no install and no admin rights; the Shell spawns the agent from `agent/WinEvo.Agent.exe` next to itself, unelevated by default and UAC-promoted on demand.
2. 🔲 **Signed installer** *(target)* — an `.msi`/`.exe` built from `WinEvo.Installer.wixproj`, carrying the same payload. Also the route to a Store listing, under a policy that accepts a download URL to a signed installer instead of a package.
3. ❌ **MSIX** — built during 0.1–0.2 and abandoned; the Store refused the restricted capability a packaged build needs in order to write real user settings.

Either shipping channel embeds the agent **inside** its own payload — there is no separate agent download. Service mode remains unwired; the installer in (2) is its prerequisite, since registering a `LocalSystem` service needs an installer that a packaged app could never provide.

`WinEvo.Tray.exe` is a stub and is not yet shipped in either channel.

## Safety model

See [security-model.md](security-model.md) for the full treatment. In brief:

- Elevation is opt-in per action. An action that does not need it runs in the unelevated broker. ✅ wired.
- *(target)* Every destructive operation supports **undo** with state backup in `%ProgramData%\WinEvo\UndoStore\`.
- *(target)* Restore points are opt-in per action, not a default.
- *(target)* Dry-run preview is opt-in per action.
- *(target)* All executions are logged to `%ProgramData%\WinEvo\Logs\executions.jsonl` for audit + undo discovery.

## Project layout

See the top-level [README.md](../README.md) and solution file for the canonical project structure. Dependency flow is strictly acyclic: `Actions.Abstractions` → `Actions.Operations` → `Actions`, with `Contracts` → `Ipc` beside them. `Agent.Core` and `Shell.Core` depend on `Actions.Abstractions` and `Ipc` only; the two executables, `Agent` and `Shell`, are where the actions assembly is brought in and the catalog is composed. `Shell.Core` has no reference to agent internals.
