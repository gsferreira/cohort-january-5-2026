# Workshop Step 054: Refactor NLQ to Agentic Pattern (Bonus)

## Mission

In this step, you'll refactor the existing `QueryAssistantService` from a single-pass RAG pattern into an agentic system that uses tool calling — the same pattern you built in Step 053 for recommendations. The interface, endpoint, and frontend remain unchanged. Only the internals of the service change.

**Your goal**: Build a new `GetCategorySpendingTool` and apply the agent loop pattern from `RecommendationAgent` to refactor `QueryAssistantService` in-place, replacing manual retrieval with autonomous tool calling.

**Learning Objectives**:
- Implementing a new agent tool using typed parameters with `[Description]` attributes
- Refactoring from RAG to agentic while preserving the public interface
- Combining multiple tools (search + aggregation) for richer agent capabilities
- Maintaining backward compatibility through interface-driven design

---

## Prerequisites

Before starting, ensure you completed:
- [042-nlq-backend.md](042-nlq-backend.md) - NLQ Backend (Week 5)
- [043-nlq-ui.md](043-nlq-ui.md) - NLQ UI (Week 5)
- [053-agentic-tools.md](053-agentic-tools.md) - Agentic Tools with SearchTransactions (Week 6)

You should have:
- Working NLQ query assistant (backend + UI) via `POST /api/query/ask`
- `SearchTransactionsTool` registered with `[Description]` attributes
- `IToolRegistry` available via dependency injection
- `IAgentContext` for user context injection
- `IChatClient` configured for Azure OpenAI

---

## Background: RAG vs Agentic — Before and After

### Before: RAG Pattern (current `QueryAssistantService`)

```
User Question
    │
    ▼
DB Query (recent txns) ──┐
                          ├──▶ Build Large Prompt ──▶ Single LLM Call ──▶ Answer
Semantic Search ──────────┘
```

The current implementation in `QueryAssistantService`:
1. Queries the database directly for the user's recent transactions
2. Calls `ISemanticSearchService` to find relevant transactions by embedding similarity
3. Stuffs both sets into a large prompt with summary statistics
4. Makes a single LLM call to generate an answer
5. Parses the response and matches transactions back to database records

### After: Agentic Pattern (refactored `QueryAssistantService`)

```
User Question
    │
    ▼
Agent Loop ◄─────────────────────┐
    │                             │
    ▼                             │
LLM decides ──▶ Tool Call? ──YES──┘
    │                   │
    NO                  ▼
    │            Execute Tool
    ▼            (Search or Aggregate)
Final Answer
```

The refactored implementation:
1. Passes the user's question to the LLM with available tools
2. LLM decides whether to call `SearchTransactions` or `GetCategorySpending`
3. Tool executes and returns results to the conversation
4. LLM decides: call another tool, or answer?
5. Loop continues until the LLM generates a final answer

### Why Two Tools?

The `SearchTransactions` tool finds specific transactions using semantic search — great for "show me my Amazon purchases" or "find my subscriptions." But questions like "how much did I spend on dining?" or "what are my top spending categories?" need **aggregation**, not search.

By adding `GetCategorySpendingTool`, the agent can:
- **Search** for specific transactions (SearchTransactions)
- **Aggregate** spending by category (GetCategorySpending)
- **Combine** both in a single query: "Find my dining expenses and tell me the total"

### Three Limitations of the Current RAG Approach

1. **Fixed retrieval strategy**: The service always does one semantic search plus one recent-transactions query, regardless of the question. A question like "show me my subscriptions" gets the same retrieval pipeline as "what was my biggest expense?"

