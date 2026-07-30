namespace AgentForge.Areas.Agents.Runtime.Workspace;

public sealed class ConversationReadContext
{
    private static readonly AsyncLocal<ConversationReadContext?> CurrentLocal = new();

    public ConversationReadContext(string root)
    {
        Root = root;
    }

    public static ConversationReadContext? Current
    {
        get => CurrentLocal.Value;
        set => CurrentLocal.Value = value;
    }

    public string Root { get; }
}
