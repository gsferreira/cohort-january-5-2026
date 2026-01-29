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
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(queryText);
            var vectorString = queryEmbedding.ToString();

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