2. **No iterative refinement**: If semantic search returns poor results (e.g., the embedding doesn't capture the intent well), the LLM has no way to try a different search. It must answer with whatever context it received.

3. **Prompt stuffing**: The `CreateUserPrompt` method serializes transaction data, category breakdowns, and summary statistics into a single massive prompt. The LLM processes all of this even when the question only needs a simple lookup.

---

## Step 054.1: Review What Changes and What Stays

*Orientation before making any changes.*

This refactoring changes the internals of `QueryAssistantService` while keeping everything external the same. Here's what's affected:

| Aspect | Before (RAG) | After (Agentic) |
|--------|-------------|-----------------|
| **Class name** | `QueryAssistantService` | `QueryAssistantService` (same) |
| **Interface** | `IQueryAssistantService` | `IQueryAssistantService` (same) |
| **Endpoint** | `POST /api/query/ask` | `POST /api/query/ask` (same) |
| **Request/Response** | `QueryRequest` / `QueryResponse` | Same DTOs |
| **Dependencies** | `BudgetTrackerContext`, `ISemanticSearchService`, `IChatClient` | `IChatClient`, `IServiceProvider` |
| **Flow** | DB query → Semantic search → Single LLM call | Agent loop with tool calling |
| **Frontend** | NLQ UI component | No changes needed |

**Files created:**
- `src/BudgetTracker.Api/Features/Intelligence/Tools/GetCategorySpendingTool.cs` — new aggregation tool

**Files modified:**
- `src/BudgetTracker.Api/Features/Intelligence/Query/QueryAssistantService.cs` — rewrite internals (removes `GetUserTransactions`, `ProcessQueryDirectlyWithAi`, `CreateUserPrompt`, `ParseAiResponse`, and inner classes `AiQueryResponse`/`AiTransactionReference`)
- `src/BudgetTracker.Api/Features/Intelligence/Tools/ToolRegistry.cs` — add new tool
- `src/BudgetTracker.Api/Program.cs` — register new tool

**Files untouched:**
- `src/BudgetTracker.Api/Features/Intelligence/Query/IQueryAssistantService.cs`
- `src/BudgetTracker.Api/Features/Intelligence/Query/QueryApi.cs`
- All frontend files

---

## Step 054.2: Implement GetCategorySpendingTool

*Create an aggregation tool using typed parameters with `[Description]` attributes.*

The `SearchTransactionsTool` finds individual transactions. This new tool aggregates spending totals by category — perfect for questions like "how much did I spend on dining?" or "what are my top spending categories?"

Create `src/BudgetTracker.Api/Features/Intelligence/Tools/GetCategorySpendingTool.cs`:

```csharp
using System.ComponentModel;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Intelligence.Tools;

public class GetCategorySpendingTool
{
    private readonly BudgetTrackerContext _context;
    private readonly IAgentContext _agentContext;
    private readonly ILogger<GetCategorySpendingTool> _logger;

    public GetCategorySpendingTool(
        BudgetTrackerContext context,
        IAgentContext agentContext,
        ILogger<GetCategorySpendingTool> logger)
    {
        _context = context;
        _agentContext = agentContext;
        _logger = logger;
    }

    [Description("Get spending totals grouped by category. Use this to answer questions about " +
                 "how much was spent in each category, what the top spending categories are, " +
                 "or to compare spending across categories. Returns category names with total amounts.")]
    public async Task<CategorySpendingResult> GetCategorySpendingAsync(
        [Description("Number of top categories to return (default: 10, max: 20)")]
        int topN = 10,
        [Description("Include income categories (positive amounts). Default is false (expenses only).")]
        bool includeIncome = false)
    {
        _logger.LogInformation(
            "GetCategorySpending called: topN={TopN}, includeIncome={IncludeIncome}",
            topN, includeIncome);

        topN = Math.Min(topN, 20);

        var query = _context.Transactions
            .Where(t => t.UserId == _agentContext.UserId && t.Category != null);

        if (!includeIncome)
        {
            query = query.Where(t => t.Amount < 0);
        }

        var categorySpending = await query
            .GroupBy(t => t.Category)
            .Select(g => new
            {
                Category = g.Key,
                Total = g.Sum(t => t.Amount),
                Count = g.Count()
            })
            .OrderBy(c => c.Total) // Most negative (biggest expense) first
            .Take(topN)
            .ToListAsync();

        if (!categorySpending.Any())
        {
            return new CategorySpendingResult
            {
                Success = true,
                Count = 0,
                Message = "No categorized transactions found.",
                GrandTotal = 0,
                Categories = []
            };
        }

        var results = categorySpending.Select(c => new CategorySpendingItem
        {
            Category = c.Category ?? "Uncategorized",
            Total = Math.Abs(c.Total),
            TransactionCount = c.Count
        }).ToList();

        var grandTotal = results.Sum(r => r.Total);

        return new CategorySpendingResult
        {
            Success = true,
            Count = results.Count,
            GrandTotal = grandTotal,
            Categories = results
        };
    }
}

public class CategorySpendingResult
{
    public bool Success { get; init; }
    public int Count { get; init; }
    public string? Message { get; init; }
    public decimal GrandTotal { get; init; }
    public List<CategorySpendingItem> Categories { get; init; } = [];
}

public class CategorySpendingItem
{
    public string Category { get; init; } = string.Empty;
    public decimal Total { get; init; }
    public int TransactionCount { get; init; }
}
```

**Key design decisions:**
- **Typed parameters**: `int topN` and `bool includeIncome` with default values
- **`[Description]` attributes**: On both the method and parameters for automatic schema generation
- **Returns typed result**: `CategorySpendingResult` instead of JSON string
- **Injects `IAgentContext`**: Gets userId from scoped context
- Returns absolute values for totals (easier for the LLM to reason about)
- Includes transaction count per category (useful context)
- Calculates grand total across all returned categories
- Ordered by spending amount (biggest expenses first)

---

## Step 054.3: Update Tool Registry

*Add the new tool to the registry.*

Update `src/BudgetTracker.Api/Features/Intelligence/Tools/ToolRegistry.cs` to include the new tool:

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
    private readonly GetCategorySpendingTool _getCategorySpendingTool;

    public ToolRegistry(
        SearchTransactionsTool searchTransactionsTool,
        GetCategorySpendingTool getCategorySpendingTool)
    {
        _searchTransactionsTool = searchTransactionsTool;
        _getCategorySpendingTool = getCategorySpendingTool;
    }

    public IList<AITool> GetTools()
    {
        return
        [
            AIFunctionFactory.Create(
                _searchTransactionsTool.SearchTransactionsAsync,
                name: "SearchTransactions"),
            AIFunctionFactory.Create(
                _getCategorySpendingTool.GetCategorySpendingAsync,
                name: "GetCategorySpending")
        ];
    }
}
```

---

## Step 054.4: Register the New Tool

*Add the tool to dependency injection so it's available to all agents.*

Update `src/BudgetTracker.Api/Program.cs` to register the new tool alongside `SearchTransactionsTool`:

```csharp
// Add tool registration
builder.Services.AddScoped<AgentContext>();
builder.Services.AddScoped<IAgentContext>(sp => sp.GetRequiredService<AgentContext>());
builder.Services.AddScoped<SearchTransactionsTool>();
builder.Services.AddScoped<GetCategorySpendingTool>();  // Add this line
builder.Services.AddScoped<IToolRegistry, ToolRegistry>();
```

Because `ToolRegistry` receives both tools via constructor injection, the new tool is automatically available — no additional changes needed to the registry pattern.

---

## Step 054.5: Update Constructor Dependencies

*Replace database and search dependencies with IServiceProvider.*

The RAG version needed `BudgetTrackerContext` and `ISemanticSearchService` to manually query data. The agentic version delegates data access to tools via scoped context.

**Current constructor:**

```csharp
public class QueryAssistantService : IQueryAssistantService
{
    private readonly BudgetTrackerContext _context;
    private readonly ISemanticSearchService _semanticSearchService;
    private readonly IChatClient _chatClient;
    private readonly ILogger<QueryAssistantService> _logger;

