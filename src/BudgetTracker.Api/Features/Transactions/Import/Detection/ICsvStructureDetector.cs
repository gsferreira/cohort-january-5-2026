namespace BudgetTracker.Api.Features.Transactions.Import.Detection;

public interface ICsvStructureDetector
{
    Task<CsvStructureDetectionResult> DetectStructureAsync(Stream csvStream);
}
