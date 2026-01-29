using BudgetTracker.Api.Features.Transactions;

namespace BudgetTracker.Api.Features.Intelligence.Search;

public interface ISemanticSearchService
{
    Task<List<Transaction>> FindRelevantTransactionsAsync(string queryText, string userId, int maxResults = 50);
}
