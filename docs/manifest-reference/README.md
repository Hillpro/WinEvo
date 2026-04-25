# Manifest reference snapshots

These files are a frozen snapshot of the **target** action-manifest shape — the
schema and example manifests as they should look once the engine catches up.

The live files in [actions/schemas/](../../actions/schemas/) and
[actions/](../../actions/) are trimmed to alpha reality: every field the
runtime doesn't actually consume has been stripped so manifests don't lie
about capabilities they can't deliver.

## What's stripped from the live files (and what re-enables each)

| Stripped field | Live location it was removed from | Re-enabled by |
|---|---|---|
| `requirements.minWindowsBuild` | both manifests + schema | OS-build gate evaluator in `Agent.Core` |
| `requirements.architectures` | both manifests + schema | Arch gate evaluator in `Agent.Core` |
| `requirements.packageIdentity` | both manifests + schema | MSIX-identity gate (post-Store submission) |
| `requirements.minAgentVersion` | schema only | Agent-version gate evaluator |
| `requirements.disabled` | schema only | Manifest-disable mechanism |
| `preconditions` (top-level) + `precondition` $def | `wipe-free-space.json` + schema | Precondition engine (roadmap) |
| `execution.createRestorePoint` | `wipe-free-space.json` + schema | `system-restore-point` operation wired |
| `step.progress` + `progress` $def | `wipe-free-space.json` + schema | Streaming progress events over IPC (gRPC migration) |
| `step.condition` | schema only | Per-step condition evaluator (depends on precondition engine) |
| `step.backupForUndo` | `disable-bing-in-search.json` (× 2 steps) | Undo system (roadmap item 4) |
| `undo` block + `undo` $def | both manifests + schema | Undo system (roadmap item 4) |
| `dryRun` block + `dryRun` $def | both manifests + schema | Dry-run executor (roadmap) |
| `localizedResourceRef` $def | schema only | Re-introduced with undo / dryRun (its only consumers) |
| Sub-action step (`subActionStep` $def, `parameterBindings` $def, `step.oneOf`) | schema only | Sub-action expander in `ActionExecutor` |
| Operations: `registry-read`, `service-stop`, `service-start`, `service-restart`, `file-delete`, `file-copy`, `file-move`, `sysinternals-tool`, `system-restore-point` | schema enum | Each operation wired individually in `Actions.Operations` |

Parameter types `wifi-profile`, `file-path`, `directory-path`, `service-name`
are kept in the live schema even though the UI renders them as plain text
boxes today — they're future-proof metadata that doesn't actively mislead.

## How to use these snapshots

- **Authoring a new manifest for alpha?** Use the live schema. The features
  it omits aren't deliverable yet.
- **Designing the engine work to re-enable a feature?** Use the snapshot here
  as the target shape. When the engine ships, port the relevant block back
  from this snapshot to the live schema.
- **Updating these snapshots?** Only when the design of a future field
  changes. The snapshot tracks intent, not history — for history, use
  `git log`.
