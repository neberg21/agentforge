# Agent Definition Suggestions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prefill New agent with an unused German first name (Bogus `de`), and inject that same style of unique name into each Agent Builder session via a hidden System message while updating the builder prompt to stop asking for names.

**Architecture:** Shared `AgentSuggestionService` (Bogus + name-taken checks + suffix fallback) backs `GET /api/agents/definitions/suggestions` and `POST /api/agents/builder/session`. Builder session syncs the Agent Builder definition to current defaults, creates a conversation with an initial System seed message, and the UI hides System roles. No DB migration.

**Tech Stack:** .NET 10, Bogus 35.6.5, xUnit v3; React 19, TypeScript, Vite, Vitest.

**Spec:** `docs/superpowers/specs/2026-07-30-agent-definition-suggestions-design.md`

## Global Constraints

- Repo root: `C:\Users\NEWA002\source\repos\agentforge`.
- No C# primary constructors; do not inline object creation into method/ctor calls.
- No DB migration.
- Frontend under `frontend/`.
- Windows: no `.ps1`/`.sh`; commits via message file + `git commit -F`; English `feat:`/`test:`/`chore:`/`docs:`.
- After each task: commit only that task’s files.
- TDD: failing test → implement → pass → commit.
- UI copy: English.

## File Structure

**Backend — create**
- `backend/src/Areas/AgentForge.Areas.Agents/Application/IAgentNameCandidateSource.cs`
- `backend/src/Areas/AgentForge.Areas.Agents/Application/BogusGermanFirstNameSource.cs`
- `backend/src/Areas/AgentForge.Areas.Agents/Application/AgentSuggestionService.cs`
- `backend/tests/AgentForge.Areas.Agents.Unit/AgentSuggestionServiceTests.cs`

**Backend — modify**
- `backend/Directory.Packages.props` — `Bogus` 35.6.5
- `backend/src/Areas/AgentForge.Areas.Agents/AgentForge.Areas.Agents.csproj` — PackageReference Bogus
- `backend/src/Areas/AgentForge.Areas.Agents/Application/AgentService.cs` — public `IsNameTakenAsync`
- `backend/src/Areas/AgentForge.Areas.Agents/Application/AgentBuilderDefaults.cs` — prompt + system seed format helper
- `backend/src/Areas/AgentForge.Areas.Agents/Application/BuilderSessionService.cs` — sync prompt, suggest name, seed system message
- `backend/src/Areas/AgentForge.Areas.Agents/Application/ConversationService.cs` — optional initial system message on create; list excerpt skips System
- `backend/src/Areas/AgentForge.Areas.Agents/Http/Responses.cs` — `AgentSuggestionsResponse`
- `backend/src/Areas/AgentForge.Areas.Agents/Http/AgentEndpoints.cs` — `GET /definitions/suggestions`
- `backend/src/Areas/AgentForge.Areas.Agents/AgentsArea.cs` — register suggestion services
- `backend/tests/AgentForge.Areas.Agents.Unit/BuilderSessionServiceTests.cs`
- `backend/tests/AgentForge.Host.Integration/AgentEndpointTests.cs` (or new suggestions/builder integration tests)

**Frontend — modify**
- `frontend/src/areas/agents/types.ts` — `AgentSuggestionsDto`
- `frontend/src/areas/agents/api.ts` — `getAgentSuggestions`
- `frontend/src/areas/agents/AgentFormPage.tsx` — create-only prefill
- `frontend/src/areas/agents/ConversationPages.tsx` — hide System messages
- `frontend/src/__tests__/agentsApi.test.ts`

---

### Task 1: Shared name suggestion service (Bogus + uniqueness)

**Files:**
- Modify: `backend/Directory.Packages.props`
- Modify: `backend/src/Areas/AgentForge.Areas.Agents/AgentForge.Areas.Agents.csproj`
- Modify: `backend/src/Areas/AgentForge.Areas.Agents/Application/AgentService.cs`
- Create: `backend/src/Areas/AgentForge.Areas.Agents/Application/IAgentNameCandidateSource.cs`
- Create: `backend/src/Areas/AgentForge.Areas.Agents/Application/BogusGermanFirstNameSource.cs`
- Create: `backend/src/Areas/AgentForge.Areas.Agents/Application/AgentSuggestionService.cs`
- Create: `backend/tests/AgentForge.Areas.Agents.Unit/AgentSuggestionServiceTests.cs`
- Modify: `backend/src/Areas/AgentForge.Areas.Agents/AgentsArea.cs`

