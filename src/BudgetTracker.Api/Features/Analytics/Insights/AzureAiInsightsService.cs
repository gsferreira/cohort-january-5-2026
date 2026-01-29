using Microsoft.Extensions.AI;
using BudgetTracker.Api.Features.Transactions;
using BudgetTracker.Api.Features.Transactions.List;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Analytics.Insights;

public class AzureAiInsightsService : IInsightsService
{
    private readonly BudgetTrackerContext _context;
    private readonly IChatClient _chatClient;
    private readonly ILogger<AzureAiInsightsService> _logger;

    public AzureAiInsightsService(
        BudgetTrackerContext context,
        IChatClient chatClient,
        ILogger<AzureAiInsightsService> logger)
    {
        _context = context;
        _chatClient = chatClient;
        _logger = logger;
    }

    public async Task<BudgetInsights> GenerateInsightsAsync(string userId)
    {
        try
        {
            var transactions = await _context.Transactions
                .Where(t => t.UserId == userId)
                .ToListAsync();

            if (transactions.Count == 0)
            {
                return new BudgetInsights
                {
                    Summary = "No transactions available for analysis.",
                    Health = new BudgetHealth { Status = "No Data", IsHealthy = false, Areas = [] }
                };
            }

            var budgetBreakdown = CalculateBudgetBreakdown(transactions);
            var summary = await GenerateAiSummaryAsync(budgetBreakdown, transactions);

            return new BudgetInsights
            {
                BudgetBreakdown = budgetBreakdown,
                Summary = summary,
                Health = budgetBreakdown.CalculateHealth()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate insights for user {UserId}", userId);
            return new BudgetInsights
            {
                Summary = "Unable to generate insights at this time.",
                Health = new BudgetHealth { Status = "Error", IsHealthy = false, Areas = [] }
            };
        }
    }

    private BudgetBreakdown CalculateBudgetBreakdown(List<Transaction> transactions)
    {
        var expenses = transactions.Where(t => t.Amount < 0).ToList();
        var totalExpenses = Math.Abs(expenses.Sum(t => t.Amount));

        if (totalExpenses == 0)
        {
            return new BudgetBreakdown();
        }

        var needsCategories = new[] { "Housing", "Transportation", "Groceries", "Healthcare", "Utilities", "Insurance" };
        var savingsCategories = new[] { "Savings", "Investment", "Retirement" };

        var needsAmount = Math.Abs(expenses
            .Where(t => needsCategories.Contains(t.Category, StringComparer.OrdinalIgnoreCase))
            .Sum(t => t.Amount));

        var savingsAmount = Math.Abs(expenses
            .Where(t => savingsCategories.Contains(t.Category, StringComparer.OrdinalIgnoreCase))
            .Sum(t => t.Amount));

        var wantsAmount = totalExpenses - needsAmount - savingsAmount;

        return new BudgetBreakdown
        {
            TotalExpenses = totalExpenses,
            NeedsAmount = needsAmount,
            WantsAmount = wantsAmount,
            SavingsAmount = savingsAmount,
            NeedsPercentage = totalExpenses > 0 ? Math.Round((needsAmount / totalExpenses) * 100, 1) : 0,
            WantsPercentage = totalExpenses > 0 ? Math.Round((wantsAmount / totalExpenses) * 100, 1) : 0,
            SavingsPercentage = totalExpenses > 0 ? Math.Round((savingsAmount / totalExpenses) * 100, 1) : 0
        };
    }

    private async Task<string> GenerateAiSummaryAsync(
        BudgetBreakdown breakdown, List<Transaction> transactions)
    {
        var systemPrompt = CreateSystemPrompt();
        var userPrompt = CreateUserPrompt(breakdown, transactions);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userPrompt)
        };

        var response = await _chatClient.GetResponseAsync(messages);
        return ParseAiResponse(response.Text ?? string.Empty);
    }

    private static string CreateSystemPrompt()
    {
        return """
               You are a financial analyst providing budget insights using the 50/30/20 budget method.

               The 50/30/20 rule suggests:
               - 50% of after-tax income for needs (housing, transportation, groceries, healthcare, utilities)
               - 20% for savings and debt repayment
               - 30% for wants (entertainment, dining out, hobbies, shopping)

               Provide a concise 2-3 sentence summary analyzing their spending breakdown compared to the 50/20/30 rule.
               Focus on factual analysis, not recommendations.

               Return only the summary text, no JSON formatting.
               """;
    }

    private static string CreateUserPrompt(BudgetBreakdown breakdown, List<Transaction> transactions)
    {
        var totalTransactions = transactions.Count;
        var timeSpan = transactions.Any() ?
            (transactions.Max(t => t.Date) - transactions.Min(t => t.Date)).Days : 0;

        return $"""
               Analyze this spending breakdown:
               - Needs: {breakdown.NeedsPercentage}% (${breakdown.NeedsAmount:F2})
               - Wants: {breakdown.WantsPercentage}% (${breakdown.WantsAmount:F2})
               - Savings: {breakdown.SavingsPercentage}% (${breakdown.SavingsAmount:F2})
               - Total Expenses: ${breakdown.TotalExpenses:F2}
               - Transaction Count: {totalTransactions}
               - Time Period: {timeSpan} days
               """;
    }

    private string ParseAiResponse(string content)
    {
        return !string.IsNullOrWhiteSpace(content)
            ? content.Trim()
            : "Your spending has been analyzed according to the 50/20/30 budget rule.";
    }
}
