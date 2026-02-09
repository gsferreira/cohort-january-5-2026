using System.ComponentModel;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

[McpServerResourceType]
public static class BudgetTrackerResources
{
    private static string GetApiBaseUrl(IServiceProvider serviceProvider)
    {
        var configuration = serviceProvider.GetRequiredService<IOptions<BudgetTrackerConfiguration>>();
        return configuration.Value.ApiBaseUrl ?? "http://localhost:5295";
    }

    private static void ConfigureHttpClient(HttpClient httpClient)
    {
        var apiKey = Environment.GetEnvironmentVariable("BUDGET_TRACKER_API_KEY");
        httpClient.DefaultRequestHeaders.Remove("X-API-Key");
        if (!string.IsNullOrEmpty(apiKey))
        {
            httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        }
    }

    [McpServerResource(
        UriTemplate = "budget://transactions/recent",
        Name = "RecentTransactions")]
    public static async Task<string> RecentTransactions(
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
            ConfigureHttpClient(httpClient);

            var apiBaseUrl = GetApiBaseUrl(serviceProvider);
            var response = await httpClient.GetAsync($"{apiBaseUrl}/api/transactions?pageSize=10");

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }

            return $"Error fetching recent transactions: {response.StatusCode}";
        }
        catch (Exception ex)
        {
            return $"Error connecting to Budget Tracker API: {ex.Message}";
        }
    }

    [McpServerResource(
        UriTemplate = "budget://accounts/{account}/summary",
        Name = "AccountSummary")]
    public static async Task<string> AccountSummary(
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
            ConfigureHttpClient(httpClient);

            var apiBaseUrl = GetApiBaseUrl(serviceProvider);
            var response = await httpClient.PostAsJsonAsync(
                $"{apiBaseUrl}/api/query/ask",
                new { Query = $"Give me a spending summary for account: {account}" });

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }

            return $"Error fetching account summary: {response.StatusCode}";
        }
        catch (Exception ex)
        {
            return $"Error connecting to Budget Tracker API: {ex.Message}";
        }
    }
}
