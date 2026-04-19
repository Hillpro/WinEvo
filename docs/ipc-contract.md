# IPC contract

> **Current runtime status:** the Shell ↔ Agent link is implemented as **length-prefixed JSON messages over a named pipe** (see [`WinEvo.Ipc/PipeFraming.cs`](../src/WinEvo.Ipc/PipeFraming.cs) and [`PipeMessages.cs`](../src/WinEvo.Ipc/PipeMessages.cs)). The gRPC contract described below is the **target design** — the `.proto` already lives in [`WinEvo.Contracts/Protos/agent-service.proto`](../src/WinEvo.Contracts/Protos/agent-service.proto) and the `Grpc.Tools` package generates client + server stubs at build time, but neither end hosts/consumes the generated code yet. Everything else in this document (pipe naming, ACLs, client authentication, handshake/versioning semantics) applies to both the JSON and the future gRPC transport.

## Transport

Target: gRPC over a named pipe, using `Grpc.Net.Client.ConnectCallback` on the client side to open the pipe and `Grpc.AspNetCore` on the server side hosting inside the agent via `Microsoft.Extensions.Hosting`.

## Pipe naming & ACL

| Mode | Pipe name | Allowed principals |
|---|---|---|
| Service | `\\.\pipe\WinEvo.Agent.System` | `LocalSystem`, interactive user SIDs currently logged in |
| Broker  | `\\.\pipe\WinEvo.Agent.User.{sessionId}` | Interactive user SID of `sessionId` only |

ACLs are applied via `PipeSecurity` when the pipe is created. The agent creates a single server instance and accepts multiple concurrent clients (Shell, Tray, future CLI).

## Client authentication

On accepting a connection:

1. Resolve client PID via `GetNamedPipeClientProcessId`.
2. Open the client process and read its module path.
3. Verify the module's Authenticode signature against the agent's expected publisher.
4. Reject the connection if verification fails, or allow in DEBUG builds with a warning logged.

## Handshake

The first call on any new connection must be `Handshake`.

```proto
message HandshakeRequest {
  string client_kind      = 1;  // "shell" | "tray" | "cli"
  string client_version   = 2;  // semver
  int32  protocol_version = 3;  // currently 1
}

message HandshakeResponse {
  int32  agent_protocol_version   = 1;
  string agent_version            = 2;
  AgentMode mode                  = 3;  // SERVICE | BROKER
  repeated string supported_operations = 4;
  repeated string capability_flags     = 5;
}

enum AgentMode { SERVICE = 0; BROKER = 1; }
```

### Versioning policy

- **protocol_version** mismatch → agent rejects the connection with `FAILED_PRECONDITION`. The Shell offers to update the agent.
- **agent_version** older than an action's `minAgentVersion` → that specific action is marked unexecutable in the UI with an "update agent" CTA.
- **supported_operations** lets the Shell grey out manifests that depend on operations the installed agent doesn't know.

## Service shape

```proto
service AgentService {
  rpc Handshake(HandshakeRequest) returns (HandshakeResponse);

  rpc GetAgentStatus(Empty) returns (AgentStatus);

  rpc ListOperations(Empty) returns (OperationCatalog);
  rpc ValidateAction(ActionManifest) returns (ValidationReport);
  rpc DryRunAction(DryRunRequest) returns (DryRunReport);

  rpc ExecuteAction(ExecuteRequest) returns (stream ExecutionEvent);
  rpc CancelExecution(CancelRequest) returns (CancelResponse);
  rpc UndoExecution(UndoRequest) returns (stream ExecutionEvent);

  rpc ListRunningExecutions(Empty) returns (RunningExecutionList);
  rpc SubscribeEvents(Empty) returns (stream AgentEvent);

  rpc Shutdown(ShutdownRequest) returns (ShutdownResponse);   // broker mode only
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
| `INVALID_ARGUMENT` | Manifest failed validation. Includes `ValidationReport` as a detail. |
| `FAILED_PRECONDITION` | Protocol / version mismatch, or action preconditions not satisfied. |
| `PERMISSION_DENIED` | Client signature check failed. |
| `UNAVAILABLE` | Agent is shutting down. |
| `INTERNAL` | Unexpected agent failure. |

## Shutdown semantics

- **Service mode:** `Shutdown` is rejected (`FAILED_PRECONDITION`). The service is managed by SCM.
- **Broker mode:** `Shutdown` triggers graceful drain — refuse new executions, await running ones to complete (or cancel on `force: true`), then exit. The Shell issues `Shutdown` when closing if no running executions remain.
