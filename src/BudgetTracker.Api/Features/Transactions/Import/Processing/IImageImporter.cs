namespace BudgetTracker.Api.Features.Transactions.Import.Processing;

public interface IImageImporter
{
    Task<(ImportResult Result, List<Transaction> Transactions)> ProcessImageAsync(
        Stream imageStream, string sourceFileName, string userId, string account);
}
