# Workshop Step 033: Multimodal Image Import Backend

## Mission

In this step, you'll add multimodal AI capabilities to your budget tracker, allowing users to upload images of bank statements and automatically extract transaction data using GPT-4 Vision. The AI will analyze statement images and convert them into structured transaction data.

**Your goal**: Implement image processing functionality that can extract transactions from uploaded bank statement images using Azure OpenAI's vision capabilities.

**Learning Objectives**:
- Implementing multimodal AI with GPT-4 Vision for document processing
- Creating image processing workflows for financial data extraction
- Integrating image uploads with existing CSV import infrastructure
- Building reliable data extraction from unstructured image sources
- Handling confidence scoring and validation for AI-extracted data

---

## Prerequisites

Before starting, ensure you completed:
- [031-smart-csv-detection-backend.md](031-smart-csv-detection-backend.md) - CSV detection backend
- [032-smart-csv-detection-ui.md](032-smart-csv-detection-ui.md) - Smart CSV detection UI

---

## Step 33.1: Create Image Import Interface

*Define the interface for processing bank statement images.*

The image import functionality needs a dedicated interface to handle the processing of uploaded images. This interface will be responsible for converting image streams into structured transaction data that can be integrated with the existing import workflow.

Create `src/BudgetTracker.Api/Features/Transactions/Import/Processing/IImageImporter.cs`:

```csharp
using BudgetTracker.Api.Features.Transactions;
using BudgetTracker.Api.Features.Transactions.List;

namespace BudgetTracker.Api.Features.Transactions.Import.Processing;

public interface IImageImporter
{
    Task<(ImportResult Result, List<Transaction> Transactions)> ProcessImageAsync(
        Stream imageStream, string sourceFileName, string userId, string account);
}
```

## Step 33.2: Verify JSON Extraction Extension Exists

*Verify the shared utility for parsing JSON responses from AI code blocks.*

The `StringExtensions.cs` file with the `ExtractJsonFromCodeBlock` method was already created in Step 31.4. This extension method handles parsing JSON responses from AI that may be wrapped in code blocks.

Verify that `src/BudgetTracker.Api/Infrastructure/Extensions/StringExtensions.cs` exists with the `ExtractJsonFromCodeBlock` method. If not, refer back to [031-smart-csv-detection-backend.md](031-smart-csv-detection-backend.md).

## Step 33.3: Update Transaction Enhancer to Use Extension

*Refactor the existing transaction enhancer to use the shared JSON extraction utility.*

The `TransactionEnhancer` currently has its own `ExtractJsonFromCodeBlock` method. Update it to use the shared extension method.

Update `src/BudgetTracker.Api/Features/Transactions/Import/Enhancement/TransactionEnhancer.cs`:

```csharp
using BudgetTracker.Api.Infrastructure.Extensions; // Add this using statement

// In the ParseEnhancedDescriptions method, change:
var enhancedDescriptions = JsonSerializer.Deserialize<List<EnhancedTransactionDescription>>(
    content.ExtractJsonFromCodeBlock(), // Use extension method
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

// Remove the old ExtractJsonFromCodeBlock private method
```

## Step 33.4: Implement Image Processing Service

*Create the core image processing service using Azure OpenAI Vision capabilities.*

The `ImageImporter` service will handle the complete workflow of processing bank statement images: converting images to base64, sending them to GPT-4 Vision for analysis, and parsing the AI response into structured transaction data.

Create `src/BudgetTracker.Api/Features/Transactions/Import/Processing/ImageImporter.cs`:

```csharp
using System.Text.Json;
using Microsoft.Extensions.AI;
using BudgetTracker.Api.Features.Transactions;
using BudgetTracker.Api.Features.Transactions.List;
using BudgetTracker.Api.Infrastructure.Extensions;

namespace BudgetTracker.Api.Features.Transactions.Import.Processing;

public class ImageImporter : IImageImporter
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<ImageImporter> _logger;

    public ImageImporter(
        IChatClient chatClient,
        ILogger<ImageImporter> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
    }

    public async Task<(ImportResult Result, List<Transaction> Transactions)> ProcessImageAsync(
        Stream imageStream, string sourceFileName, string userId, string account)
    {
        var result = new ImportResult
        {
            SourceFile = sourceFileName,
            ImportedAt = DateTime.UtcNow
        };

        var transactions = new List<Transaction>();

        try
        {
            // Convert image to bytes
            var imageBytes = await ReadImageBytesAsync(imageStream);

            _logger.LogInformation("Processing bank statement image {FileName} ({Size} bytes)",
                sourceFileName, imageBytes.Length);

            // Process image with GPT-4 Vision
            var extractedData = await ExtractTransactionsFromImageAsync(imageBytes, sourceFileName);

            // Parse and validate results
            var (parseResult, parsedTransactions) = ParseExtractionResults(extractedData, sourceFileName, userId, account);

            // Merge results
            result.TotalRows = parseResult.TotalRows;
            result.ImportedCount = parseResult.ImportedCount;
            result.FailedCount = parseResult.FailedCount;
            result.Errors.AddRange(parseResult.Errors);

            return (result, parsedTransactions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process image {FileName}", sourceFileName);
            result.Errors.Add($"Image processing error: {ex.Message}. Please ensure the image shows a clear bank statement.");
            return (result, transactions);
        }
    }

    private async Task<byte[]> ReadImageBytesAsync(Stream imageStream)
    {
        using var memoryStream = new MemoryStream();
        await imageStream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }

    private async Task<string> ExtractTransactionsFromImageAsync(byte[] imageBytes, string fileName)
    {
        var systemPrompt = CreateTransactionExtractionPrompt();
        var mediaType = GetMediaType(fileName);

        var response = await _chatClient.GetResponseAsync([
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, [
                new TextContent("Extract all transactions from this bank statement image:"),
                new DataContent(imageBytes, mediaType)
            ])
        ]);

        return response.Text ?? string.Empty;
    }

    private static string GetMediaType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "image/png"
        };
    }

    private string CreateTransactionExtractionPrompt()
    {
        return """
            You are a financial data extraction specialist. Extract transaction data from bank statement images.

            Return a JSON object with this exact structure:
            {
              "confidence_score": 0.95,
              "transactions": [
                {
                  "date": "2024-01-15",
                  "description": "STARBUCKS COFFEE #1234",
                  "amount": -4.50,
                  "balance": 1234.56,
                  "category": null
                }
              ]
            }

            Guidelines:
            - Extract ALL visible transactions from the statement
            - Use negative amounts for debits/expenses, positive for credits/income
            - Include running balance if visible
            - Date format: YYYY-MM-DD
            - Leave category as null (will be enhanced later)
            - Provide confidence score (0.0-1.0) based on image clarity and data completeness
            - If no transactions found, return empty transactions array with confidence explanation
            """;
    }

    private (ImportResult Result, List<Transaction> Transactions) ParseExtractionResults(
        string extractedData, string sourceFileName, string userId, string account)
    {
        var result = new ImportResult
        {
            SourceFile = sourceFileName,
            ImportedAt = DateTime.UtcNow
        };

        var transactions = new List<Transaction>();

        try
        {
            var jsonDocument = JsonDocument.Parse(extractedData.ExtractJsonFromCodeBlock());
            var root = jsonDocument.RootElement;

            if (root.TryGetProperty("confidence_score", out var confidenceElement))
            {
                var confidence = confidenceElement.GetDouble();
                _logger.LogInformation("Image extraction confidence: {Confidence}", confidence);

                if (confidence < 0.7)
                {
                    result.Errors.Add($"Low confidence extraction ({confidence:P0}). Please verify the results carefully.");
                }
            }

            if (root.TryGetProperty("transactions", out var transactionsElement))
            {
                foreach (var transactionElement in transactionsElement.EnumerateArray())
                {
                    try
                    {
                        var transaction = ParseTransactionFromJson(transactionElement, userId, account);
                        if (transaction != null)
                        {
                            transactions.Add(transaction);
                            result.ImportedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        result.FailedCount++;
                        result.Errors.Add($"Failed to parse transaction: {ex.Message}");
                    }
                }
            }

            result.TotalRows = result.ImportedCount + result.FailedCount;
            return (result, transactions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse extraction results");
            result.Errors.Add($"Failed to parse AI response: {ex.Message}");
            return (result, transactions);
        }
    }

    private Transaction? ParseTransactionFromJson(JsonElement transactionElement, string userId, string account)
    {
        if (!transactionElement.TryGetProperty("date", out var dateElement) ||
            !transactionElement.TryGetProperty("description", out var descriptionElement) ||
            !transactionElement.TryGetProperty("amount", out var amountElement))
        {
            return null;
        }

        if (!DateTime.TryParse(dateElement.GetString(), out var date))
        {
            throw new ArgumentException("Invalid date format");
        }

        var description = descriptionElement.GetString();
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description is required");
        }

        if (!amountElement.TryGetDecimal(out var amount))
        {
            throw new ArgumentException("Invalid amount format");
        }

        decimal? balance = null;
        if (transactionElement.TryGetProperty("balance", out var balanceElement) &&
            balanceElement.TryGetDecimal(out var balanceValue))
        {
            balance = balanceValue;
        }

        return new Transaction
        {
            Id = Guid.NewGuid(),
            Date = DateTime.SpecifyKind(date, DateTimeKind.Utc),
            Description = description,
            Amount = amount,
            Balance = balance,
            Category = null, // Will be enhanced later
            UserId = userId,
            Account = account,
            ImportedAt = DateTime.UtcNow
        };
    }
}
```

## Step 33.5: Register Image Import Service

*Add the image import service to the dependency injection container.*

The `ImageImporter` service needs to be registered with the DI container so it can be injected into the import API endpoints.

Update `src/BudgetTracker.Api/Program.cs`:

```csharp
// Add import services
builder.Services.AddScoped<CsvImporter>();
builder.Services.AddScoped<IImageImporter, ImageImporter>(); // Add this line
```

## Step 33.6: Update Import API for Image Processing

*Modify the import API to handle both CSV and image files.*

The current import API only handles CSV files. Update it to detect the file type and route image files to the new image processing service while maintaining the existing CSV processing functionality.

Update `src/BudgetTracker.Api/Features/Transactions/Import/ImportApi.cs`:

```csharp
// Update the ImportAsync method to include IImageImporter
private static async Task<Results<Ok<ImportResult>, BadRequest<string>>> ImportAsync(
    IFormFile file, [FromForm] string account,
    CsvImporter csvImporter, IImageImporter imageImporter, BudgetTrackerContext context,
    ITransactionEnhancer enhancementService, ClaimsPrincipal claimsPrincipal,
    ICsvStructureDetector detectionService
)
{
    var validationResult = ValidateFileInput(file);
    if (validationResult != null)
    {
        return validationResult;
    }

    try
    {
        var userId = claimsPrincipal.GetUserId();
        await using var stream = file.OpenReadStream();

        var (importResult, transactions, detectionResult) = await ProcessFileAsync(
            stream, file.FileName, userId, account, csvImporter, imageImporter, detectionService);

        // ... rest of existing code (session hash, enhancement, save) ...

        return TypedResults.Ok(result);
    }
    catch (Exception ex)
    {
        return TypedResults.BadRequest(ex.Message);
    }
}

// Add the ProcessFileAsync method to route files to appropriate processors
private static async Task<(ImportResult, List<Transaction>, CsvStructureDetectionResult?)> ProcessFileAsync(
    Stream stream, string fileName, string userId, string account,
    CsvImporter csvImporter, IImageImporter imageImporter, ICsvStructureDetector detectionService)
{
    var fileExtension = Path.GetExtension(fileName).ToLowerInvariant();
    return fileExtension switch
    {
        ".csv" => await ProcessCsvFileAsync(stream, fileName, userId, account, csvImporter, detectionService),
        ".png" or ".jpg" or ".jpeg" => await ProcessImageFileAsync(stream, fileName, userId, account, imageImporter),
        _ => throw new InvalidOperationException("Unsupported file type")
    };
}

// Add the ProcessImageFileAsync method
private static async Task<(ImportResult, List<Transaction>, CsvStructureDetectionResult?)> ProcessImageFileAsync(
    Stream stream, string fileName, string userId, string account,
    IImageImporter imageImporter)
{
    var (importResult, transactions) = await imageImporter.ProcessImageAsync(stream, fileName, userId, account);

    return (importResult, transactions, null); // Images don't have CSV detection result
}

// Update ValidateFileInput to accept image files
private static BadRequest<string>? ValidateFileInput(IFormFile file)
{
    if (file == null || file.Length == 0)
    {
        return TypedResults.BadRequest("Please select a valid file.");
    }

    const int maxFileSize = 10 * 1024 * 1024; // 10MB
    if (file.Length > maxFileSize)
    {
        return TypedResults.BadRequest("File size must be less than 10MB.");
    }

    var allowedExtensions = new[] { ".csv", ".png", ".jpg", ".jpeg" };
    var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

    if (!allowedExtensions.Contains(fileExtension))
    {
        return TypedResults.BadRequest("Only CSV files and images (PNG, JPG, JPEG) are supported.");
    }

    return null;
}
```

---

## Testing

### Test Image Upload

Test the image import functionality with a sample bank statement:

```http
### Test image import with a bank statement screenshot
POST http://localhost:5295/api/transactions/import
X-API-Key: test-key-user1
Content-Type: multipart/form-data; boundary=WebAppBoundary

--WebAppBoundary
Content-Disposition: form-data; name="account"

Checking Account
--WebAppBoundary
Content-Disposition: form-data; name="file"; filename="bank-statement.png"
Content-Type: image/png

[Binary image data]
--WebAppBoundary--
```

### Expected Behavior

- GPT-4 Vision successfully analyzes the bank statement image
- Transactions are extracted with proper dates, descriptions, and amounts
- Low confidence extractions display appropriate warnings
- Extracted transactions flow through the existing enhancement pipeline

### Test Error Scenarios

1. **Unsupported File Type** - Upload a .txt file, should return error
2. **Large File Size** - Upload file >10MB, should return size limit error
3. **Poor Quality Image** - Upload blurry image, should return low confidence warning

---

## Summary

You've successfully implemented:

- **Multimodal AI Integration**: GPT-4 Vision processes bank statement images
- **Intelligent Extraction**: AI analyzes images and identifies transactions with confidence scoring
- **Seamless Integration**: Image processing integrates with existing CSV import workflow
- **Error Handling**: Comprehensive validation and error reporting for image processing failures

**Next Step**: Move to `034-image-import-ui.md` to build the frontend for image upload support.
