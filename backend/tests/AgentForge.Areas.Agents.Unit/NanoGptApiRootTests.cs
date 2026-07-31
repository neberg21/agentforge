using AgentForge.Areas.Agents.Runtime.Billing;

namespace AgentForge.Areas.Agents.Unit;

public class NanoGptApiRootTests
{
    [Theory]
    [InlineData("https://nano-gpt.com/api/v1", "https://nano-gpt.com/api")]
    [InlineData("https://nano-gpt.com/api/v1/", "https://nano-gpt.com/api")]
    [InlineData("https://nano-gpt.com/api", "https://nano-gpt.com/api")]
    [InlineData("https://nano-gpt.com/api/", "https://nano-gpt.com/api")]
    public void FromLlmBaseUrl_StripsTrailingV1(string input, string expected) =>
        Assert.Equal(expected, NanoGptApiRoot.FromLlmBaseUrl(input));
}
