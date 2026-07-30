using System.Text.Json;
using AgentForge.Areas.Agents.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentForge.Areas.Agents.Persistence;

internal static class JsonColumn
{
    private static readonly JsonSerializerOptions Options = new();

    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Options)!;
}

internal sealed class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        builder.ToTable(AgentsDbContext.TablePrefix + "agent");
        builder.HasKey(agent => agent.Id);

        builder.Property(agent => agent.OwnerId).HasMaxLength(100).IsRequired();
        builder.Property(agent => agent.Name).HasMaxLength(100).IsRequired();
        builder.Property(agent => agent.Description).HasMaxLength(1000);
        builder.Property(agent => agent.SystemPrompt).IsRequired();
        builder.Property(agent => agent.Model).HasMaxLength(100).IsRequired();
        builder.Property(agent => agent.ConcurrencyToken).IsConcurrencyToken();

        builder.Property(agent => agent.AllowedTools)
            .HasConversion(
                value => JsonColumn.Write(value),
                json => JsonColumn.Read<string[]>(json),
                new ValueComparer<string[]>(
                    (left, right) => left!.SequenceEqual(right!),
                    value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode(StringComparison.Ordinal))),
                    value => value.ToArray()))
            .IsRequired();

        builder.HasIndex(agent => agent.OwnerId);

        builder.HasIndex(agent => new { agent.OwnerId, agent.Name })
            .IsUnique()
            .HasFilter("\"ArchivedAt\" IS NULL");
    }
}

internal sealed class RunConfiguration : IEntityTypeConfiguration<Run>
{
    public void Configure(EntityTypeBuilder<Run> builder)
    {
        builder.ToTable(AgentsDbContext.TablePrefix + "run");
        builder.HasKey(run => run.Id);

        builder.Property(run => run.OwnerId).HasMaxLength(100).IsRequired();
        builder.Property(run => run.Objective).IsRequired();
        builder.Property(run => run.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(run => run.ConcurrencyToken).IsConcurrencyToken();

        builder.Property(run => run.AgentSnapshot)
            .HasConversion(
                value => JsonColumn.Write(value),
                json => JsonColumn.Read<AgentSnapshot>(json),
                new ValueComparer<AgentSnapshot>(
                    (left, right) => JsonColumn.Write(left) == JsonColumn.Write(right),
                    value => JsonColumn.Write(value).GetHashCode(StringComparison.Ordinal),
                    value => JsonColumn.Read<AgentSnapshot>(JsonColumn.Write(value))))
            .IsRequired();

        builder.HasOne<Agent>()
            .WithMany()
            .HasForeignKey(run => run.AgentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(run => run.Messages)
            .WithOne()
            .HasForeignKey(message => message.RunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(run => run.Messages)
            .HasField("_messages")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(run => new { run.OwnerId, run.AgentId });
    }
}

internal sealed class RunMessageConfiguration : IEntityTypeConfiguration<RunMessage>
{
    public void Configure(EntityTypeBuilder<RunMessage> builder)
    {
        builder.ToTable(AgentsDbContext.TablePrefix + "run_message");
        builder.HasKey(message => message.Id);

        builder.Property(message => message.OwnerId).HasMaxLength(100).IsRequired();
        builder.Property(message => message.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(message => message.ToolCallId).HasMaxLength(100);

        builder.HasIndex(message => new { message.RunId, message.Sequence }).IsUnique();
    }
}