**Interfaces:**
- Consumes: `AgentService.IsNameTakenAsync(string name, CancellationToken ct) → Task<bool>`
- Produces:
  - `IAgentNameCandidateSource.NextFirstName() → string`
  - `BogusGermanFirstNameSource` — `new Faker("de").Person.FirstName` per call
  - `AgentSuggestionService.SuggestNameAsync(CancellationToken ct) → Task<string>`
  - Constants: `MaxRandomAttempts = 32`

- [ ] **Step 1: Add Bogus package references**

In `backend/Directory.Packages.props` add:

```xml
<PackageVersion Include="Bogus" Version="35.6.5" />
```

In `AgentForge.Areas.Agents.csproj` add:

```xml
<PackageReference Include="Bogus" />
```

- [ ] **Step 2: Write the failing unit tests**

Create `AgentSuggestionServiceTests.cs`:

```csharp
using AgentForge.Areas.Agents.Application;
using AgentForge.Areas.Agents.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentForge.Areas.Agents.Unit;

public class AgentSuggestionServiceTests
{
    private sealed class QueueNames : IAgentNameCandidateSource
    {
        private readonly Queue<string> _names;

        public QueueNames(params string[] names)
        {
            _names = new Queue<string>(names);
        }

        public string NextFirstName() => _names.Dequeue();
    }

    private static AgentDefinition Definition(string name) =>
        new(name, null, "prompt", "model", 0.5, 2048, 10, []);

    private static (
        AgentsDbContext Context,
        AgentService Agents,
        AgentSuggestionService Suggestions) NewServices(
        AgentsDatabase database,
        IClock clock,
        IAgentNameCandidateSource names)
    {
        var context = database.NewContext();
        var agents = new AgentService(context, database.CurrentUser, clock);
        var suggestions = new AgentSuggestionService(agents, names);
        return (context, agents, suggestions);
    }

    [Fact]
    public async Task SuggestNameAsync_WhenCandidateFree_ReturnsCandidate()
    {
        using var database = new AgentsDatabase();
        var names = new QueueNames("Lena");
        var (context, _, suggestions) = NewServices(database, TestClock.AtEpoch(), names);
        await using var _ = context;

        var name = await suggestions.SuggestNameAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Lena", name);
    }

    [Fact]
    public async Task SuggestNameAsync_WhenFirstTaken_ReturnsNextFree()
    {
        using var database = new AgentsDatabase();
        var clock = TestClock.AtEpoch();
        var names = new QueueNames("Lena", "Max");
        var (context, agents, suggestions) = NewServices(database, clock, names);
        await using var _ = context;
        await agents.CreateAsync(Definition("Lena"), TestContext.Current.CancellationToken);

        var name = await suggestions.SuggestNameAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Max", name);
    }

    [Fact]
    public async Task SuggestNameAsync_WhenRandomPoolExhausted_UsesNumericSuffix()
    {
        using var database = new AgentsDatabase();
        var clock = TestClock.AtEpoch();
        var taken = Enumerable.Repeat("Lena", AgentSuggestionService.MaxRandomAttempts).ToArray();
        var names = new QueueNames(taken);
        var (context, agents, suggestions) = NewServices(database, clock, names);
        await using var _ = context;
        await agents.CreateAsync(Definition("Lena"), TestContext.Current.CancellationToken);

        var name = await suggestions.SuggestNameAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Lena-2", name);
    }

    [Fact]
    public async Task SuggestNameAsync_WhenArchived_AllowsReuse()
    {
        using var database = new AgentsDatabase();
        var clock = TestClock.AtEpoch();
        var names = new QueueNames("Lena");
        var (context, agents, suggestions) = NewServices(database, clock, names);
        await using var _ = context;
        var created = await agents.CreateAsync(Definition("Lena"), TestContext.Current.CancellationToken);
        await agents.ArchiveAsync(created.Value!.Id, TestContext.Current.CancellationToken);

        var name = await suggestions.SuggestNameAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Lena", name);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

Run:

```cmd
dotnet test backend/tests/AgentForge.Areas.Agents.Unit/AgentForge.Areas.Agents.Unit.csproj --filter "FullyQualifiedName~AgentSuggestionServiceTests"
```

Expected: FAIL (types missing / compile errors).

- [ ] **Step 4: Implement minimal code**

Expose on `AgentService`:

```csharp
public Task<bool> IsNameTakenAsync(string name, CancellationToken ct) =>
    NameIsTakenAsync(name, null, ct);
