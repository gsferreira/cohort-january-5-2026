# Workshop Step 071: Add Helicone LLM Observability

## Mission

In this step, you'll add comprehensive LLM observability to your budget tracking application using Helicone. You'll learn how to monitor AI requests, track costs, analyse performance, and debug LLM interactions in real-time.

**Your goal**: Integrate Helicone observability into your existing AI-powered transaction enhancement system to gain insights into LLM usage, costs, and performance.

**Learning Objectives**:
- Understanding LLM observability and its importance
- Setting up Helicone for Azure OpenAI monitoring
- Configuring secure API key management with .NET user secrets
- Implementing request/response logging and cost tracking
- Using Helicone dashboard for performance analysis and debugging
- Measuring and optimising LLM performance

---

## Prerequisites

**Required**: You need an Azure OpenAI service with a deployed model (e.g., gpt-4o-mini).

---

## Step 71.1: Create Helicone Account and Get API Key

*Set up a Helicone account to start monitoring your LLM requests.*

### 71.1.1: Sign up for Helicone

1. **Visit Helicone**: Go to [https://helicone.ai](https://helicone.ai)
2. **Create Account**: Create an account with your email or GitHub account (No credit card required, 7-day free trial). Use the European Data region.
3. **Verify Email**: Complete email verification if required
4. **Access Dashboard**: Once logged in, you'll see the Helicone dashboard

### 71.1.2: Generate API Key

1. **Navigate to Settings**: Click on your profile → "Settings"
2. **API Keys Section**: Go to the "API Keys" tab
3. **Create New Key**: Click "Create New API Key"
4. **Name Your Key**: Give it a descriptive name like "Budget Tracker Workshop". Read and Write permissions.
5. **Copy Key**: Save the API key securely - you'll need it in the next step

**Important**: Keep your Helicone API key secure and never commit it to version control.

---

## Step 71.2: Configure User Secrets for API Keys

*Set up secure storage for sensitive API keys using .NET user secrets instead of configuration files.*

### 71.2.1: Verify User Secrets Are Initialised

Your project should already have user secrets initialised from Week 2. Verify by checking that your `.csproj` file contains a `UserSecretsId`. If not, initialise:

```bash
cd src/BudgetTracker.Api
dotnet user-secrets init
```

### 71.2.2: Configure Helicone Headers

Add your Helicone API key to user secrets:

```bash
# Replace with your actual Helicone API key
dotnet user-secrets set "AzureAI:Headers:Helicone-Auth" "Bearer sk-helicone-xxxxx"
dotnet user-secrets set "AzureAI:Headers:Helicone-OpenAI-Api-Base" "https://your-resource.openai.azure.com/"
```

### 71.2.3: Switch to Helicone Endpoint

Update the endpoint to route through Helicone:

```bash
# Change endpoint to Helicone proxy
dotnet user-secrets set "AzureAI:Endpoint" "https://oai.helicone.ai"
```

---

## Step 71.3: Add Headers Support to Configuration

*Add the Headers dictionary to AzureAiConfiguration to support custom headers.*

### 71.3.1: Update AzureAiConfiguration

Update `src/BudgetTracker.Api/Infrastructure/AzureAiConfiguration.cs` to add Headers support:

```csharp
namespace BudgetTracker.Api.Infrastructure;

public class AzureAiConfiguration
{
    public const string SectionName = "AzureAI";

    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
    public string EmbeddingDeploymentName { get; set; } = string.Empty;
    public Dictionary<string, string> Headers { get; set; } = new();
}
```

**Note**: The class now has all 5 properties: `Endpoint`, `ApiKey`, `DeploymentName`, `EmbeddingDeploymentName` (added in Week 5), and the new `Headers` dictionary. The `Headers` property allows us to add custom headers like Helicone authentication headers to all Azure OpenAI requests.

---

## Step 71.4: Create Custom Header Policy

*Create a policy class to inject custom headers into Azure OpenAI requests.*

### 71.4.1: Create CustomHeaderPolicy

Create `src/BudgetTracker.Api/Infrastructure/CustomHeaderPolicy.cs`:

```csharp
using System.ClientModel.Primitives;

namespace BudgetTracker.Api.Infrastructure;

public class CustomHeaderPolicy : PipelinePolicy
{
    private readonly string _headerName;
    private readonly string _headerValue;

    public CustomHeaderPolicy(string headerName, string headerValue)
    {
        _headerName = headerName;
        _headerValue = headerValue;
    }

    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        message.Request.Headers.Add(_headerName, _headerValue);
        ProcessNext(message, pipeline, currentIndex);
    }

    public override ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        message.Request.Headers.Add(_headerName, _headerValue);
        return ProcessNextAsync(message, pipeline, currentIndex);
    }
}
```

---

## Step 71.5: Update IChatClient and IEmbeddingGenerator Registrations

*Modify the existing service registrations in Program.cs to inject Helicone headers via AzureOpenAIClientOptions.*

Your `Program.cs` already registers `IChatClient` and `IEmbeddingGenerator` as singletons. You need to modify these registrations so the underlying `AzureOpenAIClient` is created with custom header policies.

### 71.5.1: Create the AzureOpenAIClient with Headers

In `Program.cs`, first add the following `using` statement at the top of the file, alongside the other `using` directives:

```csharp
using System.ClientModel.Primitives;
```

Then, locate the existing `IChatClient` and `IEmbeddingGenerator` registrations. You should find this section:

```csharp
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

// Register IEmbeddingGenerator using Microsoft.Extensions.AI
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
{
    var config = sp.GetRequiredService<IOptions<AzureAiConfiguration>>().Value;
    return new AzureOpenAIClient(
        new Uri(config.Endpoint),
        new System.ClientModel.ApiKeyCredential(config.ApiKey))
        .GetEmbeddingClient(config.EmbeddingDeploymentName)
        .AsIEmbeddingGenerator();
});
```

**Replace** the entire block above with the code shown across steps 71.5.1, 71.5.2, and 71.5.3 below. Note that the configuration validation is removed for simplicity. User secrets will provide the required values.

First, add the shared client creation:

```csharp
// Build the AzureOpenAIClient with custom header policies for Helicone
var azureAiConfig = builder.Configuration
    .GetSection(AzureAiConfiguration.SectionName)
    .Get<AzureAiConfiguration>()!;

var clientOptions = new AzureOpenAIClientOptions();

foreach (var header in azureAiConfig.Headers)
{
    clientOptions.AddPolicy(
        new CustomHeaderPolicy(header.Key, header.Value),
        PipelinePosition.PerCall);
}

var azureClient = new AzureOpenAIClient(
    new Uri(azureAiConfig.Endpoint),
    new System.ClientModel.ApiKeyCredential(azureAiConfig.ApiKey),
    clientOptions);
```

### 71.5.2: Add IChatClient Registration

Then, add the new `IChatClient` registration using the shared client:

```csharp
builder.Services.AddSingleton<IChatClient>(
    azureClient
        .GetChatClient(azureAiConfig.DeploymentName)
        .AsIChatClient());
```

### 71.5.3: Add IEmbeddingGenerator Registration

Then, add the new `IEmbeddingGenerator` registration:

```csharp
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(
    azureClient
        .GetEmbeddingClient(azureAiConfig.EmbeddingDeploymentName)
        .AsIEmbeddingGenerator());
```

**Key insight**: Because all services (`TransactionEnhancerService`, `RecommendationService`, etc.) inject `IChatClient` or `IEmbeddingGenerator`, they automatically get Helicone tracking without any changes. This is the benefit of the current architecture: a single registration point for all AI access.

---

## Step 71.6: Update appsettings.json

*Add the Headers structure to the configuration file.*

### 71.6.1: Update appsettings.json Structure

Update `src/BudgetTracker.Api/appsettings.json` to include the `Headers` key in the `AzureAI` section:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AzureAI": {
    "Endpoint": "",
    "ApiKey": "",
    "DeploymentName": "",
    "EmbeddingDeploymentName": "",
    "Headers": {}
  },
  "AllowedHosts": "*"
}
```

**Important**:
- The `Headers` object enables the system to read Helicone headers from configuration
- All sensitive values (Endpoint, ApiKey, Headers) remain empty here
- User secrets will populate these values securely
- This structure is available to all environments

---

## Step 71.7: Test Helicone Integration

*Verify that Helicone is properly capturing and logging your LLM requests.*

### 71.7.1: Build and Run the Application

```bash
# Build the solution to ensure everything compiles
dotnet build

