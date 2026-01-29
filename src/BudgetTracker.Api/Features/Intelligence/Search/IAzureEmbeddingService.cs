using Pgvector;

namespace BudgetTracker.Api.Features.Intelligence.Search;

public interface IAzureEmbeddingService
{
    Task<Vector> GenerateEmbeddingAsync(string text);

    Task<Vector> GenerateTransactionEmbeddingAsync(string description, string? category = null);
}
