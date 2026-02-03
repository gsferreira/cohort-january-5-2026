# Workshop Step 053: Agentic Recommendation System with Tool Calling

## Mission

In this step, you'll transform the recommendation engine from a batch-analysis system into an autonomous AI agent that uses function calling to explore transaction data dynamically. Instead of dumping all pattern data into a single AI prompt, the agent will decide which tools to use, execute queries, and reason through findings to generate more targeted recommendations.

**Your goal**: Implement an agentic recommendation system that uses function calling with tools (SearchTransactions), enabling multi-turn reasoning and autonomous decision-making.

**Learning Objectives**:
- Understanding AI function calling and tool execution patterns
- Building autonomous agentic systems with multi-turn reasoning
- Using `AIFunctionFactory.Create` with typed parameters and `[Description]` attributes
- Integrating function calling with Microsoft.Extensions.AI
- Creating explainable AI recommendations with tool call chains
- Designing agent loops with iteration limits and completion detection

---

## Prerequisites

Before starting, ensure you completed:
- [051-recommendation-agent.md](051-recommendation-agent.md) - Recommendation Agent Backend
- [052-recommendation-agent-ui.md](052-recommendation-agent-ui.md) - Recommendation Agent UI

---

## Background: Batch Analysis vs Agentic Approach

### Current System (Simple AI Recommendations)

The current recommendation system (from Step 051) uses a simple, single-pass approach:

1. **Basic Statistics**: Gathers high-level stats via `GetBasicStatsAsync()` (total income, expenses, top categories)
2. **Single Prompt**: Sends summary statistics to AI in one large prompt
3. **Single AI Call**: Gets back 3-5 general recommendations in one shot via `GenerateSimpleRecommendationsAsync()`
4. **No Exploration**: AI cannot query specific transactions or dig deeper into patterns

**Limitations:**
- AI only sees summary statistics, not actual transactions
- No targeted investigation of specific spending patterns
- Cannot adapt analysis based on discoveries
- Limited explainability (can't see AI's reasoning process)
- Recommendations are general, not evidence-based

### New System (Agentic with Tool Calling)

The agentic system uses function calling for dynamic exploration:

1. **Initial Assessment**: Agent gets high-level context about the user
2. **Tool Discovery**: Agent decides which tools to call based on what it wants to investigate
3. **Multi-Turn Execution**: Agent calls tools over multiple iterations
4. **Adaptive Analysis**: Agent adjusts investigation based on tool results
5. **Recommendation Generation**: Agent synthesizes findings into specific recommendations

**Benefits:**
- Only queries data that's needed (efficient)
- Targeted investigations (e.g., "search for subscriptions")
- Autonomous decision-making (agent chooses tools)
- Explainable (can trace tool call chain)
- Extensible (easy to add new tools)

---

## Step 53.1: Create Agent Context for User Injection

*Create an abstraction for injecting user context into tools.*

When tools execute, they need access to the current user's ID. Instead of passing `userId` as a parameter to every tool method (which would expose it in the AI's schema), we use a scoped context that gets set before tool execution.

Create `src/BudgetTracker.Api/Features/Intelligence/Tools/IAgentContext.cs`:

```csharp
namespace BudgetTracker.Api.Features.Intelligence.Tools;

public interface IAgentContext
{
    string UserId { get; }
}

public class AgentContext : IAgentContext
{
    public string UserId { get; set; } = string.Empty;
}
```

**Key Design:**
- `IAgentContext` provides read-only access to the user ID
- `AgentContext` is the mutable implementation that agents set before tool execution
- Registered as scoped so each request gets its own instance
- Tools inject `IAgentContext` to access the current user

---

## Step 53.2: Implement SearchTransactions Tool

*Create the search tool using typed parameters with `[Description]` attributes.*

This tool enables the agent to discover transactions using natural language queries. Instead of manually defining a JSON schema, we use `[Description]` attributes on method parameters — the `AIFunctionFactory` automatically generates the schema from the method signature.

Create `src/BudgetTracker.Api/Features/Intelligence/Tools/SearchTransactionsTool.cs`:

```csharp
using System.ComponentModel;
using BudgetTracker.Api.Features.Intelligence.Search;

namespace BudgetTracker.Api.Features.Intelligence.Tools;

public class SearchTransactionsTool
{
    private readonly ISemanticSearchService _searchService;
    private readonly IAgentContext _agentContext;
    private readonly ILogger<SearchTransactionsTool> _logger;

    public SearchTransactionsTool(
        ISemanticSearchService searchService,
        IAgentContext agentContext,
        ILogger<SearchTransactionsTool> logger)
    {
        _searchService = searchService;
        _agentContext = agentContext;
        _logger = logger;
    }

    [Description("Search transactions using semantic search. Use this to find specific patterns, merchants, " +
                 "or transaction types. Examples: 'subscriptions', 'coffee shops', 'shopping', " +
                 "'dining'. Returns up to maxResults transactions with descriptions and amounts.")]
    public async Task<TransactionSearchResult> SearchTransactionsAsync(
        [Description("Natural language search query describing what transactions to find")]
        string query,
        [Description("Maximum number of results to return (default: 10, max: 20)")]
        int maxResults = 10)
    {
        _logger.LogInformation("SearchTransactions called: query={Query}, maxResults={MaxResults}",
            query, maxResults);

        maxResults = Math.Min(maxResults, 20);

        var results = await _searchService.FindRelevantTransactionsAsync(
            query, _agentContext.UserId, maxResults);

        if (!results.Any())
        {
            return new TransactionSearchResult
            {
                Success = true,
                Count = 0,
                Message = "No transactions found matching the query.",
                Query = query,
                Transactions = []
            };
        }

        var transactions = results.Select(t => new TransactionSearchItem
        {
            Id = t.Id,
            Date = t.Date.ToString("yyyy-MM-dd"),
            Description = t.Description,
            Amount = t.Amount,
            Category = t.Category,
            Account = t.Account
        }).ToList();

        return new TransactionSearchResult
        {
            Success = true,
            Count = transactions.Count,
            Query = query,
            Transactions = transactions
        };
    }
}

public class TransactionSearchResult
{
    public bool Success { get; init; }
    public int Count { get; init; }
    public string? Message { get; init; }
    public string Query { get; init; } = string.Empty;
    public List<TransactionSearchItem> Transactions { get; init; } = [];
}

public class TransactionSearchItem
{
    public Guid Id { get; init; }
    public string Date { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string? Category { get; init; }
    public string? Account { get; init; }
}
```

**Key Points:**
- **No interface**: Just a plain class with a method decorated with `[Description]`
- **Typed parameters**: `string query` and `int maxResults` with default value
- **`[Description]` attributes**: On both the method and parameters — these become the AI's schema
- **Returns typed result**: `TransactionSearchResult` instead of JSON string
- **Injects `IAgentContext`**: Gets userId from scoped context, not as a parameter
- Uses existing `ISemanticSearchService` for semantic search

---

## Step 53.3: Create Tool Registry

*Build a registry that uses `AIFunctionFactory.Create` with the typed method.*

The tool registry provides a centralized way to access tools and convert them to the format required by Microsoft.Extensions.AI.

Create `src/BudgetTracker.Api/Features/Intelligence/Tools/ToolRegistry.cs`:

```csharp
using Microsoft.Extensions.AI;

namespace BudgetTracker.Api.Features.Intelligence.Tools;

public interface IToolRegistry
{
    IList<AITool> GetTools();
}

public class ToolRegistry : IToolRegistry
{
    private readonly SearchTransactionsTool _searchTransactionsTool;

    public ToolRegistry(SearchTransactionsTool searchTransactionsTool)
    {
        _searchTransactionsTool = searchTransactionsTool;
    }

    public IList<AITool> GetTools()
    {
        return
        [
            AIFunctionFactory.Create(
                _searchTransactionsTool.SearchTransactionsAsync,
                name: "SearchTransactions")
        ];
    }
}
```

**Key Design:**
- **Simple interface**: Just `GetTools()` that returns AI tools
- **Direct tool injection**: `SearchTransactionsTool` injected via constructor
- **`AIFunctionFactory.Create`**: Wraps the typed method, automatically generating schema from `[Description]` attributes
- **Explicit name**: Passed to `AIFunctionFactory.Create` to control the tool name

