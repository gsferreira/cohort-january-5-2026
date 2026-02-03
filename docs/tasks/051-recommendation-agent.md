# Workshop Step 051: Recommendation Agent

## Mission

In this step, you'll implement the **foundation** for an autonomous, AI-powered recommendation system that proactively analyzes user spending patterns and provides intelligent financial advice. This system will work in the background to generate recommendations automatically.

**Important**: This is **Step 1** in building an agentic recommendation system. This step creates the backend infrastructure and basic AI integration. In **Workshop Step 052**, you'll build the frontend UI components. In **Workshop Step 053**, you'll enhance the agent with tool-calling capabilities for sophisticated, evidence-based analysis.

**Your goal**: Build the backend for the recommendation system including autonomous background processing, database schema, API endpoints, and a simple AI-powered recommendation generator. You'll create a working backend that you'll enhance with frontend UI in Step 052 and agentic capabilities in Step 053.

**Learning Objectives**:
- Implementing autonomous background services for continuous analysis
- Designing background processing architectures for scheduled execution
- Creating proactive recommendation systems with priority scoring
- Integrating basic AI-powered analysis into backend services
- Building recommendation infrastructure (database, API)
- Understanding the progression from simple AI to agentic systems

---

## Prerequisites

Before starting, ensure you completed:
- [043-nlq-ui.md](043-nlq-ui.md) - Natural Language Query UI (Week 5)

---

## Background: Why Start Simple?

This workshop implements a **simple, working recommendation system** as a foundation for agentic AI:

**What You'll Build in This Step:**
- Autonomous background processing (runs without user intervention)
- Basic AI-powered recommendations (high-level financial advice)
- Backend infrastructure (database, API endpoints)
- Scheduled generation with smart caching

**What It Does:**
Analyzes high-level transaction statistics (total income, expenses, top categories) and generates general financial recommendations using AI.

**What It Doesn't Do (Yet):**
Sophisticated, targeted analysis of specific spending patterns. The AI receives only summary statistics, not detailed transaction data.

**Why Start Simple?**
This approach teaches the evolution from basic AI integration to autonomous agentic systems:
- **Step 051 (This Step)**: Backend with simple AI call → General recommendations
- **Step 052 (Next Step)**: Frontend UI components → Display recommendations
- **Step 053 (After That)**: Agentic AI with tool-calling → Evidence-based, targeted recommendations

---

## Step 51.1: Create Recommendation Data Model

*Define the core recommendation entity and supporting data structures.*

The recommendation system requires a robust data model that can store various types of recommendations with different priorities and track their lifecycle. This foundation will support the autonomous agent's decision-making process.

Create `src/BudgetTracker.Api/Features/Intelligence/Recommendations/Recommendation.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using BudgetTracker.Api.Auth;

namespace BudgetTracker.Api.Features.Intelligence.Recommendations;

public class Recommendation
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    [Required]
    public RecommendationType Type { get; set; }

    [Required]
    public RecommendationPriority Priority { get; set; }

    [Required]
    [Column(TypeName = "timestamptz")]
    public DateTime GeneratedAt { get; set; }

    [Required]
    [Column(TypeName = "timestamptz")]
    public DateTime ExpiresAt { get; set; }

    [Required]
    public RecommendationStatus Status { get; set; } = RecommendationStatus.Active;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RecommendationType
{
    SpendingAlert,
    SavingsOpportunity,
    BehavioralInsight,
    BudgetWarning
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RecommendationPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum RecommendationStatus
{
    Active,
    Expired
}

public class RecommendationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public RecommendationType Type { get; set; }
    public RecommendationPriority Priority { get; set; }
    public DateTime GeneratedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

internal static class RecommendationExtensions
{
    public static RecommendationDto MapToDto(this Recommendation recommendation)
    {
        return new RecommendationDto
        {
            Id = recommendation.Id,
            Title = recommendation.Title,
            Message = recommendation.Message,
            Type = recommendation.Type,
            Priority = recommendation.Priority,
            GeneratedAt = recommendation.GeneratedAt,
            ExpiresAt = recommendation.ExpiresAt
        };
    }
}
```

