# NanoGPT Billing Monitor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Operator endpoints under `/api/agents/billing` to read NanoGPT balance (with low-balance flag), usage aggregates, and create/track BTC Lightning deposits using the host LLM API key.

**Architecture:** Thin Agents-area proxy: `INanoGptAccountClient` (HTTP or fake) talks to NanoGPT API root; `BillingService` applies threshold/`Result` error mapping; Minimal API `BillingEndpoints` expose five routes. No local deposit persistence. Testing/UseFake registers a deterministic fake client (no live NanoGPT in CI).

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, `HttpClient`, xUnit v3, hand-written fakes (no new mocking package), existing `Result`/`ErrorKind`/`ToHttpResult` patterns.

**Spec:** `docs/superpowers/specs/2026-07-31-nanogpt-billing-monitor-design.md`

## Global Constraints

- Repo root: `C:\Users\NEWA002\source\repos\agentforge`. Paths relative to it; run `dotnet`/`git` from repo root unless noted.
- No C# primary constructors; do not inline object creation into method/ctor calls — assign locals, then pass.
- Windows: no `.ps1`/`.sh`; commits via `git commit -F` message file (use a `.cmd` helper under `.git/` if the shell injects a Co-authored trailer that breaks PowerShell).
- English commit messages: `feat:` / `test:` / `chore:` / `docs:`.
- After each task: commit only that task’s files.
- TDD: failing test → implement → pass → commit.
- Deposit ticker fixed to `btc-ln` only.
- No live NanoGPT in CI; fake client when `Llm:UseFake` or `Testing` environment.
- Do not overload `ILlmClient` with account APIs.

## File Structure

**Create**
- `backend/src/Areas/AgentForge.Areas.Agents/Runtime/Billing/INanoGptAccountClient.cs`
- `backend/src/Areas/AgentForge.Areas.Agents/Runtime/Billing/NanoGptAccountModels.cs`
- `backend/src/Areas/AgentForge.Areas.Agents/Runtime/Billing/NanoGptApiRoot.cs`
- `backend/src/Areas/AgentForge.Areas.Agents/Runtime/Billing/NanoGptAccountClient.cs`
- `backend/src/Areas/AgentForge.Areas.Agents/Runtime/Billing/FakeNanoGptAccountClient.cs`
- `backend/src/Areas/AgentForge.Areas.Agents/Application/BillingService.cs`
- `backend/src/Areas/AgentForge.Areas.Agents/Http/BillingEndpoints.cs`
- `backend/tests/AgentForge.Areas.Agents.Unit/BillingBalanceTests.cs`
- `backend/tests/AgentForge.Areas.Agents.Unit/BillingServiceTests.cs`
- `backend/tests/AgentForge.Areas.Agents.Unit/NanoGptAccountClientTests.cs`
- `backend/tests/AgentForge.Areas.Agents.Unit/NanoGptApiRootTests.cs`
- `backend/tests/AgentForge.Host.Integration/BillingEndpointTests.cs`

**Modify**
- `backend/src/AgentForge.Core/Result.cs` — add `ErrorKind.RateLimited`, `ErrorKind.DependencyFailure`
- `backend/src/AgentForge.Areas.Abstractions/ResultExtensions.cs` — map new kinds to 429 / 502
- `backend/tests/AgentForge.Areas.Abstractions.Unit/ResultExtensionsTests.cs`
- `backend/src/Areas/AgentForge.Areas.Agents/Runtime/AgentsOptions.cs` — `Billing` options
- `backend/src/Areas/AgentForge.Areas.Agents/Http/Requests.cs` — `CreateDepositRequest`
- `backend/src/Areas/AgentForge.Areas.Agents/Http/Responses.cs` — billing response DTOs
- `backend/src/Areas/AgentForge.Areas.Agents/AgentsArea.cs` — register client + map endpoints
- `backend/src/AgentForge.Host/appsettings.json` — `Billing` defaults
- `README.md` — document billing config + endpoints briefly

---

### Task 1: ErrorKind for upstream failures

