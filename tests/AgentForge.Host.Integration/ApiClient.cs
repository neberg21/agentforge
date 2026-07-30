namespace AgentForge.Host.Integration;

public static class ApiClient
{
    public static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response, CancellationToken ct)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    public static CreateAgentRequest NewAgent(string name = "Builder") =>
        new(name, "Baut Dinge.", "Du bist hilfreich.", "some-model", 0.5, 2048, 10, ["read_file"]);

    public static async Task<AgentResponse> CreateAgentAsync(HttpClient client, string name, CancellationToken ct)
    {
        using var response = await client.PostAsJsonAsync("/api/agents/definitions", NewAgent(name), ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AgentResponse>(ct))!;
    }
}