```

Create `IAgentNameCandidateSource.cs`:

```csharp
namespace AgentForge.Areas.Agents.Application;

public interface IAgentNameCandidateSource
{
    string NextFirstName();
}
```

Create `BogusGermanFirstNameSource.cs`:

```csharp
using Bogus;

namespace AgentForge.Areas.Agents.Application;

public sealed class BogusGermanFirstNameSource : IAgentNameCandidateSource
{
    public string NextFirstName()
    {
        var faker = new Faker("de");
        return faker.Person.FirstName;
    }
}
```

Create `AgentSuggestionService.cs`:

```csharp
namespace AgentForge.Areas.Agents.Application;

public sealed class AgentSuggestionService
{
    public const int MaxRandomAttempts = 32;

    private readonly AgentService _agents;
    private readonly IAgentNameCandidateSource _names;

    public AgentSuggestionService(AgentService agents, IAgentNameCandidateSource names)
    {
        _agents = agents;
        _names = names;
    }

    public async Task<string> SuggestNameAsync(CancellationToken ct)
    {
        string? last = null;
        for (var attempt = 0; attempt < MaxRandomAttempts; attempt++)
        {
            var candidate = _names.NextFirstName();
            last = candidate;
            if (!await _agents.IsNameTakenAsync(candidate, ct))
            {
                return candidate;
            }
        }

        var baseName = last ?? _names.NextFirstName();
        var suffix = 2;
        while (true)
        {
            var candidate = $"{baseName}-{suffix}";
            if (!await _agents.IsNameTakenAsync(candidate, ct))
            {
                return candidate;
            }

            suffix++;
        }
    }
}
```

In `AgentsArea.cs` next to other scoped services:

```csharp
services.AddSingleton<IAgentNameCandidateSource, BogusGermanFirstNameSource>();
services.AddScoped<AgentSuggestionService>();
```

- [ ] **Step 5: Run tests to verify they pass**

Run the same `dotnet test` filter. Expected: PASS.

- [ ] **Step 6: Commit**

```cmd
(
echo test: add AgentSuggestionService for unique DE first names
) > %TEMP%\commitmsg.txt
git add backend/Directory.Packages.props backend/src/Areas/AgentForge.Areas.Agents/AgentForge.Areas.Agents.csproj backend/src/Areas/AgentForge.Areas.Agents/Application/AgentService.cs backend/src/Areas/AgentForge.Areas.Agents/Application/IAgentNameCandidateSource.cs backend/src/Areas/AgentForge.Areas.Agents/Application/BogusGermanFirstNameSource.cs backend/src/Areas/AgentForge.Areas.Agents/Application/AgentSuggestionService.cs backend/src/Areas/AgentForge.Areas.Agents/AgentsArea.cs backend/tests/AgentForge.Areas.Agents.Unit/AgentSuggestionServiceTests.cs
git commit -F %TEMP%\commitmsg.txt
del %TEMP%\commitmsg.txt
```

---

### Task 2: `GET /api/agents/definitions/suggestions`

**Files:**
- Modify: `backend/src/Areas/AgentForge.Areas.Agents/Http/Responses.cs`
- Modify: `backend/src/Areas/AgentForge.Areas.Agents/Http/AgentEndpoints.cs`
- Modify: `backend/tests/AgentForge.Host.Integration/AgentEndpointTests.cs`

**Interfaces:**
- Consumes: `AgentSuggestionService.SuggestNameAsync`
- Produces: `AgentSuggestionsResponse(string Name)` JSON `{ "name": "..." }`

- [ ] **Step 1: Write the failing integration test**

Add to `AgentEndpointTests.cs`:

```csharp
[Fact]
public async Task AgentSuggestions_WhenCalled_ReturnsUnusedName()
{
    var ct = TestContext.Current.CancellationToken;
    using var client = _factory.CreateClient();

    var suggestions = await client.GetFromJsonAsync<AgentSuggestionsResponse>(
        "/api/agents/definitions/suggestions",
        ct);

    Assert.False(string.IsNullOrWhiteSpace(suggestions!.Name));

    using var created = await client.PostAsJsonAsync(
        "/api/agents/definitions",
        ApiClient.NewAgent(suggestions.Name),
        ct);
    Assert.Equal(HttpStatusCode.Created, created.StatusCode);
}
```

If `AgentSuggestionsResponse` is internal to the area assembly, either reference it from integration tests the same way other response types are referenced, or use a local record `record AgentSuggestionsDto(string Name);` with case-insensitive JSON options already used by the host.

Check how integration tests import response types — mirror that. Prefer:

```csharp
using AgentForge.Areas.Agents.Http;
```

and the real `AgentSuggestionsResponse` record.

- [ ] **Step 2: Run test to verify it fails**

```cmd
dotnet test backend/tests/AgentForge.Host.Integration/AgentForge.Host.Integration.csproj --filter "FullyQualifiedName~AgentSuggestions_WhenCalled_ReturnsUnusedName"
```

Expected: FAIL (404 or missing type).

- [ ] **Step 3: Implement endpoint**

In `Responses.cs`:

```csharp
public sealed record AgentSuggestionsResponse(string Name)
{
    public static AgentSuggestionsResponse From(string name) => new(name);
}
```

In `AgentEndpoints.MapAgentEndpoints`, **before** `group.MapGet("/{id:guid}", ...)`, add:

```csharp
group.MapGet("/suggestions", async (AgentSuggestionService suggestions, CancellationToken ct) =>
{
    var name = await suggestions.SuggestNameAsync(ct);
    return TypedResults.Ok(AgentSuggestionsResponse.From(name));
});
```

- [ ] **Step 4: Run test to verify it passes**

Same filter. Expected: PASS.

- [ ] **Step 5: Commit**

```cmd
(
echo feat: expose GET agent definition suggestions
) > %TEMP%\commitmsg.txt
git add backend/src/Areas/AgentForge.Areas.Agents/Http/Responses.cs backend/src/Areas/AgentForge.Areas.Agents/Http/AgentEndpoints.cs backend/tests/AgentForge.Host.Integration/AgentEndpointTests.cs
git commit -F %TEMP%\commitmsg.txt
del %TEMP%\commitmsg.txt
```

---

### Task 3: Conversation create with initial System message

**Files:**
- Modify: `backend/src/Areas/AgentForge.Areas.Agents/Application/ConversationService.cs`
- Modify: `backend/tests/AgentForge.Areas.Agents.Unit/ConversationServiceTests.cs`

**Interfaces:**
- Consumes: existing `Conversation.AppendMessage`
- Produces: overload / optional `initialSystemMessage` on `CreateAsync` so builder can seed atomically with the conversation row

- [ ] **Step 1: Write the failing tests**

Add to `ConversationServiceTests.cs` using the existing `NewServices` helper:

```csharp
[Fact]
public async Task CreateAsync_WhenInitialSystemMessage_PersistsSystemMessage()
{
    using var database = new AgentsDatabase();
    var (context, conversations, agents, _) = NewServices(database, TestClock.AtEpoch());
    await using var _ = context;
    var leo = await agents.CreateAsync(Definition("leo"), TestContext.Current.CancellationToken);
    var ids = new[] { leo.Value!.Id };

    var created = await conversations.CreateAsync(
        "New agent",
        ids,
        "Suggested agent name for this session: Lena. Use this exact name...",
        TestContext.Current.CancellationToken);

    Assert.True(created.IsSuccess);
    var messages = await conversations.GetMessagesAsync(
        created.Value!.Id,
        TestContext.Current.CancellationToken);
    Assert.True(messages.IsSuccess);
    Assert.Single(messages.Value!);
    Assert.Equal(MessageRole.System, messages.Value[0].Role);
    Assert.Contains("Lena", messages.Value[0].Content);
}

