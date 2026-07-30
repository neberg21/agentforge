# AgentForge — Workspace Tools (Teilprojekt 4) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace stub `read_file` / `write_file` / `run_shell` with host tools on a per-run git worktree; push branch `run/{runId}` only after successful work, then mark `Completed`.

**Architecture:** Add `Runtime/Workspace/*` (`IGitWorkspace`, `IRunWorkspaceSession`, path jail, options). Register real tools when `Workspace:Enabled`. `RunWorker` begins/finishes the session around `RunLoop`. With workspace on, the loop leaves successful runs in `Running`; `FinishAsync` pushes then `Complete`s (or `Fail`s on push error). Domain allows `Pending→Failed` for begin failures.

**Tech Stack:** .NET 10, existing Agents runtime, `git` CLI via `System.Diagnostics.Process`, xUnit v3, hand-written fakes (no mocking libraries).

**Spec:** `docs/superpowers/specs/2026-07-30-agentforge-workspace-tools-design.md`

## Global Constraints

- Repo root: `C:\Users\NEWA002\source\repos\agentforge`. Commands run from there.
- Follow skeleton/runtime constraints: `net10.0`, CPM, xUnit v3, no assertion/mocking libs, `Guid.CreateVersion7()`, `IClock`, English commits (`feat:` / `test:` / `chore:` / `docs:`).
- **No C# primary constructors.** Traditional constructors with fields.
- **Do not inline object creation into method/ctor calls** — local variable, then pass.
- **Windows:** no `.ps1` / `.sh`. Use `cmd /c` or direct `dotnet`/`git`. Commit messages via `git commit -F` file.
- Do not add new top-level directories beyond `src`, `tests`, `docs`. Worktrees live under configured `WorktreesRoot` (gitignored `workspaces/` is fine).
- After each task: commit only that task's files.

## File Structure

**Modify**
- `Domain/RunTransitions.cs` — allow `Pending → Failed`
- `Runtime/AgentsOptions.cs` — nested `Workspace`
- `Runtime/Tools/ITool.cs` (ToolRegistry) — register real tools; `EnsureStubs` skips existing names (already does)
- `Runtime/RunLoop.cs` — defer `Complete` when workspace enabled
- `Runtime/Queue/RunWorker.cs` — Begin/Finish session around loop
- `AgentsArea.cs` — DI + validation
- `src/AgentForge.Host/appsettings.json`, `appsettings.Development.json`
- `README.md`

**Create**
- `Runtime/Workspace/WorkspaceOptions.cs`
- `Runtime/Workspace/WorkspacePath.cs` — path jail
- `Runtime/Workspace/IGitWorkspace.cs`, `GitCliWorkspace.cs`
- `Runtime/Workspace/IRunWorkspaceSession.cs`, `RunWorkspaceSession.cs`, `RunWorkspaceContext.cs`
- `Runtime/Tools/ReadFileTool.cs`, `WriteFileTool.cs`, `RunShellTool.cs`

**Tests**
- `tests/AgentForge.Areas.Agents.Unit/WorkspacePathTests.cs`
- `tests/AgentForge.Areas.Agents.Unit/WorkspaceToolTests.cs`
- `tests/AgentForge.Areas.Agents.Unit/RunWorkspaceSessionTests.cs`
- `tests/AgentForge.Areas.Agents.Unit/RunTransitionsTests.cs` (extend)
- `tests/AgentForge.Host.Integration/WorkspaceRunTests.cs` (optional fake wiring)

---

### Task 1: Pending→Failed and Workspace options

**Files:**
- Modify: `src/Areas/AgentForge.Areas.Agents/Domain/RunTransitions.cs`
- Modify: `src/Areas/AgentForge.Areas.Agents/Runtime/AgentsOptions.cs`
- Create: `src/Areas/AgentForge.Areas.Agents/Runtime/Workspace/WorkspaceOptions.cs`
- Modify: `tests/AgentForge.Areas.Agents.Unit/RunTransitionsTests.cs`
- Test: `tests/AgentForge.Areas.Agents.Unit/WorkspaceOptionsTests.cs` (optional bind smoke — skip if redundant)

