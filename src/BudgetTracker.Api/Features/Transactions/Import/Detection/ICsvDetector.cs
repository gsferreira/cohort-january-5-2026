namespace BudgetTracker.Api.Features.Transactions.Import.Detection;

public interface ICsvDetector
{
    Task<CsvStructureDetectionResult> AnalyzeCsvStructureAsync(Stream csvStream);
}
