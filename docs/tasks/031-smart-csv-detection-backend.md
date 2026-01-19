# Workshop Step 031: Smart CSV Detection Backend

## Mission

In this step, you'll implement intelligent CSV structure detection using AI to handle a wide variety of bank CSV formats, even those you've never seen before. The system will automatically detect column separators, date formats, and column mappings for international CSV files, falling back to AI analysis when rule-based detection fails.

**Your goal**: Build a robust CSV detection system that can handle any bank CSV format by combining rule-based detection with LLM-powered analysis for unknown structures.

**Learning Objectives**:
- Implementing a layered detection approach with rule-based and AI fallbacks
- Using LLMs to analyze and understand unknown CSV structures
- Building culture-aware parsing for international CSV formats
- Creating confidence scoring systems for structure detection accuracy
- Integrating detection results with existing CSV parsing workflows

---

## Prerequisites

Before starting, ensure you completed:
- Week 3 (AI transaction enhancement)
- Working Azure OpenAI setup with IChatClient

---

## Step 31.1: Create CSV Structure Detection Result Types

*Define the data structures for storing CSV detection results and confidence scores.*

The detection system needs a comprehensive way to represent the results of CSV analysis, including column mappings, culture settings, and confidence scoring. This will support both simple rule-based detection and complex AI-driven analysis.

Create `src/BudgetTracker.Api/Features/Transactions/Import/Detection/CsvStructureDetectionResult.cs`:

```csharp
namespace BudgetTracker.Api.Features.Transactions.Import.Detection;

public class CsvStructureDetectionResult
{
    public string Delimiter { get; set; } = ",";
    public Dictionary<string, string> ColumnMappings { get; set; } = new();
    public string CultureCode { get; set; } = "en-US";
    public double ConfidenceScore { get; set; }
    public DetectionMethod DetectionMethod { get; set; }
}

public enum DetectionMethod
{
    RuleBased,
    AI
}
```

## Step 31.2: Create Column Mapping Dictionary

*Define standard column name patterns for rule-based detection.*

Before falling back to AI analysis, the system will attempt to match common English column names using predefined patterns. This provides fast detection for standard formats while preserving AI resources for complex cases.

Create `src/BudgetTracker.Api/Features/Transactions/Import/Detection/ColumnMappingDictionary.cs`:

```csharp
namespace BudgetTracker.Api.Features.Transactions.Import.Detection;

public static class ColumnMappingDictionary
{
    // Simple English-only column mappings - if it doesn't match, use AI
    public static readonly string[] DateColumns =
        ["Date", "Transaction Date", "Posting Date", "Value Date", "Txn Date"];

    public static readonly string[] DescriptionColumns =
        ["Description", "Memo", "Details", "Transaction Description", "Reference"];

    public static readonly string[] AmountColumns =
        ["Amount", "Transaction Amount", "Debit", "Credit", "Value"];
}
```

## Step 31.3: Create CSV Structure Detection Interface

*Define the interface for CSV structure detection services.*

The detection system needs a clean interface that can be implemented by different detection strategies. This allows for easy testing and future extension with additional detection methods.

Create `src/BudgetTracker.Api/Features/Transactions/Import/Detection/ICsvStructureDetector.cs`:

```csharp
namespace BudgetTracker.Api.Features.Transactions.Import.Detection;

public interface ICsvStructureDetector
{
    Task<CsvStructureDetectionResult> DetectStructureAsync(Stream csvStream);
}
```

## Step 31.4: Create AI-Powered CSV Analyzer

*Implement the AI service that analyzes CSV structures using Azure OpenAI.*

The `CsvAnalyzer` is the core AI component that sends CSV samples to the language model for intelligent structure analysis. It creates detailed prompts that guide the AI to identify column mappings, delimiters, and cultural formatting.

Create `src/BudgetTracker.Api/Features/Transactions/Import/Processing/ICsvAnalyzer.cs`:

```csharp
namespace BudgetTracker.Api.Features.Transactions.Import.Processing;

public interface ICsvAnalyzer
{
    Task<string> AnalyzeCsvStructureAsync(string csvContent);
}
```

Create `src/BudgetTracker.Api/Features/Transactions/Import/Processing/CsvAnalyzer.cs`:

```csharp
using System.Text;
using Microsoft.Extensions.AI;

namespace BudgetTracker.Api.Features.Transactions.Import.Processing;

public class CsvAnalyzer : ICsvAnalyzer
{
    private readonly IChatClient _chatClient;

    public CsvAnalyzer(IChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    public async Task<string> AnalyzeCsvStructureAsync(string csvContent)
    {
        var systemPrompt = "You are a CSV structure analysis expert. Analyze CSV files and identify their format, columns, and cultural settings.";
        var userPrompt = CreateStructureAnalysisPrompt(csvContent);

        var response = await _chatClient.GetResponseAsync([
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, userPrompt)
        ]);

        return response.Text ?? string.Empty;
    }

    private string CreateStructureAnalysisPrompt(string csvContent)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine("Analyze this CSV file structure and identify the following elements:");
        prompt.AppendLine("1. Column separator (comma, semicolon, tab, pipe)");
        prompt.AppendLine("2. Culture/locale for number and date parsing (e.g., 'en-US', 'pt-PT', 'de-DE', 'fr-FR')");
        prompt.AppendLine("3. Date column name and format pattern");
        prompt.AppendLine("4. Description/memo column name");
        prompt.AppendLine("5. Amount/value column name");
        prompt.AppendLine("6. Confidence score (0-100)");
        prompt.AppendLine();
        prompt.AppendLine("CSV Data:");
        prompt.AppendLine(csvContent);
        prompt.AppendLine();
        prompt.AppendLine("Respond with a JSON object with this exact structure:");
        prompt.AppendLine("{");
        prompt.AppendLine("  \"columnSeparator\": \",\" | \";\" | \"\\t\" | \"|\",");
        prompt.AppendLine("  \"cultureCode\": \"en-US\" | \"pt-PT\" | \"de-DE\" | \"fr-FR\" | \"es-ES\" | \"it-IT\" | etc,");
        prompt.AppendLine("  \"dateColumn\": \"column_name\",");
        prompt.AppendLine("  \"dateFormat\": \"MM/dd/yyyy\" | \"dd/MM/yyyy\" | \"yyyy-MM-dd\" | etc,");
        prompt.AppendLine("  \"descriptionColumn\": \"column_name\",");
        prompt.AppendLine("  \"amountColumn\": \"column_name\",");
        prompt.AppendLine("  \"confidenceScore\": 85");
        prompt.AppendLine("}");

        return prompt.ToString();
    }
}
```

## Step 31.5: Create JSON Extraction Extension

*Create a shared utility for parsing JSON responses from AI code blocks.*

AI responses often wrap JSON in markdown code blocks. We'll create a reusable extension method to handle this common pattern, which will be used by multiple services including the CSV detector and image importer.

Create `src/BudgetTracker.Api/Infrastructure/Extensions/StringExtensions.cs`:

```csharp
using System.Text.RegularExpressions;

namespace BudgetTracker.Api.Infrastructure.Extensions;

public static class StringExtensions
{
    public static string ExtractJsonFromCodeBlock(this string input)
    {
        if (!input.Contains("```json"))
            return input;

        var match = Regex.Match(input, @"```json\s*([\s\S]*?)\s*```");

        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        throw new FormatException("Could not extract JSON from the input string");
    }
}
```

## Step 31.6: Create AI Detection Service

*Implement the service that processes AI responses and converts them to detection results.*

The `CsvDetector` service acts as a bridge between the raw AI analysis and the structured detection results. It handles JSON parsing, error recovery, and confidence assessment of AI responses.

Create `src/BudgetTracker.Api/Features/Transactions/Import/Detection/ICsvDetector.cs`:

```csharp
namespace BudgetTracker.Api.Features.Transactions.Import.Detection;

