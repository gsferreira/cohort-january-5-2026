using System.ComponentModel;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Intelligence.Tools;

public class GetCategorySpendingTool
{
    private readonly BudgetTrackerContext _context;
    private readonly IAgentContext _agentContext;
    private readonly ILogger<GetCategorySpendingTool> _logger;

    public GetCategorySpendingTool(
        BudgetTrackerContext context,
        IAgentContext agentContext,
        ILogger<GetCategorySpendingTool> logger)
    {
        _context = context;
        _agentContext = agentContext;
        _logger = logger;
    }

    [Description("Get spending totals grouped by category. Use this to answer questions about " +
                 "how much was spent in each category, what the top spending categories are, " +
                 "or to compare spending across categories. Returns category names with total amounts.")]
    public async Task<CategorySpendingResult> GetCategorySpendingAsync(
        [Description("Number of top categories to return (default: 10, max: 20)")]
        int topN = 10,
        [Description("Include income categories (positive amounts). Default is false (expenses only).")]
        bool includeIncome = false)
    {
        _logger.LogInformation(
            "GetCategorySpending called: topN={TopN}, includeIncome={IncludeIncome}",
            topN, includeIncome);

        topN = Math.Min(topN, 20);

        var query = _context.Transactions
            .Where(t => t.UserId == _agentContext.UserId && t.Category != null);

        if (!includeIncome)
        {
            query = query.Where(t => t.Amount < 0);
        }

        var categorySpending = await query
            .GroupBy(t => t.Category)
            .Select(g => new
            {
                Category = g.Key,
                Total = g.Sum(t => t.Amount),
                Count = g.Count()
            })
            .OrderBy(c => c.Total) // Most negative (biggest expense) first
            .Take(topN)
            .ToListAsync();

        if (!categorySpending.Any())
        {
            return new CategorySpendingResult
            {
                Success = true,
                Count = 0,
                Message = "No categorized transactions found.",
                GrandTotal = 0,
                Categories = []
            };
        }

        var results = categorySpending.Select(c => new CategorySpendingItem
        {
            Category = c.Category ?? "Uncategorized",
            Total = Math.Abs(c.Total),
            TransactionCount = c.Count
        }).ToList();

        var grandTotal = results.Sum(r => r.Total);

        return new CategorySpendingResult
        {
            Success = true,
            Count = results.Count,
            GrandTotal = grandTotal,
            Categories = results
        };
    }
}

public class CategorySpendingResult
{
    public bool Success { get; init; }
    public int Count { get; init; }
    public string? Message { get; init; }
    public decimal GrandTotal { get; init; }
    public List<CategorySpendingItem> Categories { get; init; } = [];
}

public class CategorySpendingItem
{
    public string Category { get; init; } = string.Empty;
    public decimal Total { get; init; }
    public int TransactionCount { get; init; }
}