## Step 51.2: Create Recommendation Repository Interface

*Define the contract for recommendation data access and business logic.*

The repository interface provides a clean abstraction for recommendation operations, enabling proper dependency injection and testability.

Create `src/BudgetTracker.Api/Features/Intelligence/Recommendations/IRecommendationRepository.cs`:

```csharp
namespace BudgetTracker.Api.Features.Intelligence.Recommendations;

public interface IRecommendationRepository
{
    Task<List<Recommendation>> GetActiveRecommendationsAsync(string userId);
    Task GenerateRecommendationsAsync(string userId);
}

public interface IRecommendationWorker
{
    Task ProcessAllUsersRecommendationsAsync();
    Task ProcessUserRecommendationsAsync(string userId);
}
```

## Step 51.3: Implement Simple Recommendation Agent

*Build a basic recommendation agent with simple statistics and AI integration.*

The recommendation agent provides the foundation for the recommendation system. It gathers basic transaction statistics and uses AI to generate general financial recommendations. In the next workshop step, you'll enhance this with tool-calling capabilities for sophisticated analysis.

Create `src/BudgetTracker.Api/Features/Intelligence/Recommendations/RecommendationAgent.cs`:

```csharp
using System.Text.Json;
using Microsoft.Extensions.AI;
using BudgetTracker.Api.Features.Transactions;
using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Infrastructure.Extensions;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Intelligence.Recommendations;

internal class GeneratedRecommendation
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public RecommendationType Type { get; set; }
    public RecommendationPriority Priority { get; set; }
}

internal class BasicStats
{
    public int TransactionCount { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public string DateRange { get; set; } = string.Empty;
    public List<string> TopCategories { get; set; } = new();
}

public class RecommendationAgent : IRecommendationRepository
{
    private readonly BudgetTrackerContext _context;
    private readonly IChatClient _chatClient;
    private readonly ILogger<RecommendationAgent> _logger;

    public RecommendationAgent(
        BudgetTrackerContext context,
        IChatClient chatClient,
        ILogger<RecommendationAgent> logger)
    {
        _context = context;
        _chatClient = chatClient;
        _logger = logger;
    }

    public async Task<List<Recommendation>> GetActiveRecommendationsAsync(string userId)
    {
        return await _context.Recommendations
            .Where(r => r.UserId == userId &&
                       r.Status == RecommendationStatus.Active &&
                       r.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(r => r.Priority)
            .ThenByDescending(r => r.GeneratedAt)
            .Take(5)
            .ToListAsync();
    }

    public async Task GenerateRecommendationsAsync(string userId)
    {
        try
        {
            // 1. Check if we need to regenerate
            var lastGenerated = await _context.Recommendations
                .Where(r => r.UserId == userId)
                .MaxAsync(r => (DateTime?)r.GeneratedAt);

            var lastImported = await _context.Transactions
                .Where(t => t.UserId == userId)
                .MaxAsync(t => (DateTime?)t.ImportedAt);

            // Skip if no new transactions since last generation (within 1 minute for dev testing)
            if (lastGenerated.HasValue && lastImported.HasValue &&
                lastGenerated > lastImported.Value.AddMinutes(-1)) // DEMO
            {
                _logger.LogInformation("Skipping generation - no new data for user {UserId}", userId);
                return;
            }

            // 2. Check minimum transaction count
            var transactionCount = await _context.Transactions
                .Where(t => t.UserId == userId)
                .CountAsync();

            if (transactionCount < 5)
            {
                _logger.LogInformation("Insufficient transaction data for user {UserId}", userId);
                return;
            }

            // 3. Get basic statistics
            var basicStats = await GetBasicStatsAsync(userId);

            // 4. Generate recommendations with AI
            var recommendations = await GenerateSimpleRecommendationsAsync(basicStats);

            // 5. Store recommendations
            await StoreRecommendationsAsync(userId, recommendations);

            _logger.LogInformation("Generated {Count} recommendations for user {UserId}",
                recommendations.Count, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate recommendations for user {UserId}", userId);
        }
    }

    private async Task<BasicStats> GetBasicStatsAsync(string userId)
    {
        var transactions = await _context.Transactions
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.Date)
            .Take(1000)
            .ToListAsync();

        if (!transactions.Any())
        {
            return new BasicStats();
        }

        return new BasicStats
        {
            TransactionCount = transactions.Count,
            TotalIncome = transactions.Where(t => t.Amount > 0).Sum(t => t.Amount),
            TotalExpenses = Math.Abs(transactions.Where(t => t.Amount < 0).Sum(t => t.Amount)),
            DateRange = $"{transactions.Min(t => t.Date):yyyy-MM-dd} to {transactions.Max(t => t.Date):yyyy-MM-dd}",
            TopCategories = transactions
                .Where(t => t.Amount < 0 && !string.IsNullOrEmpty(t.Category))
                .GroupBy(t => t.Category)
                .OrderByDescending(g => Math.Abs(g.Sum(t => t.Amount)))
                .Take(5)
                .Select(g => g.Key!)
                .ToList()
        };
    }

    private async Task<List<GeneratedRecommendation>> GenerateSimpleRecommendationsAsync(BasicStats stats)
    {
        var systemPrompt = """
            You are a financial assistant providing general recommendations based on high-level transaction statistics.

            Generate 3-5 actionable financial recommendations in JSON format:
            {
              "recommendations": [
                {
                  "title": "Brief, attention-grabbing title",
                  "message": "Actionable recommendation based on the statistics provided",
                  "type": "SpendingAlert|SavingsOpportunity|BehavioralInsight|BudgetWarning",
                  "priority": "Low|Medium|High|Critical"
                }
              ]
            }

            Make recommendations:
            - GENERAL: Based on overall spending patterns
            - ACTIONABLE: Clear next steps users can take
            - RELEVANT: Focus on income/expense balance and top spending categories
            """;

        var userPrompt = $"""
            Based on these high-level statistics, provide 3-5 financial recommendations:

            - Total Income: ${stats.TotalIncome:F2}
            - Total Expenses: ${stats.TotalExpenses:F2}
            - Net: ${stats.TotalIncome - stats.TotalExpenses:F2}
            - Transaction Count: {stats.TransactionCount}
            - Date Range: {stats.DateRange}
            - Top Spending Categories: {string.Join(", ", stats.TopCategories)}

            Provide helpful, general financial advice based on these statistics.
            """;

        try
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, systemPrompt),
                new(ChatRole.User, userPrompt)
            };

            var response = await _chatClient.GetResponseAsync(messages);
            var content = response.Text ?? string.Empty;
            return ParseRecommendations(content.ExtractJsonFromCodeBlock());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate AI recommendations");
            return new List<GeneratedRecommendation>();
        }
    }

    private List<GeneratedRecommendation> ParseRecommendations(string response)
    {
        try
        {
            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(response);
            var recommendations = new List<GeneratedRecommendation>();

            if (jsonResponse.TryGetProperty("recommendations", out var recsArray))
            {
                foreach (var rec in recsArray.EnumerateArray())
                {
                    if (rec.TryGetProperty("title", out var title) &&
                        rec.TryGetProperty("message", out var message) &&
                        rec.TryGetProperty("type", out var type) &&
                        rec.TryGetProperty("priority", out var priority))
                    {
                        recommendations.Add(new GeneratedRecommendation
                        {
                            Title = title.GetString() ?? "",
                            Message = message.GetString() ?? "",
                            Type = Enum.TryParse<RecommendationType>(type.GetString(), out var t) ? t : RecommendationType.BehavioralInsight,
                            Priority = Enum.TryParse<RecommendationPriority>(priority.GetString(), out var p) ? p : RecommendationPriority.Medium
                        });
                    }
                }
            }

            return recommendations.Take(5).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI response: {Response}", response);
            return new List<GeneratedRecommendation>();
        }
    }

    private async Task StoreRecommendationsAsync(string userId, List<GeneratedRecommendation> aiRecommendations)
    {
        if (!aiRecommendations.Any()) return;

        // Expire old active recommendations
        var oldRecommendations = await _context.Recommendations
            .Where(r => r.UserId == userId && r.Status == RecommendationStatus.Active)
            .ToListAsync();

        foreach (var old in oldRecommendations)
        {
            old.Status = RecommendationStatus.Expired;
        }

        // Add new recommendations
        var newRecommendations = aiRecommendations.Select(ai => new Recommendation
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = ai.Title,
            Message = ai.Message,
            Type = ai.Type,
            Priority = ai.Priority,
            GeneratedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = RecommendationStatus.Active
        }).ToList();

        await _context.Recommendations.AddRangeAsync(newRecommendations);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Stored {Count} recommendations for user {UserId}", newRecommendations.Count, userId);
    }
}
```