    public QueryAssistantService(
        BudgetTrackerContext context,
        ISemanticSearchService semanticSearchService,
        IChatClient chatClient,
        ILogger<QueryAssistantService> logger)
    {
        _context = context;
        _semanticSearchService = semanticSearchService;
        _chatClient = chatClient;
        _logger = logger;
    }
}
```

**Replace with:**

```csharp
using System.Text.Json;
using BudgetTracker.Api.Features.Intelligence.Tools;
using BudgetTracker.Api.Features.Transactions;
using BudgetTracker.Api.Infrastructure.Extensions;
using Microsoft.Extensions.AI;

namespace BudgetTracker.Api.Features.Intelligence.Query;

public class QueryAssistantService : IQueryAssistantService
{
    private readonly IChatClient _chatClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<QueryAssistantService> _logger;

    public QueryAssistantService(
        IChatClient chatClient,
        IServiceProvider serviceProvider,
        ILogger<QueryAssistantService> logger)
    {
        _chatClient = chatClient;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
}
```

**What changed:**
- Removed `BudgetTrackerContext` — no direct database access needed
- Removed `ISemanticSearchService` — the `SearchTransactionsTool` uses this internally
- Added `IServiceProvider` — creates scope and sets agent context before tool execution
- `IChatClient` stays — still needed for LLM calls

---

## Step 054.6: Replace ProcessQueryAsync with Agent Loop

*Replace the RAG flow with a multi-turn agent loop, following the same pattern as `RecommendationAgent.GenerateAgenticRecommendationsAsync`.*

Keep the validation guards at the top (empty query, too-long query, missing user ID). Replace everything after validation with the agent loop.

```csharp
public async Task<QueryResponse> ProcessQueryAsync(string query, string userId)
{
    if (string.IsNullOrWhiteSpace(query))
    {
        return new QueryResponse { Answer = "Please provide a question about your finances." };
    }

    if (query.Length > 500)
    {
        return new QueryResponse { Answer = "Your question is too long. Please keep it under 500 characters." };
    }

    if (string.IsNullOrWhiteSpace(userId))
    {
        return new QueryResponse { Answer = "User authentication required." };
    }

    try
    {
        // Create a scope to set up the agent context for this user
        using var scope = _serviceProvider.CreateScope();
        var agentContext = scope.ServiceProvider.GetRequiredService<AgentContext>();
        agentContext.UserId = userId;

        var toolRegistry = scope.ServiceProvider.GetRequiredService<IToolRegistry>();

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, CreateSystemPrompt()),
            new(ChatRole.User, query)
        };

