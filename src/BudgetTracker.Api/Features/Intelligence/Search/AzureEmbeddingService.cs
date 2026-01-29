using Microsoft.Extensions.AI;
using Pgvector;

namespace BudgetTracker.Api.Features.Intelligence.Search;

public class AzureEmbeddingService : IAzureEmbeddingService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly ILogger<AzureEmbeddingService> _logger;

    public AzureEmbeddingService(
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        ILogger<AzureEmbeddingService> logger)
    {
        _embeddingGenerator = embeddingGenerator;
        _logger = logger;
    }

    public async Task<Vector> GenerateEmbeddingAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be null or empty", nameof(text));
        }

        try
        {
            var result = await _embeddingGenerator.GenerateAsync(text);
            return new Vector(result.Vector.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding for text: {Text}", text[..Math.Min(text.Length, 50)]);
            throw;
        }
    }

    public async Task<Vector> GenerateTransactionEmbeddingAsync(string description, string? category = null)
    {
        var text = string.IsNullOrEmpty(category)
            ? description
            : $"{description} [{category}]";

        return await GenerateEmbeddingAsync(text);
    }
}
