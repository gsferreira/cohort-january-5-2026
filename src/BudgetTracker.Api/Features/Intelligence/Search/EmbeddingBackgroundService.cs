using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Intelligence.Search;

public class EmbeddingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmbeddingBackgroundService> _logger;
    private readonly TimeSpan _processingInterval = TimeSpan.FromMinutes(5);
    private const int BatchSize = 50;

    public EmbeddingBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<EmbeddingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Embedding background service started - processing new transactions only");

        using var timer = new PeriodicTimer(_processingInterval);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessPendingEmbeddings(stoppingToken);
        }
    }

    private async Task ProcessPendingEmbeddings(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BudgetTrackerContext>();
            var embeddingService = scope.ServiceProvider.GetRequiredService<IAzureEmbeddingService>();

            var cutoffTime = DateTime.UtcNow.AddHours(-24);
            var transactionsWithoutEmbeddings = await context.Transactions
                .Where(t => t.Embedding == null && t.ImportedAt >= cutoffTime)
                .OrderByDescending(t => t.ImportedAt)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            if (!transactionsWithoutEmbeddings.Any())
            {
                _logger.LogDebug("No recent transactions found that need embeddings");
                return;
            }

            _logger.LogInformation("Processing embeddings for {Count} recent transactions", transactionsWithoutEmbeddings.Count);

            var successCount = 0;
            var errorCount = 0;

            foreach (var transaction in transactionsWithoutEmbeddings)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    var embedding = await embeddingService.GenerateTransactionEmbeddingAsync(
                        transaction.Description,
                        transaction.Category);

                    transaction.Embedding = embedding;
                    successCount++;

                    _logger.LogDebug("Generated embedding for transaction {Id}: {Description}",
                        transaction.Id, transaction.Description[..Math.Min(transaction.Description.Length, 50)]);
                }
                catch (Exception ex)
                {
                    errorCount++;
                    _logger.LogWarning(ex, "Failed to generate embedding for transaction {Id}: {Description}",
                        transaction.Id, transaction.Description[..Math.Min(transaction.Description.Length, 50)]);

                    continue;
                }
            }

            if (successCount > 0)
            {
                await context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Successfully generated embeddings for {SuccessCount} transactions, {ErrorCount} errors",
                    successCount, errorCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during embedding processing");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Embedding background service is stopping");
        await base.StopAsync(cancellationToken);
    }
}
