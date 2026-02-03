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
}