**Files:**
- Modify: `backend/src/AgentForge.Core/Result.cs`
- Modify: `backend/src/AgentForge.Areas.Abstractions/ResultExtensions.cs`
- Modify: `backend/tests/AgentForge.Areas.Abstractions.Unit/ResultExtensionsTests.cs`

**Interfaces:**
- Consumes: existing `Error`, `ErrorKind`, `ToProblem`
- Produces: `ErrorKind.RateLimited` → HTTP 429 title `"Zu viele Anfragen"`; `ErrorKind.DependencyFailure` → HTTP 502 title `"Abhängigkeit fehlgeschlagen"`

- [ ] **Step 1: Extend the failing status-code theory**

In `ResultExtensionsTests.cs`, add InlineData rows:

```csharp
[InlineData(ErrorKind.RateLimited, StatusCodes.Status429TooManyRequests)]
[InlineData(ErrorKind.DependencyFailure, StatusCodes.Status502BadGateway)]
```

Keep existing NotFound/Conflict/Validation rows.

- [ ] **Step 2: Run test — expect compile/fail**

Run: `dotnet test backend/tests/AgentForge.Areas.Abstractions.Unit/AgentForge.Areas.Abstractions.Unit.csproj --filter ResultExtensionsTests`

Expected: FAIL (unknown enum members or missing switch arms).

- [ ] **Step 3: Implement enum + mapping**

In `Result.cs`:

```csharp
public enum ErrorKind
{
    NotFound,
    Conflict,
    Validation,
    RateLimited,
    DependencyFailure
}
```

In `ResultExtensions.ToProblem` switch, add:

```csharp
ErrorKind.RateLimited => (StatusCodes.Status429TooManyRequests, "Zu viele Anfragen"),
ErrorKind.DependencyFailure => (StatusCodes.Status502BadGateway, "Abhängigkeit fehlgeschlagen"),
```

- [ ] **Step 4: Run tests — expect PASS**

Run: `dotnet test backend/tests/AgentForge.Areas.Abstractions.Unit/AgentForge.Areas.Abstractions.Unit.csproj --filter ResultExtensionsTests`

Expected: PASS

- [ ] **Step 5: Commit**

```cmd
git add backend/src/AgentForge.Core/Result.cs backend/src/AgentForge.Areas.Abstractions/ResultExtensions.cs backend/tests/AgentForge.Areas.Abstractions.Unit/ResultExtensionsTests.cs
```

Commit message: `feat: map RateLimited and DependencyFailure error kinds to HTTP`

---

### Task 2: Billing options and API root helper

**Files:**
- Modify: `backend/src/Areas/AgentForge.Areas.Agents/Runtime/AgentsOptions.cs`
- Create: `backend/src/Areas/AgentForge.Areas.Agents/Runtime/Billing/NanoGptApiRoot.cs`
- Modify: `backend/src/AgentForge.Host/appsettings.json`
- Create: `backend/tests/AgentForge.Areas.Agents.Unit/NanoGptApiRootTests.cs`
- Create: `backend/tests/AgentForge.Areas.Agents.Unit/BillingBalanceTests.cs`

**Interfaces:**
- Consumes: `AgentsOptions`
- Produces:
  - `AgentsBillingOptions` with `decimal LowBalanceUsdThreshold` default `5`, `[Range(0, double.MaxValue)]`
  - `AgentsOptions.Billing` property (required instance, default `new()`)
  - `static string NanoGptApiRoot.FromLlmBaseUrl(string llmBaseUrl)` — trim trailing `/`, if ends with `/v1` (ordinal ignore case) strip it; ensure result has no trailing slash except the scheme host path; return absolute URI string ending without `/` for use as `HttpClient.BaseAddress` with trailing `/` added at registration
  - `static bool BillingBalance.IsLow(decimal usdBalance, decimal threshold)` → `usdBalance < threshold`

- [ ] **Step 1: Write failing helper tests**

`NanoGptApiRootTests.cs`:

```csharp
using AgentForge.Areas.Agents.Runtime.Billing;

namespace AgentForge.Areas.Agents.Unit;

public class NanoGptApiRootTests
{
    [Theory]
    [InlineData("https://nano-gpt.com/api/v1", "https://nano-gpt.com/api")]
    [InlineData("https://nano-gpt.com/api/v1/", "https://nano-gpt.com/api")]
    [InlineData("https://nano-gpt.com/api", "https://nano-gpt.com/api")]
    [InlineData("https://nano-gpt.com/api/", "https://nano-gpt.com/api")]
    public void FromLlmBaseUrl_StripsTrailingV1(string input, string expected) =>
        Assert.Equal(expected, NanoGptApiRoot.FromLlmBaseUrl(input));
}
```

`BillingBalanceTests.cs`:

```csharp
using AgentForge.Areas.Agents.Runtime.Billing;

namespace AgentForge.Areas.Agents.Unit;

public class BillingBalanceTests
{
    [Theory]
    [InlineData(4.99, 5.0, true)]
    [InlineData(5.0, 5.0, false)]
    [InlineData(12.0, 5.0, false)]
    public void IsLow_ComparesStrictlyLess(decimal usd, decimal threshold, bool expected) =>
        Assert.Equal(expected, BillingBalance.IsLow(usd, threshold));
}
```

- [ ] **Step 2: Run — expect FAIL**

Run: `dotnet test backend/tests/AgentForge.Areas.Agents.Unit/AgentForge.Areas.Agents.Unit.csproj --filter "FullyQualifiedName~NanoGptApiRootTests|FullyQualifiedName~BillingBalanceTests"`

Expected: FAIL (types missing).

- [ ] **Step 3: Implement options + helpers**

Add to `AgentsOptions.cs`:

```csharp
[Required]
public AgentsBillingOptions Billing { get; set; } = new();
```

```csharp
public sealed class AgentsBillingOptions
{
    [Range(0, double.MaxValue)]
    public decimal LowBalanceUsdThreshold { get; set; } = 5m;
}
```

Create `NanoGptApiRoot.cs` and put `BillingBalance.IsLow` in the same file or a tiny `BillingBalance.cs` next to it — prefer `BillingBalance.cs` for the static helper so each file has one job.

In `appsettings.json` under `Areas:Agents`, add:

```json
"Billing": {
  "LowBalanceUsdThreshold": 5.0
}
```

- [ ] **Step 4: Run — expect PASS**

Same filter as Step 2. Expected: PASS

- [ ] **Step 5: Commit**

Message: `feat: add billing options and NanoGPT API root helper`

---

### Task 3: Account client contract + fake

**Files:**
- Create: `backend/src/Areas/AgentForge.Areas.Agents/Runtime/Billing/NanoGptAccountModels.cs`
- Create: `backend/src/Areas/AgentForge.Areas.Agents/Runtime/Billing/INanoGptAccountClient.cs`
- Create: `backend/src/Areas/AgentForge.Areas.Agents/Runtime/Billing/FakeNanoGptAccountClient.cs`

**Interfaces:**
- Consumes: none beyond BCL
- Produces:

```csharp
public sealed record NanoGptBalance(
    decimal UsdBalance,
    decimal NanoBalance,
    string? NanoDepositAddress);

public sealed record NanoGptUsageQuery(string? From, string? To, string? GroupBy);

public sealed record NanoGptUsageTotals(
    int Requests,
    decimal CostUsd,
    decimal RefundedUsd,
    decimal NetCostUsd,
    long InputTokens,
    long OutputTokens,
    long ReasoningTokens,
    long TotalTokens);

public sealed record NanoGptUsageBucket(
    string? Date,
    string? Model,
    int Requests,
    decimal CostUsd,
    decimal RefundedUsd,
    decimal NetCostUsd,
    long InputTokens,
    long OutputTokens,
    long ReasoningTokens,
    long TotalTokens);

public sealed record NanoGptUsage(
    string From,
    string To,
    NanoGptUsageTotals Totals,
    IReadOnlyList<NanoGptUsageBucket>? ByDay,
    IReadOnlyList<NanoGptUsageBucket>? ByModel,
    IReadOnlyList<NanoGptUsageBucket>? ByDayModel);

public sealed record NanoGptDepositLimits(
    decimal Minimum,
    decimal Maximum,
    decimal? FiatEquivalentMinimum,
    decimal? FiatEquivalentMaximum);

public sealed record NanoGptDeposit(
    string TxId,
    decimal Amount,
    string Status,
    string? PaymentLink,
    string? Address,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? ExpiresAt);

public interface INanoGptAccountClient
{
    Task<NanoGptBalance> GetBalanceAsync(CancellationToken ct);
    Task<NanoGptUsage> GetUsageAsync(NanoGptUsageQuery query, CancellationToken ct);
    Task<NanoGptDepositLimits> GetBtcLnLimitsAsync(CancellationToken ct);
    Task<NanoGptDeposit> CreateBtcLnDepositAsync(decimal amount, CancellationToken ct);
    Task<NanoGptDeposit> GetBtcLnDepositAsync(string txId, CancellationToken ct);
}
```

