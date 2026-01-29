namespace BudgetTracker.Api.Features.Transactions.Import.Enhancement;

public interface ITransactionEnhancer
{
    Task<List<EnhancedTransactionDescription>> EnhanceDescriptionsAsync(
        List<string> descriptions,
        string account,
        string userId,
        string? currentImportSessionHash = null);
}

public class EnhancedTransactionDescription
{
    public string OriginalDescription { get; set; } = string.Empty;
    public string EnhancedDescription { get; set; } = string.Empty;
    public string? SuggestedCategory { get; set; }
    public double ConfidenceScore { get; set; }
}
