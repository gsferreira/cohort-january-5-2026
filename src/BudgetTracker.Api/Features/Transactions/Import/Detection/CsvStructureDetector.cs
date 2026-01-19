using System.Text;

namespace BudgetTracker.Api.Features.Transactions.Import.Detection;

public class CsvStructureDetector : ICsvStructureDetector
{
    private readonly ICsvDetector _aiDetector;
    private readonly ILogger<CsvStructureDetector> _logger;
    private const double MinConfidenceThreshold = 0.85;

    public CsvStructureDetector(ICsvDetector aiDetector, ILogger<CsvStructureDetector> logger)
    {
        _aiDetector = aiDetector;
        _logger = logger;
    }

    public async Task<CsvStructureDetectionResult> DetectStructureAsync(Stream csvStream)
    {
        var ruleBasedResult = await TryRuleBasedDetectionAsync(csvStream);

        if (ruleBasedResult.ConfidenceScore >= MinConfidenceThreshold)
        {
            _logger.LogInformation(
                "Rule-based detection succeeded with confidence {Confidence}",
                ruleBasedResult.ConfidenceScore);
            return ruleBasedResult;
        }

        _logger.LogInformation(
            "Rule-based detection confidence {Confidence} below threshold, falling back to AI",
            ruleBasedResult.ConfidenceScore);

        csvStream.Position = 0;
        return await _aiDetector.AnalyzeCsvStructureAsync(csvStream);
    }

    private async Task<CsvStructureDetectionResult> TryRuleBasedDetectionAsync(Stream csvStream)
    {
        csvStream.Position = 0;
        using var reader = new StreamReader(csvStream, Encoding.UTF8, leaveOpen: true);
        var headerLine = await reader.ReadLineAsync();

        if (string.IsNullOrEmpty(headerLine))
        {
            return CreateLowConfidenceResult();
        }

        var delimiter = DetectDelimiter(headerLine);
        var headers = headerLine.Split(delimiter).Select(h => h.Trim()).ToList();

        var columnMappings = new Dictionary<string, string>();
        var matchedCount = 0;

        var dateColumn = ColumnMappingDictionary.FindMatchingColumn(headers, ColumnMappingDictionary.DateColumns);
        if (dateColumn != null)
        {
            columnMappings[dateColumn] = "Date";
            matchedCount++;
        }

        var descriptionColumn = ColumnMappingDictionary.FindMatchingColumn(headers, ColumnMappingDictionary.DescriptionColumns);
        if (descriptionColumn != null)
        {
            columnMappings[descriptionColumn] = "Description";
            matchedCount++;
        }

        var amountColumn = ColumnMappingDictionary.FindMatchingColumn(headers, ColumnMappingDictionary.AmountColumns);
        if (amountColumn != null)
        {
            columnMappings[amountColumn] = "Amount";
            matchedCount++;
        }

        var balanceColumn = ColumnMappingDictionary.FindMatchingColumn(headers, ColumnMappingDictionary.BalanceColumns);
        if (balanceColumn != null)
        {
            columnMappings[balanceColumn] = "Balance";
            matchedCount++;
        }

        var categoryColumn = ColumnMappingDictionary.FindMatchingColumn(headers, ColumnMappingDictionary.CategoryColumns);
        if (categoryColumn != null)
        {
            columnMappings[categoryColumn] = "Category";
            matchedCount++;
        }

        var requiredFieldsMatched = dateColumn != null && descriptionColumn != null && amountColumn != null;
        var confidenceScore = CalculateConfidence(matchedCount, requiredFieldsMatched);

        csvStream.Position = 0;

        return new CsvStructureDetectionResult
        {
            Delimiter = delimiter,
            CultureCode = "en-US",
            ColumnMappings = columnMappings,
            ConfidenceScore = confidenceScore,
            DetectionMethod = DetectionMethod.RuleBased
        };
    }

    private static char DetectDelimiter(string headerLine)
    {
        var delimiters = new[] { ',', ';', '\t', '|' };
        var maxCount = 0;
        var bestDelimiter = ',';

        foreach (var delimiter in delimiters)
        {
            var count = headerLine.Count(c => c == delimiter);
            if (count > maxCount)
            {
                maxCount = count;
                bestDelimiter = delimiter;
            }
        }

        return bestDelimiter;
    }

    private static double CalculateConfidence(int matchedCount, bool requiredFieldsMatched)
    {
        if (!requiredFieldsMatched)
            return 0.3;

        return matchedCount switch
        {
            >= 4 => 0.95,
            3 => 0.90,
            _ => 0.5
        };
    }

    private static CsvStructureDetectionResult CreateLowConfidenceResult()
    {
        return new CsvStructureDetectionResult
        {
            Delimiter = ',',
            CultureCode = "en-US",
            ColumnMappings = new Dictionary<string, string>(),
            ConfidenceScore = 0.0,
            DetectionMethod = DetectionMethod.RuleBased
        };
    }
}
