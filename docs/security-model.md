# Security model

## Trust boundaries

```
  Untrusted input ─► Shell (unelevated) ─► IPC ─► Agent (elevated)
                                   │
                              Community JSON
                            (parsed but not code)
```

Three layers of defence:

1. **The Shell is never elevated.** A compromise of the UI cannot escalate without the agent agreeing.
2. **The agent accepts only JSON, never code.** Manifests can reference operations and pass parameters — they cannot inject arbitrary binaries or scripts except through operations like `powershell` or `external-process`, whose inputs are constrained.
3. **The operation set is the security boundary.** Adding an operation is a trusted-code PR; composing existing operations (and sub-actions) is an untrusted-data contribution. Reviewers scrutinize new operations heavily.

## Elevation

- Default install has **background execution enabled** — the service is registered at first need. UAC once, then silent operation.
- Disabling background execution uninstalls the service (UAC) and switches to the broker model: one UAC per elevated batch.
- Actions declare `elevation: required | not-required | optional`. Non-elevated actions bypass IPC entirely.
- The agent verifies every connecting client's Authenticode signature before accepting commands.

## Community actions

Community-contributed JSON manifests are:

- Parsed and validated against the schema.
- Rendered in the UI with their declared warnings and severity.
- **Never** auto-run. The user always confirms execution.
- Executed using only **built-in operations**, whose implementations have been reviewed.

## `external-process`, `powershell`, and `command`

These are the most powerful operations and deserve extra care:

- `external-process` runs a user-supplied executable path. For community manifests, the path is expected to resolve to a system binary (`%SystemRoot%\System32\...`), a Sysinternals tool (mediated by `Tools.Sysinternals`), or a binary bundled with the manifest (future). Arbitrary user-supplied paths are flagged by validation and require a `danger`-severity warning.
- `powershell` runs a user-supplied script. Execution is logged verbatim, transcripted, and requires a `danger`-severity warning.
- `command` runs a user-supplied `cmd.exe` script. Parameter values substituted via `{{...}}` are **not** auto-escaped — unescaped shell metacharacters (`&`, `|`, `>`, `<`, `^`, `"`) from untrusted input are a command-injection vector. Reviewers scrutinize every parameter substitution in a `command` script and reject manifests that interpolate user-free-form strings into a command line without explicit quoting. Execution is logged verbatim; requires a `danger`-severity warning.

## Undo

Per-operation undo captures the minimum state required to revert:

- `registry-set` / `registry-delete` — previous value or "did not exist" marker.
- `service-stop` / `service-start` — previous running state.
- `file-delete` — file contents backed up below a size threshold; larger files trigger a manual confirmation ("cannot undo this delete").
- `file-copy` / `file-move` — destination state before operation.
- `process-kill` — inherently irreversible; documented and flagged.
- `external-process` / `powershell` — irreversible unless the manifest supplies explicit undo steps.

Undo state lives in `%ProgramData%\WinEvo\UndoStore\<exec-id>.json` with an index in `UndoStore\index.db`.

## Restore points

Opt-in per manifest (`execution.createRestorePoint: true`). When set, the agent creates a System Restore Point before the first step. Restore points are **defense in depth**, not the primary recovery path.

## Audit log

Every execution writes to `%ProgramData%\WinEvo\Logs\executions.jsonl`:

- execution id, action id, version
- parameters
- outcome (success / failed / cancelled / rolled-back)
- per-step result and duration
- undo state reference

The log is append-only; no API exposes delete. Users can browse past executions in the Shell's History view and invoke Undo from there.

## Supply chain

- Code signing required for Shell, Tray, Agent binaries. The agent's client-signature check relies on this.
- Sysinternals tools are downloaded from `live.sysinternals.com` over HTTPS and hash-verified against known-good fingerprints.
- Dependencies are pinned in `Directory.Packages.props`; version bumps are deliberate.
- GitHub Actions (when introduced) will use pinned action SHAs, not floating tags.

## Out of scope for v1

- Sandboxing the agent inside an AppContainer or similar. The agent runs as LocalSystem (service mode) or as an elevated user process (broker mode). Future: investigate AppContainer for operations that don't need the full privilege set.
- Remote administration. All IPC is local-machine only; there is no network listener.
- Multi-user/tenanted execution. One agent per user session.
