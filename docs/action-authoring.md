# Authoring Actions

> **Implementation status.** The manifest loader, the `ExecutionMode` enum, `{{params.X}}` substitution, and localized `name` / `description` are live. Operation coverage:
>
> | Operation              | Status    | In live schema? |
> |------------------------|-----------|-----------------|
> | `registry-set`         | ✅ wired  | yes             |
> | `registry-delete`      | ✅ wired  | yes             |
> | `process-kill`         | ✅ wired  | yes             |
> | `external-process`     | ✅ wired  | yes             |
> | `builtin-tool`         | ✅ wired  | yes             |
> | `powershell`           | ✅ wired  | yes             |
> | `command`              | ✅ wired  | yes             |
> | `delay`                | ✅ wired  | yes             |
> | `registry-read`        | 🔲 target | reference only  |
> | `service-stop`         | 🔲 target | reference only  |
> | `service-start`        | 🔲 target | reference only  |
> | `service-restart`      | 🔲 target | reference only  |
> | `file-delete`          | 🔲 target | reference only  |
> | `file-copy`            | 🔲 target | reference only  |
> | `file-move`            | 🔲 target | reference only  |
> | `sysinternals-tool`    | 🔲 target | reference only  |
> | `system-restore-point` | 🔲 target | reference only  |
>
> DISM, SFC, and similar System32 tools don't need a dedicated operation — invoke them through `builtin-tool`.
>
> **Live schema vs reference schema.** The live schema at [../actions/schemas/action.schema.json](../actions/schemas/action.schema.json) is alpha-trimmed: it accepts only what the runtime actually consumes. Sections in this guide marked *(reference only)* describe target features that have been stripped from the live schema and live in [../docs/manifest-reference/](manifest-reference/) until the engine catches up — `undo`, `dryRun`, `preconditions`, sub-action steps, restore points, progress events, OS/arch/package gates, and the operations marked *reference only* above. The runtime parser is more lenient than the live schema (it still tolerates some stripped fields as no-ops or runtime-failures), so manifests authored against the reference schema today don't crash, but a future JSON-Schema validator will reject them — author against the **live** schema for anything you intend to ship.

An **action** in WinEvo is a JSON document describing what to do, what to warn the user about, what parameters to collect, and (eventually) how to undo the change. Actions live in `actions/<category>/<id>.json` (shipped with the app) or `%LOCALAPPDATA%\WinEvo\Actions\<category>\<id>.json` (added by users).

The schema is in [../actions/schemas/action.schema.json](../actions/schemas/action.schema.json). Point your editor at it for autocomplete and validation.

## File skeleton

Live (alpha) schema:

```json
{
  "$schema": "../schemas/action.schema.json",
  "id": "category.short-identifier",
  "version": "1.0.0",
  "name": "Human name",
  "description": "One-sentence description.",
  "category": "customization | resources | storage | updates",
  "tags": ["privacy", "..."],
  "icon": "icon-id",
  "author": "your-handle",

  "requirements": { ... },
  "warnings":     [ ... ],
  "parameters":   [ ... ],
  "execution":    { ... },

  "localization": { "fr": { "name": "...", "description": "..." } }
}
```

The reference schema in [manifest-reference/](manifest-reference/) additionally has top-level `preconditions`, `undo`, and `dryRun` blocks; those are described below as *(reference only)*.

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

- **Inline localization** (above) — ✅ for text unique to this manifest.
- **Resource-key references** — for text shared across many manifests. Used by `warnings[].key` ✅ wired (resolved against `resources/Strings.{en,fr}.json`), and by `undo.reason` / `dryRun.preview` *(reference only — those blocks live in [manifest-reference/](manifest-reference/))*. A generic warning like "this action is destructive" is translated once and reused everywhere.

## Requirements

Live (alpha) schema:

```json
"requirements": {
  "elevation": "required | not-required | optional"
}
```

- **elevation** — ✅ enforced. `required` triggers the Shell's lazy UAC-promotion flow before the action runs.

*(reference only)* The target schema also carries `packageIdentity`, `minWindowsBuild`, `architectures`, `minAgentVersion`, and `disabled` — these were stripped from the live schema because the runtime never consults them. They live in [manifest-reference/action.schema.json](manifest-reference/action.schema.json).

## Warnings

Each warning has a **severity** and a **resource key**, and optionally a `tokens` map for per-occurrence values. Keys are resolved against the shared Shell string bundle (`resources/Strings.<lang>.json`) at confirmation time; missing keys fall back to the English bundle and then to the raw key (surfaced as-is so the mistake is visible).

