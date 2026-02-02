using System.Text.Json;
using Microsoft.Extensions.AI;
using BudgetTracker.Api.Features.Transactions;
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

internal class BasicStats
{
    public int TransactionCount { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public string DateRange { get; set; } = string.Empty;
    public List<string> TopCategories { get; set; } = new();
}

public class RecommendationAgent : IRecommendationRepository
{
    private readonly BudgetTrackerContext _context;
    private readonly IChatClient _chatClient;
    private readonly IToolRegistry _toolRegistry;
    private readonly ILogger<RecommendationAgent> _logger;

    public RecommendationAgent(
        BudgetTrackerContext context,
        IChatClient chatClient,
        IToolRegistry toolRegistry,
        ILogger<RecommendationAgent> logger)
    {
        _context = context;
        _chatClient = chatClient;
        _toolRegistry = toolRegistry;
        _logger = logger;
    }

    public async Task<List<Recommendation>> GetActiveRecommendationsAsync(string userId)
    {
        return await _context.Recommendations
            .Where(r => r.UserId == userId &&
                       r.Status == RecommendationStatus.Active &&
                       r.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(r => r.Priority)
            .ThenByDescending(r => r.GeneratedAt)
            .Take(5)
            .ToListAsync();
    }

    public async Task GenerateRecommendationsAsync(string userId)
    {
        try
        {
            // 1. Check if we need to regenerate
            var lastGenerated = await _context.Recommendations
                .Where(r => r.UserId == userId)
                .MaxAsync(r => (DateTime?)r.GeneratedAt);

            var lastImported = await _context.Transactions
                .Where(t => t.UserId == userId)
                .MaxAsync(t => (DateTime?)t.ImportedAt);

            // Skip if no new transactions since last generation (within 1 minute for dev testing)
            if (lastGenerated.HasValue && lastImported.HasValue &&
                lastGenerated > lastImported.Value.AddMinutes(-1)) // DEMO
            {
                _logger.LogInformation("Skipping generation - no new data for user {UserId}", userId);
                return;
            }

            // 2. Check minimum transaction count
            var transactionCount = await _context.Transactions
                .Where(t => t.UserId == userId)
                .CountAsync();

            if (transactionCount < 5)
            {
                _logger.LogInformation("Insufficient transaction data for user {UserId}", userId);
                return;
            }

            // Run agentic recommendation generation
            var recommendations = await GenerateAgenticRecommendationsAsync(userId, maxIterations: 5);

            if (!recommendations.Any())
            {
                _logger.LogInformation("Agent generated no recommendations for {UserId}", userId);
                return;
            }

            // Store recommendations
            await StoreRecommendationsAsync(userId, recommendations);

            _logger.LogInformation("Generated {Count} recommendations for user {UserId}",
                recommendations.Count, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate recommendations for user {UserId}", userId);
        }
    }

    private async Task<List<GeneratedRecommendation>> GenerateAgenticRecommendationsAsync(
        string userId,
        int maxIterations)
    {
        // Initialize conversation with Microsoft.Extensions.AI ChatMessage
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, CreateSystemPrompt()),
            new(ChatRole.User, CreateInitialUserPrompt())
        };

        // Prepare tools and options
        var tools = _toolRegistry.ToAITools();
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
                await ExecuteToolCallsAsync(userId, messages, toolCalls);
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

    private async Task ExecuteToolCallsAsync(
        string userId,
        List<ChatMessage> messages,
        List<FunctionCallContent> toolCalls)
    {
        _logger.LogInformation("Executing {Count} tool call(s)", toolCalls.Count);

        foreach (var toolCall in toolCalls)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var tool = _toolRegistry.GetTool(toolCall.Name);
                if (tool == null)
                {
                    _logger.LogWarning("Tool not found: {ToolName}", toolCall.Name);
                    messages.Add(new ChatMessage(ChatRole.Tool, [
                        new FunctionResultContent(toolCall.CallId,
                            JsonSerializer.Serialize(new { error = "Tool not found" }))
                    ]));
                    continue;
                }

                // Parse arguments from the tool call
                var argumentsJson = JsonSerializer.Serialize(toolCall.Arguments);
                var arguments = JsonDocument.Parse(argumentsJson).RootElement;
                var result = await tool.ExecuteAsync(userId, arguments);

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

            When you've completed your analysis (after 2-4 tool calls), respond with JSON in this format:
            {
              "recommendations": [
                {
                  "title": "Brief, attention-grabbing title",
                  "message": "Specific recommendation with evidence from your searches",
                  "type": "SpendingAlert|SavingsOpportunity|BehavioralInsight|BudgetWarning",
                  "priority": "Low|Medium|High|Critical"
                }
              ]
            }

            Think step-by-step. Use the search tool to explore before making recommendations.
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

        _logger.LogInformation("Stored {Count} recommendations for user {UserId}", newRecommendations.Count, userId);
    }
}
