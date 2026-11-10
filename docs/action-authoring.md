# Authoring actions

> **Implementation status.** Actions are C# classes compiled into WinEvo. Everything described here is wired unless a line is marked *(not implemented yet)*. The last section lists what does not exist yet: undo, dry-run, restore points, progress reporting, and actions supplied by third parties.

An **action** is one thing the user can do from the WinEvo window: flip a setting, clean something up, run a maintenance task. It is a class with two halves:

- a **descriptor**: plain data the Shell uses to list the action and draw its inputs (name, description, parameters, warnings, elevation need);
- **behaviour**: code that runs only inside the agent, built from **operations**, the small trusted primitives that actually touch the system.

The Shell never runs action code. It reads the descriptor, collects the inputs, and asks the agent to run the action by id.

## Where things live

| What | Where |
|---|---|
| Actions that ship with WinEvo | [src/WinEvo.Actions/](../src/WinEvo.Actions/), one folder per category |
| Their English and French text | [Strings.resx](../src/WinEvo.Actions/Strings.resx), [Strings.fr.resx](../src/WinEvo.Actions/Strings.fr.resx) |
| The list of actions | [CoreActionCatalog.cs](../src/WinEvo.Actions/CoreActionCatalog.cs) |
| The contract (`ActionDefinition`, `ActionDescriptor`, `Parameter`, ...) | [src/WinEvo.Actions.Abstractions/](../src/WinEvo.Actions.Abstractions/) |
| Operations | [src/WinEvo.Actions.Operations/](../src/WinEvo.Actions.Operations/) |

## A one-shot action

A one-shot action shows its parameters and an **Execute** button. Derive from `ActionDefinition`:

```csharp
public sealed class WipeFreeSpaceAction : ActionDefinition
{
    private const string DriveParameterId = "drive";

    public override ActionDescriptor Descriptor { get; } = new()
    {
        Id = "storage.wipe-free-space",
        Category = "storage",
        Name = ActionText.For("WipeFreeSpace_Name"),
        Description = ActionText.For("WipeFreeSpace_Description"),
        Author = "Hillpro",
        Elevation = ElevationRequirement.Required,
        Warnings =
        [
            new ActionWarning
            {
                Severity = WarningSeverity.Warning,
                Message = ActionText.For("WipeFreeSpace_LongRunningWarning"),
            },
        ],
        Parameters =
        [
            new DriveParameter
            {
                Id = DriveParameterId,
                Name = ActionText.For("WipeFreeSpace_DriveLabel"),
                Required = true,
                AllowedDriveTypes = ["fixed", "removable"],
            },
        ],
    };

    public override Task ExecuteAsync(ActionExecution execution, CancellationToken cancellationToken)
    {
        var drive = execution.GetString(DriveParameterId);
        var wipe = new BuiltinToolOperation { Name = "cipher", Args = [$"/w:{drive}"] };
        return execution.RunAsync(wipe, cancellationToken);
    }
}
```

The full version is [WipeFreeSpaceAction.cs](../src/WinEvo.Actions/Storage/WipeFreeSpaceAction.cs).

## A toggle action

A toggle action is a single on/off switch over a piece of system state. It has no Execute button: flipping the switch runs it. Derive from `ToggleActionDefinition`, which adds the switch to the descriptor for you:

```csharp
public sealed class BingSearchResultsAction : ToggleActionDefinition
{
    private const string SearchKey = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Search";

    public BingSearchResultsAction()
        : base(
            new ActionDescriptor
            {
                Id = "customization.bing-search-results",
                Category = "customization",
                Name = ActionText.For("BingSearchResults_Name"),
                Description = ActionText.For("BingSearchResults_Description"),
            },
            switchLabel: ActionText.For("BingSearchResults_SwitchLabel"),
            isOnWhenUnknown: true)
    {
    }

    protected override Task<bool?> ReadIsOnAsync(CancellationToken cancellationToken)
    {
        var enabled = RegistryValueReader.Read(SearchKey, "BingSearchEnabled");
        return Task.FromResult<bool?>(enabled is null ? null : enabled is 1);
    }

    protected override async Task ApplyAsync(bool isOn, ActionExecution execution, CancellationToken cancellationToken)
    {
        var enabled = isOn ? 1 : 0;
        await execution.RunAsync(RegistrySetOperation.Dword(SearchKey, "BingSearchEnabled", enabled), cancellationToken);
        await execution.TryRunAsync(new ProcessKillOperation { Name = "SearchUI" }, cancellationToken);
    }
}
```

