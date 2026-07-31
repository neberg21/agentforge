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