---

## Step 53.4: Use IChatClient for Tool Support

*Leverage the existing IChatClient from Microsoft.Extensions.AI for tool calling.*

The `IChatClient` interface from Microsoft.Extensions.AI already supports tools via `ChatOptions`. We'll use it directly in the agent rather than creating a custom abstraction.

The `IChatClient` is already registered in your application (from Week 2/3). It provides:

```csharp
public interface IChatClient
{
    Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

**Key Points:**
- `IChatClient` is the standard Microsoft.Extensions.AI interface
- `ChatOptions` includes `Tools` property for passing AI tools
- `ChatResponse` contains the completion result with tool call information
- No need for custom `IAzureChatService` - use `IChatClient` directly

---

## Step 53.5: Evolve RecommendationAgent with Agent Logic

*Add agent capabilities directly to the existing RecommendationAgent.*

Instead of creating a separate agent class, we'll incorporate the agentic workflow directly into `RecommendationAgent.GenerateRecommendationsAsync()`. The key change is injecting `IServiceProvider` to create a scope and set the user context before tool execution.

**Update `src/BudgetTracker.Api/Features/Intelligence/Recommendations/RecommendationAgent.cs`:**

Add the required dependencies:

```csharp
using System.Text.Json;
using Microsoft.Extensions.AI;
using BudgetTracker.Api.Features.Intelligence.Tools;
using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Intelligence.Recommendations;

internal class GeneratedRecommendation
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public RecommendationType Type { get; set; }
    public RecommendationPriority Priority { get; set; }
}

public class RecommendationAgent : IRecommendationRepository
{
    private readonly BudgetTrackerContext _context;
    private readonly IChatClient _chatClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RecommendationAgent> _logger;

