using System.Text;
using System.Text.Json;
using BudgetTracker.Api.Features.Transactions.Import.Processing;
using BudgetTracker.Api.Infrastructure.Extensions;

namespace BudgetTracker.Api.Features.Transactions.Import.Detection;

public class CsvDetector : ICsvDetector
{
    private readonly ICsvAnalyzer _csvAnalyzer;
    private readonly ILogger<CsvDetector> _logger;
    private const int MaxLinesForAnalysis = 10;

    public CsvDetector(ICsvAnalyzer csvAnalyzer, ILogger<CsvDetector> logger)
    {
        _csvAnalyzer = csvAnalyzer;
        _logger = logger;
    }

    public async Task<CsvStructureDetectionResult> AnalyzeCsvStructureAsync(Stream csvStream)
    {
        var csvContent = await ReadFirstLinesAsync(csvStream);
        var aiResponse = await _csvAnalyzer.AnalyzeCsvStructureAsync(csvContent);

        return ParseAiResponse(aiResponse);
    }

    private static async Task<string> ReadFirstLinesAsync(Stream stream)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

        var lines = new List<string>();
        for (var i = 0; i < MaxLinesForAnalysis; i++)
        {
            var line = await reader.ReadLineAsync();
            if (line == null) break;
            lines.Add(line);
        }

        stream.Position = 0;
        return string.Join('\n', lines);
    }

    private CsvStructureDetectionResult ParseAiResponse(string aiResponse)
    {
        try
        {
            var jsonContent = aiResponse.ExtractJsonFromCodeBlock();
            var parsed = JsonSerializer.Deserialize<AiDetectionResponse>(
                jsonContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (parsed == null)
            {
                _logger.LogWarning("AI response parsed to null");
                return CreateFallbackResult();
            }

            return new CsvStructureDetectionResult
            {
                Delimiter = ParseDelimiter(parsed.Delimiter),
                CultureCode = parsed.CultureCode ?? "en-US",
                ColumnMappings = parsed.ColumnMappings ?? new Dictionary<string, string>(),
                ConfidenceScore = parsed.ConfidenceScore,
                DetectionMethod = DetectionMethod.AI
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI detection response");
            return CreateFallbackResult();
        }
    }

    private static char ParseDelimiter(string? delimiter)
    {
        return delimiter switch
        {
            ";" => ';',
            "\\t" or "\t" => '\t',
            "|" => '|',
            _ => ','
        };
    }

    private static CsvStructureDetectionResult CreateFallbackResult()
    {
        return new CsvStructureDetectionResult
        {
            Delimiter = ',',
            CultureCode = "en-US",
            ColumnMappings = new Dictionary<string, string>(),
            ConfidenceScore = 0.0,
            DetectionMethod = DetectionMethod.AI
        };
    }

    private class AiDetectionResponse
    {
        public string? Delimiter { get; set; }
        public string? CultureCode { get; set; }
        public Dictionary<string, string>? ColumnMappings { get; set; }
        public double ConfidenceScore { get; set; }
    }
}
