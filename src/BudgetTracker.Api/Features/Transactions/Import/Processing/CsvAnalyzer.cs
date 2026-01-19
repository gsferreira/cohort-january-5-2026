using Microsoft.Extensions.AI;

namespace BudgetTracker.Api.Features.Transactions.Import.Processing;

public class CsvAnalyzer : ICsvAnalyzer
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<CsvAnalyzer> _logger;

    public CsvAnalyzer(IChatClient chatClient, ILogger<CsvAnalyzer> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
    }

    public async Task<string> AnalyzeCsvStructureAsync(string csvContent)
    {
        var systemPrompt = CreateSystemPrompt();
        var userPrompt = CreateUserPrompt(csvContent);

        try
        {
            var response = await _chatClient.GetResponseAsync([
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, userPrompt)
            ]);

            return response.Text ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze CSV structure via AI");
            throw;
        }
    }

    private static string CreateSystemPrompt()
    {
        return """
            You are a CSV structure analyzer. Analyze the provided CSV content and determine:
            1. The delimiter used (comma, semicolon, tab, or pipe)
            2. The culture/locale for number and date formatting
            3. Column mappings to standard fields: Date, Description, Amount, Balance, Category

            Respond with a JSON object containing:
            - "delimiter": the character used as delimiter (single char: "," or ";" or "\t" or "|")
            - "cultureCode": the locale code (e.g., "en-US", "pt-PT", "de-DE", "fr-FR")
            - "columnMappings": object mapping CSV column names to standard fields
            - "confidenceScore": number between 0-1 indicating your confidence

            Example response:
            ```json
            {
                "delimiter": ";",
                "cultureCode": "pt-PT",
                "columnMappings": {
                    "Data": "Date",
                    "Descrição": "Description",
                    "Valor": "Amount",
                    "Saldo": "Balance"
                },
                "confidenceScore": 0.95
            }
            ```

            Important rules:
            - For delimiter detection, check the header row pattern
            - For culture detection, look at date formats (DD/MM/YYYY vs MM/DD/YYYY) and number formats (1.234,56 vs 1,234.56)
            - Column mappings should map the actual CSV column names to these standard fields: Date, Description, Amount, Balance, Category
            - Be conservative with confidence scores - only use high scores (>0.85) when patterns are clear
            """;
    }

    private static string CreateUserPrompt(string csvContent)
    {
        return $"Please analyze this CSV content and determine its structure:\n\n{csvContent}";
    }
}
