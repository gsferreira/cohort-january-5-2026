# Workshop Step 042: Natural Language Query Assistant - Backend

## Mission

In this step, you'll implement a natural language query assistant that allows users to ask questions about their financial data in plain English. Using the RAG infrastructure built in the previous step, you'll create a semantic search system and conversational AI interface.

**Your goal**: Build a complete backend for the natural language query assistant that can answer questions like "What was my biggest expense last week?" or "Show me all coffee-related purchases."

**Learning Objectives**:
- Building semantic search services with vector similarity
- Creating query assistant services with AI integration
- Designing AI prompts for financial question answering
- Implementing JSON response parsing from AI
- Building RESTful API endpoints for query processing

---

## Prerequisites

Before starting, ensure you completed:
- [041-rag-enhancement-backend.md](041-rag-enhancement-backend.md) - RAG infrastructure with embeddings

You should have:
- Working semantic search infrastructure with embeddings
- Background service generating embeddings for transactions
- Azure OpenAI integration configured
- PostgreSQL with pgvector extension

---

## Part 1: Semantic Search Service

### Step 1.1: Create Semantic Search Interface

*Define the interface for searching transactions by semantic similarity.*

Create `src/BudgetTracker.Api/Features/Intelligence/Search/ISemanticSearchService.cs`:

```csharp
using BudgetTracker.Api.Features.Transactions;

namespace BudgetTracker.Api.Features.Intelligence.Search;

public interface ISemanticSearchService
{
    Task<List<Transaction>> FindRelevantTransactionsAsync(string queryText, string userId, int maxResults = 50);
}
```

### Step 1.2: Implement Semantic Search Service

*Build the semantic search service that finds relevant transactions using vector similarity.*

Create `src/BudgetTracker.Api/Features/Intelligence/Search/SemanticSearchService.cs`:

```csharp
using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Features.Transactions;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Intelligence.Search;

public class SemanticSearchService : ISemanticSearchService
{
    private readonly BudgetTrackerContext _context;
    private readonly IAzureEmbeddingService _embeddingService;
    private readonly ILogger<SemanticSearchService> _logger;

    public SemanticSearchService(
        BudgetTrackerContext context,
        IAzureEmbeddingService embeddingService,
        ILogger<SemanticSearchService> logger)
    {
        _context = context;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public async Task<List<Transaction>> FindRelevantTransactionsAsync(
        string queryText,
        string userId,
        int maxResults = 50)
    {
        if (string.IsNullOrWhiteSpace(queryText) || string.IsNullOrWhiteSpace(userId))
        {
            return new List<Transaction>();
        }

        try
        {
            // Generate embedding for the query
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(queryText);
            var vectorString = queryEmbedding.ToString();

            // Use raw SQL with pgvector cosine_distance for efficient similarity search
            var similarTransactions = await _context.Transactions
                .FromSqlRaw(@"
                    SELECT *
                    FROM ""Transactions""
                    WHERE ""Embedding"" IS NOT NULL
                    AND ""UserId"" = {0}
                    ORDER BY cosine_distance(""Embedding"", {1}::vector) ASC
                    LIMIT {2}", userId, vectorString, maxResults)
                .ToListAsync();

            _logger.LogInformation("Found {Count} relevant transactions for query: {Query}",
                similarTransactions.Count, queryText[..Math.Min(queryText.Length, 50)]);

            return similarTransactions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find relevant transactions for query: {Query}", queryText);
            return new List<Transaction>();
        }
    }
}
```

---

## Part 2: Query Assistant Service

### Step 2.1: Create Query Assistant Interface and Types

*Define the interface and types for the query assistant service.*

Create `src/BudgetTracker.Api/Features/Intelligence/Query/IQueryAssistantService.cs`:

```csharp
using BudgetTracker.Api.Features.Transactions;

namespace BudgetTracker.Api.Features.Intelligence.Query;

public interface IQueryAssistantService
{
    Task<QueryResponse> ProcessQueryAsync(string query, string userId);
}

public class QueryRequest
{
    public string Query { get; set; } = string.Empty;
}

public class QueryResponse
{
    public string Answer { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public List<TransactionDto>? Transactions { get; set; }
}
```

### Step 2.2: Implement Query Assistant Service

