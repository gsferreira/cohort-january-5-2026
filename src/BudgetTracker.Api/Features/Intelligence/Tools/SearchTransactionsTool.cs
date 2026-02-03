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
