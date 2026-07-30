namespace AgentForge.Core.Unit;

public class ResultTests
{
    [Fact]
    public void Success_WhenCreated_CarriesValueWithoutError()
    {
        Result<int> result = 42;

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_WhenCreated_CarriesErrorWithoutValue()
    {
        Result<int> result = new Error(ErrorKind.NotFound, "agent_not_found", "Nicht gefunden.");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.NotFound, result.Error!.Value.Kind);
        Assert.Equal("agent_not_found", result.Error!.Value.Code);
        Assert.Equal(default, result.Value);
    }

    [Fact]
    public void Match_WhenInvoked_SelectsMatchingBranch()
    {
        Result<int> ok = 7;
        Result<int> bad = new Error(ErrorKind.Conflict, "conflict", "Konflikt.");

        Assert.Equal("7", ok.Match(v => v.ToString(), e => e.Code));
        Assert.Equal("conflict", bad.Match(v => v.ToString(), e => e.Code));
    }
}
