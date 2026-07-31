using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AgentForge.Areas.Abstractions.Unit;

public class ResultExtensionsTests
{
    [Theory]
    [InlineData(ErrorKind.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorKind.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ErrorKind.Validation, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorKind.RateLimited, StatusCodes.Status429TooManyRequests)]
    [InlineData(ErrorKind.DependencyFailure, StatusCodes.Status502BadGateway)]
    public void ToProblem_WhenErrorKindMapped_ReturnsExpectedStatusCode(ErrorKind kind, int expectedStatus)
    {
        var result = new Error(kind, "some_code", "Beschreibung.").ToProblem();

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(expectedStatus, problem.StatusCode);
        Assert.Equal("Beschreibung.", problem.ProblemDetails.Detail);
        Assert.Equal("some_code", Assert.Contains("code", problem.ProblemDetails.Extensions));
    }

    [Fact]
    public void ToHttpResult_WhenSuccess_InvokesSuccessBranch()
    {
        Result<int> result = 5;

        var httpResult = result.ToHttpResult(value => TypedResults.Ok(value));

        Assert.Equal(5, Assert.IsType<Ok<int>>(httpResult).Value);
    }

    [Fact]
    public void ToHttpResult_WhenFailure_ReturnsProblem()
    {
        Result<int> result = new Error(ErrorKind.NotFound, "missing", "Weg.");

        var httpResult = result.ToHttpResult(value => TypedResults.Ok(value));

        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ProblemHttpResult>(httpResult).StatusCode);
    }
}
