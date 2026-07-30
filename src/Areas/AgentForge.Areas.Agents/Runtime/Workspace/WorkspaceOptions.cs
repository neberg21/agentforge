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
