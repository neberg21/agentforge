using AgentForge.Areas.Abstractions;
using AgentForge.Areas.Agents.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AgentForge.Areas.Agents.Http;

public static class AgentEndpoints
{
    public static void MapAgentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/definitions").WithTags("agent-definitions");

        group.MapGet("/", async (AgentService service, int? skip, int? take, CancellationToken ct) =>
        {
            var page = await service.ListAsync(PageRequest.From(skip, take), ct);

            return TypedResults.Ok(new PagedResponse<AgentResponse>(
                [.. page.Items.Select(AgentResponse.From)],
                page.Total,
                page.Skip,
                page.Take));
        });

        group.MapGet("/{id:guid}", async (AgentService service, Guid id, CancellationToken ct) =>
            (await service.GetAsync(id, ct)).ToHttpResult(agent => TypedResults.Ok(AgentResponse.From(agent))));

        group.MapPost("/", async (AgentService service, CreateAgentRequest request, CancellationToken ct) =>
                (await service.CreateAsync(request.ToDefinition(), ct)).ToHttpResult(agent =>
                    TypedResults.Created($"/api/agents/definitions/{agent.Id}", AgentResponse.From(agent))))
            .AddEndpointFilter<ValidationFilter<CreateAgentRequest>>();

        group.MapPut("/{id:guid}", async (AgentService service, Guid id, UpdateAgentRequest request, CancellationToken ct) =>
                (await service.UpdateAsync(id, request.ToDefinition(), request.ConcurrencyToken, ct))
                    .ToHttpResult(agent => TypedResults.Ok(AgentResponse.From(agent))))
            .AddEndpointFilter<ValidationFilter<UpdateAgentRequest>>();

        group.MapDelete("/{id:guid}", async (AgentService service, Guid id, CancellationToken ct) =>
            (await service.ArchiveAsync(id, ct)).ToHttpResult(agent => TypedResults.Ok(AgentResponse.From(agent))));
    }
}