| Severity | UI treatment | Status |
|---|---|---|
| `info` | Shown in the confirmation list; "Continue" is immediately enabled. | ✅ wired |
| `warning` | Yellow banner; "Continue" is immediately enabled. | ✅ wired |
| `danger` | Red banner; "I understand" checkbox gates the primary button. | ✅ wired |
| `critical` | Red banner; checkbox **and** typed-phrase challenge (type the action name exactly) both required. | ✅ wired |

When a manifest declares multiple warnings, they are deduplicated by `key` (highest severity per key wins), displayed in first-occurrence order, and the dialog's overall presentation is driven by the max severity across all remaining warnings. A manifest with no warnings executes without a confirmation dialog.

```json
"warnings": [
  { "severity": "warning", "key": "storage.wipe.longRunning" },
  { "severity": "info",    "key": "storage.wipe.destructiveHint" }
]
```

Templates support `{name}` placeholders; authors supply values via a per-warning `tokens` object:

```json
{
  "severity": "danger",
  "key": "service.stopping",
  "tokens": { "service": "wuauserv" }
}
```

## Parameters

Parameters are rendered in the Shell as form fields and bound by id into the execution template via `{{params.<id>}}`.

Supported parameter `type`s (v1): `string`, `integer`, `boolean`, `enum`, `drive`, `wifi-profile`, `file-path`, `directory-path`, `service-name`.

- ✅ **Binding + substitution** works — every declared parameter becomes a form field in the detail view and is templated into operation properties at execute time.
- ✅ **Type-specific pickers** (partial) — `string` / unknown → TextBox; `integer` → NumberBox honoring `min` / `max`; `boolean` → ToggleSwitch; `enum` → ComboBox populated from `choices`; `drive` → ComboBox of live drives filtered by `filter.driveType`.
- *(not implemented yet)* Pickers for `wifi-profile`, `file-path`, `directory-path`, `service-name` — these fall back to the TextBox renderer until a dedicated widget ships.

## Execution

`execution.mode` is `sequential` (steps run in order, stop on first failure) or `sequential-continue-on-error` (best-effort). ✅ Both wired.

`execution.steps` is an ordered list of steps. Each step is either an **operation** (an atomic invocation) or a **sub-action** (a reference to another action manifest). See [../actions/schemas/action.schema.json](../actions/schemas/action.schema.json) for the full set of operations.

### Example step

```json
{
  "id": "cipher-wipe",
  "operation": "external-process",
  "path": "%SystemRoot%\\System32\\cipher.exe",
  "args": ["/w:{{params.drive}}"],
  "timeout": null
}
```

*(reference only)* A `progress: { "type": "indeterminate" | "determinate", "parser": "..." }` block on a step is target-design for the streaming-events transport. Stripped from the live schema; see [manifest-reference/](manifest-reference/).

Path variables (`%SystemRoot%`, `%ProgramFiles%`, etc.) are expanded via `Environment.ExpandEnvironmentVariables` at execution time.

### Choosing between execution operations

| Operation | When to use | Status |
|---|---|---|
| `external-process` | Argv-style invocation of an exe. Safe default — arguments are not shell-interpreted. | ✅ wired |
| `builtin-tool` | Narrowed `external-process` for built-in tools under `%SystemRoot%\System32`. Give the bare tool `name` (no path, `.exe` optional). | ✅ wired |
| `powershell` | PowerShell script or cmdlet invocation. | ✅ wired |
| `command` | `cmd.exe`-style script — single or multi-line — for pipes / redirects / `&&`. Substitutions are **not** auto-escaped; author is responsible for safe quoting. | ✅ wired |

`command` shape (target):

```json
{
  "operation": "command",
  "script": "ipconfig /release && ipconfig /renew && ipconfig /flushdns",
  "workingDirectory": "C:\\",
  "timeout": 60
}
```

Additional wired operations: `registry-delete` (delete a value or an entire subtree — idempotent) and `delay` (cooperative wait, e.g. between `netsh disconnect` / `connect` steps).

Still *(reference only)*: `registry-read`, `service-stop`, `service-start`, `service-restart`, `file-delete`, `file-copy`, `file-move`, `sysinternals-tool`, `system-restore-point`. None are in the live schema; they live in [manifest-reference/action.schema.json](manifest-reference/action.schema.json) and will be added back as each operation is wired.

## Templating

