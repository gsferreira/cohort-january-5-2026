# Workshop Step 041: RAG-Enhanced Transaction Categorization

## Mission

In this step, you'll implement Retrieval Augmented Generation (RAG) to improve your AI transaction enhancement system. Instead of relying only on generic examples, the AI will now retrieve and analyze historical transactions from the user's account to provide more personalized and accurate categorization suggestions.

**Your goal**: Enhance the existing AI transaction enhancement system with RAG capabilities that leverage historical transaction patterns to improve categorization accuracy and personalization.

**Learning Objectives**:
- Understanding RAG concepts and implementation patterns
- Adding vector embeddings to transaction data models
- Implementing context retrieval from historical transactions
- Enhancing AI prompts with retrieved transaction patterns
- Using semantic similarity search with pgvector
- Combining recent transactions with semantic context

---

## Prerequisites

Before starting, ensure you completed:
- AI categorization and enhancements (Week 3)
- Azure OpenAI integration configured

---

## Part 1: Setting Up Vector Infrastructure

### Step 1.1: Install Pgvector NuGet Package

*Add the required NuGet package for PostgreSQL vector operations.*

The RAG system requires pgvector support for storing and querying vector embeddings. We need to install the Entity Framework Core integration package.

From the `src/BudgetTracker.Api/` directory, run:

```bash
# Install pgvector Entity Framework Core package
dotnet add package Pgvector.EntityFrameworkCore --version 0.2.2
```

This will add the package reference to your `.csproj` file:

```xml
<PackageReference Include="Pgvector.EntityFrameworkCore" Version="0.2.2" />
```

### Step 1.2: Add Vector Embeddings to Transaction Entity

*Extend the Transaction entity to support vector embeddings for semantic similarity search.*

Now that we have the pgvector package installed, we can add vector embedding capabilities to the Transaction entity. The embedding property will store 1536-dimensional vectors for semantic search.

Update `src/BudgetTracker.Api/Features/Transactions/TransactionTypes.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BudgetTracker.Api.Auth;
using Pgvector; // Add pgvector reference

namespace BudgetTracker.Api.Features.Transactions;

public class Transaction
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [Column(TypeName = "timestamptz")]
    public DateTime Date { get; set; }

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? Balance { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    [MaxLength(200)]
    public string? Labels { get; set; }

    [Required]
    [Column(TypeName = "timestamptz")]
    public DateTime ImportedAt { get; set; }

    [Required]
    [MaxLength(100)]
    public string Account { get; set; } = string.Empty;

    [Required]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? ImportSessionHash { get; set; }

    /// <summary>
    /// Vector embedding for semantic search (1536 dimensions for text-embedding-3-small)
    /// </summary>
    public Vector? Embedding { get; set; } // Add vector embedding property
}

// Rest of the file remains unchanged...
```

### Step 1.3: Configure Database for Vector Operations

*Update the database context to support pgvector extension and configure vector columns with appropriate indexes.*

The database needs to be configured to support vector operations efficiently. This includes enabling the pgvector extension, configuring the vector column type, and adding specialized indexes for both RAG context queries and semantic search.

Update `src/BudgetTracker.Api/Infrastructure/BudgetTrackerContext.cs`:

```csharp
using BudgetTracker.Api.Auth;
using BudgetTracker.Api.Features.Transactions;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore; // Add pgvector EF Core support

namespace BudgetTracker.Api.Infrastructure;

public class BudgetTrackerContext : IdentityDbContext<ApplicationUser>
{
    public BudgetTrackerContext(DbContextOptions<BudgetTrackerContext> options) : base(options)
    {
    }

    public DbSet<Transaction> Transactions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Enable pgvector extension
        modelBuilder.HasPostgresExtension("vector");

        // Configure Transaction entity
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()");

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_Transactions_UserId");

            // Composite index for RAG context queries (most selective first)
            entity.HasIndex(e => new { e.UserId, e.Account, e.Date })
                .HasDatabaseName("IX_Transactions_RagContext")
                .IsDescending(false, false, true); // Date descending for recent first

            // Category index for context analysis (with filter for non-null values)
            entity.HasIndex(e => e.Category)
                .HasDatabaseName("IX_Transactions_Category")
                .HasFilter("\"Category\" IS NOT NULL");

            // Configure vector column with explicit dimensions (1536 for text-embedding-3-small)
            entity.Property(e => e.Embedding)
                .HasColumnType("vector(1536)");

            // Vector index for semantic search (HNSW for fast similarity search)
            entity.HasIndex(e => e.Embedding)
                .HasDatabaseName("IX_Transactions_Embedding")
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops");

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .HasPrincipalKey(u => u.Id);
        });
    }
}
```

