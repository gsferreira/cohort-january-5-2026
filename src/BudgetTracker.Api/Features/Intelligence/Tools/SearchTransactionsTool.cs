using System.Text.Json;
using BudgetTracker.Api.Features.Intelligence.Search;

namespace BudgetTracker.Api.Features.Intelligence.Tools;

public class SearchTransactionsTool : IAgentTool
{
    private readonly ISemanticSearchService _searchService;
    private readonly ILogger<SearchTransactionsTool> _logger;

    public SearchTransactionsTool(
        ISemanticSearchService searchService,
        ILogger<SearchTransactionsTool> logger)
    {
        _searchService = searchService;
        _logger = logger;
    }

    public string Name => "SearchTransactions";

    public string Description =>
        "Search transactions using semantic search. Use this to find specific patterns, merchants, " +
        "or transaction types. Examples: 'subscriptions', 'coffee shops', 'shopping', " +
        "'dining'. Returns up to maxResults transactions with descriptions and amounts.";

    public BinaryData ParametersSchema => BinaryData.FromObjectAsJson(new
    {
        type = "object",
        properties = new
        {
            query = new
            {
                type = "string",
                description = "Natural language search query describing what transactions to find"
            },
            maxResults = new
            {
                type = "integer",
                description = "Maximum number of results to return (default: 10, max: 20)",
                @default = 10
            }
        },
        required = new[] { "query" }
    },
    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    public async Task<string> ExecuteAsync(string userId, JsonElement arguments)
    {
        try
        {
            var query = arguments.GetProperty("query").GetString()
                ?? throw new ArgumentException("Query is required");

            var maxResults = arguments.TryGetProperty("maxResults", out var maxResultsEl)
                ? maxResultsEl.GetInt32()
                : 10;

            maxResults = Math.Min(maxResults, 20);

            _logger.LogInformation("SearchTransactions called: query={Query}, maxResults={MaxResults}",
                query, maxResults);

            var results = await _searchService.FindRelevantTransactionsAsync(query, userId, maxResults);

            if (!results.Any())
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    count = 0,
                    message = "No transactions found matching the query.",
                    transactions = Array.Empty<object>()
                });
            }

            var transactions = results.Select(t => new
            {
                id = t.Id,
                date = t.Date.ToString("yyyy-MM-dd"),
                description = t.Description,
                amount = t.Amount,
                category = t.Category,
                account = t.Account
            }).ToList();

            return JsonSerializer.Serialize(new
            {
                success = true,
                count = transactions.Count,
                query,
                transactions
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing SearchTransactions tool");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }
}