[Fact]
public async Task ListAsync_WhenOnlySystemMessage_ExcerptIsNull()
{
    using var database = new AgentsDatabase();
    var (context, conversations, agents, _) = NewServices(database, TestClock.AtEpoch());
    await using var _ = context;
    var leo = await agents.CreateAsync(Definition("leo"), TestContext.Current.CancellationToken);
    var ids = new[] { leo.Value!.Id };

    await conversations.CreateAsync(
        "New agent",
        ids,
        "Suggested agent name for this session: Lena.",
        TestContext.Current.CancellationToken);

    var page = await conversations.ListAsync(
        PageRequest.From(0, 50),
        TestContext.Current.CancellationToken);

    Assert.Null(page.Items[0].LastMessageExcerpt);
}
```

- [ ] **Step 2: Run tests to verify they fail**

```cmd
dotnet test backend/tests/AgentForge.Areas.Agents.Unit/AgentForge.Areas.Agents.Unit.csproj --filter "FullyQualifiedName~CreateAsync_WhenInitialSystemMessage|FullyQualifiedName~ListAsync_WhenOnlySystemMessage"
```

Expected: FAIL (no matching overload / excerpt still set).

- [ ] **Step 3: Implement**

Change `CreateAsync` signature to:

```csharp
public async Task<Result<Conversation>> CreateAsync(
    string? title,
    IReadOnlyList<Guid> participantAgentIds,
    CancellationToken ct) =>
    await CreateAsync(title, participantAgentIds, initialSystemMessage: null, ct);

