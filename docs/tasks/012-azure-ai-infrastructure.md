# Workshop Step 012: Azure AI Infrastructure

## Mission 🎯

In this step, you'll set up the infrastructure to connect your .NET application to Azure OpenAI. This creates the foundation for AI-powered features in future steps.

**Your goal**: Create a reusable Azure OpenAI chat service that can be used by any feature in your application.

**Learning Objectives**:
- .NET User Secrets for secure credential management
- Microsoft.Extensions.AI abstraction layer
- IChatClient interface for AI provider flexibility
- Configuration binding with strongly-typed options

---

## Prerequisites

Before starting, ensure you completed:
- [011-azure-ai-setup.md](011-azure-ai-setup.md) - Azure OpenAI resource and configuration

---

## Step 12.1: Configure Development Environment

*Set up your local environment with the Azure credentials using .NET User Secrets for security.*

### 12.1.1: Initialize User Secrets

*User Secrets keeps sensitive data out of your codebase and git repository.*

```bash
cd src/BudgetTracker.Api/
dotnet user-secrets init
```

This creates a unique secrets ID in your project file and prepares the secrets storage.

### 12.1.2: Set Azure OpenAI Secrets

*Add your Azure OpenAI credentials to the secure user secrets store.*

```bash
# Set your Azure OpenAI endpoint
dotnet user-secrets set "AzureAI:Endpoint" "https://your-resource.cognitiveservices.azure.com/"

# Set your API key
dotnet user-secrets set "AzureAI:ApiKey" "your-api-key-here"

# Set your deployment name
dotnet user-secrets set "AzureAI:DeploymentName" "gpt-4.1-mini"
```

**Replace with your actual values:**
- `your-resource`: Your actual Azure resource name
- `your-api-key-here`: The API key you copied from Azure portal
- `gpt-4.1-mini`: Your actual deployment name

### 12.1.3: Verify User Secrets

*List your secrets to verify they were set correctly (values are hidden for security).*

```bash
dotnet user-secrets list
```

You should see output like:
```
AzureAI:Endpoint = https://your-resource.openai.azure.com/
AzureAI:ApiKey = [Hidden]
AzureAI:DeploymentName = gpt-4.1-mini
```

### 12.1.4: Update appsettings Structure

*Add the AzureAI configuration section to your appsettings.json (without sensitive values).*

Update `src/BudgetTracker.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=budgettracker;Username=budgetuser;Password=budgetpass123"
  },
  "AzureAI": {
    "Endpoint": "",
    "ApiKey": "",
    "DeploymentName": ""
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

**Note**: The empty values will be overridden by user secrets during development. This shows the expected configuration structure without exposing secrets.

---

## Step 12.2: Install AI SDK Packages

*Add Microsoft.Extensions.AI with Azure OpenAI support to your API project.*

```bash
cd src/BudgetTracker.Api/
dotnet add package Azure.AI.OpenAI --version 2.1.0
dotnet add package Microsoft.Extensions.AI.OpenAI --version 10.1.1-preview.1.25612.2
```

**Why Microsoft.Extensions.AI?**
- Unified `IChatClient` abstraction works with any AI provider
- Built-in support for dependency injection
- Easy to swap providers (Azure, OpenAI, local models)
- Part of the official .NET ecosystem

## Step 12.3: Create AI Configuration Class

*Create a strongly-typed configuration class for Azure OpenAI settings.*

Create `src/BudgetTracker.Api/Infrastructure/AzureAiConfiguration.cs`:

```csharp
namespace BudgetTracker.Api.Infrastructure;

public class AzureAiConfiguration
{
    public const string SectionName = "AzureAI";

    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
}
```

## Step 12.4: Register AI Services

*Configure dependency injection for the Azure AI chat client.*

Update `src/BudgetTracker.Api/Program.cs` to register the configuration and `IChatClient`:

```csharp
using Azure.AI.OpenAI;
using BudgetTracker.Api.Infrastructure;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

// ... existing code ...

// Configure Azure AI
builder.Services.Configure<AzureAiConfiguration>(
    builder.Configuration.GetSection(AzureAiConfiguration.SectionName));

// Register IChatClient for Azure OpenAI
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var config = sp.GetRequiredService<IOptions<AzureAiConfiguration>>().Value;

    if (string.IsNullOrEmpty(config.Endpoint) || string.IsNullOrEmpty(config.ApiKey))
    {
        throw new InvalidOperationException(
            "Azure AI configuration is missing. Please configure Endpoint and ApiKey in user secrets.");
    }

    return new AzureOpenAIClient(
        new Uri(config.Endpoint),
        new System.ClientModel.ApiKeyCredential(config.ApiKey))
        .GetChatClient(config.DeploymentName)
        .AsIChatClient();
});

// ... rest of existing configuration ...
```

**Note**: The `IChatClient` interface from Microsoft.Extensions.AI provides a unified abstraction. You can inject it into any service that needs AI chat capabilities.

---

## Step 12.5: Test Azure Connection

*Verify your Azure OpenAI setup works by creating a simple test endpoint.*

### 12.5.1: Create Test Endpoint

Add a temporary test endpoint to verify the connection. Update `src/BudgetTracker.Api/Program.cs`:

```csharp
// Add this temporary endpoint for testing (remove after verification)
app.MapGet("/api/ai/test", async (IChatClient chatClient) =>
{
    var response = await chatClient.GetResponseAsync("Say 'Hello from Azure OpenAI!' in exactly those words.");
    return Results.Ok(new { message = response.Text });
}).WithTags("AI Test");
```

### 12.5.2: Run the Test

1. Start your API:
```bash
cd src/BudgetTracker.Api/
dotnet run
```

2. Test with curl or your browser:
```bash
curl http://localhost:5295/api/ai/test
```

3. **Expected response**:
```json
{
  "message": "Hello from Azure OpenAI!"
}
```

### 12.5.3: Clean Up

After verifying the connection works, **remove the test endpoint** from Program.cs.

---

## Troubleshooting 🔧

**Azure OpenAI Connection Issues:**
- Verify endpoint URL and API key from Step 011
- Check deployment name matches exactly
- Ensure Azure OpenAI resource is running

**User Secrets not working:**
- Ensure you ran `dotnet user-secrets init` from the correct project directory
- Verify secrets are set with `dotnet user-secrets list`
- Check that the secrets key names match exactly (case-sensitive)

**API key issues:**
- Regenerate keys in Azure portal if needed
- Ensure no extra spaces in copied keys
- Try Key 2 if Key 1 doesn't work

**Service Registration Issues:**
- Ensure all services registered in Program.cs
- Verify configuration section "AzureAI" exists in appsettings

---

## Summary ✅

You've successfully set up:

✅ **Local Configuration**: Secure credential management with .NET User Secrets
✅ **Microsoft.Extensions.AI**: Unified AI abstraction layer installed
✅ **Configuration Class**: Strongly-typed settings for Azure AI
✅ **IChatClient**: Registered Azure OpenAI chat client in DI
✅ **Connection Verified**: Tested communication with Azure OpenAI

**Key Infrastructure Components**:
- `AzureAiConfiguration` - Strongly-typed configuration
- `IChatClient` - Unified chat interface from Microsoft.Extensions.AI

**Next Step**: Use `IChatClient` in your features to add AI-powered capabilities to your application.

---

## Additional Resources

- **Microsoft.Extensions.AI**: https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai
- **Azure OpenAI .NET SDK**: https://learn.microsoft.com/en-us/dotnet/api/overview/azure/ai.openai-readme
- **.NET User Secrets**: https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets