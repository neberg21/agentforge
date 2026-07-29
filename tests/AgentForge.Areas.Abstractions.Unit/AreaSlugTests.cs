namespace AgentForge.Areas.Abstractions.Unit;

public class AreaSlugTests
{
    [Theory]
    [InlineData("agents")]
    [InlineData("dnd")]
    [InlineData("agent-runtime")]
    [InlineData("a1")]
    public void Gueltige_Slugs_werden_akzeptiert(string slug) => Assert.True(AreaSlug.IsValid(slug));

    [Theory]
    [InlineData("")]
    [InlineData("Agents")]
    [InlineData("agents_area")]
    [InlineData("-agents")]
    [InlineData("agents-")]
    [InlineData("agents--area")]
    [InlineData("agents/runs")]
    public void Ungueltige_Slugs_werden_abgelehnt(string slug) => Assert.False(AreaSlug.IsValid(slug));

    [Fact]
    public void Validate_wirft_bei_ungueltigem_Slug()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => AreaSlug.Validate("Agents"));
        Assert.Contains("Agents", exception.Message, StringComparison.Ordinal);
    }
}
