# IPC contract

> **Current runtime status:** the Shell ↔ Agent link is implemented as **length-prefixed JSON messages over a named pipe** (see [`WinEvo.Ipc/PipeFraming.cs`](../src/WinEvo.Ipc/PipeFraming.cs) and [`PipeMessages.cs`](../src/WinEvo.Ipc/PipeMessages.cs)). The gRPC contract described below is the **target design** — the `.proto` lives in [`WinEvo.Contracts/Protos/agent-service.proto`](../src/WinEvo.Contracts/Protos/agent-service.proto) as a **non-building design document**: the `Grpc.Tools` codegen and the gRPC runtime packages have been **removed for now** (the bring-back recipe is in `WinEvo.Contracts.csproj`), so nothing generates or consumes stubs today. The `.proto` may therefore lag the live JSON `PipeMessages`. Everything else in this document (pipe naming, ACLs, client authentication, handshake/versioning semantics) is intended to apply to both the JSON and the future gRPC transport — but see the per-section status notes for what is actually wired.

## Transport

Target: gRPC over a named pipe, using `Grpc.Net.Client.ConnectCallback` on the client side to open the pipe and `Grpc.AspNetCore` on the server side hosting inside the agent via `Microsoft.Extensions.Hosting`.

## Pipe naming & ACL

| Mode | Pipe name | Allowed principals |
|---|---|---|
| Service | `\\.\pipe\WinEvo.Agent.System` | `LocalSystem`, interactive user SIDs currently logged in |
| Broker  | `\\.\pipe\WinEvo.Agent.User.{sessionId}` | Interactive user SID of `sessionId` only |

> **Current vs target.** The broker pipe carries the session-id suffix today (see [`PipeNames.cs`](../src/WinEvo.Ipc/PipeNames.cs)). The service pipe isn't created yet; service mode itself is unimplemented.

ACLs are applied via `PipeSecurity` when the pipe is created. **Today the agent accepts a single client at a time** (`maxNumberOfServerInstances: 1` in [`AgentHost.cs`](../src/WinEvo.Agent.Core/AgentHost.cs)); multi-client fan-out (Shell + Tray + future CLI on the same agent) is target design.

## Client authentication

On accepting a connection:

1. Resolve client PID via `GetNamedPipeClientProcessId`.
2. Open the client process and read its module path.
3. Verify the module's Authenticode signature against the agent's expected publisher.
4. Reject the connection if verification fails, or allow in DEBUG builds with a warning logged.

## Messages in the current JSON transport

Each message is one frame: a 4-byte big-endian length, then a UTF-8 JSON object whose `type` field selects the message. The client sends one request and reads one response before sending the next.

| Request (`type`) | Carries | Response (`type`) |
|---|---|---|
| `handshake` | `clientKind`, `clientVersion`, `protocolVersion` | `handshake-response`: `agentVersion`, `agentProtocolVersion` |
| `execute` | `actionId`, `parameters` (values keyed by parameter id) | `execution-response`: `success`, `message`, `error`, `stepResults`, `log` |
| `read-state` | `actionId` | `state-response`: `values` (live values keyed by parameter id; a parameter with no readable state is absent) |

Any request can instead be answered by `error` (`message`). An `execute` for an id the agent does not have, or for an action that requires elevation while the agent is not elevated, is answered by an `execution-response` with `success: false`.

**The agent runs only the actions in its own catalog.** A request names an action; it cannot supply steps, scripts or a definition of one. The elevation requirement is read from the agent's catalog, never from the request.

The protocol version is 2. Version 1 shipped the action's JSON manifest inside `execute`.

## Handshake

The first call on any new connection must be `Handshake`.

```proto
message HandshakeRequest {
  string client_kind      = 1;  // "shell" | "tray" | "cli"
  string client_version   = 2;  // semver
  int32  protocol_version = 3;  // currently 2
}

message HandshakeResponse {
  int32  agent_protocol_version    = 1;
  string agent_version             = 2;
  repeated string action_ids       = 3;  // target; not sent by the JSON transport
  repeated string capability_flags = 4;  // target; not sent by the JSON transport
}
```

