using AgentForge.Areas.Abstractions;
using AgentForge.Areas.Agents.Application;
using AgentForge.Areas.Agents.Domain;
using AgentForge.Areas.Agents.Runtime.Events;
using AgentForge.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AgentForge.Areas.Agents.Http;

public static class ConversationEndpoints
{
    public static void MapConversationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/conversations").WithTags("agent-conversations");

        group.MapGet("/", async (ConversationService service, int? skip, int? take, CancellationToken ct) =>
        {
            var page = await service.ListAsync(PageRequest.From(skip, take), ct);
            return TypedResults.Ok(new PagedResponse<ConversationResponse>(
                [.. page.Items.Select(ConversationResponse.From)],
                page.Total,
                page.Skip,
                page.Take));
        });

        group.MapGet("/{id:guid}", async (ConversationService service, Guid id, CancellationToken ct) =>
            (await service.GetAsync(id, ct)).ToHttpResult(detail =>
                TypedResults.Ok(ConversationResponse.From(detail))));

        group.MapPost("/", async (ConversationService service, CreateConversationRequest request, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(request.Title, request.ParticipantAgentIds, ct);
            if (!created.IsSuccess)
            {
                return created.Error!.Value.ToProblem();
            }

            var detail = await service.GetAsync(created.Value!.Id, ct);
            return detail.ToHttpResult(value =>
                TypedResults.Created(
                    $"/api/agents/conversations/{value.Conversation.Id}",
                    ConversationResponse.From(value)));
        })
            .AddEndpointFilter<ValidationFilter<CreateConversationRequest>>();

        group.MapPut("/{id:guid}", async (
                ConversationService service,
                Guid id,
                UpdateConversationRequest request,
                CancellationToken ct) =>
        {
            var updated = await service.UpdateAsync(
                id,
                request.Title,
                request.ParticipantAgentIds,
                request.ConcurrencyToken,
                ct);
            if (!updated.IsSuccess)
            {
                return updated.Error!.Value.ToProblem();
            }

            var detail = await service.GetAsync(id, ct);
            return detail.ToHttpResult(value => TypedResults.Ok(ConversationResponse.From(value)));
        })
            .AddEndpointFilter<ValidationFilter<UpdateConversationRequest>>();

        group.MapPatch("/{id:guid}/title", async (
                ConversationService service,
                Guid id,
                PatchConversationTitleRequest request,
                CancellationToken ct) =>
        {
            Result<Conversation> updated;
            if (string.Equals(request.Action, "set", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(request.Title))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["title"] = ["Title is required when action is set."]
                    });
                }

                updated = await service.SetTitleAsync(id, request.Title, request.ConcurrencyToken, ct);
            }
            else if (string.Equals(request.Action, "lock", StringComparison.OrdinalIgnoreCase))
            {
                updated = await service.LockTitleAsync(id, request.ConcurrencyToken, ct);
            }
            else if (string.Equals(request.Action, "resume", StringComparison.OrdinalIgnoreCase))
            {
                updated = await service.ResumeAutoTitleAsync(id, request.ConcurrencyToken, ct);
            }
            else
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["action"] = ["Action must be set, lock, or resume."]
                });
            }

            if (!updated.IsSuccess)
            {
                return updated.Error!.Value.ToProblem();
            }

            var detail = await service.GetAsync(id, ct);
            return detail.ToHttpResult(value => TypedResults.Ok(ConversationResponse.From(value)));
        })
            .AddEndpointFilter<ValidationFilter<PatchConversationTitleRequest>>();

        group.MapDelete("/{id:guid}", async (ConversationService service, Guid id, CancellationToken ct) =>
        {
            var archived = await service.ArchiveAsync(id, ct);
            if (!archived.IsSuccess)
            {
                return archived.Error!.Value.ToProblem();
            }

            var detail = await service.GetAsync(id, ct);
            return detail.ToHttpResult(value => TypedResults.Ok(ConversationResponse.From(value)));
        });

        group.MapGet("/{id:guid}/messages", async (ConversationService service, Guid id, CancellationToken ct) =>
            (await service.GetMessagesAsync(id, ct)).ToHttpResult(messages =>
                TypedResults.Ok(messages.Select(ConversationMessageResponse.From).ToArray())));

        group.MapPost("/{id:guid}/messages", async (
                ConversationService service,
                Guid id,
                PostConversationMessageRequest request,
                CancellationToken ct) =>
            {
                var mentions = request.Mentions ?? [];
                return (await service.PostMessageAsync(id, request.Content, mentions, ct))
                    .ToHttpResult(streamId => Results.Json(
                        new PostMessageAcceptedResponse(streamId),
                        statusCode: StatusCodes.Status202Accepted));
            })
            .AddEndpointFilter<ValidationFilter<PostConversationMessageRequest>>();

        group.MapGet("/{id:guid}/stream", StreamConversationAsync);

        group.MapPost("/{id:guid}/draft-run", async (
                ConversationService service,
                Guid id,
                DraftRunRequest? request,
                CancellationToken ct) =>
                (await service.DraftRunAsync(id, request?.AgentId, ct)).ToHttpResult(proposal =>
                    TypedResults.Ok(new DraftRunResponse(proposal.Objective, proposal.AgentId))));
    }

    private static async Task<IResult> StreamConversationAsync(
        Guid id,
        ConversationService conversations,
        IConversationEventBus bus,
        HttpContext http,
        CancellationToken ct)
    {
        var existing = await conversations.GetAsync(id, ct);
        if (!existing.IsSuccess)
        {
            return existing.Error!.Value.ToProblem();
        }

        var response = http.Response;
        response.Headers.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        await response.StartAsync(ct);

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
