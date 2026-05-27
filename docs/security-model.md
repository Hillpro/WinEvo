# Security model

> **Implementation status.** The Shell / Agent split and lazy UAC promotion are wired end-to-end. Many defences described below are **target design** — they document the intended model, not what runs today. Sections and lines marked *(not implemented yet)* describe behaviour that's on the roadmap; everything else reflects the current runtime.

## Trust boundaries

```
  Untrusted input ─► Shell (unelevated) ─► IPC ─► Agent (elevated on demand)
                                   │
                              Community JSON
                            (parsed but not code)
```

Three layers of defence:

1. **The Shell is never elevated.** A compromise of the UI cannot escalate without the agent agreeing. — ✅ The Shell launches `asInvoker`; the elevated broker is spawned only when an action requires it.
2. **The agent accepts only JSON, never code.** Manifests reference operations and pass parameters; they cannot inject arbitrary binaries or scripts except through operations like `powershell` or `external-process`, whose inputs are constrained by the operation implementation. — ✅ Structurally enforced; *(not implemented yet)* runtime input-sanitisation rules for those operations (see below).
3. **The operation set is the security boundary.** Adding an operation is a trusted-code PR; composing existing operations (and sub-actions) is an untrusted-data contribution. — ✅ Enforced by project layout. *(not implemented yet)* JSON-Schema validation of manifests at load time — parsing is currently lenient and unknown fields are silently ignored.

## Elevation

- Actions declare `elevation: required | not-required | optional`.
- When an action requires elevation, the Shell tears down its current broker and relaunches an elevated one via a single UAC prompt; the elevated broker persists for the session. See the UAC flow in [architecture.md](architecture.md).
- *(not implemented yet)* **Service mode.** The target design installs a Windows Service for persistent background execution; the UI toggle that switches between service and on-demand broker is not wired. Only the on-demand broker is shipped today.
- *(not implemented yet)* **Authenticode client verification.** The agent will eventually resolve the connecting client's PID via `GetNamedPipeClientProcessId` and verify its Authenticode signature before accepting commands. The named-pipe ACL is the only access control today — DACL grants the same-user SID full duplex access, and when the broker is elevated the pipe's mandatory integrity label is lowered to Medium so an unelevated Shell can still connect.

## Community actions

What happens to a community manifest dropped into `%LOCALAPPDATA%\WinEvo\Actions\`:

- **Parsed** — ✅ lenient parse; unknown properties ignored. *(not implemented yet)* schema validation via `JsonSchema.Net`.
- **Rendered in the UI with declared warnings and severity** — ✅ severity-adapted `ContentDialog` gates execution: `info`/`warning` show a plain Continue, `danger` requires an "I understand" checkbox, `critical` additionally requires the user to type the action name. Warning keys resolve against `resources/Strings.{en,fr}.json` with `{token}` interpolation; dedup by key with max severity per key wins.
- **Never auto-run; the user always confirms** — ✅ execution requires an explicit click on the detail view, followed by the confirmation dialog above when warnings are declared.
- **Executed using only built-in operations, whose implementations have been reviewed** — ✅ 9 operations wired: `registry-set`, `registry-delete`, `registry-read`, `process-kill`, `external-process`, `builtin-tool`, `powershell`, `command`, `delay`. Manifests that use any other operation fail cleanly at runtime (`operation 'X' is not implemented`).

## `external-process`, `builtin-tool`, `powershell`, and `command`

These are the most powerful operations and deserve extra care.

- `external-process` ✅ runs a user-supplied executable path. For community manifests the path should resolve to a system binary (`%SystemRoot%\System32\...`), a Sysinternals tool (mediated by `Tools.Sysinternals`), or a binary bundled with the manifest (future). *(not implemented yet)* — no validator currently flags arbitrary user-supplied paths or forces a `danger`-severity warning on them.
- `builtin-tool` ✅ narrowed alias of `external-process` for stock Windows tools. Accepts a bare tool `name` (no path), appends `.exe` if missing, rejects any input containing `\`, `/`, `:`, or `..`, and always resolves against `Environment.SystemDirectory`. Preferred over raw `external-process` for `cipher`, `sc`, `ipconfig`, etc. because the manifest intent and the target binary are unambiguous at review time.
- `powershell` ✅ runs a user-supplied script via `powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command <script>`. `{{params.X}}` substitutions are rendered before dispatch. Community manifests using this operation should declare a `danger`-severity warning.
- `command` ✅ runs a user-supplied `cmd.exe` script (single- or multi-line). Parameter values substituted via `{{...}}` are **not** auto-escaped; reviewers must scrutinise every parameter substitution that lands in a shell command.

## Undo *(not implemented yet)*

Per-operation undo will capture the minimum state needed to revert. Nothing is wired today. The `undo` block and per-step `backupForUndo: true` flag have been stripped from the live schema for alpha; both are preserved in [manifest-reference/](manifest-reference/) as the target shape. The runtime parser is more lenient than the live schema and still tolerates a stray `backupForUndo` as a no-op, but new manifests should not declare it.

Target shape:

- `registry-set` / `registry-delete` — previous value or "did not exist" marker.
- `service-stop` / `service-start` — previous running state.
- `file-delete` — file contents backed up below a size threshold; larger files trigger a manual confirmation ("cannot undo this delete").
- `file-copy` / `file-move` — destination state before operation.
- `process-kill` — inherently irreversible; documented and flagged.
- `external-process` / `powershell` / `command` — irreversible unless the manifest supplies explicit undo steps.

Undo state will live in `%ProgramData%\WinEvo\UndoStore\<exec-id>.json` with an index in `UndoStore\index.db`.

## Restore points *(not implemented yet)*

Opt-in per manifest (target shape: `execution.createRestorePoint: true`). When wired, the agent will create a System Restore Point before the first step. Restore points are **defence in depth**, not the primary recovery path. The `createRestorePoint` field has been stripped from the live schema for alpha and lives in [manifest-reference/](manifest-reference/) until the `system-restore-point` operation is wired.

## Audit log

- ✅ **Diagnostic log** — the agent writes startup events and unhandled exceptions to `%TEMP%\winevo-agent.log` (`WinEvo.Agent.Core.AgentLog`). This is the only log today and it's for troubleshooting, not auditing.
- *(not implemented yet)* **Execution audit log** at `%ProgramData%\WinEvo\Logs\executions.jsonl` — will record execution id, action id, version, parameters, outcome (success / failed / cancelled / rolled-back), per-step result + duration, and undo-state reference. Append-only, no delete API. The Shell's History view will browse past executions and drive Undo.

## Supply chain

- *(not implemented yet)* **Code signing** for Shell, Tray, Agent binaries. The future Authenticode client-signature check in the agent depends on this.
- *(not implemented yet)* **Sysinternals tools** — will be downloaded from `live.sysinternals.com` over HTTPS and hash-verified against known-good fingerprints. The `Tools.Sysinternals` project is still a stub.
- ✅ **Dependencies pinned** in `Directory.Packages.props`; version bumps are deliberate.
- *(future)* GitHub Actions workflows will use pinned action SHAs, not floating tags.

## Out of scope for v1

- Sandboxing the agent inside an AppContainer or similar. The agent runs as LocalSystem (service mode, later) or as an elevated user process (broker mode, today). Future work: investigate AppContainer for operations that don't need the full privilege set.
- Remote administration. All IPC is local-machine only; there is no network listener.
- Multi-user / tenanted execution. One agent per user session.
