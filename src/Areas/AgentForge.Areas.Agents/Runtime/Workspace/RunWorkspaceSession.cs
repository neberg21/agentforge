using AgentForge.Areas.Agents.Domain;
using AgentForge.Areas.Agents.Persistence;
using AgentForge.Areas.Agents.Runtime.Events;
using AgentForge.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentForge.Areas.Agents.Runtime.Workspace;

public sealed class RunWorkspaceSession : IRunWorkspaceSession
{
    private readonly IGitWorkspace _git;
    private readonly AgentsDbContext _db;
    private readonly IClock _clock;
    private readonly AgentsOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly IRunEventBus _events;
    private readonly ILogger<RunWorkspaceSession> _logger;
    private RunWorkspaceContext? _context;

    public RunWorkspaceSession(
        IGitWorkspace git,
        AgentsDbContext db,
        IClock clock,
        IOptions<AgentsOptions> options,
        IHostEnvironment environment,
        IRunEventBus events,
        ILogger<RunWorkspaceSession> logger)
    {
        _git = git;
        _db = db;
        _clock = clock;
        _options = options.Value;
        _environment = environment;
        _events = events;
        _logger = logger;
    }

    public async Task<bool> BeginAsync(Guid runId, CancellationToken ct)
    {
        if (!_options.Workspace.Enabled)
        {
            return true;
        }

        try
        {
            var localPath = ResolvePath(_options.Workspace.LocalPath);
            var worktreesRoot = ResolvePath(_options.Workspace.WorktreesRoot);
            var worktreePath = Path.Combine(worktreesRoot, runId.ToString("N"));
            var branchName = "run/" + runId.ToString("D");

            await _git.EnsureCloneAsync(_options.Workspace.RemoteUrl, localPath, ct);
            await _git.FetchAsync(localPath, ct);
            await _git.AddWorktreeAsync(
                localPath,
                worktreePath,
                branchName,
                _options.Workspace.BaseRef,
                ct);

            _context = new RunWorkspaceContext(runId, worktreePath, branchName);
            return true;
        }
        catch (Exception ex)
        {
            _context = null;
            await FailPendingAsync(runId, ex.Message);
            return false;
        }
    }

    public void Bind()
    {
        RunWorkspaceContext.Current = _context;
    }

    public void Unbind()
    {
        RunWorkspaceContext.Current = null;
    }

    public async Task FinishAsync(Guid runId, CancellationToken ct)
    {
        if (!_options.Workspace.Enabled)
        {
            _context = null;
            return;
        }

        var localPath = ResolvePath(_options.Workspace.LocalPath);
        var worktreesRoot = ResolvePath(_options.Workspace.WorktreesRoot);
        var worktreePath = Path.Combine(worktreesRoot, runId.ToString("N"));
        var branchName = "run/" + runId.ToString("D");

        try
        {
            var run = await LoadRunAsync(runId, ct);
            if (run is not null && run.Status == RunStatus.Running)
            {
                try
                {
                    await _git.PushBranchAsync(worktreePath, branchName, ct);
                    run.Complete(_clock.UtcNow);
                    await _db.SaveChangesAsync(ct);
                    Publish(run.Id, RunEventType.Status, $"{{\"status\":\"{run.Status}\"}}");
                    Publish(run.Id, RunEventType.Done, "{}");
                }
                catch (Exception ex)
                {
                    run = await LoadRunAsync(runId, CancellationToken.None);
                    if (run is not null && run.Status == RunStatus.Running)
                    {
                        run.Fail(ex.Message, _clock.UtcNow);
                        await _db.SaveChangesAsync(CancellationToken.None);
                        Publish(run.Id, RunEventType.Error, $"{{\"message\":\"{Escape(ex.Message)}\"}}");
                        Publish(run.Id, RunEventType.Done, "{}");
                    }
                }
            }
        }
        finally
        {
            try
            {
                await _git.RemoveWorktreeAsync(localPath, worktreePath, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove worktree for run {RunId}.", runId);
            }

            _context = null;
        }
    }

    private async Task FailPendingAsync(Guid runId, string message)
    {
        var run = await LoadRunAsync(runId, CancellationToken.None);
        if (run is null || run.Status != RunStatus.Pending)
        {
            return;
        }

        run.Fail(message, _clock.UtcNow);
        await _db.SaveChangesAsync(CancellationToken.None);
        Publish(run.Id, RunEventType.Error, $"{{\"message\":\"{Escape(message)}\"}}");
        Publish(run.Id, RunEventType.Done, "{}");
    }

    private async Task<Run?> LoadRunAsync(Guid runId, CancellationToken ct)
    {
        _db.ChangeTracker.Clear();
        return await _db.Runs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(candidate => candidate.Id == runId, ct);
    }

    private string ResolvePath(string configured)
    {
        if (Path.IsPathRooted(configured))
        {
            return Path.GetFullPath(configured);
        }

        var combined = Path.Combine(_environment.ContentRootPath, configured);
        return Path.GetFullPath(combined);
    }

    private void Publish(Guid runId, RunEventType type, string payload)
    {
        var ev = new RunEvent(runId, type, payload);
        _events.Publish(ev);
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