### Step 1.4: Prepare for Database Schema Changes

*Important: Switching to pgvector requires a fresh database. This will delete all existing data.*

Since we're changing the PostgreSQL image to support pgvector, we need to start with a clean database. **This will delete all your existing transactions and user accounts.**

#### 1.4.1: Reset the Database

```bash
# Stop and remove existing containers and volumes (THIS DELETES ALL DATA)
docker compose down -v

# Start fresh with pgvector-enabled PostgreSQL
docker compose up -d
```

#### 1.4.2: Register a New User Account

Since the database was reset, you need to create a new user account.

1. **Start both the API and web app**:

   In one terminal:
   ```bash
   cd src/BudgetTracker.Api/
   dotnet run
   ```

   In another terminal:
   ```bash
   cd src/BudgetTracker.Web/
   npm run dev
   ```

2. **Register a new account**:
   - Open your browser to `http://localhost:5173`
   - You'll see the Sign in screen
   - Navigate to `http://localhost:5173/register`
   - Fill in the registration form:
     - Email: Use any email (e.g., `test@example.com`)
     - Password: Must have at least 6 characters, one number, and one special character (e.g., `P@ssw0rd`)
   - Submit the form and log in with your new credentials

#### 1.4.3: Get Your New User ID

1. Make sure you're logged in to the web app
2. Open browser developer tools (F12)
3. Go to the **Network** tab
4. Reload the page
5. Find the request ending with `/me`
6. Click it and check the **Response** tab
7. Copy the `userId` value from the JSON response

#### 1.4.4: Update Your API Key Configuration

Update `src/BudgetTracker.Api/appsettings.Development.json` with your new user ID:

```json
{
  "StaticApiKeys": {
    "Keys": {
      "test-key-user1": {
        "UserId": "<paste-your-new-user-id-here>",
        "Name": "Test User",
        "Description": "API key for cohort testing"
      }
    }
  }
}
```

**Important**: Replace `<paste-your-new-user-id-here>` with the actual user ID you copied.

#### 1.4.5: Restart the API

Stop and restart the API for the configuration changes to take effect:

```bash
cd src/BudgetTracker.Api/
dotnet run
```

Your API key is now linked to your new user account.

### Step 1.5: Create Database Migration for Vector Support

*Generate and apply a database migration to add vector support and the new embedding column.*

From the `src/BudgetTracker.Api/` directory, run:

```bash
# Generate migration for vector support
dotnet ef migrations add AddVectorEmbeddings

# Apply the migration
dotnet ef database update
```

---

## Part 2: Implementing Embedding Services

### Step 2.1: Deploy Text Embedding Model in Azure AI Foundry

*Create a deployment for the text-embedding-3-small model that will generate vector embeddings.*

Before we can implement the embedding service, we need to deploy the embedding model in Azure AI Foundry. This model will convert transaction descriptions into 1536-dimensional vectors.

1. **Go to Azure AI Foundry**: https://ai.azure.com/
2. **Sign in** with your Azure account credentials
3. In the left sidebar, click **"Deployments"**
4. Click **"Deploy model"** or **"Create new deployment"**

**Deployment Settings:**
- **Model**: Search for and select **"text-embedding-3-small"**
- **Model version**: Use the default latest version
- **Deployment name**: Enter **"text-embedding-3-small"** (remember this name!)
- **Deployment type**: Standard

**Click "Deploy"** to create the embedding model deployment.

### Step 2.1b: Configure Embedding Deployment Name

*Store the embedding model deployment name in user secrets so the application can connect to it.*