*Create the main query assistant service that processes natural language questions.*

Create `src/BudgetTracker.Api/Features/Intelligence/Query/QueryAssistantService.cs`:

```csharp
using System.Text.Json;
using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Features.Intelligence.Search;
using BudgetTracker.Api.Features.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace BudgetTracker.Api.Features.Intelligence.Query;

public class QueryAssistantService : IQueryAssistantService
{
    private readonly BudgetTrackerContext _context;
    private readonly ISemanticSearchService _semanticSearchService;
    private readonly IChatClient _chatClient;  // Use Microsoft.Extensions.AI abstraction
    private readonly ILogger<QueryAssistantService> _logger;

    public QueryAssistantService(
        BudgetTrackerContext context,
        ISemanticSearchService semanticSearchService,
        IChatClient chatClient,  // Use IChatClient
        ILogger<QueryAssistantService> logger)
    {
        _context = context;
        _semanticSearchService = semanticSearchService;
        _chatClient = chatClient;
        _logger = logger;
    }

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
            var userTransactions = GetUserTransactions(userId);

            if (!await userTransactions.AnyAsync())
            {
                return new QueryResponse
                {
                    Answer =
                        "You don't have any transactions yet. Import some transactions to start asking questions about your finances."
                };
            }

            var relevantTransactions = await _semanticSearchService.FindRelevantTransactionsAsync(
                query, userId, maxResults: 10);

            var recentTransactions = await userTransactions.Take(10).ToListAsync();

            return await ProcessQueryDirectlyWithAi(query, recentTransactions, relevantTransactions);
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

    private IOrderedQueryable<Transaction> GetUserTransactions(string userId)
    {
        return _context.Transactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.Date);
    }

    private async Task<QueryResponse> ProcessQueryDirectlyWithAi(string query, List<Transaction> transactions,
        List<Transaction> relevantTransactions)
    {
        var systemPrompt = CreateSystemPrompt();
        var userPrompt = CreateUserPrompt(query, transactions, relevantTransactions);

        // Use Microsoft.Extensions.AI IChatClient
        var response = await _chatClient.GetResponseAsync([
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, userPrompt)
        ]);

        var content = response.Text ?? string.Empty;
        return ParseAiResponse(content, transactions);
    }

    private static string CreateSystemPrompt()
    {
        return """
               You are a helpful financial assistant that answers questions about the user's spending and transactions.

               You can analyze spending patterns, find specific transactions, calculate totals, identify trends, and provide insights.
               Be conversational and helpful. Provide specific numbers, dates, and transaction details when relevant.

               The transactions provided to you have been semantically filtered to be most relevant to the user's query,
               so you're working with the most pertinent financial data for their question.

               When responding, provide:
               1. A clear, natural language answer to their question
               2. If relevant, include specific transaction details or amounts
               3. If showing multiple transactions, limit to the most relevant 3-5 items

               Always respond with JSON in this exact format:
               {
                 "answer": "Your natural language response here",
                 "amount": null or decimal value if relevant,
                 "transactions": null or array of relevant transaction objects
               }

               For transactions, use this format:
               {
                 "id": "transaction-guid",
                 "date": "YYYY-MM-DD",
                 "description": "transaction description",
                 "amount": decimal-value,
                 "category": "category-name-or-null",
                 "account": "account-name"
               }

               Examples of queries you can handle:
               - "What was my biggest expense last week?"
               - "Show me all Amazon purchases"
               - "What categories do I spend the most on?"
               - "Show me transactions over $100"
               - "When did I last go to Starbucks?"
               - "How much have I saved this year?"
               - "Find all my coffee-related expenses"
               - "Show me subscription services I'm paying for"
               - "What do I spend on transportation?"
               """;
    }

    private static string CreateUserPrompt(string query, List<Transaction> transactions,
        List<Transaction> relevantTransactions)
    {
        var earliestDate = transactions.Min(t => t.Date);
        var latestDate = transactions.Max(t => t.Date);
        var totalTransactions = transactions.Count;
        var totalIncome = transactions.Where(t => t.Amount > 0).Sum(t => t.Amount);
        var totalExpenses = Math.Abs(transactions.Where(t => t.Amount < 0).Sum(t => t.Amount));

        // Get category breakdown
        var categoryBreakdown = transactions
            .Where(t => t.Amount < 0 && !string.IsNullOrEmpty(t.Category))
            .GroupBy(t => t.Category!)
            .Select(g => new { Category = g.Key, Total = Math.Abs(g.Sum(t => t.Amount)) })
            .OrderByDescending(c => c.Total)
            .Take(10)
            .ToList();

        // Get recent transactions sample
        var recentTransactions = transactions
            .OrderByDescending(t => t.Date)
            .Take(10)
            .Select(t => new
            {
                Id = t.Id,
                Date = t.Date.ToString("yyyy-MM-dd"),
                Description = t.Description,
                Amount = t.Amount,
                Category = t.Category,
                Account = t.Account
            })
            .ToList();

        var relatedTransactions = relevantTransactions
            .OrderByDescending(t => t.Date)
            .Take(10)
            .Select(t => new
            {
                Id = t.Id,
                Date = t.Date.ToString("yyyy-MM-dd"),
                Description = t.Description,
                Amount = t.Amount,
                Category = t.Category,
                Account = t.Account
            })
            .ToList();

        var transactionsJson =
            JsonSerializer.Serialize(recentTransactions, new JsonSerializerOptions { WriteIndented = false });

        var relevantTransactionsJson =
            JsonSerializer.Serialize(relatedTransactions, new JsonSerializerOptions { WriteIndented = false });

        return $"""
                User query: "{query}"

                Transaction Summary:
                - Total transactions: {totalTransactions}
                - Date range: {earliestDate:yyyy-MM-dd} to {latestDate:yyyy-MM-dd}
                - Total income: €{totalIncome:F2}
                - Total expenses: €{totalExpenses:F2}
                - Net amount: €{(totalIncome - totalExpenses):F2}

                Top spending categories:
                {string.Join("\n", categoryBreakdown.Select(c => $"- {c.Category}: €{c.Total:F2}"))}

                Recent transactions (sample of {recentTransactions.Count}):
                {transactionsJson}

                Relevant transactions for the prompt:
                {relevantTransactionsJson}

                Please analyze this data and answer the user's query. Include specific transaction details in your response when relevant.
                """;
    }

    private QueryResponse ParseAiResponse(string content, List<Transaction> transactions)
    {
        try
        {
            var jsonResponse = JsonSerializer.Deserialize<AiQueryResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (jsonResponse == null)
            {
                return new QueryResponse { Answer = "I couldn't process your question. Please try rephrasing it." };
            }

            var response = new QueryResponse
            {
                Answer = jsonResponse.Answer ?? "I processed your query but couldn't generate a response.",
                Amount = jsonResponse.Amount
            };

            // If AI provided transaction references, try to match them with actual transactions
            if (jsonResponse.Transactions == null || jsonResponse.Transactions.Count == 0) return response;

            var matchedTransactions = new List<TransactionDto>();

            foreach (var aiTransaction in jsonResponse.Transactions.Take(5))
            {
                if (Guid.TryParse(aiTransaction.Id, out var transactionId))
                {
                    var actualTransaction = transactions.FirstOrDefault(t => t.Id == transactionId);
                    if (actualTransaction != null)
                    {
                        matchedTransactions.Add(actualTransaction.MapToDto());
                        continue;
                    }
                }

                // If no exact match, create a DTO from AI response
                if (DateTime.TryParse(aiTransaction.Date, out var date))
                {
                    matchedTransactions.Add(new TransactionDto
                    {
                        Id = Guid.TryParse(aiTransaction.Id, out var id) ? id : Guid.NewGuid(),
                        Date = date,
                        Description = aiTransaction.Description ?? "Transaction",
                        Amount = aiTransaction.Amount,
                        Category = aiTransaction.Category,
                        Account = aiTransaction.Account ?? "Account",
                        ImportedAt = DateTime.UtcNow
                    });
                }
            }

            if (matchedTransactions.Count != 0)
            {
                response.Transactions = matchedTransactions;
            }

            return response;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI response: {Content}", content);
            return new QueryResponse
            {
                Answer =
                    "I processed your question but had trouble formatting the response. Please try asking in a different way."
            };
        }
    }

    private class AiQueryResponse
    {
        public string? Answer { get; set; }
        public decimal? Amount { get; set; }
        public List<AiTransactionReference>? Transactions { get; set; }
    }

    private class AiTransactionReference
    {
        public string Id { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public string? Category { get; set; }
        public string? Account { get; set; }
    }
}
```