public async Task<Result<Conversation>> CreateAsync(
    string? title,
    IReadOnlyList<Guid> participantAgentIds,
    string? initialSystemMessage,
    CancellationToken ct)
{
    // existing participant load + Conversation.Create ...
    if (!string.IsNullOrWhiteSpace(initialSystemMessage))
    {
        var systemMessage = conversation.AppendMessage(
            MessageRole.System,
            initialSystemMessage.Trim(),
            _clock.UtcNow,
            senderAgentId: null,
            senderName: null,
            mentionsJson: null,
            toolCallsJson: null,
            toolCallId: null);
        _db.ConversationMessages.Add(systemMessage);
    }

    _db.Conversations.Add(conversation);
    await _db.SaveChangesAsync(ct);
    return conversation;
}
```

In `ListAsync` excerpt selection, skip System messages:

```csharp
var last = conversation.Messages
    .Where(message => message.Role != MessageRole.System)
    .OrderByDescending(message => message.Sequence)
    .FirstOrDefault();
```

Keep the existing 3-arg `CreateAsync` call sites compiling via the forwarding overload.

- [ ] **Step 4: Run tests to verify they pass**

Same filter + full conversation unit tests:

```cmd
dotnet test backend/tests/AgentForge.Areas.Agents.Unit/AgentForge.Areas.Agents.Unit.csproj --filter "FullyQualifiedName~ConversationServiceTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```cmd
(
echo feat: allow seeding a System message when creating conversations
) > %TEMP%\commitmsg.txt
git add backend/src/Areas/AgentForge.Areas.Agents/Application/ConversationService.cs backend/tests/AgentForge.Areas.Agents.Unit/ConversationServiceTests.cs
git commit -F %TEMP%\commitmsg.txt
del %TEMP%\commitmsg.txt
```

---

### Task 4: Builder prompt + session injects suggested name

**Files:**
- Modify: `backend/src/Areas/AgentForge.Areas.Agents/Application/AgentBuilderDefaults.cs`
- Modify: `backend/src/Areas/AgentForge.Areas.Agents/Application/BuilderSessionService.cs`
- Modify: `backend/tests/AgentForge.Areas.Agents.Unit/BuilderSessionServiceTests.cs`

**Interfaces:**
- Consumes: `AgentSuggestionService.SuggestNameAsync`, `ConversationService.CreateAsync(..., initialSystemMessage, ct)`, `AgentService.UpdateAsync`
- Produces: session still `BuilderSession(ConversationId, BuilderAgentId)`; system seed text via `AgentBuilderDefaults.FormatSuggestedNameMessage(string name)`

- [ ] **Step 1: Write the failing builder tests**

Update `NewServices` to construct `AgentSuggestionService` (use `BogusGermanFirstNameSource` or a `QueueNames` if asserting exact name — prefer Bogus for “contains Suggested agent name” assertion).

Add/adjust tests:

```csharp
[Fact]
public async Task StartAsync_WhenCalled_SeedsSystemMessageWithSuggestedName()
{
    using var database = new AgentsDatabase();
    var (context, builder, _, conversations) = NewServices(database, TestClock.AtEpoch());
    await using var _ = context;

    var result = await builder.StartAsync(TestContext.Current.CancellationToken);

    Assert.True(result.IsSuccess);
    var messages = await conversations.GetMessagesAsync(
        result.Value!.ConversationId,
        TestContext.Current.CancellationToken);
    Assert.True(messages.IsSuccess);
    var system = Assert.Single(messages.Value!);
    Assert.Equal(MessageRole.System, system.Role);
    Assert.StartsWith("Suggested agent name for this session:", system.Content);
}

