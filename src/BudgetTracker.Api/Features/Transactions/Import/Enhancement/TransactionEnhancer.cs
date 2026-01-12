using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace BudgetTracker.Api.Features.Transactions.Import.Enhancement;

public class TransactionEnhancer : ITransactionEnhancer
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<TransactionEnhancer> _logger;

    public TransactionEnhancer(
        IChatClient chatClient,
        ILogger<TransactionEnhancer> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
    }

    public async Task<List<EnhancedTransactionDescription>> EnhanceDescriptionsAsync(
        List<string> descriptions,
        string account,
        string userId,
        string? currentImportSessionHash = null)
    {
        if (!descriptions.Any())
            return new List<EnhancedTransactionDescription>();

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var systemPrompt = CreateSystemPrompt();
            var userPrompt = CreateUserPrompt(descriptions);

            var response = await _chatClient.GetResponseAsync([
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, userPrompt)
            ]);

            var content = response.Text ?? string.Empty;
            var results = ParseEnhancedDescriptions(content, descriptions);

            _logger.LogInformation("AI processing completed in {ProcessingTime}ms", stopwatch.ElapsedMilliseconds);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enhance transaction descriptions");
            return CreateFallbackResponse(descriptions);
        }
    }

    private static string CreateSystemPrompt()
    {
        return """
            You are a transaction enhancement and categorization assistant. Your job is to clean up messy bank transaction descriptions and suggest appropriate spending categories.

            Guidelines:
            1. Transform cryptic merchant codes and bank jargon into clear, readable descriptions
            2. Remove unnecessary reference numbers, codes, and technical identifiers
            3. Identify the actual merchant or service provider
            4. Suggest appropriate spending categories based on the merchant type and transaction purpose
            5. Maintain accuracy - don't invent information not present in the original

            Examples:
            - "AMZN MKTP US*123456789" → "Amazon Marketplace Purchase" (Category: Shopping)
            - "STARBUCKS COFFEE #1234" → "Starbucks Coffee" (Category: Food & Drink)
            - "SHELL OIL #4567" → "Shell Gas Station" (Category: Gas & Fuel)
            - "DD VODAFONE PORTU 222111000" → "Vodafone Portugal - Direct Debit" (Category: Utilities)
            - "COMPRA 0000 TEMU.COM DUBLIN" → "Temu Online Purchase" (Category: Shopping)
            - "TRF MB WAY P/ Manuel Silva" → "MB WAY Transfer to Manuel Silva" (Category: Transfer)

            Common categories to use:
            - Shopping, Groceries, Food & Drink, Entertainment, Gas & Fuel
            - Utilities, Transportation, Healthcare, Transfer, Cash & ATM
            - Technology, Subscriptions, Travel, Education, Other

            Respond with a JSON array where each object has:
            - "originalDescription": the input description
            - "enhancedDescription": the cleaned description
            - "suggestedCategory": appropriate category from the list above
            - "confidenceScore": number between 0-1 indicating confidence in both enhancement and categorization

            Be conservative with confidence scores - only use high scores (>0.8) when you're very certain about the merchant identification and category.
            """;
    }

    private static string CreateUserPrompt(List<string> descriptions)
    {
        var descriptionsJson = JsonSerializer.Serialize(descriptions);
        return $"Please enhance these transaction descriptions:\n{descriptionsJson}";
    }

    private List<EnhancedTransactionDescription> ParseEnhancedDescriptions(
        string content,
        List<string> originalDescriptions)
    {
        try
        {
            var jsonContent = ExtractJsonFromCodeBlock(content);
            var enhancedDescriptions = JsonSerializer.Deserialize<List<EnhancedTransactionDescription>>(
                jsonContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (enhancedDescriptions?.Count == originalDescriptions.Count)
            {
                return enhancedDescriptions;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI response as JSON: {Content}", content);
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(ex, "Failed to extract JSON from AI response");
        }

        _logger.LogWarning("AI response format was invalid, returning original descriptions");
        return CreateFallbackResponse(originalDescriptions);
    }

    private static string ExtractJsonFromCodeBlock(string input)
    {
        // Look for content between ```json and ``` markers
        var match = Regex.Match(input, @"```json\s*([\s\S]*?)\s*```");

        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        // Try to find a JSON array directly
        var arrayMatch = Regex.Match(input, @"\[[\s\S]*\]");
        if (arrayMatch.Success)
        {
            return arrayMatch.Value;
        }

        throw new FormatException("Could not extract JSON from the input string");
    }

    private static List<EnhancedTransactionDescription> CreateFallbackResponse(List<string> descriptions)
    {
        return descriptions.Select(d => new EnhancedTransactionDescription
        {
            OriginalDescription = d,
            EnhancedDescription = d,
            SuggestedCategory = null,
            ConfidenceScore = 0.0
        }).ToList();
    }
}
