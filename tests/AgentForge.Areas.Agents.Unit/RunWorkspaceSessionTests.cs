using AgentForge.Areas.Agents.Domain;
using AgentForge.Areas.Agents.Runtime;
using AgentForge.Areas.Agents.Runtime.Events;
using AgentForge.Areas.Agents.Runtime.Workspace;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AgentForge.Areas.Agents.Unit;

public class RunWorkspaceSessionTests
{
    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string contentRootPath)
        {
            ContentRootPath = contentRootPath;
        }

        public string EnvironmentName { get; set; } = "Testing";

        public string ApplicationName { get; set; } = "AgentForge.Tests";

        public string ContentRootPath { get; set; }

        public IFileProvider ContentRootFileProvider
        {
            get => new NullFileProvider();
            set { }
        }
    }

    private static AgentsOptions EnabledOptions(string localPath, string worktreesRoot) =>
        new()
        {
            Workspace =
            {
                Enabled = true,
                RemoteUrl = "https://example.invalid/repo.git",
                LocalPath = localPath,
                WorktreesRoot = worktreesRoot,
                BaseRef = "main"
            },
            Pricing =
            {
                PromptTokenPerMillion = 1m,
                CompletionTokenPerMillion = 2m
            }
        };

    [Fact]
    public async Task BeginAsync_calls_ensure_fetch_and_add_worktree()
    {
        using var database = new AgentsDatabase();
        var root = Path.Combine(Path.GetTempPath(), "ws-sess-" + Guid.NewGuid().ToString("N"));
        var localPath = Path.Combine(root, "clone");
        var worktreesRoot = Path.Combine(root, "worktrees");
        Directory.CreateDirectory(root);

        try
        {
            var agent = Agent.Create(
                database.CurrentUser.OwnerId,
                new AgentDefinition("A", null, "sys", "m", 0.2, 100, 5, []),
                TestClock.AtEpoch().UtcNow);
            var run = Run.Create(agent, "go", TestClock.AtEpoch().UtcNow);

            await using (var seed = database.NewContext())
            {
                seed.Agents.Add(agent);
                seed.Runs.Add(run);
                await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var git = new RecordingGitWorkspace();
            var options = Options.Create(EnabledOptions(localPath, worktreesRoot));
            var environment = new TestHostEnvironment(root);
            var events = new InProcessRunEventBus();
            await using var db = database.NewContext();
            var session = new RunWorkspaceSession(
                git,
                db,
                TestClock.AtEpoch(),
                options,
                environment,
                events,
                NullLogger<RunWorkspaceSession>.Instance);

            var started = await session.BeginAsync(run.Id, TestContext.Current.CancellationToken);

            Assert.True(started);
            session.Bind();
            Assert.NotNull(RunWorkspaceContext.Current);
            Assert.Contains(git.Calls, c => c.StartsWith("EnsureClone:", StringComparison.Ordinal));
            Assert.Contains(git.Calls, c => c.StartsWith("Fetch:", StringComparison.Ordinal));
            Assert.Contains(git.Calls, c => c.StartsWith("AddWorktree:", StringComparison.Ordinal));

            await session.FinishAsync(run.Id, TestContext.Current.CancellationToken);
        }
        finally
        {
            RunWorkspaceContext.Current = null;
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task FinishAsync_when_Running_pushes_then_completes_and_removes()
    {
        using var database = new AgentsDatabase();
        var root = Path.Combine(Path.GetTempPath(), "ws-sess-" + Guid.NewGuid().ToString("N"));
        var localPath = Path.Combine(root, "clone");
        var worktreesRoot = Path.Combine(root, "worktrees");
        Directory.CreateDirectory(root);

        try
        {
            var agent = Agent.Create(
                database.CurrentUser.OwnerId,
                new AgentDefinition("A", null, "sys", "m", 0.2, 100, 5, []),
                TestClock.AtEpoch().UtcNow);
            var run = Run.Create(agent, "go", TestClock.AtEpoch().UtcNow);

            await using (var seed = database.NewContext())
            {
                seed.Agents.Add(agent);
                seed.Runs.Add(run);
                await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var git = new RecordingGitWorkspace();
            var options = Options.Create(EnabledOptions(localPath, worktreesRoot));
            var environment = new TestHostEnvironment(root);
            var events = new InProcessRunEventBus();
            await using var db = database.NewContext();
            var session = new RunWorkspaceSession(
                git,
                db,
                TestClock.AtEpoch(),
                options,
                environment,
                events,
                NullLogger<RunWorkspaceSession>.Instance);

            Assert.True(await session.BeginAsync(run.Id, TestContext.Current.CancellationToken));
            session.Bind();

            await using (var mark = database.NewContext())
            {
                var loaded = await mark.Runs.IgnoreQueryFilters().SingleAsync(TestContext.Current.CancellationToken);
                loaded.MarkRunning(TestClock.AtEpoch().UtcNow);
                await mark.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await session.FinishAsync(run.Id, TestContext.Current.CancellationToken);

            Assert.Contains(git.Calls, c => c.StartsWith("PushBranch:", StringComparison.Ordinal));
            Assert.Contains(git.Calls, c => c.StartsWith("RemoveWorktree:", StringComparison.Ordinal));

            await using var verify = database.NewContext();
            var final = await verify.Runs.IgnoreQueryFilters().SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(RunStatus.Completed, final.Status);
            session.Unbind();
            Assert.Null(RunWorkspaceContext.Current);
        }
        finally
        {
            RunWorkspaceContext.Current = null;
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task FinishAsync_when_Failed_does_not_push()
    {
        using var database = new AgentsDatabase();
        var root = Path.Combine(Path.GetTempPath(), "ws-sess-" + Guid.NewGuid().ToString("N"));
        var localPath = Path.Combine(root, "clone");
        var worktreesRoot = Path.Combine(root, "worktrees");
        Directory.CreateDirectory(root);

        try
        {
            var agent = Agent.Create(
                database.CurrentUser.OwnerId,
                new AgentDefinition("A", null, "sys", "m", 0.2, 100, 5, []),
                TestClock.AtEpoch().UtcNow);
            var run = Run.Create(agent, "go", TestClock.AtEpoch().UtcNow);

            await using (var seed = database.NewContext())
            {
                seed.Agents.Add(agent);
                seed.Runs.Add(run);
                await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            var git = new RecordingGitWorkspace();
            var options = Options.Create(EnabledOptions(localPath, worktreesRoot));
            var environment = new TestHostEnvironment(root);
            var events = new InProcessRunEventBus();
            await using var db = database.NewContext();
            var session = new RunWorkspaceSession(
                git,
                db,
                TestClock.AtEpoch(),
                options,
                environment,
                events,
                NullLogger<RunWorkspaceSession>.Instance);

            Assert.True(await session.BeginAsync(run.Id, TestContext.Current.CancellationToken));
            session.Bind();

            await using (var mark = database.NewContext())
            {
                var loaded = await mark.Runs.IgnoreQueryFilters().SingleAsync(TestContext.Current.CancellationToken);
                loaded.MarkRunning(TestClock.AtEpoch().UtcNow);
                loaded.Fail("boom", TestClock.AtEpoch().UtcNow);
                await mark.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await session.FinishAsync(run.Id, TestContext.Current.CancellationToken);

            Assert.DoesNotContain(git.Calls, c => c.StartsWith("PushBranch:", StringComparison.Ordinal));
            Assert.Contains(git.Calls, c => c.StartsWith("RemoveWorktree:", StringComparison.Ordinal));
        }
        finally
        {
            RunWorkspaceContext.Current = null;
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