`FakeNanoGptAccountClient` (no primary ctor):
- Balance: `UsdBalance = 12.34m`, `NanoBalance = 1.2m`, address `"nano_fake"`
- Usage: fixed totals with `From`/`To` from query or `"2026-01-01"` / `"2026-01-31"`; empty optional arrays when not requested — simplest: always return totals only (`ByDay`/`ByModel`/`ByDayModel` = null)
- Limits: `Minimum = 0.00001m`, `Maximum = 0.1m`, fiat min `0.10m`, fiat max `500m`
- Create: store deposit by `txId = "fake-tx-1"` (increment if called again: `fake-tx-2`, …), status `"New"`, `PaymentLink = "lightning:fake"`, amount as requested, `CreatedAt`/`ExpiresAt` = fixed UTC timestamps
- Get: return stored deposit or throw `HttpRequestException` with 404 if unknown

Also define:

```csharp
public sealed class NanoGptAccountException : Exception
{
    public NanoGptAccountException(HttpStatusCode statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode StatusCode { get; }
}
```

Fake may throw this for unknown tx (404 → later mapped as DependencyFailure or Validation — treat unknown tx as `Validation`/`NotFound` in service; use status 404 on the exception).

- [ ] **Step 1: Add a small unit smoke test for the fake**

Create `backend/tests/AgentForge.Areas.Agents.Unit/FakeNanoGptAccountClientTests.cs`:

```csharp
using AgentForge.Areas.Agents.Runtime.Billing;

namespace AgentForge.Areas.Agents.Unit;

public class FakeNanoGptAccountClientTests
{
    [Fact]
    public async Task CreateThenGet_ReturnsSameTx()
    {
        var client = new FakeNanoGptAccountClient();
        var amount = 0.00002m;

        var created = await client.CreateBtcLnDepositAsync(amount, CancellationToken.None);
        var loaded = await client.GetBtcLnDepositAsync(created.TxId, CancellationToken.None);

        Assert.Equal(created.TxId, loaded.TxId);
        Assert.Equal(amount, loaded.Amount);
        Assert.Equal("New", loaded.Status);
    }
}
```

- [ ] **Step 2: Run — expect FAIL**

Run: `dotnet test backend/tests/AgentForge.Areas.Agents.Unit/AgentForge.Areas.Agents.Unit.csproj --filter FakeNanoGptAccountClientTests`

Expected: FAIL

- [ ] **Step 3: Implement models, interface, fake, exception**

- [ ] **Step 4: Run — expect PASS**

- [ ] **Step 5: Commit**

Message: `feat: add NanoGPT account client contract and fake`

---

### Task 4: BillingService

**Files:**
- Create: `backend/src/Areas/AgentForge.Areas.Agents/Application/BillingService.cs`
- Create: `backend/tests/AgentForge.Areas.Agents.Unit/BillingServiceTests.cs`
- Modify: `backend/src/Areas/AgentForge.Areas.Agents/Http/Responses.cs` (add response records used by service return types — or keep application DTOs in Application and map in HTTP; prefer application returns domain/account models + balance view, HTTP maps to responses in Task 5)

**Interfaces:**
- Consumes: `INanoGptAccountClient`, `IOptions<AgentsOptions>`
- Produces:

```csharp
public sealed record BillingBalanceView(
    decimal UsdBalance,
    decimal NanoBalance,
    string? NanoDepositAddress,
    bool LowBalance,
    decimal LowBalanceThresholdUsd);

public sealed class BillingService
{
    public BillingService(INanoGptAccountClient account, IOptions<AgentsOptions> options);
    Task<Result<BillingBalanceView>> GetBalanceAsync(CancellationToken ct);
    Task<Result<NanoGptUsage>> GetUsageAsync(string? from, string? to, string? groupBy, CancellationToken ct);
    Task<Result<NanoGptDepositLimits>> GetDepositLimitsAsync(CancellationToken ct);
    Task<Result<NanoGptDeposit>> CreateDepositAsync(decimal amount, CancellationToken ct);
    Task<Result<NanoGptDeposit>> GetDepositAsync(string txId, CancellationToken ct);
}
```

Error mapping from `NanoGptAccountException`:
- 400 → `Error(ErrorKind.Validation, "nanogpt_validation", safe message)` — use exception message if non-empty, else `"Invalid request to NanoGPT."`
- 401 → `Error(ErrorKind.DependencyFailure, "nanogpt_auth", "NanoGPT authentication failed.")`
- 404 on get deposit → `Error(ErrorKind.NotFound, "deposit_not_found", "Deposit was not found.")`
- 429 → `Error(ErrorKind.RateLimited, "nanogpt_rate_limited", "NanoGPT rate limit exceeded.")`
- other / transport `HttpRequestException` / `TaskCanceledException` → `Error(ErrorKind.DependencyFailure, "nanogpt_unavailable", "NanoGPT is unavailable.")`

Never include API key material in messages.

`GetBalanceAsync`: call client; build view with `BillingBalance.IsLow` and threshold from options.

- [ ] **Step 1: Write failing service tests**

Use a hand-written stub for `INanoGptAccountClient` (do not add NSubstitute — existing Agents unit tests use hand fakes).

```csharp
using AgentForge.Areas.Agents.Application;
using AgentForge.Areas.Agents.Runtime;
using AgentForge.Areas.Agents.Runtime.Billing;
using AgentForge.Core;
using Microsoft.Extensions.Options;

namespace AgentForge.Areas.Agents.Unit;

public class BillingServiceTests
{
    private sealed class StubAccountClient : INanoGptAccountClient
    {
        public Func<CancellationToken, Task<NanoGptBalance>> BalanceAsync { get; set; } =
            _ => Task.FromResult(new NanoGptBalance(12m, 0m, null));

        public Func<decimal, CancellationToken, Task<NanoGptDeposit>> CreateDepositAsyncFn { get; set; } =
            (amount, _) => Task.FromResult(new NanoGptDeposit(
                "tx", amount, "New", null, null, null, null));

        public Task<NanoGptBalance> GetBalanceAsync(CancellationToken ct) => BalanceAsync(ct);

        public Task<NanoGptUsage> GetUsageAsync(NanoGptUsageQuery query, CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<NanoGptDepositLimits> GetBtcLnLimitsAsync(CancellationToken ct) =>
            throw new NotImplementedException();

        public Task<NanoGptDeposit> CreateBtcLnDepositAsync(decimal amount, CancellationToken ct) =>
            CreateDepositAsyncFn(amount, ct);

        public Task<NanoGptDeposit> GetBtcLnDepositAsync(string txId, CancellationToken ct) =>
            throw new NotImplementedException();
    }

    private static BillingService CreateService(
        INanoGptAccountClient account,
        decimal threshold = 5m)
    {
        var options = new AgentsOptions
        {
            Llm = new AgentsLlmOptions { BaseUrl = "https://nano-gpt.com/api/v1" },
            Pricing = new AgentsPricingOptions(),
            Billing = new AgentsBillingOptions { LowBalanceUsdThreshold = threshold }
        };
        var wrapped = Options.Create(options);
        return new BillingService(account, wrapped);
    }

    [Fact]
    public async Task GetBalanceAsync_WhenBelowThreshold_SetsLowBalance()
    {
        var account = new StubAccountClient
        {
            BalanceAsync = _ => Task.FromResult(new NanoGptBalance(3m, 0m, null))
        };
        var service = CreateService(account, threshold: 5m);

        var result = await service.GetBalanceAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.LowBalance);
        Assert.Equal(5m, result.Value.LowBalanceThresholdUsd);
    }

    [Fact]
    public async Task GetBalanceAsync_WhenUnauthorized_ReturnsDependencyFailure()
    {
        var account = new StubAccountClient
        {
            BalanceAsync = _ => throw new NanoGptAccountException(
                System.Net.HttpStatusCode.Unauthorized,
                "nope")
        };
        var service = CreateService(account);

        var result = await service.GetBalanceAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorKind.DependencyFailure, result.Error!.Value.Kind);
        Assert.Equal("nanogpt_auth", result.Error.Value.Code);
    }

    [Fact]
    public async Task CreateDepositAsync_WhenAmountRejected_ReturnsValidation()
    {
        var account = new StubAccountClient
        {
            CreateDepositAsyncFn = (_, _) => throw new NanoGptAccountException(
                System.Net.HttpStatusCode.BadRequest,
                "Minimum amount is X")
        };
        var service = CreateService(account);

        var result = await service.CreateDepositAsync(0.0000001m, CancellationToken.None);

        Assert.Equal(ErrorKind.Validation, result.Error!.Value.Kind);
    }
}
```