# Start the database
cd docker
docker compose up -d

# Run the API
cd ../src/BudgetTracker.Api
dotnet run
```

### 71.7.2: Test AI Enhancement with Sample Data

Upload a sample CSV file to trigger AI enhancement:

```http
### Test AI enhancement with Helicone tracking
POST http://localhost:5295/api/transactions/import
X-API-Key: test-key-user1
Content-Type: multipart/form-data; boundary=WebAppBoundary

--WebAppBoundary
Content-Disposition: form-data; name="account"

Checking Account
--WebAppBoundary
Content-Disposition: form-data; name="file"; filename="sample-transactions.csv"
Content-Type: text/csv

Date,Description,Amount,Balance
2025-01-15,AMZN MKTP US*123456789,-45.67,1250.33
2025-01-16,STARBUCKS COFFEE #1234,-5.89,1244.44
2025-01-17,DD VODAFONE PORTU 222111000,-52.30,3676.15
--WebAppBoundary--
```

### 71.7.3: Verify Helicone Dashboard

1. **Access Helicone Dashboard**: Go to [https://app.helicone.ai](https://app.helicone.ai)
2. **Login**: Use your Helicone account credentials
3. **Check Requests**: You should see your AI requests appear in the dashboard
4. **Inspect Details**: Click on individual requests to see:
   - Request/response payloads
   - Token usage and costs
   - Response times
   - Custom properties

**Expected Results**:
- Requests appear in Helicone dashboard within 30 seconds
- Request details show Azure OpenAI endpoint routing
- Token counts and estimated costs are displayed
- Custom properties like "service" and "environment" are visible

---

## Step 71.8: Monitor and Analyse LLM Performance

*Use Helicone's analytics features to understand your AI usage patterns.*

### 71.8.1: Key Metrics to Monitor

In the Helicone dashboard, focus on these important metrics:

**Cost Analysis**:
- Total daily/weekly/monthly costs
- Cost per request
- Token usage patterns
- Most expensive operations

**Performance Metrics**:
- Average response time
- Success/failure rates
- Rate limiting issues
- Error patterns

**Usage Patterns**:
- Request volume over time
- Peak usage hours
- Feature adoption (enhancement vs. categorisation)
- User behaviour analysis

### 71.8.2: Set Up Alerts (Optional)

1. **Cost Alerts**: Set up notifications when daily costs exceed thresholds
2. **Performance Alerts**: Monitor when response times spike
3. **Error Alerts**: Get notified of API failures or rate limits

### 71.8.3: Optimise Based on Insights

Use Helicone data to optimise your AI integration:

**Cost Optimisation**:
- Identify expensive prompts that could be shortened
- Find opportunities to use smaller models for simple tasks
- Implement request caching for repeated queries

**Performance Optimisation**:
- Monitor slow requests and optimise prompts
- Identify bottlenecks in your AI pipeline
- Adjust retry strategies based on error patterns

---

## Summary

You've successfully integrated Helicone LLM observability into your budget tracking application:

- **Account Setup**: Created Helicone account and obtained API keys
- **Secure Configuration**: Used .NET user secrets for sensitive API keys
- **Service Integration**: Configured Azure OpenAI client to route through Helicone
- **Request Tracking**: All AI requests are now logged and monitored
- **Dashboard Access**: Real-time monitoring of AI requests and performance
- **Cost Tracking**: Visibility into token usage and LLM costs

**Key Features Implemented**:
- **Complete Observability**: Every LLM request is tracked with full context
- **Cost Monitoring**: Real-time tracking of AI usage costs
- **Performance Analytics**: Response time and success rate monitoring
- **Error Tracking**: Detailed logging of failures and debugging information
- **Secure Configuration**: API keys stored safely using .NET user secrets

**Technical Achievements**:
- **Proxy Integration**: Seamless routing through Helicone without code changes to services
- **Configuration Management**: Secure handling of sensitive API credentials
- **Minimal Code Changes**: Only `AzureAiConfiguration`, `CustomHeaderPolicy`, and `Program.cs` are modified. All existing services automatically benefit
- **Both Chat and Embeddings**: Helicone tracks both `IChatClient` and `IEmbeddingGenerator` requests