## Step 51.4: Implement Background Processing Service

*Create the background service that runs the recommendation agent autonomously.*

The background service ensures that recommendations are generated proactively without impacting user experience, running on a schedule and triggered by new data.

Create `src/BudgetTracker.Api/Features/Intelligence/Recommendations/RecommendationBackgroundService.cs`:

```csharp
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Intelligence.Recommendations;

public class RecommendationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RecommendationBackgroundService> _logger;
    private readonly TimeSpan _dailyRunTime = TimeSpan.FromHours(6); // 6 AM daily

    public RecommendationBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<RecommendationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        _logger.LogInformation("Recommendation background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IRecommendationWorker>();

                await processor.ProcessAllUsersRecommendationsAsync();
                await CleanupExpiredRecommendationsAsync();

                var nextRun = GetNextRunTime();
                var delay = nextRun - DateTime.UtcNow;

                if (delay > TimeSpan.Zero)
                {
                    _logger.LogInformation("Next recommendation run scheduled for {NextRun}", nextRun);
                    await Task.Delay(delay, stoppingToken);
                }
                else
                {
                    await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Recommendation background service cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in recommendation background service");
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
            }
        }

        _logger.LogInformation("Recommendation background service stopped");
    }

    private async Task CleanupExpiredRecommendationsAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<BudgetTrackerContext>();

            var expiredRecommendations = await context.Recommendations
                .Where(r => r.Status == RecommendationStatus.Active && r.ExpiresAt <= DateTime.UtcNow)
                .ToListAsync();

            if (expiredRecommendations.Any())
            {
                foreach (var recommendation in expiredRecommendations)
                {
                    recommendation.Status = RecommendationStatus.Expired;
                }

                await context.SaveChangesAsync();
                _logger.LogInformation("Marked {Count} recommendations as expired", expiredRecommendations.Count);
            }

            var cutoffDate = DateTime.UtcNow.AddDays(-30);
            var oldRecommendations = await context.Recommendations
                .Where(r => r.GeneratedAt < cutoffDate)
                .ToListAsync();

            if (oldRecommendations.Any())
            {
                context.Recommendations.RemoveRange(oldRecommendations);
                await context.SaveChangesAsync();
                _logger.LogInformation("Deleted {Count} old recommendations", oldRecommendations.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup expired recommendations");
        }
    }

    private DateTime GetNextRunTime()
    {
        var now = DateTime.UtcNow;
        var today6Am = DateTime.UtcNow.Date.Add(_dailyRunTime);

        if (now < today6Am)
        {
            return today6Am; // Today at 6 AM UTC
        }
        else
        {
            return DateTime.UtcNow.Date.AddDays(1).Add(_dailyRunTime); // Tomorrow at 6 AM UTC
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Stopping recommendation background service...");
        await base.StopAsync(stoppingToken);
    }
}
```