- [ ] **Step 2: Run — expect FAIL**

Run: `dotnet test backend/tests/AgentForge.Areas.Agents.Unit/AgentForge.Areas.Agents.Unit.csproj --filter BillingServiceTests`

- [ ] **Step 3: Implement `BillingService`**

Traditional ctor; private `MapException` helper; wrap each public method in try/catch for `NanoGptAccountException`, `HttpRequestException`, `OperationCanceledException` (rethrow if `ct.IsCancellationRequested`, else dependency failure).

- [ ] **Step 4: Run — expect PASS**

- [ ] **Step 5: Commit**

Message: `feat: add BillingService with NanoGPT error mapping`

---

### Task 5: HTTP `NanoGptAccountClient`

**Files:**
- Create: `backend/src/Areas/AgentForge.Areas.Agents/Runtime/Billing/NanoGptAccountClient.cs`
- Create: `backend/tests/AgentForge.Areas.Agents.Unit/NanoGptAccountClientTests.cs`

**Interfaces:**
- Consumes: `HttpClient`, `IOptions<AgentsOptions>`
- Produces: HTTP implementation of `INanoGptAccountClient`
  - Auth: `Authorization: Bearer {ApiKey}`
  - `POST check-balance` (empty JSON body `{}`)
  - `GET v1/usage?...`
  - `GET transaction/limits/btc-ln`
  - `POST transaction/create/btc-ln` with `{ "amount": ... }`
  - `GET transaction/status/btc-ln/{txId}`
  - On non-success: read body string (truncate to 500 chars for message), throw `NanoGptAccountException(status, message)`
  - JSON: camelCase; NanoGPT balance fields are snake_case (`usd_balance`, `nano_balance`, `nanoDepositAddress`) — use `[JsonPropertyName]` on private DTO types
  - Amount on create: JSON number
  - Parse balance strings with `decimal.Parse(..., CultureInfo.InvariantCulture)`

- [ ] **Step 1: Write failing HTTP client tests with a stub handler**

