using AgentForge.Core;
using Microsoft.AspNetCore.Http;

namespace AgentForge.Areas.Abstractions;

public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess) =>
        result.Match(onSuccess, ToProblem);

    public static IResult ToProblem(this Error error)
    {
        var (status, title) = error.Kind switch
        {
            ErrorKind.NotFound => (StatusCodes.Status404NotFound, "Nicht gefunden"),
            ErrorKind.Conflict => (StatusCodes.Status409Conflict, "Konflikt"),
            ErrorKind.Validation => (StatusCodes.Status400BadRequest, "Ungültige Anfrage"),
            _ => (StatusCodes.Status500InternalServerError, "Unerwarteter Fehler")
        };

        return TypedResults.Problem(
            detail: error.Message,
            statusCode: status,
            title: title,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }
}