---

## Part 3: API Endpoints

### Step 3.1: Create Query API Endpoints

*Create API endpoints for the natural language query functionality.*

Create `src/BudgetTracker.Api/Features/Intelligence/Query/QueryApi.cs`:

```csharp
using System.Security.Claims;
using BudgetTracker.Api.Auth;
using Microsoft.AspNetCore.Mvc;

namespace BudgetTracker.Api.Features.Intelligence.Query;

public static class QueryApi
{
    public static IEndpointRouteBuilder MapQueryEndpoints(this IEndpointRouteBuilder routes)
    {
        var queryGroup = routes.MapGroup("/query")
            .WithTags("Query Assistant")
            .RequireAuthorization();

        queryGroup.MapPost("/ask", async (
            [FromBody] QueryRequest request,
            IQueryAssistantService queryService,
            ClaimsPrincipal claimsPrincipal) =>
        {
            var userId = claimsPrincipal.GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            var response = await queryService.ProcessQueryAsync(request.Query, userId);
            return Results.Ok(response);
        })
        .WithName("AskQuery")
        .WithSummary("Ask a natural language question about your finances")
        .WithDescription("Process natural language queries like 'What was my biggest expense last week?' or 'How much did I spend on groceries this month?'")
        .Produces<QueryResponse>()
        .ProducesProblem(400)
        .ProducesProblem(401);

        return routes;
    }
}
```