```csharp
using System.Net;
using System.Text;
using AgentForge.Areas.Agents.Runtime;
using AgentForge.Areas.Agents.Runtime.Billing;
using Microsoft.Extensions.Options;

namespace AgentForge.Areas.Agents.Unit;

public class NanoGptAccountClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public HttpResponseMessage Response { get; set; } =
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"usd_balance":"10.5","nano_balance":"2","nanoDepositAddress":"nano_x"}""",
                    Encoding.UTF8,
                    "application/json")
            };

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(Response);
        }
    }

    [Fact]
    public async Task GetBalanceAsync_ParsesSnakeCaseBalances()
    {
        var handler = new StubHandler();
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://nano-gpt.com/api/")
        };
        var options = Options.Create(new AgentsOptions
        {
            Llm = new AgentsLlmOptions
            {
                BaseUrl = "https://nano-gpt.com/api/v1",
                ApiKey = "secret-key"
            },
            Pricing = new AgentsPricingOptions()
        });
        var client = new NanoGptAccountClient(http, options);

        var balance = await client.GetBalanceAsync(CancellationToken.None);

        Assert.Equal(10.5m, balance.UsdBalance);
        Assert.Equal(2m, balance.NanoBalance);
        Assert.Equal("nano_x", balance.NanoDepositAddress);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("Bearer secret-key", handler.LastRequest.Headers.Authorization!.ToString());
    }

    [Fact]
    public async Task GetBalanceAsync_When401_ThrowsNanoGptAccountException()
    {
        var handler = new StubHandler
        {
            Response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"message\":\"bad key\"}")
            }
        };
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://nano-gpt.com/api/") };
        var options = Options.Create(new AgentsOptions
        {
            Llm = new AgentsLlmOptions { BaseUrl = "https://nano-gpt.com/api/v1", ApiKey = "x" },
            Pricing = new AgentsPricingOptions()
        });
        var client = new NanoGptAccountClient(http, options);

        var ex = await Assert.ThrowsAsync<NanoGptAccountException>(
            () => client.GetBalanceAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
    }
}
```

- [ ] **Step 2: Run — expect FAIL**

- [ ] **Step 3: Implement `NanoGptAccountClient`**

Keep private request/response DTO nested types (same style as `OpenAiCompatibleLlmClient`). Relative URIs without leading slash when `BaseAddress` ends with `/`.

- [ ] **Step 4: Run — expect PASS**

Optional: one more test for `CreateBtcLnDepositAsync` JSON body if time — recommended.

- [ ] **Step 5: Commit**

Message: `feat: implement NanoGptAccountClient HTTP adapter`

---

### Task 6: Endpoints, DI, integration tests

**Files:**
- Create: `backend/src/Areas/AgentForge.Areas.Agents/Http/BillingEndpoints.cs`
- Modify: `backend/src/Areas/AgentForge.Areas.Agents/Http/Requests.cs`
- Modify: `backend/src/Areas/AgentForge.Areas.Agents/Http/Responses.cs`
- Modify: `backend/src/Areas/AgentForge.Areas.Agents/AgentsArea.cs`
- Create: `backend/tests/AgentForge.Host.Integration/BillingEndpointTests.cs`
- Modify: `backend/tests/AgentForge.Host.Integration/AgentForgeFactory.cs` only if fake registration needs an override hook (prefer registering fake in `AgentsArea` for Testing/UseFake so factory works unchanged)

**Interfaces:**
- Consumes: `BillingService`, `ValidationFilter<>`
- Produces routes under `/billing`:
  - `GET /balance` → `BillingBalanceResponse`
  - `GET /usage`
  - `GET /deposits/limits`
  - `POST /deposits` + `CreateDepositRequest` with `[Range(double.Epsilon, double.MaxValue)]` or `[Range(0.00000001, double.MaxValue)]` on `Amount`
  - `GET /deposits/{txId}`

Response records (camelCase JSON):

```csharp
public sealed record BillingBalanceResponse(
    decimal UsdBalance,
    decimal NanoBalance,
    string? NanoDepositAddress,
    bool LowBalance,
    decimal LowBalanceThresholdUsd)
{
    public static BillingBalanceResponse From(BillingBalanceView view) =>
        new(view.UsdBalance, view.NanoBalance, view.NanoDepositAddress, view.LowBalance, view.LowBalanceThresholdUsd);
}

public sealed record CreateDepositRequest(
    [property: Range(0.00000001, double.MaxValue)] decimal Amount);

// Mirror NanoGptUsage / Deposit / Limits as HTTP records or reuse Runtime records if already JSON-friendly.
```

Prefer mapping to dedicated HTTP records for usage/deposit to avoid leaking Runtime types as API contracts — keep them thin 1:1.

**DI in `AgentsArea`:**
- Always `services.AddScoped<BillingService>();`
- Extend `RegisterLlmClient` pattern: new `RegisterAccountClient`:
  - if UseFake or Testing → `AddSingleton<INanoGptAccountClient, FakeNanoGptAccountClient>()`
  - else `AddHttpClient<INanoGptAccountClient, NanoGptAccountClient>` with BaseAddress = `NanoGptApiRoot.FromLlmBaseUrl(...) + "/"` and Timeout from `Llm.Timeout`
