using AgentForge.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AgentForge.Areas.Agents.Persistence;

public sealed class AgentsDbContextFactory : IDesignTimeDbContextFactory<AgentsDbContext>
{
    public AgentsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AgentsDbContext>();
        optionsBuilder.UseSqlite("Data Source=agents-design.db");
        var options = optionsBuilder.Options;
        var currentUser = new DesignTimeCurrentUser();
        return new AgentsDbContext(options, currentUser);
    }

    private sealed class DesignTimeCurrentUser : ICurrentUser
    {
        public string OwnerId => "design-time";
    }
}