### Step 3.2: Register Services and Endpoints

*Update Program.cs with all necessary service registrations.*

Update `src/BudgetTracker.Api/Program.cs`:

```csharp
using BudgetTracker.Api.Features.Intelligence.Query;
using BudgetTracker.Api.Features.Intelligence.Search;

// ... existing registrations ...

// AI Services (add if not already present)
// IChatClient should already be registered from Week 3/Task 041
builder.Services.AddScoped<ISemanticSearchService, SemanticSearchService>();
builder.Services.AddScoped<IQueryAssistantService, QueryAssistantService>();

// ... existing app configuration ...

// Map endpoints
app.MapQueryEndpoints();
```

---

## Part 4: Testing

### Step 4.1: Test with HTTP Requests

Test the query assistant with sample queries:

```http
### Test coffee-related query
POST http://localhost:5295/query/ask
X-API-Key: test-key-user1
Content-Type: application/json

{
  "query": "Show me all my coffee purchases"
}
```

```http
### Test expense analysis
POST http://localhost:5295/query/ask
X-API-Key: test-key-user1
Content-Type: application/json

{
  "query": "What was my biggest expense this month?"
}
```

```http
### Test category analysis
POST http://localhost:5295/query/ask
X-API-Key: test-key-user1
Content-Type: application/json

{
  "query": "How much did I spend on entertainment?"
}
```

### Step 4.2: Expected Results

**Expected Results:**
- **Coffee query**: Should find coffee-related transactions using semantic search
- **Biggest expense**: Should identify the largest negative transaction
- **Entertainment spending**: Should aggregate spending in entertainment category
- **Semantic matching**: Queries should work even without exact keyword matches
- **Contextual responses**: AI should provide natural language explanations with specific amounts and dates

---

## Summary

You've successfully implemented a natural language query assistant backend:

**Semantic Search Service**: Vector similarity search over transaction embeddings

**Query Assistant Service**: AI-powered question answering with RAG

**API Endpoints**: RESTful interface for query processing

**Response Parsing**: Structured JSON responses with transaction details

**Key Features Implemented**:
- **Semantic Search**: Find transactions by meaning, not just keywords
- **Natural Language Processing**: GPT-powered conversational responses
- **RAG Pattern**: Combine retrieved transactions with AI reasoning
- **Transaction Matching**: Link AI responses to actual transaction records
- **Error Handling**: Graceful handling of AI service failures

**What Users Get**:
- **Conversational Finance**: Ask questions in plain English
- **Smart Discovery**: Find transactions by concept, not exact words
- **Instant Insights**: Immediate answers about spending patterns
- **Contextual Understanding**: AI understands financial context