[Fact]
public async Task StartAsync_WhenBuilderExistsWithOldPrompt_UpdatesSystemPrompt()
{
    using var database = new AgentsDatabase();
    var (context, builder, agents, _) = NewServices(database, TestClock.AtEpoch());
    await using var _ = context;

    var stale = new AgentDefinition(
        AgentBuilderDefaults.Name,
        "old",
        "OLD PROMPT THAT ASKS FOR A NAME",
        AgentBuilderDefaults.Model,
        Agent.DefaultTemperature,
        Agent.DefaultMaxOutputTokens,
        Agent.DefaultMaxTurns,
        []);
    var created = await agents.CreateAsync(stale, TestContext.Current.CancellationToken);

    var result = await builder.StartAsync(TestContext.Current.CancellationToken);

    Assert.True(result.IsSuccess);
    var reloaded = await agents.GetAsync(created.Value!.Id, TestContext.Current.CancellationToken);
    Assert.Equal(AgentBuilderDefaults.SystemPrompt, reloaded.Value!.SystemPrompt);
    Assert.DoesNotContain("Cover essentials first: name", reloaded.Value.SystemPrompt);
}
```

Update `NewServices` return type to include `ConversationService` and wire:

```csharp
var nameSource = new BogusGermanFirstNameSource();
var suggestions = new AgentSuggestionService(agents, nameSource);
var builder = new BuilderSessionService(agents, conversations, suggestions);
```

- [ ] **Step 2: Run tests to verify they fail**

```cmd
dotnet test backend/tests/AgentForge.Areas.Agents.Unit/AgentForge.Areas.Agents.Unit.csproj --filter "FullyQualifiedName~BuilderSessionServiceTests"
```

Expected: FAIL (no system message / prompt not updated).

- [ ] **Step 3: Implement defaults + session**

In `AgentBuilderDefaults.cs` replace `SystemPrompt` with text that:

- Interviews purpose/description and system-prompt behavior (not name).
- Instructs: do not ask for a name; use the session suggested name from system context for `agent-draft.name`; only change if the user explicitly chooses another.
- Keeps the `agent-draft` fence rules and “never claim it exists” rule.

Add:

```csharp
public static string FormatSuggestedNameMessage(string name) =>
    $"Suggested agent name for this session: {name}. Use this exact name in the agent-draft \"name\" field unless the user explicitly chooses a different name.";
```

Rewrite `BuilderSessionService`:

```csharp
public sealed class BuilderSessionService
{
    private readonly AgentService _agents;
    private readonly ConversationService _conversations;
    private readonly AgentSuggestionService _suggestions;

    public BuilderSessionService(
        AgentService agents,
        ConversationService conversations,
        AgentSuggestionService suggestions)
    {
        _agents = agents;
        _conversations = conversations;
        _suggestions = suggestions;
    }

