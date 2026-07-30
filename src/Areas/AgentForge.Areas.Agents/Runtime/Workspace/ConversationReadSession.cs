using Microsoft.Extensions.Options;

namespace AgentForge.Areas.Agents.Runtime.Workspace;

public interface IConversationReadSession
{
    Task BeginAsync(CancellationToken ct);

    void Bind();

    void Unbind();
}

public sealed class ConversationReadSession : IConversationReadSession
{
    private readonly IGitWorkspace _git;
    private readonly AgentsOptions _options;
    private ConversationReadContext? _context;

    public ConversationReadSession(IGitWorkspace git, IOptions<AgentsOptions> options)
    {
        _git = git;
        _options = options.Value;
    }

    public async Task BeginAsync(CancellationToken ct)
    {
        if (!_options.Workspace.Enabled)
        {
            _context = null;
            return;
        }

        var localPath = _options.Workspace.LocalPath;
        await _git.EnsureCloneAsync(_options.Workspace.RemoteUrl, localPath, ct);
        await _git.FetchAsync(localPath, ct);
        _context = new ConversationReadContext(Path.GetFullPath(localPath));
    }

    public void Bind()
    {
        if (_context is not null)
        {
            ConversationReadContext.Current = _context;
        }
    }

    public void Unbind()
    {
        ConversationReadContext.Current = null;
    }
}
