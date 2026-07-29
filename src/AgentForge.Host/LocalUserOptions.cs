using System.ComponentModel.DataAnnotations;

namespace AgentForge.Host;

public sealed class LocalUserOptions
{
    public const string SectionName = "Auth";

    [Required]
    public string LocalOwnerId { get; set; } = "local";
}
