# Workshop Step 021: AI Transaction Enhancement Backend

## Mission

In this step, you'll implement the backend AI service that transforms cryptic bank transaction descriptions into readable text and suggests appropriate spending categories. This builds directly on your Azure OpenAI setup from Week 2.

**Your goal**: Create a complete AI enhancement service that integrates with your CSV import process to automatically improve transaction data quality.

**Learning Objectives**:
- Using IChatClient from Microsoft.Extensions.AI
- Service pattern implementation with dependency injection
- AI prompt engineering for financial data
- JSON response parsing with graceful fallbacks
- Error handling and session tracking

---

## Prerequisites

Before starting, ensure you completed:
- Week 2 Azure AI setup with IChatClient registered in DI

---

## Step 21.1: Create Enhancement Service Interface

*Define the contract for the AI enhancement service with clear input/output types.*

Create `src/BudgetTracker.Api/Features/Transactions/Import/Enhancement/ITransactionEnhancer.cs`:

```csharp
namespace BudgetTracker.Api.Features.Transactions.Import.Enhancement;

public interface ITransactionEnhancer
{
    Task<List<EnhancedTransactionDescription>> EnhanceDescriptionsAsync(
        List<string> descriptions,
        string account,
        string userId,
        string? currentImportSessionHash = null);
}

public class EnhancedTransactionDescription
{
    public string OriginalDescription { get; set; } = string.Empty;
    public string EnhancedDescription { get; set; } = string.Empty;
    public string? SuggestedCategory { get; set; }
    public double ConfidenceScore { get; set; }
}
```

## Step 21.2: Implement AI Enhancement Service

*Create the core service that integrates with Azure OpenAI via IChatClient to enhance transaction descriptions and suggest categories.*

Create `src/BudgetTracker.Api/Features/Transactions/Import/Enhancement/TransactionEnhancer.cs`:

```csharp
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
```

## Step 21.3: Update Transaction Model

*Add session tracking for import management.*

Update `src/BudgetTracker.Api/Features/Transactions/TransactionTypes.cs` to add the ImportSessionHash field:

```csharp
// Add this new field to the Transaction class for session tracking
[MaxLength(50)]
public string? ImportSessionHash { get; set; }
```

## Step 21.4: Create Database Migration

*Add the ImportSessionHash field to the database schema.*

```bash
cd src/BudgetTracker.Api/
dotnet ef migrations add AddImportSessionHash
dotnet ef database update
```

## Step 21.5: Register AI Services

*Configure dependency injection for the enhancement service.*

Update `src/BudgetTracker.Api/Program.cs`:

```csharp
using BudgetTracker.Api.Features.Transactions.Import.Enhancement;

// ... existing code ...

// Register enhancement service (IChatClient is already registered from Week 2)
builder.Services.AddScoped<ITransactionEnhancer, TransactionEnhancer>();

// ... rest of existing configuration ...
```

## Step 21.6: Update Import Result Types

*Add enhancement tracking to the import response.*

Update `src/BudgetTracker.Api/Features/Transactions/Import/ImportResult.cs`:

```csharp
namespace BudgetTracker.Api.Features.Transactions.Import;

public class ImportResult
{
    public int TotalRows { get; set; }
    public int ImportedCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> Errors { get; set; } = new();
    public string? SourceFile { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    // Enhancement support for multi-step workflow
    public string ImportSessionHash { get; set; } = string.Empty;
    public List<TransactionEnhancementResult> Enhancements { get; set; } = new();
}

public class TransactionEnhancementResult
{
    public Guid TransactionId { get; set; }
    public string ImportSessionHash { get; set; } = string.Empty;
    public int TransactionIndex { get; set; }
    public string OriginalDescription { get; set; } = string.Empty;
    public string EnhancedDescription { get; set; } = string.Empty;
    public string? SuggestedCategory { get; set; }
    public double ConfidenceScore { get; set; }
}

public class EnhanceImportRequest
{
    public string ImportSessionHash { get; set; } = string.Empty;
    public List<TransactionEnhancementResult> Enhancements { get; set; } = new();
    public double MinConfidenceScore { get; set; } = 0.5;
    public bool ApplyEnhancements { get; set; } = true;
}

public class EnhanceImportResult
{
    public string ImportSessionHash { get; set; } = string.Empty;
    public int TotalTransactions { get; set; }
    public int EnhancedCount { get; set; }
    public int SkippedCount { get; set; }
}
```