- `ReadIsOnAsync` reports where the switch stands **on this machine right now**. Return `null` when that cannot be determined; the switch then shows `isOnWhenUnknown`. Never return a guess: the switch must reflect the system, not the action's default.
- `ApplyAsync` moves the system to the requested position.
- A toggle action declares no parameters of its own. A descriptor that does is rejected when the action is constructed.

The full version is [BingSearchResultsAction.cs](../src/WinEvo.Actions/Customization/BingSearchResultsAction.cs).

## The descriptor

| Field | Meaning |
|---|---|
| `Id` | `category.kebab-case-name`. Globally unique and stable: it is what the Shell sends to the agent, and later the key for history and undo. Never change it once released. |
| `Category` | Lowercase word the Shell groups by. The id must start with it. |
| `Name`, `Description` | Localized text shown in the list and the detail pane. |
| `Elevation` | `Required` makes the Shell obtain an elevated agent through UAC before running. The agent refuses to run such an action when it is not elevated, whatever the caller says. `Optional` is declared but not acted on yet. |
| `Warnings` | See below. |
| `Parameters` | See below. |
| `Tags`, `Icon`, `Author` | Carried on the descriptor. *(not implemented yet)* The Shell does not display them. `Author` is the contributor's GitHub handle. |

## Text and localization

Every piece of user-facing text is a `LocalizableText`, resolved in the user's Windows display language when it is displayed.

1. Add a key and its English text to `Strings.resx`.
2. Add the same key with the French text to `Strings.fr.resx`.
3. Reference it from the action with `ActionText.For("YourKey")`.

A language without a translation falls back to English. A key that does not exist is shown as the key itself, so the mistake is visible instead of fatal. `ActionTextTests` fails the build if any text of any action does not resolve in English and in French, or if the two files do not declare the same keys.

Write for people who are not technical. Say what will happen to their computer in plain words; do not name tools, registry keys or file formats.

## Warnings

A warning is something the user must be told, and confirm, before the action runs. Each has a severity and a message. An action with no warnings runs without a confirmation dialog.

| Severity | Confirmation |
|---|---|
| `Info` | Listed in the dialog; **Continue** is immediately enabled. |
| `Warning` | Yellow banner; **Continue** is immediately enabled. |
| `Danger` | Red banner; an "I understand" checkbox gates the button. |
| `Critical` | Red banner; the checkbox **and** typing the action's name exactly. |

Warnings are shown in the order the action declares them. The dialog as a whole takes the strongest treatment among them.

## Parameters

Each parameter becomes an input in the detail pane. Pick the class that matches the input you need:

| Class | Input | Extra fields | Read it with |
|---|---|---|---|
| `StringParameter` | Text box | `Default` | `execution.GetString(id)` |
| `IntegerParameter` | Number box | `Default`, `Min`, `Max` | `execution.GetInteger(id)` |
| `BooleanParameter` | Switch | `Default` | `execution.GetBoolean(id)` |
| `EnumParameter` | Drop-down | `Choices`, `Default` | `execution.GetString(id)` |
| `DriveParameter` | Drop-down of the machine's drives | `AllowedDriveTypes` | `execution.GetString(id)` (the drive root, e.g. `C:\`) |

A `Required` parameter with no value and no default stops the action before it starts. A parameter that is not required and has neither is simply absent: asking `execution` for it throws, so only read what you know is there.

*(not implemented yet)* Pickers for a Wi-Fi profile, a file, a folder or a service. Adding one means a new `Parameter` subclass, a view model, a XAML template, an entry in `ParameterInputTemplateSelector` and one line in `ParameterInputFactory`.

### Showing the current state

When an input mirrors something on the system, override `ReadStateAsync` and return the live value keyed by parameter id. The Shell calls it when the action is selected and displays that value in place of the default. A parameter you leave out keeps its default. `ToggleActionDefinition` does this for its switch through `ReadIsOnAsync`.

## Running operations

Inside `ExecuteAsync` (or `ApplyAsync`), every operation goes through the `ActionExecution` you are given. That is what records it as a step in the result the user sees.

| Call | Use it when |
|---|---|
| `await execution.RunAsync(operation, ct)` | The action cannot continue if this fails. A failure ends the action there. |
| `await execution.TryRunAsync(operation, ct)` | A failure is acceptable. It returns `false`, and the action carries on. The execution is still reported as failed overall. |
| `execution.Log(line)` | You want a line in the result panel that is not tied to an operation. |