Now that you've deployed the embedding model, you need to configure your application to use it. Add the deployment name to your user secrets.

From the `src/BudgetTracker.Api/` directory, run:

```bash
dotnet user-secrets set "AzureAi:EmbeddingDeploymentName" "text-embedding-3-small"
```

**Note**: Use the exact deployment name you specified when creating the deployment in Azure AI Foundry. If you used a different name, replace `"text-embedding-3-small"` with your actual deployment name.

You can verify your user secrets are configured correctly:

```bash
dotnet user-secrets list
```

You should see output including:

```
AzureAi:EmbeddingDeploymentName = text-embedding-3-small
AzureAi:DeploymentName = gpt-4.1-mini
AzureAi:Endpoint = https://your-resource.openai.azure.com/
AzureAi:ApiKey = your-api-key
```

### Step 2.2: Create Embedding Service Interface

*Define the interface for generating vector embeddings from transaction text.*

Create `src/BudgetTracker.Api/Features/Intelligence/Search/IAzureEmbeddingService.cs`:

```csharp
using Pgvector;

namespace BudgetTracker.Api.Features.Intelligence.Search;

public interface IAzureEmbeddingService
{
    /// <summary>
    /// Generate embedding for a single text input
    /// </summary>
    Task<Vector> GenerateEmbeddingAsync(string text);

    /// <summary>
    /// Generate embedding specifically for transaction description and category
    /// </summary>
    Task<Vector> GenerateTransactionEmbeddingAsync(string description, string? category = null);
}
```

### Step 2.3: Implement Azure Embedding Service

*Create the implementation that uses Microsoft.Extensions.AI to generate embeddings.*

The embedding service uses the `IEmbeddingGenerator` abstraction from Microsoft.Extensions.AI to generate 1536-dimensional vectors. This follows the same pattern as `IChatClient`, providing a unified abstraction that works with any AI provider.

Create `src/BudgetTracker.Api/Features/Intelligence/Search/AzureEmbeddingService.cs`:

```csharp
using Microsoft.Extensions.AI;
using Pgvector;

namespace BudgetTracker.Api.Features.Intelligence.Search;

public class AzureEmbeddingService : IAzureEmbeddingService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly ILogger<AzureEmbeddingService> _logger;

    public AzureEmbeddingService(
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        ILogger<AzureEmbeddingService> logger)
    {
        _embeddingGenerator = embeddingGenerator;
        _logger = logger;
    }

    public async Task<Vector> GenerateEmbeddingAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text cannot be null or empty", nameof(text));
        }

        try
        {
            var result = await _embeddingGenerator.GenerateAsync(text);
            return new Vector(result.Vector.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding for text: {Text}", text[..Math.Min(text.Length, 50)]);
            throw;
        }
    }

    public async Task<Vector> GenerateTransactionEmbeddingAsync(string description, string? category = null)
    {
        // Combine description and category for richer semantic representation
        var text = string.IsNullOrEmpty(category)
            ? description
            : $"{description} [{category}]";

        return await GenerateEmbeddingAsync(text);
    }
}
```

**Note**: The `IEmbeddingGenerator<string, Embedding<float>>` interface from Microsoft.Extensions.AI provides a unified abstraction. The embedding model is configured via `AzureAiConfiguration.EmbeddingDeploymentName` in Program.cs, following the same pattern as `IChatClient`.

### Step 2.4: Create Embedding Background Service

*Implement a background service to automatically generate embeddings for newly imported transactions.*

Since embedding generation is an expensive operation, we'll handle it asynchronously using a background service. This service processes newly imported transactions that don't have embeddings yet.

Create `src/BudgetTracker.Api/Features/Intelligence/Search/EmbeddingBackgroundService.cs`:

```csharp
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Intelligence.Search;

public class EmbeddingBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmbeddingBackgroundService> _logger;
    private readonly TimeSpan _processingInterval = TimeSpan.FromMinutes(5); // Check every 5 minutes
    private const int BatchSize = 50; // Process 50 transactions at a time

    public EmbeddingBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<EmbeddingBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Embedding background service started - processing new transactions only");

        // Periodic processing for new transactions only
        using var timer = new PeriodicTimer(_processingInterval);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessPendingEmbeddings(stoppingToken);
        }
    }

    private async Task ProcessPendingEmbeddings(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BudgetTrackerContext>();
            var embeddingService = scope.ServiceProvider.GetRequiredService<IAzureEmbeddingService>();

            // Find recently imported transactions without embeddings (last 24 hours)
            var cutoffTime = DateTime.UtcNow.AddHours(-24);
            var transactionsWithoutEmbeddings = await context.Transactions
                .Where(t => t.Embedding == null && t.ImportedAt >= cutoffTime)
                .OrderByDescending(t => t.ImportedAt) // Process newest first
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            if (!transactionsWithoutEmbeddings.Any())
            {
                _logger.LogDebug("No recent transactions found that need embeddings");
                return;
            }

            _logger.LogInformation("Processing embeddings for {Count} recent transactions", transactionsWithoutEmbeddings.Count);

            var successCount = 0;
            var errorCount = 0;

            foreach (var transaction in transactionsWithoutEmbeddings)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    // Generate embedding for transaction
                    var embedding = await embeddingService.GenerateTransactionEmbeddingAsync(
                        transaction.Description,
                        transaction.Category);

                    // Update transaction with embedding
                    transaction.Embedding = embedding;
                    successCount++;

                    _logger.LogDebug("Generated embedding for transaction {Id}: {Description}",
                        transaction.Id, transaction.Description[..Math.Min(transaction.Description.Length, 50)]);
                }
                catch (Exception ex)
                {
                    errorCount++;
                    _logger.LogWarning(ex, "Failed to generate embedding for transaction {Id}: {Description}",
                        transaction.Id, transaction.Description[..Math.Min(transaction.Description.Length, 50)]);

                    // Continue processing other transactions
                    continue;
                }
            }

            // Save all changes
            if (successCount > 0)
            {
                await context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Successfully generated embeddings for {SuccessCount} transactions, {ErrorCount} errors",
                    successCount, errorCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during embedding processing");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Embedding background service is stopping");
        await base.StopAsync(cancellationToken);
    }
}
```

---

## Part 3: Adding RAG to Transaction Enhancement

### Step 3.1: Add RAG Configuration Constants

*Define configuration constants for RAG operations to control context window size and retrieval behavior.*

RAG systems need careful tuning of context window size and retrieval parameters. These constants provide a centralized way to manage RAG behavior and make it easy to experiment with different settings.

In `src/BudgetTracker.Api/Features/Transactions/Import/Enhancement/TransactionEnhancer.cs`, add the constants at the top of the class:

```csharp
using Microsoft.Extensions.AI;

public class TransactionEnhancer : ITransactionEnhancer
{
    private readonly IChatClient _chatClient;  // Use Microsoft.Extensions.AI abstraction
    private readonly IAzureEmbeddingService _embeddingService;  // Add embedding service
    private readonly ILogger<TransactionEnhancer> _logger;
    private readonly BudgetTrackerContext _context; // Add database context

    // RAG Configuration Constants
    private const int DefaultContextLimit = 25; // Number of context transactions to retrieve
    private const int ContextWindowDays = 365;  // Time window for context retrieval

    public TransactionEnhancer(
        IChatClient chatClient,  // Use IChatClient
        IAzureEmbeddingService embeddingService,  // Add parameter
        ILogger<TransactionEnhancer> logger,
        BudgetTrackerContext context) // Inject database context
    {
        _chatClient = chatClient;
        _embeddingService = embeddingService;  // Assign field
        _logger = logger;
        _context = context;
    }

    // Rest of existing methods...
}
```

### Step 3.2: Implement Semantic Context Retrieval

*Add a method to retrieve semantically similar transactions that provide relevant context for AI enhancement.*

The RAG system needs to intelligently retrieve historical transactions that can inform the AI's categorization decisions. This method uses cosine distance for semantic similarity.

Add this method to `TransactionEnhancer.cs`:

```csharp
private async Task<List<Transaction>> GetSemanticContextTransactionsAsync(
    List<string> descriptions,
    string userId,
    string account,
    int limit,
    string excludeImportSessionHash)
{
    try
    {
        // Combine all descriptions into a single query for embedding
        var combinedQuery = string.Join(" ", descriptions.Take(5)); // Limit to avoid token overflow

        // Generate embedding for the combined descriptions
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(combinedQuery);
        var vectorString = queryEmbedding.ToString();

        var cutoffDate = DateTime.UtcNow.AddDays(-ContextWindowDays);

        // Build the base query conditions
        var conditions = new List<string>
        {
            "\"Embedding\" IS NOT NULL",
            "\"UserId\" = {0}",
            "\"Account\" = {1}",
            "\"ImportedAt\" >= {2}",
            "\"Category\" IS NOT NULL AND \"Category\" != ''",
            "\"ImportSessionHash\" != {3}",
        };

        var parameters = new List<object> { userId, account, cutoffDate, excludeImportSessionHash, vectorString, limit };

        var whereClause = string.Join(" AND ", conditions);

        // Use semantic similarity with cosine distance, but also factor in recency
        var similarTransactions = await _context.Transactions
            .FromSqlRaw($@"
                SELECT *
                FROM ""Transactions""
                WHERE {whereClause}
                 AND cosine_distance(""Embedding"",
                  {{4}}::vector) < 0.6
                ORDER BY cosine_distance(""Embedding"", {{4}}::vector) ASC,
                         ""Date"" DESC
                LIMIT {{5}}",
                parameters.ToArray())
            .ToListAsync();

        _logger.LogInformation("Found {Count} semantically similar context transactions for enhancement",
            similarTransactions.Count);

        return similarTransactions;
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Failed to get semantic context, falling back to empty list");

        // Fallback to empty list - better to proceed without context than fail
        return new List<Transaction>();
    }
}
```

### Step 3.3: Create RAG-Enhanced System Prompt

*Replace the static system prompt with a dynamic prompt that incorporates retrieved transaction patterns.*

The enhanced system prompt will include historical transaction examples specific to the user's account, providing the AI with relevant patterns and context for better categorization decisions.

Replace the `CreateEnhancedSystemPrompt` method in `TransactionEnhancer.cs`:

```csharp
private string CreateEnhancedSystemPrompt(List<Transaction> contextTransactions)
{
    var basePrompt = """
                     You are a transaction categorization assistant. Your job is to clean up messy bank transaction descriptions and make them more readable and meaningful for users.

                     Guidelines:
                     1. Transform cryptic merchant codes and bank jargon into clear, readable descriptions
                     2. Remove unnecessary reference numbers, codes, and technical identifiers
                     3. Identify the actual merchant or service provider
                     4. Suggest appropriate spending categories when possible
                     5. Maintain accuracy - don't invent information not present in the original
                     """;

    if (contextTransactions.Any())
    {
        var contextSection = "\n\nSIMILAR TRANSACTIONS for this account:\n";
        contextSection += string.Join("\n", contextTransactions.Select(t =>
            $"- \"{t.Description}\" → Amount: {t.Amount:C} → Category: \"{t.Category}\"").Distinct());

        contextSection +=
            "\n\nThese transactions were selected based on semantic similarity to the new transactions being processed.";
        contextSection +=
            "\nUse these patterns to inform your categorization decisions, paying special attention to:";
        contextSection += "\n- Similar merchant names or transaction types";
        contextSection += "\n- Comparable amount ranges for similar categories";
        contextSection += "\n- Established categorization patterns for this user";

        basePrompt += contextSection;
    }

    basePrompt += """

                  Examples:
                  - "AMZN MKTP US*123456789" → "Amazon Marketplace Purchase"
                  - "STARBUCKS COFFEE #1234" → "Starbucks Coffee"
                  - "SHELL OIL #4567" → "Shell Gas Station"
                  - "DD VODAFONE PORTU 222111000 PT00110011" → "Vodafone Portugal - Direct Debit"
                  - "COMPRA 0000 TEMU.COM DUBLIN" → "Temu Online Purchase"
                  - "TRF MB WAY P/ Manuel Silva" → "MB WAY Transfer to Manuel Silva"

                  Respond with a JSON array where each object has:
                  - "originalDescription": the input description
                  - "enhancedDescription": the cleaned description
                  - "suggestedCategory": optional category (e.g., "Groceries", "Entertainment", "Transportation", "Utilities", "Shopping", "Food & Drink", "Gas & Fuel", "Transfer")
                  - "confidenceScore": number between 0-1 indicating confidence in the enhancement

                  Be conservative with confidence scores - only use high scores (>0.8) when you're very certain about the merchant identification.
                  """;

    return basePrompt;
}
```