public interface ICsvDetector
{
    Task<CsvStructureDetectionResult> AnalyzeCsvStructureAsync(Stream csvStream);
}
```

Create `src/BudgetTracker.Api/Features/Transactions/Import/Detection/CsvDetector.cs`:

```csharp
using System.Text;
using System.Text.Json;
using BudgetTracker.Api.Features.Transactions.Import.Processing;
using BudgetTracker.Api.Infrastructure.Extensions;

namespace BudgetTracker.Api.Features.Transactions.Import.Detection;

public class CsvDetector : ICsvDetector
{
    private readonly ICsvAnalyzer _structureAnalysisService;
    private readonly ILogger<CsvDetector> _logger;

    public CsvDetector(
        ICsvAnalyzer structureAnalysisService,
        ILogger<CsvDetector> logger)
    {
        _structureAnalysisService = structureAnalysisService;
        _logger = logger;
    }

    public async Task<CsvStructureDetectionResult> AnalyzeCsvStructureAsync(Stream csvStream)
    {
        try
        {
            _logger.LogDebug("Starting AI CSV structure analysis");

            // Read CSV headers and sample rows
            csvStream.Position = 0;
            using var reader = new StreamReader(csvStream, Encoding.UTF8, leaveOpen: true);

            var lines = new List<string>();
            for (int i = 0; i < 5 && !reader.EndOfStream; i++) // Read first 5 lines
            {
                var line = await reader.ReadLineAsync();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    lines.Add(line);
                }
            }

            if (lines.Count == 0)
            {
                _logger.LogWarning("No data found in CSV for AI analysis");
                return new CsvStructureDetectionResult { ConfidenceScore = 0, DetectionMethod = DetectionMethod.AI };
            }

            _logger.LogDebug("Sending CSV structure analysis request to AI service");

            // Use dedicated CSV structure analysis service
            var csvContent = string.Join("\n", lines);
            var responseText = await _structureAnalysisService.AnalyzeCsvStructureAsync(csvContent);

            if (string.IsNullOrEmpty(responseText))
            {
                _logger.LogWarning("AI service returned empty response for CSV structure analysis");
                return new CsvStructureDetectionResult { ConfidenceScore = 0, DetectionMethod = DetectionMethod.AI };
            }

            // Parse AI response
            var result = ParseAiResponse(responseText.ExtractJsonFromCodeBlock());
            result.DetectionMethod = DetectionMethod.AI;

            _logger.LogDebug("AI detection completed - confidence: {Confidence}%, method: AI", result.ConfidenceScore);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI CSV structure analysis failed");
            return new CsvStructureDetectionResult { ConfidenceScore = 0, DetectionMethod = DetectionMethod.AI };
        }
    }

    private CsvStructureDetectionResult ParseAiResponse(string aiResponse)
    {
        try
        {
            // Try to extract JSON from the response
            var jsonStart = aiResponse.IndexOf('{');
            var jsonEnd = aiResponse.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonContent = aiResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);

                using var document = JsonDocument.Parse(jsonContent);
                var root = document.RootElement;

                var result = new CsvStructureDetectionResult
                {
                    DetectionMethod = DetectionMethod.AI
                };

                // Extract column separator
                if (root.TryGetProperty("columnSeparator", out var columnSep))
                {
                    result.Delimiter = columnSep.GetString() ?? ",";
                    if (result.Delimiter == "\\t") result.Delimiter = "\t"; // Handle tab character
                }

                // Extract culture code for parsing
                if (root.TryGetProperty("cultureCode", out var cultureCode))
                {
                    result.CultureCode = cultureCode.GetString() ?? "en-US";
                }

                // Extract column mappings
                result.ColumnMappings = new Dictionary<string, string>();

                if (root.TryGetProperty("dateColumn", out var dateCol) && !string.IsNullOrEmpty(dateCol.GetString()))
                {
                    result.ColumnMappings["Date"] = dateCol.GetString()!;
                }

                if (root.TryGetProperty("descriptionColumn", out var descCol) && !string.IsNullOrEmpty(descCol.GetString()))
                {
                    result.ColumnMappings["Description"] = descCol.GetString()!;
                }

                if (root.TryGetProperty("amountColumn", out var amountCol) && !string.IsNullOrEmpty(amountCol.GetString()))
                {
                    result.ColumnMappings["Amount"] = amountCol.GetString()!;
                }

                // Extract confidence score
                if (root.TryGetProperty("confidenceScore", out var confidence))
                {
                    result.ConfidenceScore = confidence.GetDouble();
                }

                _logger.LogDebug("AI detection successful - separator: '{Delimiter}', culture: '{Culture}', confidence: {Confidence}%, method: AI",
                    result.Delimiter, result.CultureCode, result.ConfidenceScore);

                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI response JSON: {Response}", aiResponse);
        }

        // Fallback: return low confidence result
        _logger.LogWarning("Could not parse AI response, returning low confidence result");
        return new CsvStructureDetectionResult
        {
            ConfidenceScore = 0,
            DetectionMethod = DetectionMethod.AI
        };
    }
}
```

## Step 31.7: Implement Smart CSV Structure Detector

*Create the main detection service that combines rule-based and AI approaches.*

The `CsvStructureDetector` is the orchestrator that tries simple rule-based detection first, then falls back to AI analysis for complex or unknown formats. This provides optimal performance while ensuring comprehensive format support.

Create `src/BudgetTracker.Api/Features/Transactions/Import/Detection/CsvStructureDetector.cs`:

```csharp
using System.Globalization;