    public RecommendationAgent(
        BudgetTrackerContext context,
        IChatClient chatClient,
        IServiceProvider serviceProvider,
        ILogger<RecommendationAgent> logger)
    {
        _context = context;
        _chatClient = chatClient;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
```

Update the `GenerateRecommendationsAsync` method:

```csharp
public async Task GenerateRecommendationsAsync(string userId)
{
        // ...

        // Run agentic recommendation generation
        var recommendations = await GenerateAgenticRecommendationsAsync(userId, maxIterations: 5);

        if (!recommendations.Any())
        {
            _logger.LogInformation("Agent generated no recommendations for {UserId}", userId);
            return;
        }

        // ...
}
```

Add the agentic generation method:

```csharp
private async Task<List<GeneratedRecommendation>> GenerateAgenticRecommendationsAsync(
    string userId,
    int maxIterations)
{
    // Create a scope to set up the agent context for this user
    using var scope = _serviceProvider.CreateScope();
    var agentContext = scope.ServiceProvider.GetRequiredService<AgentContext>();
    agentContext.UserId = userId;

    var toolRegistry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();

    // Initialize conversation with Microsoft.Extensions.AI ChatMessage
    var messages = new List<ChatMessage>
    {
        new(ChatRole.System, CreateSystemPrompt()),
        new(ChatRole.User, CreateInitialUserPrompt())
    };

    // Prepare tools and options
    var tools = toolRegistry.GetTools();
    var toolsByName = tools.OfType<AIFunction>().ToDictionary(t => t.Name);
    var options = new ChatOptions { Tools = tools };

    _logger.LogInformation("Agent started for user {UserId}", userId);

    // Multi-turn agent loop
    var iteration = 0;
    while (iteration < maxIterations)
    {
        iteration++;
        _logger.LogInformation("Agent iteration {Iteration}/{Max} for user {UserId}",
            iteration, maxIterations, userId);

        var response = await _chatClient.GetResponseAsync(messages, options);

        // Add assistant's response to conversation
        messages.AddMessages(response);

        // Check for tool calls in the response
        var toolCalls = response.Messages[0].Contents
            .OfType<FunctionCallContent>()
            .ToList();

        if (toolCalls.Count > 0)
        {
            // Execute tools and add results
            await ExecuteToolCallsAsync(messages, toolCalls, toolsByName);
        }
        else if (response.FinishReason == ChatFinishReason.Stop)
        {
            // Model completed - extract recommendations
            _logger.LogInformation("Agent completed after {Iterations} iterations", iteration);
            return ExtractRecommendations(response);
        }
        else if (response.FinishReason == ChatFinishReason.Length)
        {
            _logger.LogWarning("Max tokens reached at iteration {Iteration}", iteration);
            break;
        }
        else if (response.FinishReason == ChatFinishReason.ContentFilter)
        {
            _logger.LogWarning("Content filtered at iteration {Iteration}", iteration);
            return new List<GeneratedRecommendation>();
        }
    }

    _logger.LogWarning("Agent reached max iterations ({MaxIterations}) without completion",
        maxIterations);
    return new List<GeneratedRecommendation>();
}
```

**Why Create a Scope?**

The key pattern here is creating a DI scope and setting `AgentContext.UserId` before getting the tools:
- `AgentContext` is scoped, so each scope gets its own instance
- Setting `UserId` on the scoped context makes it available to all tools in that scope
- Tools inject `IAgentContext` and read `UserId` without it appearing in their method signatures

Add the tool execution method:

```csharp
private async Task ExecuteToolCallsAsync(
    List<ChatMessage> messages,
    List<FunctionCallContent> toolCalls,
    Dictionary<string, AIFunction> toolsByName)
{
    _logger.LogInformation("Executing {Count} tool call(s)", toolCalls.Count);

    foreach (var toolCall in toolCalls)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            if (!toolsByName.TryGetValue(toolCall.Name, out var tool))
            {
                _logger.LogWarning("Tool not found: {ToolName}", toolCall.Name);
                messages.Add(new ChatMessage(ChatRole.Tool, [
                    new FunctionResultContent(toolCall.CallId,
                        JsonSerializer.Serialize(new { error = "Tool not found" }))
                ]));
                continue;
            }

            var arguments = toolCall.Arguments is not null
                ? new AIFunctionArguments(toolCall.Arguments)
                : null;
            var result = await tool.InvokeAsync(arguments);

            stopwatch.Stop();

            // Add tool result to conversation
            messages.Add(new ChatMessage(ChatRole.Tool, [
                new FunctionResultContent(toolCall.CallId, result)
            ]));

            _logger.LogInformation("Tool {ToolName} executed in {Duration}ms",
                tool.Name, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error executing tool {ToolName}", toolCall.Name);

            messages.Add(new ChatMessage(ChatRole.Tool, [
                new FunctionResultContent(toolCall.CallId,
                    JsonSerializer.Serialize(new { error = ex.Message }))
            ]));
        }
    }
}
```

**Key Difference from Manual Pattern:**

Instead of looking up a tool by name and calling `ExecuteAsync(userId, arguments)`, we use:
- `toolsByName.TryGetValue()` to get the `AIFunction`
- `tool.InvokeAsync(arguments)` to execute it directly
- The tool internally accesses `IAgentContext.UserId`

Add helper methods for prompts and parsing:

```csharp
private static string CreateSystemPrompt()
{
    return """
        You are an autonomous financial analysis agent with access to transaction data tools.

        Your goal is to investigate spending patterns and generate 3-5 highly specific, actionable recommendations.

        AVAILABLE TOOLS:
        - SearchTransactions: Find transactions using natural language queries

        ANALYSIS STRATEGY:
        1. Start with exploratory searches to discover patterns
        2. Look for recurring charges, subscriptions, and spending categories
        3. Identify behavioral patterns and opportunities
        4. Focus on the most impactful findings

        RECOMMENDATION CRITERIA:
        - SPECIFIC: Include exact merchants, dates, and patterns found
        - ACTIONABLE: Clear next steps the user can take
        - IMPACTFUL: Focus on changes that make a real difference
        - EVIDENCE-BASED: Reference the specific transactions you found

        CRITICAL OUTPUT FORMAT:
        When you've completed your analysis (after 2-4 tool calls), respond with ONLY a JSON object.
        Do NOT include any text before or after the JSON. Do NOT wrap in markdown code blocks.
        Respond with this exact JSON structure and nothing else:

        {"recommendations":[{"title":"Brief title","message":"Specific recommendation","type":"SpendingAlert|SavingsOpportunity|BehavioralInsight|BudgetWarning","priority":"Low|Medium|High|Critical"}]}

        Use the search tool to explore before making recommendations.
        """;
}

private static string CreateInitialUserPrompt()
{
    return """
        Analyze this user's transaction data to generate proactive financial recommendations.

        Use the SearchTransactions tool to investigate:
        1. Recurring charges and subscriptions
        2. Frequent spending patterns
        3. Unusual or concerning transactions
        4. Optimization opportunities

        Make 2-4 targeted searches, then provide 3-5 specific recommendations based on what you find.
        """;
}

private List<GeneratedRecommendation> ExtractRecommendations(ChatResponse response)
{
    var textContent = response.Messages[0].Contents
        .OfType<TextContent>()
        .FirstOrDefault();

    if (textContent == null)
    {
        _logger.LogWarning("No text content in final message");
        return new List<GeneratedRecommendation>();
    }

    try
    {
        return ParseRecommendations(textContent.Text.ExtractJsonFromCodeBlock());
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to parse recommendations from agent output");
        return new List<GeneratedRecommendation>();
    }
}

private List<GeneratedRecommendation> ParseRecommendations(string content)
{
    try
    {
        var jsonResponse = JsonSerializer.Deserialize<JsonElement>(content);
        var recommendations = new List<GeneratedRecommendation>();

        if (jsonResponse.TryGetProperty("recommendations", out var recsArray))
        {
            foreach (var rec in recsArray.EnumerateArray())
            {
                if (rec.TryGetProperty("title", out var title) &&
                    rec.TryGetProperty("message", out var message) &&
                    rec.TryGetProperty("type", out var type) &&
                    rec.TryGetProperty("priority", out var priority))
                {
                    recommendations.Add(new GeneratedRecommendation
                    {
                        Title = title.GetString() ?? "",
                        Message = message.GetString() ?? "",
                        Type = Enum.TryParse<RecommendationType>(type.GetString(), out var t)
                            ? t : RecommendationType.BehavioralInsight,
                        Priority = Enum.TryParse<RecommendationPriority>(priority.GetString(), out var p)
                            ? p : RecommendationPriority.Medium
                    });
                }
            }
        }

        return recommendations.Take(5).ToList();
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to parse recommendations");
        return new List<GeneratedRecommendation>();
    }
}
```

Update `StoreRecommendationsAsync`:

```csharp
private async Task StoreRecommendationsAsync(
    string userId,
    List<GeneratedRecommendation> aiRecommendations)
{
    if (!aiRecommendations.Any()) return;

    // Expire old active recommendations
    var oldRecommendations = await _context.Recommendations
        .Where(r => r.UserId == userId && r.Status == RecommendationStatus.Active)
        .ToListAsync();

    foreach (var old in oldRecommendations)
    {
        old.Status = RecommendationStatus.Expired;
    }

    // Add new recommendations
    var newRecommendations = aiRecommendations.Select(ai => new Recommendation
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Title = ai.Title,
        Message = ai.Message,
        Type = ai.Type,
        Priority = ai.Priority,
        GeneratedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddDays(7),
        Status = RecommendationStatus.Active
    }).ToList();

