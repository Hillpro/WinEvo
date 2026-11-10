# Security model

> **Implementation status.** The Shell / Agent split and lazy UAC promotion are wired end-to-end. Many defences described below are **target design** — they document the intended model, not what runs today. Sections and lines marked *(not implemented yet)* describe behaviour that's on the roadmap; everything else reflects the current runtime.

## Trust boundaries

```
  User input ─► Shell (unelevated) ─► IPC ─► Agent (elevated on demand)
                       │                         │
              reads descriptors          runs the action it is
              never runs actions         asked for, from its own
                                         compiled catalog
```

Three layers of defence:

1. **The Shell is never elevated.** A compromise of the UI cannot escalate without the agent agreeing. ✅ The Shell launches `asInvoker`; the elevated broker is spawned only when an action requires it.
2. **The agent runs only actions compiled into it.** A caller names an action by id and supplies parameter values. It cannot supply steps, scripts, binaries or a definition of an action, and it cannot lower an action's elevation requirement: that is read from the agent's own catalog. ✅ Enforced by the message format; an unknown id is refused and logged.
3. **Operations are the review boundary.** Actions are expected to touch the system only through the closed set of operations, and adding an operation gets the strictest review. ✅ By convention and code review. Nothing in the compiler stops an action from calling a Windows API directly, so reviewing an action includes checking that it does not.

What remains untrusted is the **parameter values**. They come from the Shell, and before client verification exists, from any process running as the same user that reaches the pipe. Every action must treat them as hostile input; see the operations section below.

## Elevation

- Actions declare `elevation: required | not-required | optional`.
- When an action requires elevation, the Shell tears down its current broker and relaunches an elevated one via a single UAC prompt; the elevated broker persists for the session. See the UAC flow in [architecture.md](architecture.md).
- *(not implemented yet)* **Service mode.** The target design installs a Windows Service for persistent background execution; the UI toggle that switches between service and on-demand broker is not wired. Only the on-demand broker is shipped today.
- *(not implemented yet)* **Authenticode client verification.** The agent will eventually resolve the connecting client's PID via `GetNamedPipeClientProcessId` and verify its Authenticode signature before accepting commands. The named-pipe ACL is the only access control today — DACL grants the same-user SID full duplex access, and when the broker is elevated the pipe's mandatory integrity label is lowered to Medium so an unelevated Shell can still connect.

## Where actions come from

Only the actions shipped inside WinEvo exist. There is no folder the app scans, no file a user can drop in, and no network source.

- **Reviewed before release.** ✅ An action is a C# class in the repository and reaches users only through a release build.
- **Rendered with its declared warnings and severity.** ✅ A severity-adapted `ContentDialog` gates execution: `info`/`warning` show a plain Continue, `danger` requires an "I understand" checkbox, `critical` additionally requires the user to type the action name.
- **Never auto-run; the user always confirms.** ✅ Execution requires an explicit click or switch flip in the detail view, followed by the confirmation dialog above when warnings are declared.

*(not decided)* Actions from third parties. If they return, the two candidates are a simple data format restricted to composing existing operations, or loading a locally built assembly in an explicit developer mode. The second runs arbitrary code inside the elevated agent: it would only ever be an opt-in for the person who built the assembly, loaded from a location that cannot be written without elevation.

## `external-process`, `builtin-tool`, `powershell`, and `command`

These are the most powerful operations and deserve extra care. All four are wired.

- `external-process` runs the executable an action names, with each argument passed separately so it is never re-parsed by a shell. An action should point it at a system binary or a tool WinEvo itself provides, never at a path taken from a parameter.
- `builtin-tool` is a narrowed form for stock Windows tools. It accepts a bare tool name (no path), appends `.exe` if missing, rejects any input containing `\`, `/`, `:`, or `..`, and always resolves against `Environment.SystemDirectory`. Preferred over `external-process` for `cipher`, `sc`, `ipconfig`, etc.
- `powershell` runs a script via `powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command <script>`.
- `command` runs a line through `cmd.exe /C`.

**Parameter values must never be inserted into a `powershell` script or a `command` line.** The text of both is executed as written, with the agent's privileges, so a value such as `C:\ & del ...` would run. Pass a parameter value only as its own argument to `external-process` or `builtin-tool`. *(not implemented yet)* Nothing checks this automatically; it is a review rule for every action.

## Undo *(not implemented yet)*

Per-operation undo will capture the minimum state needed to revert. Nothing is wired today: every executed action is permanent. Because every operation an action runs goes through `ActionExecution`, that is where the capture will happen.

Target shape:

- `registry-set` / `registry-delete`: previous value or "did not exist" marker.
- `service-stop` / `service-start`: previous running state.
- `file-delete`: file contents backed up below a size threshold; larger files trigger a manual confirmation ("cannot undo this delete").
- `file-copy` / `file-move`: destination state before operation.
- `process-kill`: inherently irreversible; documented and flagged.
- `external-process` / `powershell` / `command`: irreversible unless the action supplies explicit undo code.

Undo state will live in `%ProgramData%\WinEvo\UndoStore\<exec-id>.json` with an index in `UndoStore\index.db`.

## Restore points *(not implemented yet)*

Opt-in per action. When wired, the agent will create a System Restore Point before the action's first operation. Restore points are **defence in depth**, not the primary recovery path. They depend on the planned `system-restore-point` operation.

## Audit log

- ✅ **Diagnostic log** — the agent writes startup events and unhandled exceptions to `%LOCALAPPDATA%\WinEvo\agent.log`. The Shell logs to `shell.log` in the same folder. These are for troubleshooting, not auditing.
- *(not implemented yet)* **Execution audit log** at `%ProgramData%\WinEvo\Logs\executions.jsonl` — will record execution id, action id, version, parameters, outcome (success / failed / cancelled / rolled-back), per-step result + duration, and undo-state reference. Append-only, no delete API. The Shell's History view will browse past executions and drive Undo.

## Supply chain

- *(not implemented yet)* **Code signing** for Shell, Tray, Agent binaries. The future Authenticode client-signature check in the agent depends on this.
- *(not implemented yet)* **Sysinternals tools** — will be downloaded from `live.sysinternals.com` over HTTPS and hash-verified against known-good fingerprints. No resolver project exists yet; it will be created when the `sysinternals-tool` operation is built.
- ✅ **Dependencies pinned** in `Directory.Packages.props`; version bumps are deliberate.
- *(future)* GitHub Actions workflows will use pinned action SHAs, not floating tags.

## Out of scope for v1

- Sandboxing the agent inside an AppContainer or similar. The agent runs as LocalSystem (service mode, later) or as an elevated user process (broker mode, today). Future work: investigate AppContainer for operations that don't need the full privilege set.
- Remote administration. All IPC is local-machine only; there is no network listener.
- Multi-user / tenanted execution. One agent per user session.
