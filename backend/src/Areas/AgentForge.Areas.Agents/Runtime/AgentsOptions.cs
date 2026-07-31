using System.ComponentModel.DataAnnotations;
using AgentForge.Areas.Agents.Runtime.Workspace;

namespace AgentForge.Areas.Agents.Runtime;

public sealed class AgentsOptions
{
    public const string SectionName = "Areas:Agents";

    [Required]
    public AgentsLlmOptions Llm { get; set; } = new();

    [Range(1, int.MaxValue)]
    public int MaxConcurrentRuns { get; set; } = 2;

    [Required]
    public AgentsPricingOptions Pricing { get; set; } = new();

    public WorkspaceOptions Workspace { get; set; } = new();
}

public sealed class AgentsLlmOptions
{
    [Required]
    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(1);

    public bool UseFake { get; set; }

    public string TitleModel { get; set; } = "gpt-4.1-nano";
}

public sealed class AgentsPricingOptions
{
    [Range(0, double.MaxValue)]
    public decimal PromptTokenPerMillion { get; set; }

    [Range(0, double.MaxValue)]
    public decimal CompletionTokenPerMillion { get; set; }
}