        var tools = toolRegistry.GetTools();
        var toolsByName = tools.OfType<AIFunction>().ToDictionary(t => t.Name);
        var options = new ChatOptions { Tools = tools };

        var maxIterations = 5;
        var iteration = 0;

        while (iteration < maxIterations)
        {
            iteration++;
            _logger.LogInformation(
                "Query agent iteration {Iteration}/{Max} for user {UserId}",
                iteration, maxIterations, userId);

            var response = await _chatClient.GetResponseAsync(messages, options);
            messages.AddMessages(response);

            var toolCalls = response.Messages[0].Contents
                .OfType<FunctionCallContent>()
                .ToList();

            if (toolCalls.Count > 0)
            {
                await ExecuteToolCallsAsync(messages, toolCalls, toolsByName);
            }
            else if (response.FinishReason == ChatFinishReason.Stop)
            {
                _logger.LogInformation("Query agent completed after {Iterations} iterations", iteration);

                var textContent = response.Messages[0].Contents
                    .OfType<TextContent>()
                    .FirstOrDefault();

                return ParseResponse(textContent?.Text ?? "I couldn't generate an answer.");
            }
            else if (response.FinishReason == ChatFinishReason.Length)
            {
                _logger.LogWarning("Query agent max tokens reached at iteration {Iteration}", iteration);
                break;
            }
            else if (response.FinishReason == ChatFinishReason.ContentFilter)
            {
                _logger.LogWarning("Query agent content filtered at iteration {Iteration}", iteration);
                return new QueryResponse { Answer = "I'm unable to process that question." };
            }
        }