    await _context.Recommendations.AddRangeAsync(newRecommendations);
    await _context.SaveChangesAsync();
}
```

**Key Points:**
- Injects `IServiceProvider` instead of `IToolRegistry` directly
- Creates a scope and sets `AgentContext.UserId` before tool execution
- Uses `AIFunction.InvokeAsync()` for direct invocation
- Tool results are automatically serialized (no manual JSON needed)

---

## Step 53.6: Register Services

*Configure dependency injection for the tool system.*

Update `src/BudgetTracker.Api/Program.cs`:

```csharp
// Add tool registration
builder.Services.AddScoped<AgentContext>();
builder.Services.AddScoped<IAgentContext>(sp => sp.GetRequiredService<AgentContext>());
builder.Services.AddScoped<SearchTransactionsTool>();
builder.Services.AddScoped<IToolRegistry, ToolRegistry>();

// IChatClient is already registered from earlier weeks
// RecommendationAgent will receive IChatClient and IServiceProvider through DI
```

**Key Points:**
- `AgentContext` registered as itself AND as `IAgentContext`
- This allows the agent to set `UserId` on the concrete type while tools read from the interface
- `SearchTransactionsTool` registered directly (not as `IAgentTool`)
- Tools are auto-discovered via constructor injection into `ToolRegistry`

---

## Step 53.7: Test the Agentic System

*Verify the agent's tool-calling capabilities.*

### 53.7.1: Test Manual Trigger

Trigger recommendation generation manually:

```http
### Manually trigger recommendation generation
POST http://localhost:5295/api/recommendations/generate
X-API-Key: test-key-user1
```

### 53.7.2: Monitor Agent Behavior

Watch the logs while the agent runs:

```bash
dotnet run --project src/BudgetTracker.Api/
```

Look for:
```
Agent started for user test-user-1
Agent iteration 1/5 for user test-user-1
Executing 1 tool call(s)
SearchTransactions called: query=subscriptions, maxResults=10
Tool SearchTransactions executed in 234ms
Agent iteration 2/5 for user test-user-1
Executing 1 tool call(s)
SearchTransactions called: query=recurring monthly charges, maxResults=10
Tool SearchTransactions executed in 198ms
Agent iteration 3/5 for user test-user-1
Agent completed after 3 iterations
Generated 4 recommendations for test-user-1
```

### 53.7.3: Verify Recommendations

Get the generated recommendations:

```http
### Get recommendations
GET http://localhost:5295/api/recommendations
X-API-Key: test-key-user1
```

**Expected improvements:**
- Recommendations mention specific merchants found in searches
- References actual transaction patterns
- More targeted and evidence-based
- Example: "Found 3 streaming subscriptions: Netflix, Hulu, Disney+ totaling $42/month"

### 53.7.4: Test Different Scenarios

Import different transaction patterns and see how the agent explores:

**Scenario 1: Subscription-heavy spending**
- Agent searches for "subscriptions"
- Finds recurring charges
- Recommends consolidation

**Scenario 2: Category-focused spending**
- Agent searches for "dining expenses"
- Searches for "coffee purchases"
- Recommends reducing specific patterns

---

## Summary

You've successfully built an autonomous AI agent with tool-calling capabilities!

### What You Built

- **Agent Context**: Scoped user context injected into tools via `IAgentContext`
- **SearchTransactions Tool**: Typed method with `[Description]` attributes for automatic schema generation
- **Tool Registry**: Simple registry using `AIFunctionFactory.Create` with typed methods
- **Function Calling**: Using `IChatClient` with `FunctionCallContent` and `FunctionResultContent`
- **RecommendationAgent**: Evolved with scoped context and `AIFunction.InvokeAsync()`
- **Multi-turn Agent Loop**: Autonomous reasoning with iteration control and tool execution

### Pattern Comparison

| Aspect | Old Pattern | New Pattern |
|--------|-------------|-------------|
| Tool interface | `IAgentTool` with 4 members | Plain class with `[Description]` method |
| Schema definition | Manual JSON in `ParametersSchema` | Auto-generated from method signature |
| Parameters | `JsonElement` with manual parsing | Typed parameters with defaults |
| User context | Passed as `userId` parameter | Injected via scoped `IAgentContext` |
| Tool invocation | `tool.ExecuteAsync(userId, arguments)` | `aiFunction.InvokeAsync(arguments)` |
| Return type | JSON string | Typed result object |

### What's Next?

You can optionally add a second tool (`GetCategorySpending`) to see how easy it is to extend the agent:
- Adding tools requires just creating a new class with a `[Description]` method
- Register it in DI and inject it into `ToolRegistry`
- The agent automatically discovers new tools
- Tool composition happens naturally (search + aggregate)

The agentic foundation is complete - now you can easily add more capabilities!
