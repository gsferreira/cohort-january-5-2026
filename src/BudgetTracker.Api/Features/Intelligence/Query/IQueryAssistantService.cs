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
