using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Intelligence.Recommendations;

public class RecommendationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecommendationBackgroundService> _logger;
    private readonly TimeSpan _dailyRunTime = TimeSpan.FromHours(6); // 6 AM daily

    public RecommendationBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<RecommendationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        _logger.LogInformation("Recommendation background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IRecommendationWorker>();

                await processor.ProcessAllUsersRecommendationsAsync();
                await CleanupExpiredRecommendationsAsync();

                var nextRun = GetNextRunTime();
                var delay = nextRun - DateTime.UtcNow;

                if (delay > TimeSpan.Zero)
                {
                    _logger.LogInformation("Next recommendation run scheduled for {NextRun}", nextRun);
                    await Task.Delay(delay, stoppingToken);
                }
                else
                {
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Recommendation background service cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in recommendation background service");
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
        }

        _logger.LogInformation("Recommendation background service stopped");
    }

    private async Task CleanupExpiredRecommendationsAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BudgetTrackerContext>();

            var expiredRecommendations = await context.Recommendations
                .Where(r => r.Status == RecommendationStatus.Active && r.ExpiresAt <= DateTime.UtcNow)
                .ToListAsync();

            if (expiredRecommendations.Any())
            {
                foreach (var recommendation in expiredRecommendations)
                {
                    recommendation.Status = RecommendationStatus.Expired;
                }

                await context.SaveChangesAsync();
                _logger.LogInformation("Marked {Count} recommendations as expired", expiredRecommendations.Count);
            }

            var cutoffDate = DateTime.UtcNow.AddDays(-30);
            var oldRecommendations = await context.Recommendations
                .Where(r => r.GeneratedAt < cutoffDate)
                .ToListAsync();

            if (oldRecommendations.Any())
            {
                context.Recommendations.RemoveRange(oldRecommendations);
                await context.SaveChangesAsync();
                _logger.LogInformation("Deleted {Count} old recommendations", oldRecommendations.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup expired recommendations");
        }
    }

    private DateTime GetNextRunTime()
    {
        var now = DateTime.UtcNow;
        var today6Am = DateTime.UtcNow.Date.Add(_dailyRunTime);

        if (now < today6Am)
        {
            return today6Am; // Today at 6 AM UTC
        }
        else
        {
            return DateTime.UtcNow.Date.AddDays(1).Add(_dailyRunTime); // Tomorrow at 6 AM UTC
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Stopping recommendation background service...");
        await base.StopAsync(stoppingToken);
    }
}
