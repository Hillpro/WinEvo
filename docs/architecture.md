# Architecture

> **Implementation status (summary).**
> - ✅ **Wired:** Shell (WinUI 3), Agent broker process, JSON-over-pipe IPC, handshake, lazy UAC promotion with Medium-IL pipe-label handling, 9 operations (`registry-set`, `registry-delete`, `registry-read`, `process-kill`, `external-process`, `builtin-tool`, `powershell`, `command`, `delay`), end-to-end execute flow, Job Object for child-process cleanup (`JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE`), severity-adapted confirmation dialog for action warnings (info/warning/danger/critical with typed-phrase challenge), shared string bundles in `resources/Strings.{en,fr}.json` with `{token}` interpolation, deterministic agent teardown on window close.
> - ✅ **Wired (continued):** type-specific parameter inputs (string→TextBox, integer→NumberBox, boolean→ToggleSwitch, enum→ComboBox, drive→drive-picker), selected per parameter type by `ParameterInputFactory` + `ParameterInputTemplateSelector`.
> - 🚧 **Partial:** manifest parse (lenient, no JSON Schema validation), footer running-count (observes the currently-selected Detail VM only — switching selection mid-execution drops the subscription).
> - 🔲 **Not yet implemented:** Service mode, service install/uninstall, Tray ↔ Agent IPC, gRPC transport, streaming progress events, undo, dry-run, restore points, execution audit log, Authenticode client verification, code signing, Sysinternals tool resolver, roadmap operations (`service-*`, `file-*`, `sysinternals-tool`, `system-restore-point`), sub-action step execution, template functions (`drive()` / `basename()` / `dirname()`), dedicated widgets for the remaining `string`-fallback parameter types (`wifi-profile`, `file-path`, `directory-path`, `service-name`).
>
> **Live schema vs reference snapshot.** The live [actions/schemas/action.schema.json](../actions/schemas/action.schema.json) covers only what the runtime consumes today. The full target shape — undo, dryRun, preconditions, sub-actions, restore points, progress, OS/arch gates, the roadmap operations — is preserved in [docs/manifest-reference/](manifest-reference/). The narrative below describes the **full architecture** (including unimplemented pieces); when an alpha-only constraint matters, it's called out explicitly. Companion docs mark target-vs-current per feature: [ipc-contract.md](ipc-contract.md), [action-authoring.md](action-authoring.md), [security-model.md](security-model.md).

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

Actions are declarative JSON manifests composed from a closed set of **operations** (registry, external-process, service control, etc.). Composition via **sub-action** references is target-only — the runtime parses sub-action steps but execution returns "not supported", and the alpha schema doesn't list them at all (see [manifest-reference/](manifest-reference/)). See [action-authoring.md](action-authoring.md) for the schema and examples.

Community extensibility lives at the **manifest** level — anyone can write a JSON action and drop it into `%LOCALAPPDATA%\WinEvo\Actions\`. Extending the operation set (adding fundamentally new trusted-code operations to the agent) requires a code-level PR and review; this is the security boundary.

## Distribution

See [distribution.md](distribution.md) for the full picture, including why MSIX was abandoned.

1. ✅ **Unpackaged / portable** — self-contained zip containing `WinEvo.exe`, `agent/WinEvo.Agent.exe`, the bundled .NET + WinAppSDK runtimes, action manifests, and resources. Runs from any folder with no install and no admin rights; the Shell spawns the agent from `agent/WinEvo.Agent.exe` next to itself, unelevated by default and UAC-promoted on demand.
2. 🔲 **Signed installer** *(target)* — an `.msi`/`.exe` built from `WinEvo.Installer.wixproj`, carrying the same payload. Also the route to a Store listing, under a policy that accepts a download URL to a signed installer instead of a package.
3. ❌ **MSIX** — built during 0.1–0.2 and abandoned; the Store refused the restricted capability a packaged build needs in order to write real user settings.

Either shipping channel embeds the agent **inside** its own payload — there is no separate agent download. Service mode remains unwired; the installer in (2) is its prerequisite, since registering a `LocalSystem` service needs an installer that a packaged app could never provide.

`WinEvo.Tray.exe` is a stub and is not yet shipped in either channel.

## Safety model

See [security-model.md](security-model.md) for the full treatment. In brief:

- Elevation is opt-in per action. Non-elevated actions never touch the agent. ✅ wired.
- *(target)* Every destructive operation supports **undo** with state backup in `%ProgramData%\WinEvo\UndoStore\`. The `undo` block was stripped from the live schema for alpha; manifests in [manifest-reference/](manifest-reference/) document the target shape.
- *(target)* Restore points are opt-in per action manifest, not a default. (`execution.createRestorePoint` is reference-only.)
- *(target)* Dry-run preview is opt-in per action manifest. (`dryRun` block reference-only.)
- *(target)* All executions are logged to `%ProgramData%\WinEvo\Logs\executions.jsonl` for audit + undo discovery.

## Project layout

See the top-level [README.md](../README.md) and solution file for the canonical project structure. Dependency flow is strictly acyclic: `Contracts` → `ActionModel` → `Actions.Abstractions` → `Actions.Operations` → `Agent.Core` → `Agent`, with `Ipc` sitting beside `Contracts`, and `Shell.Core` / `Shell` / `Tray` consuming `Contracts` + `Ipc` only (no direct reference to agent internals).
