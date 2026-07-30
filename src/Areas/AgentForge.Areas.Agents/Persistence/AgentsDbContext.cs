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

    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();

    public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();

    private string OwnerId => _currentUser.OwnerId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AgentConfiguration());
        modelBuilder.ApplyConfiguration(new RunConfiguration());
        modelBuilder.ApplyConfiguration(new RunMessageConfiguration());
        modelBuilder.ApplyConfiguration(new ConversationConfiguration());
        modelBuilder.ApplyConfiguration(new ConversationMessageConfiguration());
        modelBuilder.ApplyConfiguration(new ConversationParticipantConfiguration());

        modelBuilder.Entity<Agent>().HasQueryFilter(agent => agent.OwnerId == OwnerId);
        modelBuilder.Entity<Run>().HasQueryFilter(run => run.OwnerId == OwnerId);
        modelBuilder.Entity<RunMessage>().HasQueryFilter(message => message.OwnerId == OwnerId);
        modelBuilder.Entity<Conversation>().HasQueryFilter(conversation => conversation.OwnerId == OwnerId);
        modelBuilder.Entity<ConversationMessage>().HasQueryFilter(message => message.OwnerId == OwnerId);
    }
}
