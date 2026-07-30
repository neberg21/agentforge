using AgentForge.Areas.Abstractions;
using AgentForge.Areas.Agents.Application;
using AgentForge.Areas.Agents.Domain;
using AgentForge.Areas.Agents.Runtime.Events;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AgentForge.Areas.Agents.Http;

public static class RunEndpoints
{
    public static void MapRunEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/runs").WithTags("agent-runs");

        group.MapGet("/", async (
            RunService service,
            Guid? agentId,
            RunStatus? status,
            int? skip,
            int? take,
            CancellationToken ct) =>
        {
            var page = await service.ListAsync(agentId, status, PageRequest.From(skip, take), ct);

            return TypedResults.Ok(new PagedResponse<RunResponse>(
                [.. page.Items.Select(RunResponse.From)],
                page.Total,
                page.Skip,
                page.Take));
        });

        group.MapGet("/{id:guid}", async (RunService service, Guid id, CancellationToken ct) =>
            (await service.GetAsync(id, ct)).ToHttpResult(run => TypedResults.Ok(RunResponse.From(run))));

        group.MapPost("/", async (RunService service, CreateRunRequest request, CancellationToken ct) =>
                (await service.CreateAsync(request.AgentId, request.Objective, ct)).ToHttpResult(run =>
                    TypedResults.Created($"/api/agents/runs/{run.Id}", RunResponse.From(run))))
            .AddEndpointFilter<ValidationFilter<CreateRunRequest>>();

        group.MapPost("/{id:guid}/cancel", async (
                RunService service,
                Guid id,
                CancelRunRequest request,
                CancellationToken ct) =>
                (await service.CancelAsync(id, request.ConcurrencyToken, ct))
                    .ToHttpResult(run => TypedResults.Ok(RunResponse.From(run))))
            .AddEndpointFilter<ValidationFilter<CancelRunRequest>>();

        group.MapGet("/{id:guid}/messages", async (RunService service, Guid id, CancellationToken ct) =>
            (await service.GetMessagesAsync(id, ct)).ToHttpResult(messages =>
                TypedResults.Ok(messages.Select(RunMessageResponse.From).ToArray())));

        group.MapGet("/{id:guid}/stream", StreamRunAsync);
    }

    private static async Task<IResult> StreamRunAsync(
        Guid id,
        RunService runs,
        IRunEventBus bus,
        HttpContext http,
        CancellationToken ct)
    {
        var existing = await runs.GetAsync(id, ct);
        if (!existing.IsSuccess)
        {
            return existing.Error!.Value.ToProblem();
        }

        var run = existing.Value!;
        var response = http.Response;
        response.Headers.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        await response.StartAsync(ct);

        if (run.Status is RunStatus.Completed or RunStatus.Failed or RunStatus.Cancelled)
        {
            await WriteSseAsync(response, RunEventType.Status, $"{{\"status\":\"{run.Status}\"}}", ct);
            await WriteSseAsync(response, RunEventType.Done, "{}", ct);
            return Results.Empty;
        }

        await foreach (var ev in bus.Subscribe(id, ct))
        {
            await WriteSseAsync(response, ev.Type, ev.JsonPayload, ct);
            if (ev.Type == RunEventType.Done)
            {
                break;
            }
        }

        return Results.Empty;
    }

    private static async Task WriteSseAsync(
        HttpResponse response,
        RunEventType type,
        string payload,
        CancellationToken ct)
    {
        await response.WriteAsync($"event: {type.ToString().ToLowerInvariant()}\n", ct);
        await response.WriteAsync($"data: {payload}\n\n", ct);
        await response.Body.FlushAsync(ct);
    }
}