### Step 3.4: Update Enhancement Method to Use RAG

*Modify the main enhancement method to retrieve context and use the RAG-enhanced prompt.*

Update the `EnhanceDescriptionsAsync` method in `TransactionEnhancer.cs`:

```csharp
public async Task<List<EnhancedTransactionDescription>> EnhanceDescriptionsAsync(
    List<string> descriptions,
    string account,
    string userId,
    string currentImportSessionHash)  // Required parameter
{
    if (!descriptions.Any())
        return new List<EnhancedTransactionDescription>();

    var stopwatch = Stopwatch.StartNew();

    try
    {
        // Get semantically similar transactions for context
        var contextTransactions = await GetSemanticContextTransactionsAsync(descriptions, userId, account,
            DefaultContextLimit, currentImportSessionHash);

        _logger.LogInformation("Retrieved {ContextCount} context transactions for account {Account}",
            contextTransactions.Count, account);

        // Always create enhanced system prompt with available context
        var systemPrompt = CreateEnhancedSystemPrompt(contextTransactions);
        var userPrompt = CreateUserPrompt(descriptions);

        // Use Microsoft.Extensions.AI IChatClient
        var response = await _chatClient.GetResponseAsync([
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, userPrompt)
        ]);

        var content = response.Text ?? string.Empty;
        var results = ParseEnhancedDescriptions(content, descriptions);

        _logger.LogInformation("AI processing completed in {ProcessingTime}ms", stopwatch.ElapsedMilliseconds);

        return results;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to enhance transaction descriptions");
        return descriptions.Select(d => new EnhancedTransactionDescription
        {
            OriginalDescription = d,
            EnhancedDescription = d,
            ConfidenceScore = 0.0
        }).ToList();
    }
}
```

### Step 3.5: Update Interface

*Ensure the interface matches the new method signature.*

Update `src/BudgetTracker.Api/Features/Transactions/Import/Enhancement/ITransactionEnhancer.cs`:

```csharp
namespace BudgetTracker.Api.Features.Transactions.Import.Enhancement;

public interface ITransactionEnhancer
{
    Task<List<EnhancedTransactionDescription>> EnhanceDescriptionsAsync(
        List<string> descriptions,
        string account,
        string userId,
        string currentImportSessionHash);  // Required parameter
}
```

---

## Part 4: Dependency Injection and Testing

### Step 4.1: Update Dependency Injection

*Register all services including the background service for automatic embedding generation.*

Update `src/BudgetTracker.Api/Program.cs` to ensure proper DI registration:

```csharp
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

// Existing service registrations...

// Register IChatClient using Microsoft.Extensions.AI (should already exist from Week 3)
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var config = sp.GetRequiredService<IOptions<AzureAiConfiguration>>().Value;
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

// Register TransactionEnhancer with all its dependencies
builder.Services.AddScoped<ITransactionEnhancer, TransactionEnhancer>();

// Register embedding service for vector generation
builder.Services.AddScoped<IAzureEmbeddingService, AzureEmbeddingService>();

// Register background service for automatic embedding generation
builder.Services.AddHostedService<EmbeddingBackgroundService>();

// Ensure BudgetTrackerContext is registered with vector support
builder.Services.AddDbContext<BudgetTrackerContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
        o => o.UseVector()));
```

**Note**: The `IEmbeddingGenerator` registration follows the same pattern as `IChatClient`. The embedding model is configured via `config.EmbeddingDeploymentName`, which should be set in user secrets (see Step 012 for configuration instructions).