## Step 21.7: Integrate with CSV Import

*Update the import API to use AI enhancement.*

Update `src/BudgetTracker.Api/Features/Transactions/Import/ImportApi.cs`:

```csharp
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BudgetTracker.Api.Auth;
using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Features.Transactions.Import.Processing;
using BudgetTracker.Api.Features.Transactions.Import.Enhancement;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace BudgetTracker.Api.Features.Transactions.Import;

public static class ImportApi
{
    public static IEndpointRouteBuilder MapTransactionImportEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/import", ImportAsync)
            .DisableAntiforgery();

        routes.MapPost("/import/enhance", EnhanceImportAsync);

        return routes;
    }

    private static async Task<Results<Ok<ImportResult>, BadRequest<string>>> ImportAsync(
        IFormFile file,
        [FromForm] string account,
        CsvImporter csvImporter,
        ITransactionEnhancer enhancer,
        BudgetTrackerContext context,
        ClaimsPrincipal claimsPrincipal)
    {
        var validationResult = ValidateFileInput(file, account);
        if (validationResult != null)
        {
            return validationResult;
        }

        try
        {
            var userId = claimsPrincipal.GetUserId();
            var sessionHash = GenerateSessionHash(file.FileName, DateTime.UtcNow);

            using var stream = file.OpenReadStream();
            var (result, transactions) = await csvImporter.ParseCsvAsync(stream, file.FileName, userId, account);

            if (transactions.Any())
            {
                // Extract descriptions for AI enhancement
                var descriptions = transactions.Select(t => t.Description).ToList();

                // Enhance descriptions with AI (includes categories)
                var enhancements = await enhancer.EnhanceDescriptionsAsync(
                    descriptions, account, userId, sessionHash);

                // Create enhancement results for preview
                var enhancementResults = new List<TransactionEnhancementResult>();

                for (var i = 0; i < transactions.Count; i++)
                {
                    var transaction = transactions[i];
                    var enhancement = enhancements.FirstOrDefault(e =>
                        e.OriginalDescription == transaction.Description) ?? enhancements[i];

                    // Set session hash for tracking
                    transaction.ImportSessionHash = sessionHash;

                    enhancementResults.Add(new TransactionEnhancementResult
                    {
                        TransactionId = transaction.Id,
                        ImportSessionHash = sessionHash,
                        TransactionIndex = i,
                        OriginalDescription = enhancement.OriginalDescription,
                        EnhancedDescription = enhancement.EnhancedDescription,
                        SuggestedCategory = enhancement.SuggestedCategory,
                        ConfidenceScore = enhancement.ConfidenceScore
                    });
                }

                // Save transactions with original descriptions
                // (enhancements applied later via enhance endpoint)
                await context.Transactions.AddRangeAsync(transactions);
                await context.SaveChangesAsync();

                result.ImportSessionHash = sessionHash;
                result.Enhancements = enhancementResults;
            }

            return TypedResults.Ok(result);
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest($"Import failed: {ex.Message}");
        }
    }

    private static async Task<Results<Ok<EnhanceImportResult>, BadRequest<string>>> EnhanceImportAsync(
        [FromBody] EnhanceImportRequest request,
        BudgetTrackerContext context,
        ClaimsPrincipal claimsPrincipal)
    {
        try
        {
            var userId = claimsPrincipal.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return TypedResults.BadRequest("User not authenticated");

            var enhancedCount = 0;

            if (request.ApplyEnhancements)
            {
                var transactions = await context.Transactions
                    .Where(t => t.UserId == userId && t.ImportSessionHash == request.ImportSessionHash)
                    .ToListAsync();

                foreach (var enhancement in request.Enhancements)
                {
                    if (enhancement.ConfidenceScore < request.MinConfidenceScore)
                        continue;

                    var transaction = transactions.FirstOrDefault(t => t.Id == enhancement.TransactionId);
                    if (transaction == null)
                        continue;

                    transaction.Description = enhancement.EnhancedDescription;

                    if (!string.IsNullOrEmpty(enhancement.SuggestedCategory))
                    {
                        transaction.Category = enhancement.SuggestedCategory;
                    }

                    enhancedCount++;
                }

                if (enhancedCount > 0)
                {
                    await context.SaveChangesAsync();
                }
            }

            return TypedResults.Ok(new EnhanceImportResult
            {
                ImportSessionHash = request.ImportSessionHash,
                TotalTransactions = request.Enhancements.Count,
                EnhancedCount = enhancedCount,
                SkippedCount = request.Enhancements.Count - enhancedCount
            });
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest($"Enhancement failed: {ex.Message}");
        }
    }

    private static string GenerateSessionHash(string fileName, DateTime timestamp)
    {
        var input = $"{fileName}_{timestamp:yyyyMMddHHmmss}_{Guid.NewGuid()}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash)[..12];
    }

    private static BadRequest<string>? ValidateFileInput(IFormFile file, string account)
    {
        if (file == null || file.Length == 0)
            return TypedResults.BadRequest("No file uploaded");

        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            return TypedResults.BadRequest("Only CSV files are supported");

        if (file.Length > 10 * 1024 * 1024)
            return TypedResults.BadRequest("File size exceeds 10MB limit");

        if (string.IsNullOrWhiteSpace(account))
            return TypedResults.BadRequest("Account name is required");

        return null;
    }
}
```

