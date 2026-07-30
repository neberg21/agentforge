using System.Diagnostics;
using System.Text;

namespace AgentForge.Areas.Agents.Runtime.Workspace;

public sealed class GitCliWorkspace : IGitWorkspace
{
    public async Task EnsureCloneAsync(string remoteUrl, string localPath, CancellationToken ct)
    {
        if (Directory.Exists(Path.Combine(localPath, ".git")))
        {
            return;
        }

        var parent = Path.GetDirectoryName(Path.GetFullPath(localPath));
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        await RunGitAsync(null, ["clone", remoteUrl, localPath], ct);
    }

    public Task FetchAsync(string localPath, CancellationToken ct) =>
        RunGitAsync(localPath, ["fetch", "--all", "--prune"], ct);

    public Task AddWorktreeAsync(
        string localPath,
        string worktreePath,
        string branchName,
        string baseRef,
        CancellationToken ct)
    {
        var parent = Path.GetDirectoryName(Path.GetFullPath(worktreePath));
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        return RunGitAsync(
            localPath,
            ["worktree", "add", "-b", branchName, worktreePath, baseRef],
            ct);
    }

    public async Task RemoveWorktreeAsync(string localPath, string worktreePath, CancellationToken ct)
    {
        try
        {
            await RunGitAsync(localPath, ["worktree", "remove", "--force", worktreePath], ct);
        }
        catch (InvalidOperationException)
        {
            if (Directory.Exists(worktreePath))
            {
                Directory.Delete(worktreePath, recursive: true);
            }

            await RunGitAsync(localPath, ["worktree", "prune"], ct);
        }
    }

    public Task PushBranchAsync(string worktreePath, string branchName, CancellationToken ct) =>
        RunGitAsync(worktreePath, ["push", "-u", "origin", branchName], ct);

    private static async Task RunGitAsync(string? workingDirectory, string[] args, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (!string.IsNullOrEmpty(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start git.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            var detail = new StringBuilder();
            detail.Append("git ");
            detail.Append(string.Join(' ', args));
            detail.Append(" failed with exit code ");
            detail.Append(process.ExitCode);
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                detail.Append(": ");
                detail.Append(stderr.Trim());
            }
            else if (!string.IsNullOrWhiteSpace(stdout))
            {
                detail.Append(": ");
                detail.Append(stdout.Trim());
            }

            throw new InvalidOperationException(detail.ToString());
        }
    }
}
