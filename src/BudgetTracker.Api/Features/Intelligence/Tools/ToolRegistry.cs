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
