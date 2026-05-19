# Privacy

WinEvo is a local-first Windows utility. This document describes what it does and does not do with your data.

## What WinEvo does not do

- **No telemetry.** WinEvo does not send usage analytics, error reports, or any other data to Hillpro or any third party. There is no opt-in, opt-out, or "anonymous statistics" toggle — there is simply no mechanism in the code that initiates a network connection.
- **No accounts.** WinEvo has no sign-in, no cloud sync, no user identifier of any kind.
- **No advertising.** WinEvo does not display ads and does not embed any analytics or advertising SDK.

## What WinEvo does locally

- **Action manifests.** WinEvo reads JSON action manifests bundled with the app. It also discovers community-contributed manifests under `%LOCALAPPDATA%\WinEvo\Actions\`. Manifests are read from disk and never uploaded.
- **Action execution.** When you run an action, the agent executes the operations declared in its manifest — for example, setting a registry value, killing a process, or running a built-in Windows tool such as `cipher.exe`. The actions shipped with WinEvo (`bing-search-results` and `wipe-free-space`) do not perform any network activity. A community manifest can in principle invoke a process that does — review a manifest before running it.
- **Local diagnostic logs.** WinEvo writes diagnostic logs to two locations on your machine. These files stay on disk and are only useful if you choose to attach them to a bug report:
  - Agent log: `%TEMP%\winevo-agent.log`
  - Shell log: `%LOCALAPPDATA%\WinEvo\shell.log`
- **Local IPC.** The Shell and the Agent communicate over a local Windows named pipe restricted to the current user. No network sockets are opened.

## Inter-process communication

WinEvo runs as two processes — an unelevated Shell (the UI) and an Agent that can be promoted to administrator via UAC for actions that require it. They exchange messages over a named pipe. The pipe ACL grants access only to the current user's SID. See [docs/security-model.md](security-model.md) and [docs/ipc-contract.md](ipc-contract.md) for the technical details.

## Microsoft Store distribution

If you install WinEvo from the Microsoft Store, the Store itself collects standard installation metrics on Microsoft's behalf. That data is governed by [Microsoft's privacy statement](https://privacy.microsoft.com/privacystatement), not this document. WinEvo does not receive that data.

## Source code

The source is available at <https://github.com/Hillpro/WinEvo> under the GPL-3.0-or-later license. You can audit any of the claims above by reading the code.

## Contact

For privacy-related questions, open an issue on GitHub.