## Step 51.5: Create Recommendation Worker

*Implement the worker that processes recommendations for multiple users.*

The recommendation processor handles the batch processing of recommendations across all users, ensuring efficient resource utilization.

Create `src/BudgetTracker.Api/Features/Intelligence/Recommendations/RecommendationProcessor.cs`:

```csharp
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Intelligence.Recommendations;

public class RecommendationProcessor : IRecommendationWorker
{
    private readonly BudgetTrackerContext _context;
    private readonly IRecommendationRepository _repository;
    private readonly ILogger<RecommendationProcessor> _logger;

    public RecommendationProcessor(
        BudgetTrackerContext context,
        IRecommendationRepository repository,
        ILogger<RecommendationProcessor> logger)
    {
        _context = context;
        _repository = repository;
        _logger = logger;
    }

    public async Task ProcessAllUsersRecommendationsAsync()
    {
        try
        {
            // Get all users with transactions
            var userIds = await _context.Transactions
                .Select(t => t.UserId)
                .Distinct()
                .ToListAsync();

            _logger.LogInformation("Processing recommendations for {UserCount} users", userIds.Count);

            var successCount = 0;
            var errorCount = 0;

            foreach (var userId in userIds)
            {
                try
                {
                    await ProcessUserRecommendationsAsync(userId);
                    successCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process recommendations for user {UserId}", userId);
                    errorCount++;
                }

                // Small delay to avoid overwhelming the system
                await Task.Delay(100);
            }

            _logger.LogInformation("Completed recommendation processing: {SuccessCount} successful, {ErrorCount} errors",
                successCount, errorCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process recommendations for all users");
        }
    }

    public async Task ProcessUserRecommendationsAsync(string userId)
    {
        try
        {
            await _repository.GenerateRecommendationsAsync(userId);
            _logger.LogDebug("Processed recommendations for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process recommendations for user {UserId}", userId);
            throw;
        }
    }
}
```

## Step 51.6: Create Recommendation API Endpoints

*Build the REST API endpoints for recommendation functionality.*

The API endpoints provide the interface for the frontend to interact with the recommendation system, allowing users to view their active recommendations.

Create `src/BudgetTracker.Api/Features/Intelligence/Recommendations/RecommendationApi.cs`:

```csharp
using System.Security.Claims;
using BudgetTracker.Api.Auth;

namespace BudgetTracker.Api.Features.Intelligence.Recommendations;

public static class RecommendationApi
{
    public static IEndpointRouteBuilder MapRecommendationEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/recommendations",
                async (IRecommendationRepository recommendationRepository, ClaimsPrincipal claimsPrincipal) =>
                {
                    var userId = claimsPrincipal.GetUserId();
                    var recommendations = await recommendationRepository.GetActiveRecommendationsAsync(userId);
                    var dtos = recommendations.Select(r => r.MapToDto()).ToList();
                    return Results.Ok(dtos);
                })
            .RequireAuthorization()
            .WithName("GetRecommendations")
            .WithSummary("Get active recommendations")
            .WithDescription("Returns up to 5 active, non-expired recommendations ordered by priority")
            .Produces<List<RecommendationDto>>();

        return routes;
    }
}
```

## Step 51.7: Set Up Intelligence Endpoints Registration

*Create the main intelligence endpoints registration class.*

This class coordinates all intelligence-related endpoints, including recommendations and other AI features.

Create `src/BudgetTracker.Api/Features/Intelligence/IntelligenceEndpoints.cs`:

```csharp
using BudgetTracker.Api.Features.Intelligence.Query;
using BudgetTracker.Api.Features.Intelligence.Recommendations;

namespace BudgetTracker.Api.Features.Intelligence;

public static class IntelligenceEndpoints
{
    public static IEndpointRouteBuilder MapIntelligenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapQueryEndpoints();
        endpoints.MapRecommendationEndpoints();
        return endpoints;
    }
}
```

## Step 51.8: Update Database Context

*Add the recommendations table to the database context.*

The database context needs to include the recommendations table for Entity Framework to manage the recommendation data.