### Step 4.2: Test RAG Enhancement System

*Test the complete RAG-enhanced categorization workflow to verify improved accuracy and personalization.*

#### 4.2.1: Prepare Test Data

First, create some historical transactions to provide context:

```http
### Create initial transactions for context
POST http://localhost:5295/api/transactions/import
X-API-Key: test-key-user1
Content-Type: multipart/form-data; boundary=WebAppBoundary

--WebAppBoundary
Content-Disposition: form-data; name="account"

Checking Account
--WebAppBoundary
Content-Disposition: form-data; name="file"; filename="historical-context.csv"
Content-Type: text/csv

Date,Description,Amount,Balance
2024-12-01,STARBUCKS COFFEE #4567,-5.50,1500.00
2024-12-02,SHELL OIL #1234,-45.00,1455.00
2024-12-03,AMZN MKTP US*567890123,-23.99,1431.01
2024-12-04,TESCO PORTUGAL,-78.45,1352.56
2024-12-05,VODAFONE DIRECT DEBIT,-35.99,1316.57
--WebAppBoundary--
```

#### 4.2.2: Manually Categorize Context Transactions

Apply categories to these transactions to create meaningful context:

```http
### Get the imported transactions to apply categories
GET http://localhost:5295/api/transactions
X-API-Key: test-key-user1
```

Then manually update categories via the database or create an endpoint to set categories.

#### 4.2.3: Test RAG-Enhanced Categorization

Now test with new transactions that should benefit from the historical context:

```http
### Test RAG enhancement with new similar transactions
POST http://localhost:5295/api/transactions/import
X-API-Key: test-key-user1
Content-Type: multipart/form-data; boundary=WebAppBoundary

--WebAppBoundary
Content-Disposition: form-data; name="account"

Checking Account
--WebAppBoundary
Content-Disposition: form-data; name="file"; filename="rag-test-transactions.csv"
Content-Type: text/csv

Date,Description,Amount,Balance
2025-01-15,STARBUCKS COFFEE #9999,-6.50,1310.07
2025-01-16,SHELL OIL #5678,-48.00,1262.07
2025-01-17,AMZN MKTP US*111222333,-31.99,1230.08
2025-01-18,TESCO SUPERMERCADO,-82.15,1147.93
2025-01-19,VODAFONE PORT DD,-35.99,1111.94
--WebAppBoundary--
```

#### 4.2.4: Verify RAG Effectiveness

**Expected Results with RAG:**
- **Better Category Consistency**: Similar merchants get consistent categories based on historical patterns
- **Improved Confidence Scores**: Higher confidence when historical patterns match
- **Personalized Categories**: Categories align with user's specific spending patterns
- **Context Logging**: Log messages show context retrieval and usage

**Log Output to Expect:**
```
Retrieved 5 context transactions for account Checking Account
AI processing completed in 1250ms
```

---

## Summary

You've successfully implemented RAG-enhanced transaction categorization that leverages historical patterns for improved accuracy:

**Vector Database Support**: Added pgvector extension and embedding columns for semantic search capabilities

**Semantic Context Retrieval**: Implemented intelligent retrieval of semantically similar historical transactions

**Enhanced Prompts**: Dynamic system prompts that include user-specific transaction patterns

**Optimized Queries**: Database indexes optimized for RAG context retrieval operations

**Personalization**: AI categorization now adapts to individual user spending patterns

**Background Processing**: Automatic embedding generation for imported transactions

**Key Features Implemented**:
- **Semantic Similarity**: Find relevant transactions using vector embeddings, not just recency
- **Combined Ranking**: Order results by both semantic similarity and recency
- **Smart Context**: Only use transactions with existing categories as learning examples
- **Fallback Mechanisms**: Continue operation even if embedding service fails
- **Configurable Thresholds**: Tunable similarity thresholds and context limits

**What Users Get**:
- **Smarter Categorization**: Categories that reflect their actual spending patterns
- **Improved Accuracy**: Better merchant identification based on historical data
- **Consistency**: Similar transactions get consistent categorization over time
- **Learning System**: Categorization improves as transaction history grows
