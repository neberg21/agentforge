using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using AgentForge.Areas.Agents.Runtime.Workspace;
using Microsoft.Extensions.Options;

namespace AgentForge.Areas.Agents.Runtime.Tools;

public sealed class RunShellTool : ITool
{
    private readonly AgentsOptions _options;

    public RunShellTool(IOptions<AgentsOptions> options)
    {
        _options = options.Value;
    }

    public string Name => "run_shell";

    public async Task<string> ExecuteAsync(string argumentsJson, CancellationToken ct)
    {
        var context = RunWorkspaceContext.Current;
        if (context is null)
        {
            return """{"ok":false,"error":"no_workspace"}""";
        }

        string? command;
        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            command = document.RootElement.TryGetProperty("command", out var commandElement)
                ? commandElement.GetString()
                : null;
        }
        catch (JsonException)
        {
            return """{"ok":false,"error":"invalid_arguments"}""";
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            return """{"ok":false,"error":"command_required"}""";
        }

        var startInfo = new ProcessStartInfo
        {
            WorkingDirectory = context.Root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            startInfo.FileName = "cmd.exe";
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(command);
        }
        else
        {
            startInfo.FileName = "/bin/sh";
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(command);
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            return """{"ok":false,"error":"process_start_failed"}""";
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_options.Workspace.ShellTimeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
            }

            var timedOutStdout = Truncate(await SafeReadAsync(stdoutTask));
            var timedOutStderr = Truncate(await SafeReadAsync(stderrTask));
            return JsonSerializer.Serialize(new
            {
                ok = false,
                error = "timeout",
                exitCode = -1,
                stdout = timedOutStdout,
                stderr = timedOutStderr
            });
        }

        var stdout = Truncate(await stdoutTask);
        var stderr = Truncate(await stderrTask);
        return JsonSerializer.Serialize(new
        {
            ok = true,
            exitCode = process.ExitCode,
            stdout,
            stderr
        });
    }

    private string Truncate(string value)
    {
        var max = _options.Workspace.MaxOutputChars;
        if (value.Length <= max)
        {
            return value;
        }

        return value[..max];
    }

    private static async Task<string> SafeReadAsync(Task<string> task)
    {
        try
        {
            return await task;
        }
        catch
        {
            return string.Empty;
        }
    }
}