Update `src/BudgetTracker.Api/Infrastructure/BudgetTrackerContext.cs` to include the Recommendations DbSet:

```csharp
// Add this property to the BudgetTrackerContext class
public DbSet<Recommendation> Recommendations { get; set; }
```

And add the using statement at the top:

```csharp
using BudgetTracker.Api.Features.Intelligence.Recommendations;
```

## Step 51.9: Register Services and Background Processing

*Configure dependency injection and background services.*

Register all the recommendation services and configure the background processing in the application startup.

Update `src/BudgetTracker.Api/Program.cs` to include recommendation services:

```csharp
// Add these service registrations after existing services
builder.Services.AddScoped<IRecommendationRepository, RecommendationAgent>();
builder.Services.AddScoped<IRecommendationWorker, RecommendationProcessor>();

// Add the background service
builder.Services.AddHostedService<RecommendationBackgroundService>();
```

Also replace the existing `.MapQueryEndpoints()` in the endpoint mapping chain with `.MapIntelligenceEndpoints()`:

```csharp
// Replace .MapQueryEndpoints() with .MapIntelligenceEndpoints()
app
    .MapGroup("/api")
    .MapAntiForgeryEndpoints()
    .MapAuthEndpoints()
    .MapTransactionEndpoints()
    .MapIntelligenceEndpoints()
    .MapAnalyticsEndpoints();
```

Add the required using statements at the top of `Program.cs`:

```csharp
using BudgetTracker.Api.Features.Intelligence;
using BudgetTracker.Api.Features.Intelligence.Recommendations;
```

## Step 51.10: Create Database Migration

*Generate and apply the database migration for recommendations.*

Create the database migration to add the recommendations table to the database schema.

```bash
# From src/BudgetTracker.Api/ directory
dotnet ef migrations add AddRecommendations
dotnet ef database update
```

## Step 51.11: Test the Backend

*Test the recommendation backend functionality.*

### 51.11.1: Test Background Service

Verify that the background service is running and processing recommendations:

```bash
# Check logs for background service startup
dotnet run --project src/BudgetTracker.Api/

# Look for logs like:
# "Recommendation background service started"
# "Processing recommendations for X users"
```

### 51.11.2: Test Recommendation Retrieval

Verify that recommendations are properly returned by the API:

```http
### Get active recommendations
GET http://localhost:5295/api/recommendations
X-API-Key: test-key-user1
```

**Expected Response:**
```json
[
  {
    "id": "guid-here",
    "title": "Review Your Budget",
    "message": "Your expenses are approaching your income. Consider reviewing your spending in top categories to identify savings opportunities.",
    "type": "BudgetWarning",
    "priority": "Medium",
    "generatedAt": "2025-01-20T10:30:00Z",
    "expiresAt": "2025-01-27T10:30:00Z"
  },
  {
    "id": "guid-here-2",
    "title": "Track Your Top Spending Categories",
    "message": "Most of your expenses are in Groceries, Dining, and Shopping. Monitoring these categories could help you save more.",
    "type": "BehavioralInsight",
    "priority": "Low",
    "generatedAt": "2025-01-20T10:30:00Z",
    "expiresAt": "2025-01-27T10:30:00Z"
  }
]
```

---

## Summary

You've successfully implemented the **backend for the recommendation system** that serves as the foundation for agentic AI capabilities:

- **Autonomous Background Processing**: Recommendations generated automatically without user intervention
- **Backend Infrastructure**: Database schema, API endpoints, and background service
- **Basic AI Integration**: Simple AI-powered recommendations using high-level transaction statistics
- **Priority-Based System**: Intelligent prioritization of recommendations based on urgency and impact
- **Scheduled Generation**: Daily background processing with smart caching to avoid redundant generation

You've built a **working recommendation backend** that demonstrates autonomous background processing and AI integration. In the next step (052), you'll build the frontend UI components. In Step 053, you'll see how tool-calling transforms this into a sophisticated agentic system!
