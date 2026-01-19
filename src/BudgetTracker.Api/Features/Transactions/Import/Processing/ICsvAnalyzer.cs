namespace BudgetTracker.Api.Features.Transactions.Import.Processing;

public interface ICsvAnalyzer
{
    Task<string> AnalyzeCsvStructureAsync(string csvContent);
}