namespace BudgetTracker.Api.Features.Transactions.Import.Detection;

public class CsvStructureDetector : ICsvStructureDetector
{
    private readonly ICsvDetector _aiDetectionService;
    private readonly ILogger<CsvStructureDetector> _logger;

    public CsvStructureDetector(
        ICsvDetector aiDetectionService,
        ILogger<CsvStructureDetector> logger)
    {
        _aiDetectionService = aiDetectionService;
        _logger = logger;
    }

    public async Task<CsvStructureDetectionResult> DetectStructureAsync(Stream csvStream)
    {
        try
        {
            _logger.LogDebug("Starting CSV structure detection");

            // Try simple parsing first
            var simpleResult = TrySimpleParsing(csvStream);

            if (simpleResult.ConfidenceScore >= 85)
            {
                _logger.LogDebug("Simple parsing successful with {Confidence}% confidence",
                    simpleResult.ConfidenceScore);
                return simpleResult;
            }

            _logger.LogDebug("Simple parsing failed, falling back to AI detection");
            csvStream.Position = 0; // Reset stream for AI analysis
            return await _aiDetectionService.AnalyzeCsvStructureAsync(csvStream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during CSV structure detection");

            _logger.LogDebug("Attempting AI fallback after error");
            csvStream.Position = 0;
            return await _aiDetectionService.AnalyzeCsvStructureAsync(csvStream);
        }
    }

    private CsvStructureDetectionResult TrySimpleParsing(Stream csvStream)
    {
        csvStream.Position = 0;
        using var reader = new StreamReader(csvStream, leaveOpen: true);

        var lines = new List<string>();
        for (var i = 0; i < 100 && !reader.EndOfStream; i++)
        {
            var line = reader.ReadLine();
            if (!string.IsNullOrWhiteSpace(line))
            {
                lines.Add(line);
            }
        }

        if (lines.Count < 1)
        {
            return new CsvStructureDetectionResult { ConfidenceScore = 0 };
        }

        var result = new CsvStructureDetectionResult
        {
            DetectionMethod = DetectionMethod.RuleBased,
            Delimiter = ",",
            CultureCode = "en-US" // Default to US format for simple parsing
        };

        // Try to find English column names in the header
        var headers = lines[0].Split(',').Select(h => h.Trim().Trim('"')).ToArray();

        var dateColumn = FindColumn(headers, ColumnMappingDictionary.DateColumns);
        var descriptionColumn = FindColumn(headers, ColumnMappingDictionary.DescriptionColumns);
        var amountColumn = FindColumn(headers, ColumnMappingDictionary.AmountColumns);

        // Check if we found the required columns
        if (dateColumn == null || descriptionColumn == null || amountColumn == null)
        {
            result.ConfidenceScore = 0; // No required columns found
            return result;
        }

        // Set up column mappings
        result.ColumnMappings["Date"] = dateColumn;
        result.ColumnMappings["Description"] = descriptionColumn;
        result.ColumnMappings["Amount"] = amountColumn;

        // Optional columns (simple patterns for English-only detection)
        var balanceColumn = FindColumn(headers, ["Balance", "Running Balance", "Account Balance"]);
        if (balanceColumn != null)
        {
            result.ColumnMappings["Balance"] = balanceColumn;
        }

        var categoryColumn = FindColumn(headers, ["Category", "Type", "Transaction Type"]);
        if (categoryColumn != null)
        {
            result.ColumnMappings["Category"] = categoryColumn;
        }

        // Try to parse a few sample rows to validate the format
        var sampleRows = lines.Skip(1).Take(3);
        var successfulParses = 0;
        var totalSamples = 0;

        foreach (var row in sampleRows)
        {
            totalSamples++;
            var parts = row.Split(',');
            if (parts.Length >= headers.Length && TryParseRow(parts, headers, result.ColumnMappings))
            {
                successfulParses++;
            }
        }

        // Calculate confidence based on successful parsing
        if (totalSamples > 0)
        {
            var successRate = (double)successfulParses / totalSamples;
            result.ConfidenceScore = successRate * 100;
        }
        else
        {
            result.ConfidenceScore = 85; // Found columns but no data to validate
        }

        return result;
    }

    private string? FindColumn(string[] headers, string[] patterns)
    {
        return headers.FirstOrDefault(header =>
            patterns.Any(pattern =>
                string.Equals(pattern, header.Trim(), StringComparison.OrdinalIgnoreCase)));
    }

    private bool TryParseRow(string[] parts, string[] headers, Dictionary<string, string> mappings)
    {
        try
        {
            // Try to parse date
            if (mappings.TryGetValue("Date", out var dateColumn))
            {
                var dateIndex = Array.IndexOf(headers, dateColumn);
                if (dateIndex >= 0 && dateIndex < parts.Length)
                {
                    var dateStr = parts[dateIndex].Trim().Trim('"');
                    if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    {
                        return false;
                    }
                }
            }

            // Try to parse amount
            if (mappings.TryGetValue("Amount", out var amountColumn))
            {
                var amountIndex = Array.IndexOf(headers, amountColumn);
                if (amountIndex >= 0 && amountIndex < parts.Length)
                {
                    var amountStr = parts[amountIndex].Trim().Trim('"').Replace("$", "").Replace(",", "");
                    if (!decimal.TryParse(amountStr, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
```

## Step 31.8: Update CSV Importer for Smart Detection

*Enhance the CSV importer to use detection results for flexible parsing.*

The existing `CsvImporter` needs to be updated to accept and use detection results, allowing it to parse CSV files with different delimiters, cultures, and column mappings determined by the detection system.

Update `src/BudgetTracker.Api/Features/Transactions/Import/Processing/CsvImporter.cs`:

```csharp
using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using BudgetTracker.Api.Features.Transactions;
using BudgetTracker.Api.Features.Transactions.Import.Detection;
using BudgetTracker.Api.Features.Transactions.List;

namespace BudgetTracker.Api.Features.Transactions.Import.Processing;

public class CsvImporter
{
    public async Task<(ImportResult Result, List<Transaction> Transactions)> ParseCsvAsync(Stream csvStream,
        string sourceFileName, string userId, string account)
    {
        return await ParseCsvAsync(csvStream, sourceFileName, userId, account, null);
    }

    public async Task<(ImportResult Result, List<Transaction> Transactions)> ParseCsvAsync(Stream csvStream,
        string sourceFileName, string userId, string account, CsvStructureDetectionResult? detectionResult)
    {
        var result = new ImportResult
        {
            SourceFile = sourceFileName,
            ImportedAt = DateTime.UtcNow
        };

        var transactions = new List<Transaction>();

        try
        {
            using var reader = new StreamReader(csvStream, Encoding.UTF8);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                BadDataFound = null,
                Delimiter = detectionResult?.Delimiter ?? ","
            });

            var rowNumber = 0;

            await foreach (var record in csv.GetRecordsAsync<dynamic>())
            {
                rowNumber++;
                result.TotalRows++;

                try
                {
                    var transaction = ParseTransactionRow(record, detectionResult);
                    if (transaction != null)
                    {
                        transaction.UserId = userId;
                        transaction.Account = account;

                        transactions.Add(transaction);
                        result.ImportedCount++;
                    }
                    else
                    {
                        result.FailedCount++;
                        result.Errors.Add($"Row {rowNumber}: Failed to parse transaction");
                    }
                }
                catch (Exception ex)
                {
                    result.FailedCount++;
                    result.Errors.Add($"Row {rowNumber}: {ex.Message}");
                }
            }

            result.ImportedCount = transactions.Count;
            result.FailedCount = result.TotalRows - result.ImportedCount;

            return (result, transactions);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"CSV parsing error: {ex.Message}");
            return (result, new List<Transaction>());
        }
    }

    private Transaction? ParseTransactionRow(dynamic record, CsvStructureDetectionResult? detectionResult = null)
    {
        try
        {
            var recordDict = (IDictionary<string, object>)record;

            // Use detected column mappings if available, otherwise fall back to English defaults
            var description = GetColumnValueWithDetection(recordDict, detectionResult, "Description",
                "Description", "Memo", "Details");

            var dateStr = GetColumnValueWithDetection(recordDict, detectionResult, "Date",
                "Date", "Transaction Date", "Posting Date");

            var amountStr = GetColumnValueWithDetection(recordDict, detectionResult, "Amount",
                "Amount", "Transaction Amount", "Debit", "Credit");

            var balanceStr = GetColumnValueWithDetection(recordDict, detectionResult, "Balance",
                "Balance", "Running Balance", "Account Balance");

            var category = GetColumnValueWithDetection(recordDict, detectionResult, "Category",
                "Category", "Type", "Transaction Type");

            // Validate required fields
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentException("Description is required");
            }

            if (string.IsNullOrWhiteSpace(dateStr))
            {
                throw new ArgumentException("Date is required");
            }

            if (string.IsNullOrWhiteSpace(amountStr))
            {
                throw new ArgumentException("Amount is required");
            }

            // Parse date
            if (!TryParseDate(dateStr, out var date, null, detectionResult))
            {
                throw new ArgumentException($"Invalid date format: {dateStr}");
            }

            // Parse amount using culture-aware parsing
            if (!TryParseAmountWithCulture(amountStr, detectionResult, out var amount))
            {
                throw new ArgumentException($"Invalid amount format: {amountStr}");
            }

            // Parse balance (optional)
            decimal? balance = null;
            if (!string.IsNullOrWhiteSpace(balanceStr))
            {
                if (TryParseAmountWithCulture(balanceStr, detectionResult, out var parsedBalance))
                {
                    balance = parsedBalance;
                }
            }

            return new Transaction
            {
                Id = Guid.NewGuid(),
                Date = date,
                Description = description.Trim(),
                Amount = amount,
                Balance = balance,
                Category = !string.IsNullOrWhiteSpace(category?.Trim()) ? category.Trim() : "Uncategorized",
                ImportedAt = DateTime.UtcNow,
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? GetColumnValueWithDetection(IDictionary<string, object> record,
        CsvStructureDetectionResult? detectionResult, string mappingKey, params string[] fallbackColumnNames)
    {
        // First, try the detected column mapping if available
        if (detectionResult?.ColumnMappings != null &&
            detectionResult.ColumnMappings.TryGetValue(mappingKey, out var detectedColumnName) &&
            !string.IsNullOrEmpty(detectedColumnName))
        {
            if (record.TryGetValue(detectedColumnName, out var detectedValue) && detectedValue != null)
            {
                return detectedValue.ToString()?.Trim();
            }
        }

        // Fall back to trying the provided column name variations
        return GetColumnValue(record, fallbackColumnNames);
    }

    private static string? GetColumnValue(IDictionary<string, object> record, params string[] columnNames)
    {
        foreach (var columnName in columnNames)
        {
            if (record.TryGetValue(columnName, out var value) && value != null)
            {
                return value.ToString()?.Trim();
            }
        }

        return null;
    }

    private bool TryParseDate(string dateStr, out DateTime date, string? detectedFormat = null, CsvStructureDetectionResult? detectionResult = null)
    {
        date = default;

        // Get culture for date parsing
        CultureInfo culture = CultureInfo.InvariantCulture;
        if (!string.IsNullOrEmpty(detectionResult?.CultureCode))
        {
            try
            {
                culture = new CultureInfo(detectionResult.CultureCode);
            }
            catch
            {
                culture = CultureInfo.InvariantCulture;
            }
        }

        // Try detected format first if available
        if (!string.IsNullOrEmpty(detectedFormat))
        {
            if (DateTime.TryParseExact(dateStr.Trim(), detectedFormat, culture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date))
            {
                return true;
            }
        }

        // Try culture-aware parsing
        if (DateTime.TryParse(dateStr.Trim(), culture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date))
        {
            return true;
        }

        // Final fallback to invariant culture
        return DateTime.TryParse(dateStr.Trim(), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date);
    }

    private static bool TryParseAmountWithCulture(string amountStr, CsvStructureDetectionResult? detectionResult, out decimal amount)
    {
        amount = 0;

        if (string.IsNullOrWhiteSpace(amountStr))
            return false;

        var cleanAmount = amountStr.Trim();

        // Remove common currency symbols
        cleanAmount = cleanAmount.Replace("$", "").Replace("€", "").Replace("£", "").Replace("¥", "").Replace("R$", "").Trim();

        // Try to get culture from detection result
        CultureInfo culture = CultureInfo.InvariantCulture;
        if (!string.IsNullOrEmpty(detectionResult?.CultureCode))
        {
            try
            {
                culture = new CultureInfo(detectionResult.CultureCode);
            }
            catch
            {
                // Fall back to invariant culture if culture code is invalid
                culture = CultureInfo.InvariantCulture;
            }
        }

        // Use culture-specific parsing - .NET handles decimal/thousand separators automatically
        return decimal.TryParse(cleanAmount, NumberStyles.Currency, culture, out amount);
    }
}
```

## Step 31.9: Update Import Result Types

*Add detection information to the import result types.*

The import results need to include information about how the CSV structure was detected, providing transparency to users about the analysis method and confidence levels.

Update `src/BudgetTracker.Api/Features/Transactions/Import/ImportResult.cs` to add these properties:

```csharp
public class ImportResult
{
    // ... existing properties ...

    public string? DetectionMethod { get; set; } // "RuleBased" or "AI"
    public double DetectionConfidence { get; set; } // 0-100
}
```

## Step 31.10: Update Import API for Smart Detection

*Integrate the detection system into the import API workflow.*

The import API needs to use the new detection system before processing CSV files. This ensures that all CSV files go through intelligent structure analysis before parsing attempts.

Update `src/BudgetTracker.Api/Features/Transactions/Import/ImportApi.cs` to include the detection service:

```csharp
// Update the ImportAsync method signature to include ICsvStructureDetector
private static async Task<Results<Ok<ImportResult>, BadRequest<string>>> ImportAsync(
    IFormFile file, [FromForm] string account,
    CsvImporter csvImporter, BudgetTrackerContext context,
    ITransactionEnhancer enhancementService, ClaimsPrincipal claimsPrincipal,
    ICsvStructureDetector detectionService
)
{
    // ... validation code ...

    try
    {
        var userId = claimsPrincipal.GetUserId();
        await using var stream = file.OpenReadStream();

        // Detect CSV structure first
        var detectionResult = await detectionService.DetectStructureAsync(stream);

        if (detectionResult.ConfidenceScore < 85)
        {
            var errorMessage = detectionResult.DetectionMethod == DetectionMethod.AI
                ? "Unable to automatically detect CSV structure using AI analysis. Please ensure your CSV contains Date, Description, and Amount columns with recognizable headers."
                : "Unable to automatically detect CSV structure. Please ensure your CSV file follows a standard banking format.";

            return TypedResults.BadRequest(errorMessage);
        }

        stream.Position = 0; // Reset stream position
        var (importResult, transactions) = await csvImporter.ParseCsvAsync(
            stream, file.FileName, userId, account, detectionResult);

        // Add detection info to result
        importResult.DetectionMethod = detectionResult.DetectionMethod.ToString();
        importResult.DetectionConfidence = detectionResult.ConfidenceScore;

        // ... rest of existing code (session hash, enhancement, save) ...

        return TypedResults.Ok(importResult);
    }
    catch (Exception ex)
    {
        return TypedResults.BadRequest(ex.Message);
    }
}
```

## Step 31.11: Register Detection Services

*Add all detection services to the dependency injection container.*

The detection services need to be registered with the DI container in the correct order to ensure proper dependency resolution and service lifetime management.

Update `src/BudgetTracker.Api/Program.cs`:

```csharp
// Add CSV detection services
builder.Services.AddScoped<ICsvStructureDetector, CsvStructureDetector>();
builder.Services.AddScoped<ICsvDetector, CsvDetector>();
builder.Services.AddScoped<ICsvAnalyzer, CsvAnalyzer>();
```

---

## Testing

### Test Rule-Based Detection

Test with a standard English CSV format:

```csv
Date,Description,Amount,Balance
2025-01-15,STARBUCKS COFFEE #1234,-4.50,1245.50
2025-01-16,SALARY DEPOSIT,2500.00,3745.50
```

**Expected Results:**
- Detection Method: RuleBased
- Confidence Score: >85%
- Fast detection without AI calls

### Test AI Detection - Portuguese Format

Test with a Portuguese bank CSV format:

```csv
Data;Descrição;Valor;Saldo
15/01/2025;COMPRA CONTINENTE;-45,67;1.234,56
16/01/2025;TRANSFERÊNCIA RECEBIDA;2.500,00;3.734,56
```

**Expected Results:**
- Detection Method: AI
- Culture: pt-PT (Portuguese formatting)
- Delimiter: Semicolon (;)

---

## Summary

You've successfully implemented:

- **Layered Detection Strategy**: Rule-based detection for common formats with AI fallback
- **Multi-Cultural Support**: Automatic detection of culture-specific number and date formats
- **Intelligent Column Mapping**: AI-powered identification of column purposes in any language
- **Confidence Scoring**: Transparent confidence assessment for detection accuracy
- **Seamless Integration**: Works with existing CSV import pipeline

**Next Step**: Move to `032-smart-csv-detection-ui.md` to build the frontend for displaying detection results.