**Interfaces:**
- Consumes: existing `RunStatus`, `AgentsOptions`
- Produces: `Pending→Failed` allowed; `AgentsOptions.Workspace` of type `WorkspaceOptions` with `Enabled`, `RemoteUrl`, `LocalPath`, `BaseRef` (default `main`), `WorktreesRoot`, `ShellTimeout` (default 5 min), `MaxOutputChars` (default 65536)

- [ ] **Step 1: Extend transition tests**

In `RunTransitionsTests.cs` add:

```csharp
[InlineData(RunStatus.Pending, RunStatus.Failed)]
```

to the allowed matrix (and ensure unsupported list does not include that pair).

- [ ] **Step 2: Run — expect FAIL** (Pending→Failed still false)

Run: `dotnet test tests/AgentForge.Areas.Agents.Unit --filter RunTransitions`

- [ ] **Step 3: Update `RunTransitions`**

```csharp
[RunStatus.Pending] = [RunStatus.Running, RunStatus.Cancelled, RunStatus.Failed],
```

- [ ] **Step 4: Add `WorkspaceOptions` and nest on `AgentsOptions`**

```csharp
namespace AgentForge.Areas.Agents.Runtime.Workspace;

public sealed class WorkspaceOptions
{
    public bool Enabled { get; set; }

    public string RemoteUrl { get; set; } = string.Empty;

    public string LocalPath { get; set; } = string.Empty;

    public string BaseRef { get; set; } = "main";

    public string WorktreesRoot { get; set; } = string.Empty;

    public TimeSpan ShellTimeout { get; set; } = TimeSpan.FromMinutes(5);

    public int MaxOutputChars { get; set; } = 65_536;
}
```

On `AgentsOptions`:

```csharp
public WorkspaceOptions Workspace { get; set; } = new();
```

Add `using AgentForge.Areas.Agents.Runtime.Workspace;`

- [ ] **Step 5: Tests PASS, commit**

```
feat: allow pending-to-failed and add workspace options
```

---

### Task 2: Path jail

**Files:**
- Create: `src/Areas/AgentForge.Areas.Agents/Runtime/Workspace/WorkspacePath.cs`
- Test: `tests/AgentForge.Areas.Agents.Unit/WorkspacePathTests.cs`

**Interfaces:**
- Produces: `static class WorkspacePath` with  
  `static bool TryResolve(string root, string relativeOrNested, out string fullPath, out string error)`  
  Canonical full path must start with canonical root (+ separator). Reject `..` escapes and rooted paths outside root.

- [ ] **Step 1: Failing tests**

```csharp
public class WorkspacePathTests
{
    [Fact]
    public void TryResolve_akzeptiert_relative_Pfade_unter_Root()
    {
        var root = Path.Combine(Path.GetTempPath(), "ws-jail-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Assert.True(WorkspacePath.TryResolve(root, "src/a.txt", out var full, out var error));
            Assert.Null(error);
            Assert.StartsWith(Path.GetFullPath(root), full, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TryResolve_lehnt_Parent_Escape_ab()
    {
        var root = Path.Combine(Path.GetTempPath(), "ws-jail-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Assert.False(WorkspacePath.TryResolve(root, "../secret.txt", out _, out var error));
            Assert.False(string.IsNullOrEmpty(error));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
```

- [ ] **Step 2: RED → implement → GREEN → commit**

```
feat: path jail helper for workspace tools
```

---

### Task 3: IGitWorkspace + recording fake

**Files:**
- Create: `src/Areas/AgentForge.Areas.Agents/Runtime/Workspace/IGitWorkspace.cs`
- Create: `src/Areas/AgentForge.Areas.Agents/Runtime/Workspace/GitCliWorkspace.cs`
- Test: `tests/AgentForge.Areas.Agents.Unit/RecordingGitWorkspace.cs` (test double)
- Test: `tests/AgentForge.Areas.Agents.Unit/GitCliWorkspaceTests.cs` (optional; skip if no git — prefer fake-only unit tests here)

**Interfaces:**
- Produces:

```csharp
public interface IGitWorkspace
{
    Task EnsureCloneAsync(string remoteUrl, string localPath, CancellationToken ct);
    Task FetchAsync(string localPath, CancellationToken ct);
    Task AddWorktreeAsync(string localPath, string worktreePath, string branchName, string baseRef, CancellationToken ct);
    Task RemoveWorktreeAsync(string localPath, string worktreePath, CancellationToken ct);
    Task PushBranchAsync(string worktreePath, string branchName, CancellationToken ct);
}
```

`GitCliWorkspace` runs `git` with `ProcessStartInfo` (`UseShellExecute = false`, redirect stdout/stderr). Non-zero exit → throw `InvalidOperationException` including stderr.

`RecordingGitWorkspace` (test project): records calls; `PushBranchAsync` can be configured to throw.

- [ ] **Step 1: Write interface + `RecordingGitWorkspace` used by a tiny test that records `AddWorktreeAsync`**
- [ ] **Step 2: Implement `GitCliWorkspace`**
- [ ] **Step 3: Commit**

```
feat: git workspace port and cli implementation
```

---

### Task 4: Run workspace session + context

**Files:**
- Create: `Runtime/Workspace/RunWorkspaceContext.cs`
- Create: `Runtime/Workspace/IRunWorkspaceSession.cs`
- Create: `Runtime/Workspace/RunWorkspaceSession.cs`
- Test: `tests/AgentForge.Areas.Agents.Unit/RunWorkspaceSessionTests.cs`

**Interfaces:**
- Consumes: `IGitWorkspace`, `IOptions<AgentsOptions>`, `IClock`, `AgentsDbContext` (for Fail/Complete on finish — or keep DB updates in worker; prefer session owns finish transitions via injected db + clock)
- Produces:

```csharp
public sealed class RunWorkspaceContext
{
    public static RunWorkspaceContext? Current { get; /* AsyncLocal */ }
    public Guid RunId { get; }
    public string Root { get; }
    public string BranchName { get; }
}

public interface IRunWorkspaceSession
{
    Task BeginAsync(Guid runId, CancellationToken ct);
    Task FinishAsync(Guid runId, CancellationToken ct);
}
```

**BeginAsync** (when `Workspace.Enabled`):
1. Resolve absolute `LocalPath` / `WorktreesRoot` (relative → `AppContext.BaseDirectory` or content root passed in ctor as `string contentRoot`)
2. `EnsureCloneAsync` + `FetchAsync`
3. `branch = "run/" + runId.ToString("D")` (or `N` — pick `D` for readability)
4. `worktreePath = Path.Combine(WorktreesRoot, runId.ToString("N"))`
5. `AddWorktreeAsync(...)`; set `RunWorkspaceContext.Current`

On exception: load run with `IgnoreQueryFilters`, if `Pending` call `Fail(message, clock.UtcNow)`, save; clear context; rethrow or return (worker should not start loop). Prefer: session catches, marks Failed, returns `false` via `Task<bool>` **or** throws after Fail. Use `Task<bool> BeginAsync` → `true` means proceed.

**FinishAsync**:
1. Read run status from DB
2. If `Running` and workspace enabled: `PushBranchAsync`; on success `Complete`; on failure `Fail(pushError)`
3. If not Running: no push
4. Always try `RemoveWorktreeAsync`; clear `Current`

When `Workspace.Enabled` is false: Begin/Finish are no-ops (Begin returns true).

- [ ] **Step 1: TDD with RecordingGitWorkspace** — Begin calls ensure/fetch/add; Finish after simulated Running success calls push once then remove; Finish when Failed does not push
- [ ] **Step 2: Implement session**
- [ ] **Step 3: Commit**

```
feat: run workspace session with push-before-complete
```

---

### Task 5: Real tools

**Files:**
- Create: `Runtime/Tools/ReadFileTool.cs`, `WriteFileTool.cs`, `RunShellTool.cs`
- Test: `tests/AgentForge.Areas.Agents.Unit/WorkspaceToolTests.cs`

**Interfaces:**
- Consumes: `RunWorkspaceContext.Current`, `IOptions<AgentsOptions>` (timeout / max chars) for shell
- Produces: `ITool` with `Name` => `read_file` | `write_file` | `run_shell`

Argument JSON as in spec. Missing context → `{"ok":false,"error":"no_workspace"}`.

`RunShellTool`: `cmd.exe /c` on Windows with `WorkingDirectory = context.Root`, kill on timeout, truncate streams to `MaxOutputChars`.

- [ ] **Step 1: Failing tests** using a temp directory + manually set context
- [ ] **Step 2: Implement tools → GREEN**
- [ ] **Step 3: Commit**

```
feat: host read write and shell tools for workspace
```

---

### Task 6: Wire DI, RunLoop defer-complete, RunWorker

**Files:**
- Modify: `AgentsArea.cs`
- Modify: `RunLoop.cs` — when `_options.Workspace.Enabled` and natural success, do **not** call `Complete`; still publish status/done? Spec: leave Running. Publish a `status` event with Running is fine; publish `done` only after Finish Complete — so **suppress Done on deferred success**; Finish publishes Status+Done after Complete/Fail.
- Modify: `RunWorker.cs`
- Modify: Host `appsettings*.json` — `"Workspace": { "Enabled": false, ... }`
- Modify: `ToolRegistry` registration: after creating singleton, register three tools (factory that resolves options)

**RunWorker.ProcessAsync sketch:**

```csharp
await using var scope = _scopeFactory.CreateAsyncScope();
var session = scope.ServiceProvider.GetRequiredService<IRunWorkspaceSession>();
var started = await session.BeginAsync(runId, stoppingToken);
if (!started)
{
    return;
}

try
{
    var loop = scope.ServiceProvider.GetRequiredService<RunLoop>();
    await loop.ExecuteAsync(runId, stoppingToken);
}
finally
{
    await session.FinishAsync(runId, CancellationToken.None);
}
```

**AgentsArea validation** when `Workspace.Enabled`: require non-empty `RemoteUrl`, `LocalPath`, `WorktreesRoot`.

**Register:** `IGitWorkspace` → `GitCliWorkspace` singleton; `IRunWorkspaceSession` scoped; tools registered into `IToolRegistry` at startup:

```csharp
services.AddSingleton<IToolRegistry>(provider =>
{
    var registry = new ToolRegistry();
    var options = provider.GetRequiredService<IOptions<AgentsOptions>>();
    if (options.Value.Workspace.Enabled)
    {
        registry.Register(new ReadFileTool());
        registry.Register(new WriteFileTool(options));
        registry.Register(new RunShellTool(options));
    }

    return registry;
});
```

(Adjust ctors so tools only need options where required.)

- [ ] **Step 1: Unit test RunLoop deferred complete** — with Workspace.Enabled true and ScriptedLlmClient single assistant reply, after ExecuteAsync status is still Running
- [ ] **Step 2: Implement wiring**
- [ ] **Step 3: Full `dotnet test` PASS**
- [ ] **Step 4: Commit**

```
feat: wire workspace session tools and deferred complete
```

---

### Task 7: Integration coverage + README

**Files:**
- Create/Modify: `tests/AgentForge.Host.Integration/WorkspaceRunTests.cs` and factory overrides to inject `RecordingGitWorkspace` + enable workspace with temp paths
- Modify: `README.md` — Workspace section
- Commit: `docs: document workspace tools and add integration coverage`

**Integration expectations:**
- With workspace enabled + recording git + scripted LLM: create run with AllowedTools including `read_file`; wait until Completed; assert `PushBranchAsync` called once; assert remove called.

- [ ] **Step 1: Write integration test**
- [ ] **Step 2: Update README** (config table, push-before-complete, Docker still future)
- [ ] **Step 3: `dotnet test` PASS, commit**

---

## Spec coverage checklist

| Spec item | Task |
|---|---|
| Workspace options + ValidateOnStart | 1, 6 |
| Path jail | 2 |
| Git clone/worktree/push CLI | 3 |
| Session Begin/Finish, AsyncLocal | 4 |
| Three real tools | 5 |
| Deferred Complete / push then Complete | 4, 6 |
| Pending→Failed on begin | 1, 4 |
| No Completed→Failed | 4, 6 |
| DI / Worker | 6 |
| Tests + README | 2–7 |

## Self-review notes

- No `Completed→Failed` anywhere.
- Begin failure uses new `Pending→Failed`.
- When `Enabled: false`, behavior matches Teilprojekt 3 (stubs via EnsureStubs, Complete in loop).