## Step 21.8: Test AI Enhancement

*Test the complete AI-enhanced import flow.*

### 12.8.1: Test with VS Code REST Client

Create or update `test-api.http`:

```http
### Test AI-Enhanced Import with Sample File
POST http://localhost:5295/api/transactions/import
X-API-Key: test-key-user1
Content-Type: multipart/form-data; boundary=WebAppBoundary

--WebAppBoundary
Content-Disposition: form-data; name="account"

Checking Account
--WebAppBoundary
Content-Disposition: form-data; name="file"; filename="sample.csv"
Content-Type: text/csv

Date,Description,Amount,Balance
01/15/2025,AMZN,-45.67,1250.33
01/16/2025,STARBUCKS COFFEE #1234,-5.89,1244.44
01/17/2025,DD VODAFONE PORTU 222111000,-52.30,1192.14
01/18/2025,NFLX Subscription,-15.99,1176.15
--WebAppBoundary--

### Apply Enhancements (use importSessionHash from import response)
POST http://localhost:5295/api/transactions/import/enhance
X-API-Key: test-key-user1
Content-Type: application/json

{
  "importSessionHash": "YOUR_SESSION_HASH_HERE",
  "enhancements": [],
  "minConfidenceScore": 0.5,
  "applyEnhancements": true
}

### View Enhanced Transactions
GET http://localhost:5295/api/transactions
X-API-Key: test-key-user1
```

**Expected AI enhancements:**
- "AMZN" → "Amazon" (Category: Shopping)
- "STARBUCKS COFFEE #1234" → "Starbucks Coffee" (Category: Food & Drink)
- "DD VODAFONE PORTU 222111000" → "Vodafone Portugal - Direct Debit" (Category: Utilities)
- "NFLX Subscription" → "Netflix Subscription" (Category: Entertainment)

---

## Troubleshooting

**IChatClient not registered:**
- Ensure you completed the Week 2 setup with IChatClient in DI
- Check that Azure AI configuration is correct in user secrets

**AI Response Parsing Errors:**
- Check application logs for AI response format
- Verify JSON extraction regex is working
- Service gracefully falls back to originals on errors

**Database Migration Issues:**
- Run `dotnet ef database update` to apply new schema
- Check that ImportSessionHash column exists in database

**Enhancement Mapping Errors:**
- Ensure transactions array order matches enhancements array order
- Check the logging output to debug mismatches

---

## Summary

You've successfully implemented:

- **AI Enhancement Service**: Using IChatClient from Microsoft.Extensions.AI
- **Combined Enhancement**: Both description cleanup and category suggestions in one call
- **Session Tracking**: Import session management for multi-step workflow
- **Graceful Fallbacks**: Original data preserved when AI fails
- **Two-Step Flow**: Import first, then apply enhancements

**Next Step**: Move to `022-react-ai-enhancement-ui.md` to build the frontend for reviewing and applying AI suggestions.