### Versioning policy

> **Status: target, not enforced yet.** The handshake already carries `protocol_version` and `agent_version`, but none of the skew checks below are implemented: today the agent answers any handshake and never rejects on version (`AgentHost.HandleAsync`), and the Shell does not compare versions. This is harmless while the Shell and Agent ship in the same package and always update together; it becomes load-bearing the moment they can drift (a portable zip re-extracted over a stale agent, or a future auto-update that touches one and not the other). Wire these before independent updates are possible.

- **protocol_version** mismatch → agent rejects the connection with `FAILED_PRECONDITION`. The Shell offers to update the agent.
- **action_ids** lets the Shell disable an action the running agent does not contain, instead of failing when the user runs it.

## Service shape

Of the RPCs below, `Handshake`, `ExecuteAction` (request / response, no streaming) and `ReadState` are implemented in the current JSON transport. The rest are target design for the gRPC swap.

```proto
service AgentService {
  rpc Handshake(HandshakeRequest) returns (HandshakeResponse);                   // ✅ wired (JSON)

  rpc GetAgentStatus(Empty) returns (AgentStatus);                                // not implemented yet

  rpc ReadState(ReadStateRequest) returns (StateResponse);                        // ✅ wired (JSON)
  rpc DryRunAction(DryRunRequest) returns (DryRunReport);                         // not implemented yet

  rpc ExecuteAction(ExecuteRequest) returns (stream ExecutionEvent);              // ✅ wired (JSON, no streaming)
  rpc CancelExecution(CancelRequest) returns (CancelResponse);                    // not implemented yet
  rpc UndoExecution(UndoRequest) returns (stream ExecutionEvent);                 // not implemented yet

  rpc ListRunningExecutions(Empty) returns (RunningExecutionList);                // not implemented yet
  rpc SubscribeEvents(Empty) returns (stream AgentEvent);                         // not implemented yet

  rpc Shutdown(ShutdownRequest) returns (ShutdownResponse);   // broker mode only; not implemented yet
}
```

## Execution stream

`ExecuteAction` returns a server-streaming sequence of events. The Shell keeps the stream open while the action runs; cancellation is signalled by a separate `CancelExecution` RPC.

```proto
message ExecutionEvent {
  string execution_id = 1;
  oneof event {
    StepStarted       step_started       = 2;
    ProgressUpdate    progress_update    = 3;
    LogLine           log_line           = 4;
    StepCompleted     step_completed     = 5;
    ExecutionFinished execution_finished = 6;
  }
}

message ProgressUpdate {
  string step_id  = 1;
  double ratio    = 2;   // 0.0..1.0, or NaN for indeterminate
  string message  = 3;
}

message ExecutionFinished {
  enum Outcome { SUCCESS = 0; FAILED = 1; CANCELLED = 2; ROLLED_BACK = 3; }
  Outcome outcome = 1;
  string  message = 2;
}
```

## Multi-client fan-out

The Tray subscribes via `SubscribeEvents` to get a read-only feed of what's running, so it can render status in the tray menu while the Shell is closed. When the Shell reopens, it connects independently, fetches `ListRunningExecutions`, and may attach new `SubscribeEvents` streams.

## Errors

Standard gRPC status codes:

| Status | Meaning |
|---|---|
| `INVALID_ARGUMENT` | Unknown action id, or a required parameter is missing. |
| `FAILED_PRECONDITION` | Protocol / version mismatch, or action preconditions not satisfied. |
| `PERMISSION_DENIED` | Client signature check failed. |
| `UNAVAILABLE` | Agent is shutting down. |
| `INTERNAL` | Unexpected agent failure. |

## Shutdown semantics

- **Service mode:** `Shutdown` is rejected (`FAILED_PRECONDITION`). The service is managed by SCM.
- **Broker mode:** `Shutdown` triggers graceful drain — refuse new executions, await running ones to complete (or cancel on `force: true`), then exit. The Shell issues `Shutdown` when closing if no running executions remain.
