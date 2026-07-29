using System.ComponentModel.DataAnnotations;

namespace AgentForge.Host;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public const string Sqlite = "sqlite";

    [Required]
    public string Provider { get; set; } = Sqlite;

    [Required]
    public string ConnectionString { get; set; } = string.Empty;
}