Wherever a string is executed, `{{params.<id>}}` substitutes the parameter value. ✅ Works today. The substitution is **shell-neutral** — args are passed as an argv array for `external-process`, so no quoting or escaping is needed or allowed.

## Undo *(reference only)*

This block was stripped from the live schema for alpha (it claimed a capability the runtime can't honor). Authoring it requires the reference schema in [manifest-reference/](manifest-reference/); the live schema rejects an `undo` property at the manifest root.

```json
"undo": {
  "supported": true,
  "strategy": "automatic | manual",
  "reason": { "key": "..." }
}
```

- **automatic** — operations that set `backupForUndo: true` will capture enough state to self-revert; the runtime composes an undo from their step outputs.
- **manual** — the manifest includes an explicit `undo.steps` list (mirror of `execution.steps`).
- **not supported** — provide a `reason` resource key explaining why (e.g. `undo.irreversible`).

Currently nothing in the undo block has a runtime effect. Every executed action is effectively permanent until the undo engine ships.

## Dry-run *(reference only)*

Like `undo`, this block was stripped from the live schema for alpha. Live in [manifest-reference/](manifest-reference/) until the executor supports it.

```json
"dryRun": {
  "supported": true,
  "preview": {
    "key": "dryrun.storage.wipe",
    "tokens": { "drive": "{{params.drive}}" }
  }
}
```

When wired, the Shell will render the resource string with the provided tokens; dry-run will **not** execute any operation.

## Composing actions — sub-action steps *(reference only)*

Sub-action composition was stripped from the live schema for alpha. The runtime parser still recognises `kind: "sub-action"` steps (returns "not supported yet" at execute time), but the live schema's `step` definition only accepts operation steps. Authoring against the reference schema in [manifest-reference/](manifest-reference/) keeps you on the target shape.

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

Sub-action steps **parse** correctly today but the executor returns a `sub-action steps are not supported yet` failure for them. The rest of this section documents the target semantics.

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

### Cycle detection *(not implemented yet)*

Action refs form a graph. The loader will run a depth-first scan before any execution and reject any cycle it finds with the full path printed.

### Version pinning *(not implemented yet)*

`minVersion` on an action step is mandatory in the schema. At runtime the loader will reject composites whose sub-actions are below that pin.

### Transitive elevation *(not implemented yet)*

A composite's effective elevation requirement = `max` of its own operations' needs and every sub-action's needs (computed recursively). The UI will show the shield icon on the parent if any descendant requires elevation.

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

| Expression | Result | Status |
|---|---|---|
| `{{params.foo}}` | Value of parameter `foo`. | ✅ wired |
| `{{drive(pathExpr)}}` | Drive letter of a path, e.g. `"C:\\"`. | *(not implemented yet)* |
| `{{basename(pathExpr)}}` | Last component of a path, e.g. `"myfolder"`. | *(not implemented yet)* |
| `{{dirname(pathExpr)}}` | Parent directory of a path, e.g. `"C:\\Users\\alice"`. | *(not implemented yet)* |

Functions may be composed. Nested template references are not supported.

## Submitting an action

1. Write the manifest under `actions/<category>/`.
2. Validate it locally against the schema (any JSON Schema validator; *(not implemented yet)* the Shell will also validate on load).
3. *(not implemented yet)* Add resource strings to `resources/Strings.en.resx` and `resources/Strings.fr.resx`.
4. Open a PR. Community manifests are reviewed for safety; the reviewer cross-checks every operation the manifest invokes.

## Adding a new operation

Manifests can only compose **existing** operations. To add a fundamentally new operation kind (e.g. "set per-app GPU preference"):

1. Open an issue describing the need.
2. Add the operation to `WinEvo.Actions.Operations` as a `sealed class` inheriting `ActionOperation`. Declare each manifest field as a `required` init property (e.g. `public required string Key { get; init; }`).
3. Implement `public static <YourOp> FromJson(JsonElement properties)` — read the raw JSON once, construct the typed instance, throw `JsonException` on missing required fields.
4. Implement `public override Task<OperationResult> ExecuteAsync(OperationContext ctx, CancellationToken ct)` using the typed fields; render user-templated strings via the inherited `RenderProperty(value, ctx)` helper.
5. Add its id to the enum in `actions/schemas/action.schema.json`.
6. Register the operation in `OperationParser`'s factory dictionary: `["your-op"] = YourOperation.FromJson`.
7. Submit a PR. This is the trusted-code boundary; expect review on security, undo correctness, and dry-run support.
