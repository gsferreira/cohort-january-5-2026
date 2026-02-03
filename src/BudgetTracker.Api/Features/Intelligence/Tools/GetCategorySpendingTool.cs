using System.Text.Json;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Intelligence.Tools;

public class GetCategorySpendingTool : IAgentTool
{
    private readonly BudgetTrackerContext _context;
    private readonly ILogger<GetCategorySpendingTool> _logger;

    public GetCategorySpendingTool(
        BudgetTrackerContext context,
        ILogger<GetCategorySpendingTool> logger)
    {
        _context = context;
        _logger = logger;
    }

    public string Name => "GetCategorySpending";

    public string Description =>
        "Get spending totals grouped by category. Use this to answer questions about " +
        "how much was spent in each category, what the top spending categories are, " +
        "or to compare spending across categories. Returns category names with total amounts.";

    public BinaryData ParametersSchema => BinaryData.FromObjectAsJson(new
    {
        type = "object",
        properties = new
        {
            topN = new
            {
                type = "integer",
                description = "Number of top categories to return (default: 10, max: 20)",
                @default = 10
            },
            includeIncome = new
            {
                type = "boolean",
                description = "Include income categories (positive amounts). Default is false (expenses only).",
                @default = false
            }
        },
        required = Array.Empty<string>()
    },
    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    public async Task<string> ExecuteAsync(string userId, JsonElement arguments)
    {
        try
        {
            var topN = arguments.TryGetProperty("topN", out var topNEl)
                ? topNEl.GetInt32()
                : 10;

            var includeIncome = arguments.TryGetProperty("includeIncome", out var incomeEl)
                && incomeEl.GetBoolean();

            topN = Math.Min(topN, 20);

            _logger.LogInformation(
                "GetCategorySpending called: topN={TopN}, includeIncome={IncludeIncome}",
                topN, includeIncome);

            var query = _context.Transactions
                .Where(t => t.UserId == userId && t.Category != null);

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
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    count = 0,
                    message = "No categorized transactions found.",
                    categories = Array.Empty<object>()
                });
            }

            var results = categorySpending.Select(c => new
            {
                category = c.Category,
                total = Math.Abs(c.Total),
                transactionCount = c.Count
            }).ToList();

            var grandTotal = results.Sum(r => r.total);

            return JsonSerializer.Serialize(new
            {
                success = true,
                count = results.Count,
                grandTotal,
                categories = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing GetCategorySpending tool");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }
}
