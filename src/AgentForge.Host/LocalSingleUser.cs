using AgentForge.Core;
using Microsoft.Extensions.Options;

namespace AgentForge.Host;

public sealed class LocalSingleUser(IOptions<LocalUserOptions> options) : ICurrentUser
{
    public string OwnerId => options.Value.LocalOwnerId;
}