    public async Task<Result<BuilderSession>> StartAsync(CancellationToken ct)
    {
        var existing = await _agents.FindActiveByNameAsync(AgentBuilderDefaults.Name, ct);
        Agent builder;
        if (existing is null)
        {
            var created = await _agents.CreateAsync(AgentBuilderDefaults.Definition, ct);
            if (!created.IsSuccess)
            {
                return created.Error!.Value;
            }

            builder = created.Value!;
        }
        else
        {
            var updated = await _agents.UpdateAsync(
                existing.Id,
                AgentBuilderDefaults.Definition,
                existing.ConcurrencyToken,
                ct);
            if (!updated.IsSuccess)
            {
                return updated.Error!.Value;
            }

            builder = updated.Value!;
        }

        var suggestedName = await _suggestions.SuggestNameAsync(ct);
        var systemMessage = AgentBuilderDefaults.FormatSuggestedNameMessage(suggestedName);

        var participantIds = new[] { builder.Id };
        var conversation = await _conversations.CreateAsync(
            AgentBuilderDefaults.ConversationTitle,
            participantIds,
            systemMessage,
            ct);
        if (!conversation.IsSuccess)
        {
            return conversation.Error!.Value;
        }

        var session = new BuilderSession(conversation.Value!.Id, builder.Id);
        return session;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```cmd
dotnet test backend/tests/AgentForge.Areas.Agents.Unit/AgentForge.Areas.Agents.Unit.csproj --filter "FullyQualifiedName~BuilderSessionServiceTests"
```

Expected: PASS (including existing reuse/archive tests).

- [ ] **Step 5: Commit**

```cmd
(
echo feat: inject suggested agent name into builder sessions
) > %TEMP%\commitmsg.txt
git add backend/src/Areas/AgentForge.Areas.Agents/Application/AgentBuilderDefaults.cs backend/src/Areas/AgentForge.Areas.Agents/Application/BuilderSessionService.cs backend/tests/AgentForge.Areas.Agents.Unit/BuilderSessionServiceTests.cs
git commit -F %TEMP%\commitmsg.txt
del %TEMP%\commitmsg.txt
```

---

### Task 5: Frontend suggestions client + New agent prefill

**Files:**
- Modify: `frontend/src/areas/agents/types.ts`
- Modify: `frontend/src/areas/agents/api.ts`
- Modify: `frontend/src/areas/agents/AgentFormPage.tsx`
- Modify: `frontend/src/__tests__/agentsApi.test.ts`

**Interfaces:**
- Consumes: `GET /api/agents/definitions/suggestions`
- Produces: `getAgentSuggestions(): Promise<AgentSuggestionsDto>` where `AgentSuggestionsDto = { name: string }`

- [ ] **Step 1: Write the failing API client test**

Add to `agentsApi.test.ts`:

```ts
import { getAgentSuggestions } from '../areas/agents/api'

it('getAgentSuggestions gets definitions suggestions', async () => {
  const fetchMock = vi.fn(
    async () =>
      new Response(JSON.stringify({ name: 'Lena' }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
  )
  vi.stubGlobal('fetch', fetchMock)
  const result = await getAgentSuggestions()
  expect(result.name).toBe('Lena')
  const [url, init] = lastFetchCall(fetchMock)
  expect(String(url)).toBe('/api/agents/definitions/suggestions')
  expect(init?.method ?? 'GET').toMatch(/GET/i)
})
```

- [ ] **Step 2: Run test to verify it fails**

```cmd
cd frontend
npm test -- --run src/__tests__/agentsApi.test.ts
```

Expected: FAIL (`getAgentSuggestions` missing).

- [ ] **Step 3: Implement client + form prefill**

In `types.ts`:

```ts
export type AgentSuggestionsDto = {
  name: string
}
```

In `api.ts`:

```ts
export function getAgentSuggestions(): Promise<AgentSuggestionsDto> {
  return apiGet(`${definitions}/suggestions`)
}
```

In `AgentFormPage.tsx`, import `getAgentSuggestions`. Add create-only effect:

```ts
useEffect(() => {
  if (editing) {
    return
  }
  let cancelled = false
  void getAgentSuggestions()
    .then((suggestions) => {
      if (cancelled) {
        return
      }
      setForm((current) => {
        if (current.name.trim() !== '') {
          return current
        }
        return { ...current, name: suggestions.name }
      })
    })
    .catch(() => {
      // leave name empty
    })
  return () => {
    cancelled = true
  }
}, [editing])
```

Do not call suggestions when `editing` is true.

- [ ] **Step 4: Run tests to verify they pass**

```cmd
cd frontend
npm test -- --run src/__tests__/agentsApi.test.ts
```

Expected: PASS.

- [ ] **Step 5: Commit**

```cmd
(
echo feat: prefill New agent name from suggestions API
) > %TEMP%\commitmsg.txt
git add frontend/src/areas/agents/types.ts frontend/src/areas/agents/api.ts frontend/src/areas/agents/AgentFormPage.tsx frontend/src/__tests__/agentsApi.test.ts
git commit -F %TEMP%\commitmsg.txt
del %TEMP%\commitmsg.txt
```

---

### Task 6: Hide System messages in conversation transcript

**Files:**
- Modify: `frontend/src/areas/agents/ConversationPages.tsx`

**Interfaces:**
- Consumes: transcript messages with `role: string`
- Produces: render path skips `role === 'System'`

- [ ] **Step 1: Locate render loop**

In `ConversationPages.tsx`, the transcript maps `messagesInOrder(state)`. Filter before map:

```tsx
{messagesInOrder(state)
  .filter((message) => message.role !== 'System')
  .map((message) => {
    // existing card render unchanged
  })}
```

If roles are typed as a union, use the same casing the API returns (`System` — confirm against `ConversationMessageDto` / existing comparisons like `'Assistant'`).

- [ ] **Step 2: Manual sanity (no dedicated UI test required)**

Optional: if a small pure helper already exists for transcript filtering, unit-test it; otherwise the filter in JSX is enough for v1.

- [ ] **Step 3: Commit**

```cmd
(
echo feat: hide System messages in conversation transcript
) > %TEMP%\commitmsg.txt
git add frontend/src/areas/agents/ConversationPages.tsx
git commit -F %TEMP%\commitmsg.txt
del %TEMP%\commitmsg.txt
```

---

### Task 7: Verification sweep

**Files:** none new

- [ ] **Step 1: Run backend unit + integration filters**

```cmd
dotnet test backend/tests/AgentForge.Areas.Agents.Unit/AgentForge.Areas.Agents.Unit.csproj --filter "FullyQualifiedName~AgentSuggestionServiceTests|FullyQualifiedName~BuilderSessionServiceTests|FullyQualifiedName~ConversationServiceTests"
dotnet test backend/tests/AgentForge.Host.Integration/AgentForge.Host.Integration.csproj --filter "FullyQualifiedName~AgentSuggestions_WhenCalled_ReturnsUnusedName"
```

Expected: all PASS.

- [ ] **Step 2: Run frontend tests**

```cmd
cd frontend
npm test -- --run src/__tests__/agentsApi.test.ts
```

Expected: PASS.

- [ ] **Step 3: Manual acceptance checklist**

1. Open New agent → name prefilled with DE first name; editable.
2. Create agent → archive it → New agent again → create succeeds (name may reuse).
3. Edit agent → name not overwritten by suggestions.
4. Create with assistant → no visible system blob; builder should not ask for a name (spot-check prompt text / first LLM turn when LLM configured).
5. Second builder session after prompt deploy uses updated `AgentBuilderDefaults.SystemPrompt` without archiving Agent Builder.

- [ ] **Step 4: Commit only if verification fixed stray files; otherwise done**

No commit if tree clean.

---

## Spec coverage self-review

| Spec requirement | Task |
|---|---|
| Bogus `de` Person.FirstName + uniqueness + suffix | Task 1 |
| `GET .../suggestions` → `{ name }` | Task 2 |
| Form prefill create-only, empty-field guard, soft fail | Task 5 |
| Builder session generate + System seed | Task 4 (+ Task 3) |
| Builder prompt: no name interview | Task 4 |
| Sync builder prompt on session start | Task 4 |
| Hide System in transcript | Task 6 |
| List excerpt not leaking system seed | Task 3 |
| Archive frees name | Task 1 tests |
| No `suggestedName` on session response | Task 4 (unchanged DTO) |
| No DB migration | all tasks |

## Placeholder scan

No TBD/TODO left in task steps. Task 3 tests use the existing `NewServices` helper in `ConversationServiceTests.cs`.
