# Workshop Step 061: MCP Server for AI Integration

## Mission

In this step, you'll build a Model Context Protocol (MCP) server that enables AI assistants to interact with your Budget Tracker through natural language. The MCP server will provide tools for querying transactions and importing CSV files, making your budget tracker accessible to AI assistants through any MCP-compatible IDE (e.g., Cursor, VS Code).

**Your goal**: Create a standalone MCP server using the Microsoft MCP SDK that provides secure access to your Budget Tracker API for natural language queries and CSV imports.

**Learning Objectives**:
- Understanding the Model Context Protocol (MCP) and its role in AI integration
- Building MCP tools with the Microsoft MCP SDK
- Implementing static API key authentication for secure tool access
- Creating natural language interfaces for financial data
- Integrating CSV import capabilities through MCP tools
- Configuring an IDE (Cursor, VS Code) to use your MCP server

---

## Prerequisites

Before starting, ensure you completed:
- [055-agentic-nlq.md](055-agentic-nlq.md) - Agentic Natural Language Queries
- Your Budget Tracker API is running with static API key authentication enabled

---

## Step 61.1: Create MCP Server Project

*Set up a standalone console application using the Microsoft MCP SDK.*

The MCP server will be a separate console application that communicates with AI assistants using the Model Context Protocol. This approach provides proper protocol compliance and separation of concerns from your main API.

Create a new console project in the solution:

```bash
# From the solution root
dotnet new console -n BudgetTracker.McpServer -o src/BudgetTracker.McpServer
dotnet sln add src/BudgetTracker.McpServer
```

Update `src/BudgetTracker.McpServer/BudgetTracker.McpServer.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.2" />
    <PackageReference Include="Microsoft.Extensions.Http" Version="10.0.2" />
    <PackageReference Include="ModelContextProtocol" Version="0.8.0-preview.1" />
  </ItemGroup>

</Project>
```

## Step 61.2: Implement Basic MCP Server Structure

*Create the MCP server with basic hosting infrastructure and stdio transport.*

The MCP server uses stdio (standard input/output) transport to communicate with AI assistants. This is the standard approach for MCP protocol compliance.

Replace `src/BudgetTracker.McpServer/Program.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.Configure<BudgetTrackerConfiguration>(
    builder.Configuration.GetSection("BudgetTracker"));

builder.Services.AddHttpClient();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
```

Create `src/BudgetTracker.McpServer/BudgetTrackerConfiguration.cs`:

```csharp
public class BudgetTrackerConfiguration
{
    public string? ApiBaseUrl { get; set; }
    public TimeSpan? DefaultTimeout { get; set; }
}
```

Create `src/BudgetTracker.McpServer/BudgetTrackerTools.cs`:

```csharp
using System.Net.Http.Json;
using System.ComponentModel;
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

    private static string GetApiBaseUrl(IServiceProvider serviceProvider)
    {
        var configuration = serviceProvider.GetRequiredService<IOptions<BudgetTrackerConfiguration>>();
        return configuration.Value.ApiBaseUrl ?? "http://localhost:5295";
    }
}
```

Create `src/BudgetTracker.McpServer/appsettings.json`:

```json
{
  "BudgetTracker": {
    "ApiBaseUrl": "http://localhost:5295",
    "DefaultTimeout": "00:00:30"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

## Step 61.3: Add CSV Import Tool

*Implement CSV import functionality through the MCP server using Base64 encoding.*

This tool enables AI assistants to help users import CSV files by accepting Base64-encoded file content and proxying it to your existing import API.

Add the CSV import tool and helper methods to the `BudgetTrackerTools` class in `BudgetTrackerTools.cs`:

```csharp
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
```

Add the required imports at the top of `BudgetTrackerTools.cs`:

```csharp
using System.Text;
using System.Text.Json;
```

## Step 61.4: IDE Integration

*Configure your IDE to use your MCP server for natural language interaction with your Budget Tracker.*

MCP works with any compatible IDE. Below are configuration examples for the two most common options — use whichever IDE you prefer.

### Cursor

Create or update `.cursor/mcp.json` in your project root:

```json
{
  "mcpServers": {
    "budget-tracker": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "src/BudgetTracker.McpServer/BudgetTracker.McpServer.csproj"
      ],
      "env": {
        "DOTNET_ENVIRONMENT": "Development",
        "BUDGET_TRACKER_API_KEY": "bt_mcp_key_2025_secure_static_api_key_for_ai_integration"
      }
    }
  }
}
```

### VS Code

Create or update `.vscode/mcp.json` in your project root:

```json
{
  "servers": {
    "budget-tracker": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "src/BudgetTracker.McpServer/BudgetTracker.McpServer.csproj"
      ],
      "env": {
        "DOTNET_ENVIRONMENT": "Development",
        "BUDGET_TRACKER_API_KEY": "bt_mcp_key_2025_secure_static_api_key_for_ai_integration"
      }
    }
  }
}
```

> **Note**: VS Code uses `"servers"` (not `"mcpServers"`) and requires `"type": "stdio"`. See the [VS Code MCP docs](https://code.visualstudio.com/docs/copilot/customization/mcp-servers) for more details.

**Important**: Make sure your Budget Tracker API is running on `http://localhost:5295` before testing the MCP server integration.

