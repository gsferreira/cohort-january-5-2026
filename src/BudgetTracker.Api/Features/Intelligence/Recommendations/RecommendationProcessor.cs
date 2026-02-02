using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Intelligence.Recommendations;

public class RecommendationProcessor : IRecommendationWorker
{
    private readonly BudgetTrackerContext _context;
    private readonly IRecommendationRepository _repository;
    private readonly ILogger<RecommendationProcessor> _logger;

    public RecommendationProcessor(
        BudgetTrackerContext context,
        IRecommendationRepository repository,
        ILogger<RecommendationProcessor> logger)
    {
        _context = context;
        _repository = repository;
        _logger = logger;
    }

    public async Task ProcessAllUsersRecommendationsAsync()
    {
        try
        {
            // Get all users with transactions
            var userIds = await _context.Transactions
                .Select(t => t.UserId)
                .Distinct()
                .ToListAsync();

            _logger.LogInformation("Processing recommendations for {UserCount} users", userIds.Count);

            var successCount = 0;
            var errorCount = 0;

            foreach (var userId in userIds)
            {
                try
                {
                    await ProcessUserRecommendationsAsync(userId);
                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process recommendations for user {UserId}", userId);
                    errorCount++;
                }

                // Small delay to avoid overwhelming the system
                await Task.Delay(100);
            }

            _logger.LogInformation("Completed recommendation processing: {SuccessCount} successful, {ErrorCount} errors",
                successCount, errorCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process recommendations for all users");
        }
    }

    public async Task ProcessUserRecommendationsAsync(string userId)
    {
        try
        {
            await _repository.GenerateRecommendationsAsync(userId);
            _logger.LogDebug("Processed recommendations for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process recommendations for user {UserId}", userId);
            throw;
        }
    }
}
