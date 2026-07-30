namespace AgentForge.Areas.Abstractions.Unit;

public class AreaSlugTests
{
    [Theory]
    [InlineData("agents")]
    [InlineData("dnd")]
    [InlineData("agent-runtime")]
    [InlineData("a1")]
    public void IsValid_WhenSlugIsWellFormed_ReturnsTrue(string slug) => Assert.True(AreaSlug.IsValid(slug));

    [Theory]
    [InlineData("")]
    [InlineData("Agents")]
    [InlineData("agents_area")]
    [InlineData("-agents")]
    [InlineData("agents-")]
    [InlineData("agents--area")]
    [InlineData("agents/runs")]
    public void IsValid_WhenSlugIsMalformed_ReturnsFalse(string slug) => Assert.False(AreaSlug.IsValid(slug));

    [Fact]
    public void Validate_WhenSlugIsInvalid_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => AreaSlug.Validate("Agents"));
        Assert.Contains("Agents", exception.Message, StringComparison.Ordinal);
    }
}
