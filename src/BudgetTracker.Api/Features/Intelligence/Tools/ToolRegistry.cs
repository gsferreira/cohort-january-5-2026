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