Between operations you write ordinary C#: conditions, loops, values computed from parameters.

## Operations

Operations are the closed set of primitives the agent can perform. Actions compose them; they do not touch the system any other way.

| Operation | What it does | How to build it |
|---|---|---|
| `registry-set` | Writes a registry value, creating the key if needed | `RegistrySetOperation.Dword(key, name, 1)`, also `Qword`, `Sz`, `ExpandSz`, `MultiSz` |
| `registry-delete` | Deletes a value, or a whole key when `ValueName` is omitted. Idempotent. | `new RegistryDeleteOperation { Key = ..., ValueName = ... }` |
| `process-kill` | Kills a process by name or PID. Nothing to kill is a success. | `new ProcessKillOperation { Name = "SearchUI" }` |
| `builtin-tool` | Runs a tool from `System32` by bare name | `new BuiltinToolOperation { Name = "cipher", Args = [...] }` |
| `external-process` | Runs any executable; `%VARIABLE%` in the path is expanded | `new ExternalProcessOperation { Path = ..., Args = [...] }` |
| `powershell` | Runs a Windows PowerShell 5.1 script | `new PowerShellOperation { Script = ... }` |
| `command` | Runs a line through `cmd.exe /C` | `new CommandOperation { Command = ... }` |
| `delay` | Waits | `new DelayOperation { Duration = TimeSpan.FromSeconds(2) }` |

The registry hive is the first segment of the key path: `HKCU\Software\...`, `HKLM\...`. To read a registry value, use `RegistryValueReader.Read(key, name)`; it returns the data, or `null` when the key or the value is absent.

The process operations take an optional `TimeoutSeconds`. Leave it unset for work that legitimately takes long.

### Choosing how to run a program

- **`builtin-tool`** for anything under `System32` (`cipher`, `sc`, `ipconfig`, DISM, SFC). Paths are rejected; the tool is always resolved against the system directory.
- **`external-process`** for any other executable.
- **`powershell`** or **`command`** only when you need the shell itself: cmdlets, pipes, redirections, `&&`.

**Never build a `powershell` script or a `command` line by inserting a value the user typed.** That value would run as code, with the agent's privileges. Pass user input as an entry of `Args` on `builtin-tool` or `external-process`: each entry reaches the program as exactly one argument and is never re-parsed.

## Registering and testing an action

1. Add the class under the matching category folder in `src/WinEvo.Actions/`.
2. Add its text to both `.resx` files.
3. Add one line to the list in `CoreActionCatalog`.
4. Run `dotnet test`. `CoreActionCatalogTests` checks that ids are unique, well-formed and prefixed by their category; `ActionTextTests` checks the text.
5. Pin what matters about your descriptor in `CoreActionsTests`: elevation, warnings, parameters.

What the action does when executed is not covered by automated tests, because it changes the machine. Try it by hand.

## Adding a new operation

Operations are the trusted-code boundary: this is where review is strictest.

1. Open an issue describing the need.
2. Add a `sealed class` deriving from `ActionOperation` in `WinEvo.Actions.Operations`. Give it an `Id` (the name shown in step results) and typed inputs: `required` init properties, or factories when the inputs must agree with each other, as in `RegistrySetOperation`.
3. Implement `ExecuteAsync`. Return `OperationResult.Ok(...)` or `OperationResult.Fail(...)`; catch what the operation can meaningfully explain, such as access denied.
4. Add tests that exercise it against a sandbox: a throwaway key under `HKCU\Software\WinEvo.Tests.<guid>`, a temporary folder.

## Not implemented yet

- **Undo.** Every executed action is permanent. The intended design captures state per operation as it runs through `ActionExecution`.
- **Dry-run** preview, **restore points**, and an **execution history**.
- **Progress reporting.** A long operation such as a free-space wipe reports nothing until it ends.
- **Planned operations:** `service-stop`, `service-start`, `service-restart`, `file-delete`, `file-copy`, `file-move`, `sysinternals-tool`, `system-restore-point`.
- **Operations that return a value.** A read made during execution goes through a helper such as `RegistryValueReader` and is not recorded as a step.
- **Actions from third parties.** Only the actions compiled into WinEvo exist. Two routes are being considered and neither is decided: a simple data format for community actions, or loading a locally built assembly in an explicit developer mode.
