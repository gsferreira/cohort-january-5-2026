namespace BudgetTracker.Api.Features.Transactions.Import.Detection;

public enum DetectionMethod
{
    RuleBased,
    AI
}

public class CsvStructureDetectionResult
{
    public char Delimiter { get; set; } = ',';
    public Dictionary<string, string> ColumnMappings { get; set; } = new();
    public string CultureCode { get; set; } = "en-US";
    public double ConfidenceScore { get; set; }
    public DetectionMethod DetectionMethod { get; set; }
}
