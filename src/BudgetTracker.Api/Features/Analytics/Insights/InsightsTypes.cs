namespace BudgetTracker.Api.Features.Analytics.Insights;

public interface IInsightsService
{
    Task<BudgetInsights> GenerateInsightsAsync(string userId);
}

public class BudgetInsights
{
    public BudgetBreakdown BudgetBreakdown { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
    public BudgetHealth Health { get; set; } = new();
}

public class BudgetBreakdown
{
    public decimal NeedsPercentage { get; set; }
    public decimal WantsPercentage { get; set; }
    public decimal SavingsPercentage { get; set; }
    public decimal NeedsAmount { get; set; }
    public decimal WantsAmount { get; set; }
    public decimal SavingsAmount { get; set; }
    public decimal TotalExpenses { get; set; }
}

public class BudgetHealth
{
    public string Status { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public List<string> Areas { get; set; } = new();
}

internal static class BudgetBreakdownExtensions
{
    public static BudgetHealth CalculateHealth(this BudgetBreakdown breakdown)
    {
        var isHealthy = breakdown.NeedsPercentage <= 50 &&
                       breakdown.WantsPercentage <= 30 &&
                       breakdown.SavingsPercentage >= 20;

        var areas = new List<string>();
        if (breakdown.NeedsPercentage > 50) areas.Add("Needs spending is high");
        if (breakdown.WantsPercentage > 30) areas.Add("Discretionary spending is high");
        if (breakdown.SavingsPercentage < 20) areas.Add("Savings rate is low");

        return new BudgetHealth
        {
            Status = isHealthy ? "Healthy" : "Needs Attention",
            IsHealthy = isHealthy,
            Areas = areas
        };
    }
}
