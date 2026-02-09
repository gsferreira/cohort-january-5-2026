# Workshop Step 062: MCP Server Prompts

## Mission

In this step, you'll add prompt templates to your MCP server that help AI assistants guide users through Budget Tracker workflows. MCP prompts provide pre-configured conversation starters that make it easier for users to interact with your tools through natural language.

**Your goal**: Add a prompt template for CSV import workflow and test it using the MCP Inspector.

**Learning Objectives**:
- Understanding MCP prompts and their role in user experience
- Implementing prompt templates with the Microsoft MCP SDK
- Creating parameterised prompts for dynamic workflows
- Testing MCP servers with the official inspector tool

---

## Prerequisites

Before starting, ensure you completed:
- [061-mcp-server.md](061-mcp-server.md) - MCP Server for AI Integration
- Node.js installed for running the MCP Inspector

---

## What Are MCP Prompts?

MCP prompts are reusable conversation templates that:
- **Guide users** through complex workflows with pre-written prompts
- **Reduce friction** by providing starting points for common tasks
- **Include parameters** to customise the prompt for specific contexts
- **Appear in IDE prompt pickers** for easy discovery

**Tools vs Prompts**:
- **Tools**: Functions the AI can call to perform actions (e.g., `QueryTransactions`)
- **Prompts**: Conversation templates that guide the AI on how to use tools (e.g., "Help me import CSV transactions")

---

## Step 62.1: Add Import Prompt

*Create a prompt template that guides users through CSV import workflow.*

Create `src/BudgetTracker.McpServer/BudgetTrackerPrompts.cs`:

```csharp
using System.ComponentModel;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

[McpServerPromptType]
public static class BudgetTrackerPrompts
{
    [McpServerPrompt(Name = "Import"), Description("Import a csv file of transactions into the budget tracker")]
    public static ChatMessage Import([Description("Account name to import.")] string account)
    {
        return new(ChatRole.User, $"Import this csv file of transactions for account: {account}");
    }
}
```

**Key Elements**:
- **`[McpServerPromptType]`**: Marks the class as containing MCP prompts
- **`static class`**: Prompts don't require instance state
- **`[McpServerPrompt]`**: Defines the prompt with a name visible in IDEs
- **`Description`**: Helps users understand when to use this prompt
- **`ChatMessage`**: Returns a user message that guides the AI's response
- **`ChatRole.User`**: Indicates this is a user prompt (not system/assistant)

---

## Step 62.2: Register Prompts

*Configure the MCP server to expose your prompts.*

Update the MCP server configuration in `Program.cs` to include prompts. Find this section:

```csharp
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();
```

Add prompt registration:

```csharp
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly()
    .WithPromptsFromAssembly();
```

---

## Step 62.3: Test with MCP Inspector

*Use the official MCP Inspector to test your prompts and tools.*

The MCP Inspector is an interactive testing tool provided by the Model Context Protocol project.

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

### Test the Import Prompt

1. **Start your Budget Tracker API** (in a separate terminal):
   ```bash
   cd src/BudgetTracker.Api
   dotnet run
   ```

2. **In the MCP Inspector UI**:
   - Connect
   - Navigate to the **Prompts** tab
   - You should see "Import" listed with its description
   - Click on the Import prompt
   - Enter a value for the `account` parameter (e.g., "Chase Checking")
   - Click "Get Prompt"
   - You'll see the generated prompt message that would be sent to the AI

3. **Test the Tools Tab**:
   - Navigate to the **Tools** tab
   - You should see both `QueryTransactions` and `ImportTransactionsCsv`
   - Try calling `QueryTransactions` with a test question
   - Verify the response from your Budget Tracker API

---

## Summary

You've added prompt templates to your MCP server:

- **Prompt Implementation**: Created an Import prompt with parameterization
- **Static Class Pattern**: Used static class for stateless prompt definitions
- **MCP Registration**: Registered prompts with `.WithPromptsFromAssembly()`
- **Inspector Testing**: Tested prompts using the official MCP Inspector tool

Key concepts learned:
- **Prompt Templates**: Pre-written conversation starters for common workflows
- **Parameterization**: Dynamic prompts that adapt based on user input
- **ChatMessage API**: Using Microsoft.Extensions.AI for structured messages
- **MCP Inspector**: Interactive testing tool for MCP server development