        _logger.LogWarning("Query agent reached max iterations for query: {Query}", query);
        return new QueryResponse
        {
            Answer = "I wasn't able to fully analyze your question. Please try rephrasing it."
        };
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to process query: {Query} for user {UserId}", query, userId);
        return new QueryResponse
        {
            Answer = "I'm sorry, I couldn't process your question right now. Please try again later."
        };
    }
}
```

**Compare with `RecommendationAgent.GenerateAgenticRecommendationsAsync`:**
- Same scope creation: `_serviceProvider.CreateScope()` and setting `AgentContext.UserId`
- Same loop structure: `while (iteration < maxIterations)`
- Same tool call detection: `response.Messages[0].Contents.OfType<FunctionCallContent>()`
- Same conversation management: `messages.AddMessages(response)`
- Same finish reason handling: `Stop`, `Length`, `ContentFilter`
- Different output: `QueryResponse` instead of `List<GeneratedRecommendation>`

---

## Step 054.7: Add ExecuteToolCallsAsync

*Same pattern as `RecommendationAgent.ExecuteToolCallsAsync` — look up the tool by name, invoke it, add result to messages.*

```csharp
private async Task ExecuteToolCallsAsync(
    List<ChatMessage> messages,
    List<FunctionCallContent> toolCalls,
    Dictionary<string, AIFunction> toolsByName)
{
    _logger.LogInformation("Executing {Count} tool call(s)", toolCalls.Count);

    foreach (var toolCall in toolCalls)
    {
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

            messages.Add(new ChatMessage(ChatRole.Tool, [
                new FunctionResultContent(toolCall.CallId, result)
            ]));

            _logger.LogInformation("Tool {ToolName} executed for query agent", tool.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {ToolName}", toolCall.Name);

            messages.Add(new ChatMessage(ChatRole.Tool, [
                new FunctionResultContent(toolCall.CallId,
                    JsonSerializer.Serialize(new { error = ex.Message }))
            ]));
        }
    }
}
```

This is nearly identical to the method in `RecommendationAgent`. The key steps are:
1. Look up the tool by name via `toolsByName.TryGetValue()`
2. Wrap `toolCall.Arguments` in `AIFunctionArguments`
3. Call `tool.InvokeAsync(arguments)`
4. Wrap the result in `FunctionResultContent` and add to messages

---

## Step 054.8: Write the System Prompt

*Create a new system prompt that references both tools.*

Replace the old `CreateSystemPrompt()` and remove `CreateUserPrompt()` entirely. With the agentic approach, the user's question goes directly as the user message — no prompt stuffing needed.

```csharp
private static string CreateSystemPrompt()
{
    return """
        You are a financial query assistant that answers questions about a user's transactions.

        You have access to two tools:
        - SearchTransactions: Find specific transactions using semantic search (e.g., "coffee", "Amazon", "subscriptions")
        - GetCategorySpending: Get spending totals grouped by category (e.g., top spending categories, total by category)

        STRATEGY:
        - For questions about specific merchants or items: use SearchTransactions
        - For questions about spending totals or category breakdowns: use GetCategorySpending
        - For complex questions: combine both tools (e.g., search first, then aggregate)
        - Only call tools when you need data — if the question is general, answer directly

        RESPONSE FORMAT:
        Always respond with JSON in this exact format:
        {
          "answer": "Your natural language answer here",
          "amount": null,
          "transactions": null
        }

        - "answer": A clear, conversational response referencing specific data you found
        - "amount": A decimal value if the question asks about a total or amount (null otherwise)
        - "transactions": An array of relevant transactions if applicable (null otherwise)

        For transactions, use this format:
        {
          "id": "transaction-guid",
          "date": "YYYY-MM-DD",
          "description": "transaction description",
          "amount": -42.50,
          "category": "category-name",
          "account": "account-name"
        }

        Include up to 5 relevant transactions when they help illustrate your answer.
        """;
}
```

**What's different from the old prompt:**
- References both `SearchTransactions` and `GetCategorySpending` tools
- Explains when to use each tool
- No mention of pre-loaded context (there is none — the agent fetches its own)
- Same JSON response format matching `QueryResponse` (`answer`, `amount`, `transactions`)

**What to remove:**
- Delete `CreateUserPrompt()` — the user's question is now passed directly as the user message
- The old method serialized transactions, category breakdowns, and summary statistics into the prompt. None of that is needed because the agent discovers data through tool calls.

---

## Step 054.9: Update Response Parsing

*Replace the complex `ParseAiResponse` and its inner classes with a simpler `ParseResponse` using `JsonElement`.*

```csharp
private QueryResponse ParseResponse(string content)
{
    try
    {
        var cleaned = content.ExtractJsonFromCodeBlock();
        var parsed = JsonSerializer.Deserialize<JsonElement>(cleaned);

        var answer = parsed.TryGetProperty("answer", out var answerEl)
            ? answerEl.GetString() ?? content
            : content;

        decimal? amount = parsed.TryGetProperty("amount", out var amountEl)
            && amountEl.ValueKind == JsonValueKind.Number
                ? amountEl.GetDecimal()
                : null;

        var transactions = new List<TransactionDto>();
        if (parsed.TryGetProperty("transactions", out var txArray)
            && txArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var tx in txArray.EnumerateArray())
            {
                transactions.Add(new TransactionDto
                {
                    Id = tx.TryGetProperty("id", out var id)
                        && Guid.TryParse(id.GetString(), out var guid) ? guid : Guid.NewGuid(),
                    Date = tx.TryGetProperty("date", out var date)
                        && DateTime.TryParse(date.GetString(), out var d) ? d : DateTime.MinValue,
                    Description = tx.TryGetProperty("description", out var desc)
                        ? desc.GetString() ?? "" : "",
                    Amount = tx.TryGetProperty("amount", out var amt)
                        && amt.ValueKind == JsonValueKind.Number ? amt.GetDecimal() : 0,
                    Category = tx.TryGetProperty("category", out var cat)
                        ? cat.GetString() : null,
                    Account = tx.TryGetProperty("account", out var acc)
                        ? acc.GetString() ?? "" : ""
                });
            }
        }

        return new QueryResponse
        {
            Answer = answer,
            Amount = amount,
            Transactions = transactions.Count > 0 ? transactions : null
        };
    }
    catch (Exception)
    {
        return new QueryResponse { Answer = content };
    }
}
```

**What to remove (dead code):**
- `GetUserTransactions()` — no longer querying the database directly
- `ProcessQueryDirectlyWithAi()` — replaced by the agent loop
- `CreateUserPrompt()` — no more prompt stuffing
- `ParseAiResponse()` — replaced by the simpler `ParseResponse`
- Inner classes `AiQueryResponse` and `AiTransactionReference` — no longer needed

The new parsing uses `JsonElement` directly (same approach as `RecommendationAgent.ParseRecommendations`) and the `ExtractJsonFromCodeBlock()` extension from `BudgetTracker.Api.Infrastructure.Extensions`.

---

## Step 054.10: Test

*Verify the refactored service works through the existing endpoint and UI.*

### 054.10.1: Test via API

The endpoint and request format are unchanged:

```http
### Ask a question (same endpoint as before)
POST http://localhost:5295/api/query/ask
Content-Type: application/json
X-API-Key: test-key-user1

