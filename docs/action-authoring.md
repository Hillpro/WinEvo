# Authoring Actions

An **action** in WinEvo is a JSON document describing what to do, what to warn the user about, what parameters to collect, and how to undo the change. Actions live in `actions/<category>/<id>.json` (shipped with the app) or `%LOCALAPPDATA%\WinEvo\Actions\<category>\<id>.json` (added by users).

The schema is in [../actions/schemas/action.schema.json](../actions/schemas/action.schema.json). Point your editor at it for autocomplete and validation.

## File skeleton

```json
{
  "$schema": "../schemas/action.schema.json",
  "id": "category.short-identifier",
  "version": "1.0.0",
  "name":        { "en": "Human name", "fr": "Nom humain" },
  "description": { "en": "One-sentence description.",
                   "fr": "Description d'une phrase." },
  "category": "customization | resources | storage | updates",
  "tags": ["privacy", "..."],
  "icon": "icon-id",
  "author": "your-handle",

  "requirements": { ... },
  "warnings":     [ ... ],
  "parameters":   [ ... ],
  "preconditions":[ ... ],
  "execution":    { ... },
  "undo":         { ... },
  "dryRun":       { ... }
}
```

## Identifiers

`id` must be globally unique and stable. Once published, the id should never change — it is the key used to track execution history, stored undo state, and user preferences. Format: `<category>.<kebab-case-name>`.

## Localization

Inline text (`name`, `description` on the action and on each parameter) is a **plain English string at the top level** — English is the base/fallback. To provide translations, add an optional `localization` block with per-language overrides:

```json
{
  "name": "Wipe free space",
  "description": "Overwrites free space on the selected drive...",
  "parameters": [
    { "id": "drive", "type": "drive", "name": "Drive", "required": true }
  ],

  "localization": {
    "fr": {
      "name": "Effacer l'espace libre",
      "description": "Écrase l'espace libre du disque sélectionné...",
      "parameters": {
        "drive": { "name": "Disque" }
      }
    }
  }
}
```

Language codes are IETF tags (`fr`, `es`, `de-AT`, …). Any field not overridden in a given language falls back to the English top-level value. Single-user / custom actions that don't need translations can skip the `localization` block entirely.

**Two distinct mechanisms:**

- **Inline localization** (above) — for text unique to this manifest.
- **Resource-key references** — for text shared across many manifests. Used by `warnings[].key`, `undo.reason`, `dryRun.preview`. The runtime resolves these keys from shared string bundles (`resources/Strings.<lang>.resx` in the Shell), so a generic warning like "this action is destructive" is translated once and reused everywhere.

## Requirements

```json
"requirements": {
  "elevation": "required | not-required | optional",
  "packageIdentity": "required | optional | forbidden",
  "minWindowsBuild": 22000,
  "architectures": ["x64", "arm64"]
}
```

- **elevation: required** — the agent must be available. If the user has background execution off, WinEvo spawns a broker on demand (UAC).
- **elevation: not-required** — runs in the Shell process, no IPC round-trip.
- **elevation: optional** — elevation improves the operation but isn't mandatory; the runtime picks the path at execution time.

## Warnings

Each warning has a **severity** and a **resource key**. Severities drive the UI presentation (icon, colour, required confirmation).

| Severity | UI treatment |
|---|---|
| `info` | Small hint, no extra prompt. |
| `warning` | Yellow banner, dismissable confirmation. |
| `danger` | Red banner, explicit confirmation required. |
| `critical` | Red, explicit confirmation + typed-phrase confirmation for irreversible operations. |

## Parameters

Parameters are rendered in the Shell as form fields and bound by id into the execution template via `{{params.<id>}}`.

Supported parameter `type`s (v1): `string`, `integer`, `boolean`, `enum`, `drive`, `wifi-profile`, `file-path`, `directory-path`, `service-name`.

## Execution

`execution.mode` is `sequential` (steps run in order, stop on first failure) or `sequential-continue-on-error` (best-effort).

`execution.steps` is an ordered list of steps. Each step is either an **operation** (an atomic invocation) or a **sub-action** (a reference to another action manifest). See [../actions/schemas/action.schema.json](../actions/schemas/action.schema.json) for the full set of operations.

### Example step

```json
{
  "id": "cipher-wipe",
  "operation": "external-process",
  "path": "%SystemRoot%\\System32\\cipher.exe",
  "args": ["/w:{{params.drive}}"],
  "timeout": null,
  "progress": { "type": "indeterminate" }
}
```

Path variables (`%SystemRoot%`, `%ProgramFiles%`, etc.) are expanded via `Environment.ExpandEnvironmentVariables` at execution time.

### Choosing between execution operations

| Operation | When to use |
|---|---|
| `external-process` | You have a specific `.exe` and want to invoke it with arguments. Argv-style — arguments are **not** interpreted by a shell, so untrusted parameter values cannot inject extra commands. Safest default. |
| `builtin-exe` | Alias for `external-process` restricted to `%SystemRoot%\System32\*.exe`. Use for `sfc`, `dism`, `cipher`, `takeown`, etc. |
| `powershell` | PowerShell script or cmdlet invocation. Pick this for Windows-automation idioms (`Export-WindowsDriver`, `Get-Service`, object pipelines). |
| `command` | `cmd.exe`-style script — single or multi-line. Pick this when you need shell features: pipes (`\|`), redirects (`>`), chaining (`&&`, `\|\|`), `for` loops, classic batch syntax. Values substituted via `{{...}}` are **not** auto-escaped — the author is responsible for safely quoting any untrusted input. Prefer `external-process` when a shell isn't actually needed. |

`command` shape:

