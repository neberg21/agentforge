using AgentForge.Areas.Abstractions;
using AgentForge.Areas.Agents.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AgentForge.Areas.Agents.Http;

public static class BillingEndpoints
{
    public static void MapBillingEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/billing").WithTags("agent-billing");

        group.MapGet("/balance", async (BillingService service, CancellationToken ct) =>
            (await service.GetBalanceAsync(ct)).ToHttpResult(view =>
                TypedResults.Ok(BillingBalanceResponse.From(view))));

        group.MapGet("/usage", async (
            BillingService service,
            string? from,
            string? to,
            string? group_by,
            CancellationToken ct) =>
            (await service.GetUsageAsync(from, to, group_by, ct)).ToHttpResult(usage =>
                TypedResults.Ok(BillingUsageResponse.FromUsage(usage))));

        group.MapGet("/deposits/limits", async (BillingService service, CancellationToken ct) =>
            (await service.GetDepositLimitsAsync(ct)).ToHttpResult(limits =>
                TypedResults.Ok(BillingDepositLimitsResponse.From(limits))));

        group.MapPost("/deposits", async (
                BillingService service,
                CreateDepositRequest request,
                CancellationToken ct) =>
                (await service.CreateDepositAsync(request.Amount, ct)).ToHttpResult(deposit =>
                    TypedResults.Ok(BillingDepositResponse.From(deposit))))
            .AddEndpointFilter<ValidationFilter<CreateDepositRequest>>();

        group.MapGet("/deposits/{txId}", async (
            BillingService service,
            string txId,
            CancellationToken ct) =>
            (await service.GetDepositAsync(txId, ct)).ToHttpResult(deposit =>
                TypedResults.Ok(BillingDepositResponse.From(deposit))));
    }
}