- `MapBillingEndpoints()` from `MapEndpoints`

**BillingEndpoints sketch:**

```csharp
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
                TypedResults.Ok(BillingUsageResponse.From(usage))));

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
```

Note: ASP.NET binds `group_by` query as-is; NanoGPT expects `group_by` — keep that name.

- [ ] **Step 1: Write integration tests**

```csharp
namespace AgentForge.Host.Integration;

public class BillingEndpointTests(AgentForgeFactory factory) : IClassFixture<AgentForgeFactory>
{
    [Fact]
    public async Task Balance_WhenRequested_ReturnsOkWithThreshold()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/agents/billing/balance",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var stream = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(doc.RootElement.TryGetProperty("usdBalance", out _));
        Assert.True(doc.RootElement.TryGetProperty("lowBalance", out _));
        Assert.Equal(5, doc.RootElement.GetProperty("lowBalanceThresholdUsd").GetDecimal());
    }

    [Fact]
    public async Task Deposits_WhenCreated_CanBeFetched()
    {
        using var client = factory.CreateClient();
        var body = new { amount = 0.00002m };
        using var create = await client.PostAsJsonAsync(
            "/api/agents/billing/deposits",
            body,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        var txId = created.GetProperty("txId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(txId));

        using var get = await client.GetAsync(
            $"/api/agents/billing/deposits/{txId}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }

    [Fact]
    public async Task Usage_WhenRequested_ReturnsOk()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(
            "/api/agents/billing/usage",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DepositLimits_WhenRequested_ReturnsOk()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(
            "/api/agents/billing/deposits/limits",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run integration filter — expect FAIL**

Run: `dotnet test backend/tests/AgentForge.Host.Integration/AgentForge.Host.Integration.csproj --filter BillingEndpointTests`

- [ ] **Step 3: Implement endpoints, requests/responses, DI registration**

- [ ] **Step 4: Run unit + integration billing tests — expect PASS**

```cmd
dotnet test backend/tests/AgentForge.Areas.Agents.Unit/AgentForge.Areas.Agents.Unit.csproj --filter "FullyQualifiedName~Billing|FullyQualifiedName~NanoGpt"
dotnet test backend/tests/AgentForge.Host.Integration/AgentForge.Host.Integration.csproj --filter BillingEndpointTests
```

- [ ] **Step 5: Commit**

Message: `feat: expose operator NanoGPT billing endpoints`

---

### Task 7: README documentation

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Document config + routes**

Add under Agents configuration table:

| `Areas:Agents:Billing:LowBalanceUsdThreshold` | USD balance below this sets `lowBalance` on `GET /api/agents/billing/balance` |

Add a short paragraph:

Operator billing (host NanoGPT key): `GET /api/agents/billing/balance`, `GET .../usage`, `GET .../deposits/limits`, `POST .../deposits` (BTC-LN only), `GET .../deposits/{txId}`. Deposit create is rate-limited upstream (~10 / 10 min). Requires real NanoGPT when `UseFake` is false.

- [ ] **Step 2: Commit**

Message: `docs: document NanoGPT billing monitor endpoints`

---

## Spec coverage checklist

| Spec item | Task |
|---|---|
| Balance + `lowBalance` | 2, 4, 6 |
| Usage proxy | 3, 4, 6 |
| BTC-LN limits / create / status | 3, 4, 5, 6 |
| Error mapping 400/401→502/429/502 | 1, 4, 5 |
| Fake when UseFake/Testing | 3, 6 |
| Config threshold | 2, 6, 7 |
| No deposit SSE / other tickers / end-user | out of scope (no tasks) |
| API root derived from Llm BaseUrl | 2, 5, 6 |

## Placeholder / consistency self-check

- All method/type names aligned: `BillingBalanceView`, `INanoGptAccountClient`, `NanoGptAccountException`, ticker path `btc-ln`.
- No TBD/TODO left in steps.
- Commit steps Windows-friendly (`git add` + `git commit -F`).
