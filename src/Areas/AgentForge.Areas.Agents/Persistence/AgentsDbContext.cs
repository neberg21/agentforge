using AgentForge.Areas.Agents.Domain;
using AgentForge.Core;
using Microsoft.EntityFrameworkCore;

namespace AgentForge.Areas.Agents.Persistence;

public sealed class AgentsDbContext : DbContext
{
    private readonly ICurrentUser _currentUser;

    public AgentsDbContext(DbContextOptions<AgentsDbContext> options, ICurrentUser currentUser)
        : base(options)
    {
        _currentUser = currentUser;
    }

    public const string TablePrefix = "agents_";

    public DbSet<Agent> Agents => Set<Agent>();

    public DbSet<Run> Runs => Set<Run>();

    public DbSet<RunMessage> RunMessages => Set<RunMessage>();

    private string OwnerId => _currentUser.OwnerId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AgentConfiguration());
        modelBuilder.ApplyConfiguration(new RunConfiguration());
        modelBuilder.ApplyConfiguration(new RunMessageConfiguration());

        modelBuilder.Entity<Agent>().HasQueryFilter(agent => agent.OwnerId == OwnerId);
        modelBuilder.Entity<Run>().HasQueryFilter(run => run.OwnerId == OwnerId);
        modelBuilder.Entity<RunMessage>().HasQueryFilter(message => message.OwnerId == OwnerId);
    }
}
