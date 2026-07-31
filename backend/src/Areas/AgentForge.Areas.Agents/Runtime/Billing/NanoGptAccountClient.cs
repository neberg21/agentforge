using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace AgentForge.Areas.Agents.Runtime.Billing;

public sealed class NanoGptAccountClient : INanoGptAccountClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly AgentsOptions _options;

    public NanoGptAccountClient(HttpClient http, IOptions<AgentsOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<NanoGptBalance> GetBalanceAsync(CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "check-balance");
        message.Content = JsonContent.Create(new { }, options: JsonOptions);
        ApplyAuth(message);

        using var response = await _http.SendAsync(message, ct);
        await EnsureSuccessAsync(response, ct);

        var parsed = await response.Content.ReadFromJsonAsync<CheckBalanceResponse>(JsonOptions, ct);
        if (parsed is null)
        {
            throw new NanoGptAccountException(HttpStatusCode.BadGateway, "Empty balance response from NanoGPT.");
        }

        var usd = ParseDecimal(parsed.UsdBalance);
        var nano = ParseDecimal(parsed.NanoBalance);
        return new NanoGptBalance(usd, nano, parsed.NanoDepositAddress);
    }

    public async Task<NanoGptUsage> GetUsageAsync(NanoGptUsageQuery query, CancellationToken ct)
    {
        var path = BuildUsagePath(query);
        using var message = new HttpRequestMessage(HttpMethod.Get, path);
        ApplyAuth(message);

        using var response = await _http.SendAsync(message, ct);
        await EnsureSuccessAsync(response, ct);

        var parsed = await response.Content.ReadFromJsonAsync<UsageResponseDto>(JsonOptions, ct);
        if (parsed?.Totals is null)
        {
            throw new NanoGptAccountException(HttpStatusCode.BadGateway, "Empty usage response from NanoGPT.");
        }

        return new NanoGptUsage(
            parsed.From ?? string.Empty,
            parsed.To ?? string.Empty,
            MapTotals(parsed.Totals),
            MapBuckets(parsed.ByDay),
            MapBuckets(parsed.ByModel),
            MapBuckets(parsed.ByDayModel));
    }

    public async Task<NanoGptDepositLimits> GetBtcLnLimitsAsync(CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, "transaction/limits/btc-ln");
        ApplyAuth(message);

        using var response = await _http.SendAsync(message, ct);
        await EnsureSuccessAsync(response, ct);

        var parsed = await response.Content.ReadFromJsonAsync<LimitsResponseDto>(JsonOptions, ct);
        if (parsed is null)
        {
            throw new NanoGptAccountException(HttpStatusCode.BadGateway, "Empty limits response from NanoGPT.");
        }

        return new NanoGptDepositLimits(
            parsed.Minimum,
            parsed.Maximum,
            parsed.FiatEquivalentMinimum,
            parsed.FiatEquivalentMaximum);
    }

    public async Task<NanoGptDeposit> CreateBtcLnDepositAsync(decimal amount, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "transaction/create/btc-ln");
        var body = new CreateDepositBody { Amount = amount };
        message.Content = JsonContent.Create(body, options: JsonOptions);
        ApplyAuth(message);

        using var response = await _http.SendAsync(message, ct);
        await EnsureSuccessAsync(response, ct);

        var parsed = await response.Content.ReadFromJsonAsync<DepositResponseDto>(JsonOptions, ct);
        if (parsed is null || string.IsNullOrWhiteSpace(parsed.TxId))
        {
            throw new NanoGptAccountException(HttpStatusCode.BadGateway, "Empty deposit response from NanoGPT.");
        }

        return MapDeposit(parsed);
    }

    public async Task<NanoGptDeposit> GetBtcLnDepositAsync(string txId, CancellationToken ct)
    {
        var path = $"transaction/status/btc-ln/{Uri.EscapeDataString(txId)}";
        using var message = new HttpRequestMessage(HttpMethod.Get, path);
        ApplyAuth(message);

        using var response = await _http.SendAsync(message, ct);
        await EnsureSuccessAsync(response, ct);

        var parsed = await response.Content.ReadFromJsonAsync<DepositResponseDto>(JsonOptions, ct);
        if (parsed is null || string.IsNullOrWhiteSpace(parsed.TxId))
        {
            throw new NanoGptAccountException(HttpStatusCode.BadGateway, "Empty deposit status from NanoGPT.");
        }

        return MapDeposit(parsed);
    }

    private void ApplyAuth(HttpRequestMessage message)
    {
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Llm.ApiKey);
    }

    private static string BuildUsagePath(NanoGptUsageQuery query)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(query.From))
        {
            parts.Add($"from={Uri.EscapeDataString(query.From)}");
        }

        if (!string.IsNullOrWhiteSpace(query.To))
        {
            parts.Add($"to={Uri.EscapeDataString(query.To)}");
        }

        if (!string.IsNullOrWhiteSpace(query.GroupBy))
        {
            parts.Add($"group_by={Uri.EscapeDataString(query.GroupBy)}");
        }

        if (parts.Count == 0)
        {
            return "v1/usage";
        }

        return "v1/usage?" + string.Join("&", parts);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        if (body.Length > 500)
        {
            body = body[..500];
        }

        var message = string.IsNullOrWhiteSpace(body)
            ? $"NanoGPT request failed with {(int)response.StatusCode}."
            : body;
        throw new NanoGptAccountException(response.StatusCode, message);
    }

    private static decimal ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0m;
        }

        return decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    private static NanoGptUsageTotals MapTotals(UsageTotalsDto dto) =>
        new(
            dto.Requests,
            dto.CostUsd,
            dto.RefundedUsd,
            dto.NetCostUsd,
            dto.InputTokens,
            dto.OutputTokens,
            dto.ReasoningTokens,
            dto.TotalTokens);

    private static IReadOnlyList<NanoGptUsageBucket>? MapBuckets(List<UsageBucketDto>? buckets)
    {
        if (buckets is null || buckets.Count == 0)
        {
            return null;
        }

        return buckets
            .Select(bucket => new NanoGptUsageBucket(
                bucket.Date,
                bucket.Model,
                bucket.Requests,
                bucket.CostUsd,
                bucket.RefundedUsd,
                bucket.NetCostUsd,
                bucket.InputTokens,
                bucket.OutputTokens,
                bucket.ReasoningTokens,
                bucket.TotalTokens))
            .ToList();
    }

    private static NanoGptDeposit MapDeposit(DepositResponseDto dto) =>
        new(
            dto.TxId ?? string.Empty,
            dto.Amount,
            dto.Status ?? string.Empty,
            dto.PaymentLink,
            dto.Address,
            dto.CreatedAt,
            dto.ExpiresAt);

    private sealed class CheckBalanceResponse
    {
        [JsonPropertyName("usd_balance")]
        public string? UsdBalance { get; set; }

        [JsonPropertyName("nano_balance")]
        public string? NanoBalance { get; set; }

        [JsonPropertyName("nanoDepositAddress")]
        public string? NanoDepositAddress { get; set; }
    }

    private sealed class CreateDepositBody
    {
        public decimal Amount { get; set; }
    }

    private sealed class LimitsResponseDto
    {
        public decimal Minimum { get; set; }

        public decimal Maximum { get; set; }

        public decimal? FiatEquivalentMinimum { get; set; }

        public decimal? FiatEquivalentMaximum { get; set; }
    }

    private sealed class DepositResponseDto
    {
        public string? TxId { get; set; }

        public decimal Amount { get; set; }

        public string? Status { get; set; }

        public string? PaymentLink { get; set; }

        public string? Address { get; set; }

        public DateTimeOffset? CreatedAt { get; set; }

        public DateTimeOffset? ExpiresAt { get; set; }
    }

    private sealed class UsageResponseDto
    {
        public string? From { get; set; }

        public string? To { get; set; }

        public UsageTotalsDto? Totals { get; set; }

        public List<UsageBucketDto>? ByDay { get; set; }

        public List<UsageBucketDto>? ByModel { get; set; }

        public List<UsageBucketDto>? ByDayModel { get; set; }
    }

    private sealed class UsageTotalsDto
    {
        public int Requests { get; set; }

        public decimal CostUsd { get; set; }

        public decimal RefundedUsd { get; set; }

        public decimal NetCostUsd { get; set; }

        public long InputTokens { get; set; }

        public long OutputTokens { get; set; }

        public long ReasoningTokens { get; set; }

        public long TotalTokens { get; set; }
    }

    private sealed class UsageBucketDto
    {
        public string? Date { get; set; }

        public string? Model { get; set; }

        public int Requests { get; set; }

        public decimal CostUsd { get; set; }

        public decimal RefundedUsd { get; set; }

        public decimal NetCostUsd { get; set; }

        public long InputTokens { get; set; }

        public long OutputTokens { get; set; }

        public long ReasoningTokens { get; set; }

        public long TotalTokens { get; set; }
    }
}