```json
{
  "operation": "command",
  "script": "ipconfig /release && ipconfig /renew && ipconfig /flushdns",
  "workingDirectory": "C:\\",
  "timeout": 60
}
```

Multi-line `script` values are written to a temporary `.cmd` file and executed via `cmd.exe /C`. For single-line scripts the agent passes `/C "<script>"` directly.

## Templating

Wherever a string is executed, `{{params.<id>}}` substitutes the parameter value. The substitution is **shell-neutral** — args are passed as an argv array, so no quoting or escaping is needed or allowed.

## Undo

```json
"undo": {
  "supported": true,
  "strategy": "automatic | manual",
  "reason": { "key": "..." }
}
```

- **automatic** — operations that set `backupForUndo: true` capture enough state to self-revert; the runtime composes an undo from their step outputs.
- **manual** — the manifest includes an explicit `undo.steps` list (mirror of `execution.steps`).
- **not supported** — provide a `reason` resource key explaining why (e.g. `undo.irreversible`).

## Dry-run

```json
"dryRun": {
  "supported": true,
  "preview": {
    "key": "dryrun.storage.wipe",
    "tokens": { "drive": "{{params.drive}}" }
  }
}
```

The Shell renders the resource string with the provided tokens. Dry-run does **not** execute any operation.

## Composing actions (calling other actions)

A step can invoke another action instead of performing an operation. Mix freely — a composite can have its own operation steps alongside sub-action steps.

```json
"execution": {
  "steps": [
    { "kind": "sub-action",
      "ref": "storage.force-delete",
      "minVersion": "1.0.0",
      "parameters": { "target": "{{params.folder}}" } },

    { "kind": "operation",
      "operation": "delay",
      "seconds": 2 },

    { "kind": "sub-action",
      "ref": "storage.wipe-free-space",
      "minVersion": "1.0.0",
      "parameters": { "drive": "{{drive(params.folder)}}" } }
  ]
}
```

### Step `kind`

| `kind`       | Required fields                              | Notes |
|--------------|----------------------------------------------|-------|
| `operation`  | `operation`                                  | `kind` is optional on operation steps when `operation` is present — kept for terser manifests. |
| `sub-action` | `kind: "sub-action"`, `ref`, `minVersion`    | `parameters` is an explicit mapping of the referenced action's parameter ids to values (no implicit pass-through). |

### Parameter binding is explicit

Parent passes child params by name, explicitly. No implicit inheritance by name-matching. This keeps the coupling visible and makes renames in the child a detectable breaking change in the parent.

```json
"parameters": {
  "target": "{{params.folder}}",         // parent's 'folder' → child's 'target'
  "confirmBypass": true,                  // literal
  "retries": 3                            // literal integer
}
```

Child params not mentioned here fall back to their schema defaults; if a required child param has no default and no mapping, validation fails at load time.

### Cycle detection

Action refs form a graph. The loader runs a depth-first scan before any execution and rejects any cycle it finds with the full path printed.

### Version pinning

`minVersion` on an action step is mandatory. If the installed version of the referenced action is older, the parent action is flagged unexecutable in the UI with an "update required" hint.

### Transitive elevation

A composite's effective elevation requirement = `max` of its own operations' needs and every sub-action's needs (computed recursively). The UI shows the shield icon on the parent if any descendant requires elevation.

### Undo is all-or-nothing

**A composite is undoable iff every reachable step (operations and sub-actions) is undoable.** If any step is irreversible, the composite is marked not-undoable and the UI explains which step blocks undo.

Why: once a non-undoable step has run, later attempts to "undo just the earlier steps" can leave the system in a silently inconsistent state — the earlier steps' undo preconditions may have been invalidated by the irreversible step. Rather than guess, we refuse. This applies equally to user-requested undo *after* execution and to auto-rollback on mid-execution failure.

If the author of a composite sets `undo.supported: true` but the validator detects an irreversible step, loading fails with a clear error naming the blocker.

### Warnings roll up

Warnings from the composite itself **and** from every sub-action it reaches are aggregated and shown in the confirmation dialog. Keys are deduplicated; the highest severity wins per key.

### Dry-run composes

If all reached sub-actions support dry-run, the composite's preview pane shows each sub-action's preview in order. If any sub-action doesn't support dry-run, the composite's dry-run is not supported either (same principle as undo).

## Template functions

Inside template expressions (`{{ ... }}`) you may use a small set of built-in functions beyond parameter references:

| Expression | Result |
|---|---|
| `{{params.foo}}` | Value of parameter `foo`. |
| `{{drive(pathExpr)}}` | Drive letter of a path, e.g. `"C:\\"`. |
| `{{basename(pathExpr)}}` | Last component of a path, e.g. `"myfolder"`. |
| `{{dirname(pathExpr)}}` | Parent directory of a path, e.g. `"C:\\Users\\alice"`. |

Functions may be composed. Nested template references are not supported.

## Submitting an action

1. Write the manifest under `actions/<category>/`.
2. Validate it locally against the schema (any JSON Schema validator; the Shell will also validate on load).
3. Add resource strings to `resources/Strings.en.resx` and `resources/Strings.fr.resx`.
4. Open a PR. Community manifests are reviewed for safety; the reviewer cross-checks every operation the manifest invokes.

## Adding a new operation

Manifests can only compose **existing** operations. To add a fundamentally new operation kind (e.g. "set per-app GPU preference"):

1. Open an issue describing the need.
2. Add the operation to `WinEvo.Actions.Operations` as a class implementing `IActionOperation`.
3. Add its schema to `actions/schemas/action.schema.json`.
4. Submit a PR. This is the trusted-code boundary; expect review on security, undo correctness, and dry-run support.
