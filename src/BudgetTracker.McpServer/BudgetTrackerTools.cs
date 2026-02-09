using System.Net.Http.Json;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

[McpServerToolType]
public static class BudgetTrackerTools
{
    [McpServerTool, Description("Query recent transactions from your budget tracker")]
    public static async Task<string> QueryTransactions(
        [Description("Natural language question about your transactions")]
        string question,
        IServiceProvider serviceProvider)
    {
        var httpClient = serviceProvider.GetRequiredService<HttpClient>();
        var apiKey = Environment.GetEnvironmentVariable("BUDGET_TRACKER_API_KEY");

        if (string.IsNullOrEmpty(apiKey))
        {
            return "API key not configured. Please set BUDGET_TRACKER_API_KEY environment variable.";
        }

        try
        {
            // Add API key to request headers
            httpClient.DefaultRequestHeaders.Remove("X-API-Key");
            httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);

            // Call the Budget Tracker API query endpoint (authenticated)
            var apiBaseUrl = GetApiBaseUrl(serviceProvider);
            var response = await httpClient.PostAsJsonAsync($"{apiBaseUrl}/api/query/ask",
                new { Query = question });

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                return result;
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return "Invalid API key. Please check your configuration.";
            }
            else
            {
                return $"Error querying transactions: {response.StatusCode}";
            }
        }
        catch (Exception ex)
        {
            return $"Error connecting to Budget Tracker API: {ex.Message}";
        }
    }

    [McpServerTool, Description("Import transactions from a CSV file to your budget tracker")]
    public static async Task<string> ImportTransactionsCsv(
        [Description("Base64-encoded CSV file content")]
        string csvContent,
        [Description("Original filename for tracking")]
        string fileName,
        [Description("Account name to associate with imported transactions")]
        string account,
        IServiceProvider serviceProvider)
    {
        var httpClient = serviceProvider.GetRequiredService<HttpClient>();
        var apiKey = Environment.GetEnvironmentVariable("BUDGET_TRACKER_API_KEY");

        if (string.IsNullOrEmpty(apiKey))
        {
            return "API key not configured. Please set BUDGET_TRACKER_API_KEY environment variable.";
        }

        try
        {
            // Decode Base64 CSV content
            byte[] csvBytes;
            try
            {
                csvBytes = Convert.FromBase64String(csvContent);
            }
            catch (FormatException)
            {
                return "Invalid Base64 CSV content. Please ensure the CSV content is properly Base64 encoded.";
            }

            // Create multipart form data
            using var content = new MultipartFormDataContent();
            using var csvStreamContent = new ByteArrayContent(csvBytes);
            csvStreamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/csv");
            content.Add(csvStreamContent, "file", fileName);
            content.Add(new StringContent(account), "account");

            // Add API key to request headers
            httpClient.DefaultRequestHeaders.Remove("X-API-Key");
            httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);

            // Call the Budget Tracker API import endpoint
            var apiBaseUrl = GetApiBaseUrl(serviceProvider);
            var response = await httpClient.PostAsync($"{apiBaseUrl}/api/transactions/import", content);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                return FormatImportResult(result, fileName, account);
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                return $"Import failed: {error}";
            }
        }
        catch (Exception ex)
        {
            return $"Error importing CSV: {ex.Message}";
        }
    }

    private static string GetApiBaseUrl(IServiceProvider serviceProvider)
    {
        var configuration = serviceProvider.GetRequiredService<IOptions<BudgetTrackerConfiguration>>();
        return configuration.Value.ApiBaseUrl ?? "http://localhost:5295";
    }

    private static string FormatImportResult(string apiResponse, string fileName, string account)
    {
        try
        {
            using var doc = JsonDocument.Parse(apiResponse);
            var root = doc.RootElement;

            var importedCount = root.TryGetProperty("importedCount", out var imported) ? imported.GetInt32() : 0;
            var failedCount = root.TryGetProperty("failedCount", out var failed) ? failed.GetInt32() : 0;

            var result = new StringBuilder();
            result.AppendLine("CSV Import Results:");
            result.AppendLine($"Imported: {importedCount} transactions");

            if (failedCount > 0)
            {
                result.AppendLine($"Failed: {failedCount} transactions");
            }

            result.AppendLine($"File: {fileName}");
            result.AppendLine($"Account: {account}");
            result.AppendLine();
            result.AppendLine("Raw API Response:");
            result.AppendLine(apiResponse);

            return result.ToString();
        }
        catch
        {
            // If parsing fails, just return the raw response
            return $"CSV Import completed.\nFile: {fileName}\nAccount: {account}\n\nRaw API Response:\n{apiResponse}";
        }
    }
}
