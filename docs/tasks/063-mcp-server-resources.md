# Workshop Step 063: MCP Server Resources

## Mission

In this step, you'll add resources to your MCP server that expose read-only financial data to AI assistants. Resources provide context data through URI-based addressing, allowing AI clients to read transaction data and account summaries without calling tools.

**Your goal**: Add a static resource for recent transactions and a resource template for per-account spending summaries, then test them with the MCP Inspector.

**Learning Objectives**:
- Understanding MCP resources and their role in providing context
- Implementing static resources with fixed URIs
- Implementing resource templates with parameterised URIs
- Registering resources with the Microsoft MCP SDK
- Testing resources with the MCP Inspector

---

## Prerequisites

Before starting, ensure you completed:
- [062-mcp-server-prompts.md](062-mcp-server-prompts.md) - MCP Server Prompts
- Your Budget Tracker API is running with transactions in the database

---

## What Are MCP Resources?

MCP resources are read-only data endpoints that AI clients can discover and read:

- **Static resources**: Fixed URI, always return the same type of data (e.g., recent transactions)
- **Resource templates**: Parameterised URI, return data for specific inputs (e.g., summary for a given account)

Resources differ from tools:
- **Tools** perform actions and can have side effects
- **Resources** provide data and are strictly read-only

---

## Step 63.1: Create Resource Class

*Set up the resource class with MCP attributes.*

Create `src/BudgetTracker.McpServer/BudgetTrackerResources.cs`:

```csharp
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
}
```

**Key Elements**:
- **`[McpServerResourceType]`**: Marks the class as containing MCP resources
- **`static class`**: Resources don't require instance state
- **Helper methods**: Shared configuration for API calls

---

## Step 63.2: Implement RecentTransactions Resource

*Add a static resource that returns the last 10 transactions.*

Add this method to the `BudgetTrackerResources` class:

```csharp
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
```

**Key Elements**:
- **Fixed URI**: `budget://transactions/recent` - always points to the same data
- **`[McpServerResource]`**: Defines the resource with `UriTemplate` and `Name`
- **Read-only**: Only fetches data, never modifies it
- **JSON output**: Returns transaction data as JSON for the AI to parse

---

## Step 63.3: Implement AccountSummary Resource Template

*Add a resource template that returns a spending summary for a given account.*

This resource uses the existing natural language query endpoint to ask about a specific account's spending, demonstrating how resources can compose with existing API capabilities.

Add this method to the `BudgetTrackerResources` class:

```csharp
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
```

**Key Elements**:
- **URI template**: `budget://accounts/{account}/summary` - the `{account}` placeholder becomes a parameter
- **`string account` parameter**: Matches the `{account}` placeholder in the URI
- **Query endpoint**: Uses the existing `/api/query/ask` endpoint to generate an account-specific summary
- **Dynamic data**: Returns different data depending on which account is requested

---

## Step 63.4: Register Resources

*Configure the MCP server to expose your resources.*

Update the MCP server configuration in `Program.cs`. Find this section:

```csharp
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly()
    .WithPromptsFromAssembly();
```

Add resource registration. Since we use `.WithResourcesFromAssembly()`, the resources in `BudgetTrackerResources.cs` are discovered automatically:

```csharp
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly()
    .WithPromptsFromAssembly()
    .WithResourcesFromAssembly();
```

---

## Step 63.5: Test with MCP Inspector

*Use the MCP Inspector to verify your resources work correctly.*

```bash
npx @modelcontextprotocol/inspector
```

### Test RecentTransactions

1. Connect to your MCP server
2. Navigate to the **Resources** tab
3. You should see "RecentTransactions" listed
4. Click to read it
5. Verify you see a JSON array of transactions

### Test AccountSummary

1. In the **Resources** tab, find the "AccountSummary" template
2. Enter an account name that exists in your database (e.g., "Chase Checking")
3. Read the resource
4. Verify you see spending summary data for that account

### Verify Everything Together

Check all three tabs:
- **Tools**: QueryTransactions and ImportTransactionsCsv still work
- **Prompts**: Import prompt still generates correct messages
- **Resources**: Both resources return valid data

---

## Summary

You've added resources to your MCP server:

- **Static Resource**: RecentTransactions at `budget://transactions/recent` returns the last 10 transactions
- **Resource Template**: AccountSummary at `budget://accounts/{account}/summary` returns per-account spending data
- **Registration**: Resources discovered automatically with `.WithResourcesFromAssembly()`
- **Testing**: Verified both resources in the MCP Inspector

Your MCP server now exposes all three MCP primitives:
- **Tools** for actions (query, import)
- **Prompts** for guided workflows (import CSV)
- **Resources** for context data (recent transactions, account summaries)