{
    "query": "What did I spend on groceries?"
}
```

### 054.10.2: Try Different Query Types

**SearchTransactions queries** — semantic search for specific items:
- "Show me my Amazon purchases"
- "When was my last coffee purchase?"
- "What are my recurring subscriptions?"

**GetCategorySpending queries** — aggregation questions:
- "What are my top spending categories?"
- "How much did I spend on dining?"
- "Show me a breakdown of my expenses by category"

**Combined queries** — agent uses both tools:
- "Find my dining expenses and tell me the total"
- "What subscriptions do I have and how much do they cost in total?"
- "Compare my spending on food vs entertainment"

### 054.10.3: Monitor Agent Iterations

Watch the application logs to see the agent using both tools:

```
Query agent iteration 1/5 for user abc-123
Executing 1 tool call(s)
Tool GetCategorySpending executed for query agent
Query agent completed after 1 iterations
```

Or for a more complex query:

```
Query agent iteration 1/5 for user abc-123
Executing 1 tool call(s)
Tool SearchTransactions executed for query agent
Query agent iteration 2/5 for user abc-123
Executing 1 tool call(s)
Tool GetCategorySpending executed for query agent
Query agent completed after 2 iterations
```

### 054.10.4: Verify Frontend

Open the NLQ UI in the browser at `http://localhost:5173`. The frontend calls `POST /api/query/ask` — the same endpoint with the same request/response shape. It should work without any changes.

---

## Architecture Comparison

| Aspect | RAG (before) | Agentic (after) |
|--------|-------------|-----------------|
| **Data access** | Direct DB queries + semantic search | Through tools (Search + Aggregate) |
| **Tools available** | None | SearchTransactions, GetCategorySpending |
| **LLM calls** | 1 (single shot) | 1-5 (multi-turn loop) |
| **Context selection** | Fixed: top N similar + top N recent | Dynamic: LLM chooses which tool |
| **Adaptability** | None — one chance to get it right | Can combine search + aggregation |
| **Latency** | Lower (1 LLM call) | Higher (multiple LLM calls) |
| **Cost** | Lower (fewer tokens) | Higher (multiple rounds) |
| **Simple queries** | Good | Good |
| **Complex queries** | Limited by pre-fetched context | Better — can use multiple tools |

Both patterns are valid. RAG is the right choice when the retrieval strategy is predictable and latency matters. Agentic is better when queries are diverse and the LLM needs flexibility to explore.

---

## Summary

You built a new tool and refactored `QueryAssistantService` from RAG to agentic by:

1. **Adding `GetCategorySpendingTool`**: Typed method with `[Description]` attributes for automatic schema generation
2. **Swapping dependencies**: `BudgetTrackerContext` + `ISemanticSearchService` → `IServiceProvider`
3. **Creating scoped context**: Setting `AgentContext.UserId` before tool execution
4. **Replacing the flow**: DB query + semantic search + single LLM call → multi-turn agent loop
5. **Combining tools**: The agent can search, aggregate, or do both depending on the question
6. **Preserving the interface**: `IQueryAssistantService`, endpoint, and frontend unchanged

**Key takeaway**: The tool infrastructure from task 053 is composable. Adding a new tool (`GetCategorySpendingTool`) required just creating a class with a `[Description]` method and registering it in DI. The query assistant now has richer capabilities while keeping the same external interface.