## Step 61.5: Test with MCP Inspector

*Use the official MCP Inspector to verify your tools are working before testing in an IDE.*

The MCP Inspector is an interactive testing tool provided by the Model Context Protocol project. It lets you connect to your MCP server and test tools directly in a browser UI.

### Install MCP Inspector

```bash
npx @modelcontextprotocol/inspector
```

### Configure Inspector

When the inspector starts, it will ask for your MCP server configuration. Enter:

**Command:**
```
dotnet
```

**Arguments:**

> **Note**: Review and adjust the path based on your platform and workspace location.
> - **Windows**: Use backslashes (`\`) instead of forward slashes: `src\BudgetTracker.McpServer\BudgetTracker.McpServer.csproj`
> - **Paths with spaces**: Wrap the entire path in double quotes (`"`) if it contains spaces

*Unix/Mac/Linux:*
```json
run --project src/BudgetTracker.McpServer/BudgetTracker.McpServer.csproj
```

*Windows:*
```json
run --project src\BudgetTracker.McpServer\BudgetTracker.McpServer.csproj
```

*Example with spaces (any platform):*
```json
run --project "src/Budget Tracker/BudgetTracker.McpServer/BudgetTracker.McpServer.csproj"
```

**Environment Variables:**

> **Note**: The `BUDGET_TRACKER_API_KEY` needs to include your user ID. To obtain it:
> 1. Start the Budget Tracker API and Web app
> 2. Log in to the web application
> 3. Open browser Developer Tools (F12) -> Network tab
> 4. Find the request to `/me` endpoint
> 5. Copy your user ID from the response (e.g., `a1b2c3d4-e5f6-7890-abcd-ef1234567890`)
> 6. Insert it into the API key in `appsettings.Development.json` at `StaticApiKeys`
> 7. Use the complete key here
>
> **Example**: If your user ID is `a1b2c3d4-e5f6-7890-abcd-ef1234567890`, the key becomes:
> ```
      "key-for-mcp": {
        "UserId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
        "Name": "Workshop Test User",
        "Description": "API key for workshop testing"
      }
> ```
> Where `key-for-mcp` is the key used for configuration.
>
> **This is a hack for demo purposes only** - In production, use proper authentication mechanisms.
>
> 8. Restart the api.

Configure the environment variables (MCP Inspector):
```
  "BUDGET_TRACKER_API_KEY": "key-for-mcp"
     "BudgetTracker__ApiBaseUrl": "http://localhost:5295"
```

### Test Your Tools

1. **Start your Budget Tracker API** (in a separate terminal):
   ```bash
   cd src/BudgetTracker.Api
   dotnet run
   ```

2. **In the MCP Inspector UI**:
   - Click **Connect**
   - Navigate to the **Tools** tab
   - You should see both `QueryTransactions` and `ImportTransactionsCsv` listed
   - Try calling `QueryTransactions` with a test question (e.g., "What are my recent transactions?")
   - Verify the response comes back from your Budget Tracker API

---

## Step 61.6: Test with AI Assistant

*Open your IDE and explore the MCP server integration with natural language prompts.*

1. **Start your Budget Tracker API**:
   ```bash
   cd src/BudgetTracker.Api
   dotnet run
   ```

2. **Open your IDE (Cursor, VS Code, etc.)** and ensure the MCP configuration is in place (see Step 61.4).

3. **Test the integration** by asking your AI assistant:

   > "What transactions do I have in my budget tracker? Show me my recent spending patterns."

The MCP server will automatically start when Claude needs to access your Budget Tracker data, and you'll see real-time responses about your financial information.

**Additional prompts to try**:
- "What did I spend on groceries this month?"
- "Show me my largest expenses from last week"
- "Help me import a CSV file with my bank transactions"

---

## Summary

You've built an MCP server for AI integration with your Budget Tracker:

- **MCP Server Structure**: Standalone console application using Microsoft MCP SDK
- **Authentication**: Static API key authentication for secure tool access
- **Natural Language Queries**: AI assistants can query financial data conversationally
- **CSV Import**: Base64-encoded file import through MCP protocol
- **MCP Inspector Testing**: Verified tools work using the official MCP Inspector
- **IDE Integration**: Ready for use with any MCP-compatible IDE (Cursor, VS Code)

Key technical decisions:
- **stdio transport** for protocol compliance and security
- **Microsoft MCP SDK** with attribute-based tool discovery
- **.NET hosting** with dependency injection and configuration
- **HttpClient** for authenticated API communication
