# Workshop Step 044: Analytics Insights (Bonus)

## Mission

In this step, you'll implement a comprehensive analytics insights system for your budget tracker application. This feature will analyze user spending patterns and provide intelligent budget breakdowns with health assessments using AI-powered analysis. Users will get actionable insights about their financial health through a dedicated insights endpoint and an enhanced dashboard interface.

**Your goal**: Build an analytics insights API endpoint that analyzes transaction data and provides budget breakdown recommendations, then integrate this with the dashboard UI to display meaningful financial insights.

**Learning Objectives**:
- Creating AI-powered analytics services for financial data analysis
- Implementing budget health assessment algorithms
- Building REST API endpoints for analytics data
- Integrating analytics insights into the React dashboard
- Designing responsive UI components for data visualization
- Handling asynchronous data loading and error states

---

## Prerequisites

Before starting, ensure you completed:
- [043-nlq-ui.md](043-nlq-ui.md) - Natural Language Query UI (Week 5)
- Basic dashboard structure is in place
- AI integration is configured and working

---

## Step 44.1: Create Analytics Feature Structure

*Set up the backend analytics feature with proper organization.*

The analytics feature will be organized following the established pattern with dedicated folders for different concerns. We'll create the foundational structure that can be extended with additional analytics capabilities in the future.

Create the analytics feature directory structure in `src/BudgetTracker.Api/Features/Analytics/`:

```bash
mkdir -p src/BudgetTracker.Api/Features/Analytics/Insights
```

## Step 44.2: Define Analytics Types and Interfaces

*Create the core data types and service interfaces for analytics functionality.*

Define the analytics data structures that will represent budget insights, health assessments, and breakdown information. These types will be used throughout the analytics system.

Create `src/BudgetTracker.Api/Features/Analytics/Insights/InsightsTypes.cs`:

```csharp
namespace BudgetTracker.Api.Features.Analytics.Insights;

public interface IInsightsService
{
    Task<BudgetInsights> GenerateInsightsAsync(string userId);
}

public class BudgetInsights
{
    public BudgetBreakdown BudgetBreakdown { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
    public BudgetHealth Health { get; set; } = new();
}

public class BudgetBreakdown
{
    public decimal NeedsPercentage { get; set; }
    public decimal WantsPercentage { get; set; }
    public decimal SavingsPercentage { get; set; }
    public decimal NeedsAmount { get; set; }
    public decimal WantsAmount { get; set; }
    public decimal SavingsAmount { get; set; }
    public decimal TotalExpenses { get; set; }
}

public class BudgetHealth
{
    public string Status { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public List<string> Areas { get; set; } = new();
}

internal static class BudgetBreakdownExtensions
{
    public static BudgetHealth CalculateHealth(this BudgetBreakdown breakdown)
    {
        var isHealthy = breakdown.NeedsPercentage <= 50 &&
                       breakdown.WantsPercentage <= 30 &&
                       breakdown.SavingsPercentage >= 20;

        var areas = new List<string>();
        if (breakdown.NeedsPercentage > 50) areas.Add("Needs spending is high");
        if (breakdown.WantsPercentage > 30) areas.Add("Discretionary spending is high");
        if (breakdown.SavingsPercentage < 20) areas.Add("Savings rate is low");

        return new BudgetHealth
        {
            Status = isHealthy ? "Healthy" : "Needs Attention",
            IsHealthy = isHealthy,
            Areas = areas
        };
    }
}
```

## Step 44.3: Implement AI-Powered Insights Service

*Create the service that analyzes transaction data and generates intelligent insights.*

The insights service will analyze transaction patterns, categorize expenses into needs/wants/savings, and use AI to provide contextual financial advice. This service integrates with the existing AI infrastructure.

Create `src/BudgetTracker.Api/Features/Analytics/Insights/AzureAiInsightsService.cs`:

```csharp
using Microsoft.Extensions.AI;
using BudgetTracker.Api.Features.Transactions;
using BudgetTracker.Api.Features.Transactions.List;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Analytics.Insights;

public class AzureAiInsightsService : IInsightsService
{
    private readonly BudgetTrackerContext _context;
    private readonly IChatClient _chatClient;
    private readonly ILogger<AzureAiInsightsService> _logger;

    public AzureAiInsightsService(
        BudgetTrackerContext context,
        IChatClient chatClient,
        ILogger<AzureAiInsightsService> logger)
    {
        _context = context;
        _chatClient = chatClient;
        _logger = logger;
    }

    public async Task<BudgetInsights> GenerateInsightsAsync(string userId)
    {
        try
        {
            var transactions = await _context.Transactions
                .Where(t => t.UserId == userId)
                .ToListAsync();

            if (transactions.Count == 0)
            {
                return new BudgetInsights
                {
                    Summary = "No transactions available for analysis.",
                    Health = new BudgetHealth { Status = "No Data", IsHealthy = false, Areas = [] }
                };
            }

            var budgetBreakdown = CalculateBudgetBreakdown(transactions);
            var summary = await GenerateAiSummaryAsync(budgetBreakdown, transactions);

            return new BudgetInsights
            {
                BudgetBreakdown = budgetBreakdown,
                Summary = summary,
                Health = budgetBreakdown.CalculateHealth()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate insights for user {UserId}", userId);
            return new BudgetInsights
            {
                Summary = "Unable to generate insights at this time.",
                Health = new BudgetHealth { Status = "Error", IsHealthy = false, Areas = [] }
            };
        }
    }

    private BudgetBreakdown CalculateBudgetBreakdown(List<Transaction> transactions)
    {
        var expenses = transactions.Where(t => t.Amount < 0).ToList();
        var totalExpenses = Math.Abs(expenses.Sum(t => t.Amount));

        if (totalExpenses == 0)
        {
            return new BudgetBreakdown();
        }

        var needsCategories = new[] { "Housing", "Transportation", "Groceries", "Healthcare", "Utilities", "Insurance" };
        var savingsCategories = new[] { "Savings", "Investment", "Retirement" };

        var needsAmount = Math.Abs(expenses
            .Where(t => needsCategories.Contains(t.Category, StringComparer.OrdinalIgnoreCase))
            .Sum(t => t.Amount));

        var savingsAmount = Math.Abs(expenses
            .Where(t => savingsCategories.Contains(t.Category, StringComparer.OrdinalIgnoreCase))
            .Sum(t => t.Amount));

        var wantsAmount = totalExpenses - needsAmount - savingsAmount;

        return new BudgetBreakdown
        {
            TotalExpenses = totalExpenses,
            NeedsAmount = needsAmount,
            WantsAmount = wantsAmount,
            SavingsAmount = savingsAmount,
            NeedsPercentage = totalExpenses > 0 ? Math.Round((needsAmount / totalExpenses) * 100, 1) : 0,
            WantsPercentage = totalExpenses > 0 ? Math.Round((wantsAmount / totalExpenses) * 100, 1) : 0,
            SavingsPercentage = totalExpenses > 0 ? Math.Round((savingsAmount / totalExpenses) * 100, 1) : 0
        };
    }

    private async Task<string> GenerateAiSummaryAsync(
        BudgetBreakdown breakdown, List<Transaction> transactions)
    {
        var systemPrompt = CreateSystemPrompt();
        var userPrompt = CreateUserPrompt(breakdown, transactions);

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userPrompt)
        };

        var response = await _chatClient.GetResponseAsync(messages);
        return ParseAiResponse(response.Message.Text ?? string.Empty);
    }

    private static string CreateSystemPrompt()
    {
        return """
               You are a financial analyst providing budget insights using the 50/30/20 budget method.

               The 50/30/20 rule suggests:
               - 50% of after-tax income for needs (housing, transportation, groceries, healthcare, utilities)
               - 20% for savings and debt repayment
               - 30% for wants (entertainment, dining out, hobbies, shopping)

               Provide a concise 2-3 sentence summary analyzing their spending breakdown compared to the 50/20/30 rule.
               Focus on factual analysis, not recommendations.

               Return only the summary text, no JSON formatting.
               """;
    }

    private static string CreateUserPrompt(BudgetBreakdown breakdown, List<Transaction> transactions)
    {
        var totalTransactions = transactions.Count;
        var timeSpan = transactions.Any() ?
            (transactions.Max(t => t.Date) - transactions.Min(t => t.Date)).Days : 0;

        return $"""
               Analyze this spending breakdown:
               - Needs: {breakdown.NeedsPercentage}% (${breakdown.NeedsAmount:F2})
               - Wants: {breakdown.WantsPercentage}% (${breakdown.WantsAmount:F2})
               - Savings: {breakdown.SavingsPercentage}% (${breakdown.SavingsAmount:F2})
               - Total Expenses: ${breakdown.TotalExpenses:F2}
               - Transaction Count: {totalTransactions}
               - Time Period: {timeSpan} days
               """;
    }

    private string ParseAiResponse(string content)
    {
        return !string.IsNullOrWhiteSpace(content)
            ? content.Trim()
            : "Your spending has been analyzed according to the 50/20/30 budget rule.";
    }
}
```

## Step 44.4: Create Insights API Endpoint

*Build the REST API endpoint that exposes insights functionality.*

The API endpoint will handle requests for budget insights and return properly formatted analytics data. This endpoint will be integrated into the existing analytics API structure.

Create `src/BudgetTracker.Api/Features/Analytics/Insights/InsightsApi.cs`:

```csharp
using System.Security.Claims;
using BudgetTracker.Api.Auth;

namespace BudgetTracker.Api.Features.Analytics.Insights;

public static class InsightsApi
{
    public static IEndpointRouteBuilder MapInsightsEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/insights",
                async (IInsightsService insightsService, ClaimsPrincipal claimsPrincipal) =>
                {
                    var userId = claimsPrincipal.GetUserId();
                    var insights = await insightsService.GenerateInsightsAsync(userId);
                    return Results.Ok(insights);
                })
            .RequireAuthorization()
            .WithName("GetInsights")
            .WithSummary("Get budget analytics")
            .WithDescription("Analyzes spending patterns and provides budget breakdown with health assessment")
            .Produces<BudgetInsights>();

        return routes;
    }
}
```

## Step 44.5: Set Up Analytics Endpoints Registration

*Create the main analytics endpoints registration class.*

This class will coordinate all analytics-related endpoints and ensure proper registration with the application's routing system.

Create `src/BudgetTracker.Api/Features/Analytics/AnalyticsEndpoints.cs`:

```csharp
using BudgetTracker.Api.Features.Analytics.Insights;

namespace BudgetTracker.Api.Features.Analytics;

public static class AnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapAnalyticsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapInsightsEndpoints();
        return endpoints;
    }
}
```

## Step 44.6: Register Analytics Services

*Configure dependency injection for analytics services.*

Add the analytics services to the application's dependency injection container. This ensures proper service registration and lifetime management.

Update `src/BudgetTracker.Api/Program.cs` to include analytics services registration:

```csharp
// Add this after the existing service registrations
builder.Services.AddScoped<IInsightsService, AzureAiInsightsService>();

// And add analytics endpoints mapping after existing endpoint mappings
app.MapAnalyticsEndpoints();
```

## Step 44.7: Create Frontend Analytics Types

*Define TypeScript interfaces for analytics data.*

Create type definitions that match the backend analytics data structures. This ensures type safety when working with analytics data in the React application.

Create `src/BudgetTracker.Web/src/features/analytics/types.ts`:

```typescript
export interface BudgetInsights {
  budgetBreakdown: BudgetBreakdown;
  summary: string;
  health: BudgetHealth;
}

export interface BudgetBreakdown {
  needsPercentage: number;
  wantsPercentage: number;
  savingsPercentage: number;
  needsAmount: number;
  wantsAmount: number;
  savingsAmount: number;
  totalExpenses: number;
}

export interface BudgetHealth {
  status: string;
  isHealthy: boolean;
  areas: string[];
}
```

## Step 44.8: Create Analytics API Client

*Build the frontend API client for analytics endpoints.*

Create the API client functions that will communicate with the analytics backend endpoints. This provides a clean interface for React components to fetch analytics data.

Create `src/BudgetTracker.Web/src/features/analytics/api.ts`:

```typescript
import apiClient from '../../api/client';
import type { BudgetInsights } from './types';

export const analyticsApi = {
  async getInsights(): Promise<BudgetInsights> {
    const response = await apiClient.get<BudgetInsights>('/insights');
    return response.data;
  }
};
```

## Step 44.9: Build Insights Card Component

*Create a React component to display budget insights.*

Build a comprehensive UI component that displays budget breakdown information, health status, and AI-generated insights. Instead of loading insights automatically on page load, the card starts with a "Generate Insights" button so the user explicitly triggers the AI call. Once generated, a refresh button in the header lets them regenerate on demand.

Create `src/BudgetTracker.Web/src/features/analytics/components/InsightsCard.tsx`:

```tsx
import type { BudgetInsights } from '../types';

interface InsightsCardProps {
  insights: BudgetInsights | null;
  isLoading: boolean;
  hasError: boolean;
  onGenerate: () => void;
}

const InfoIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <circle cx="12" cy="12" r="10"></circle>
    <path d="m9 12 2 2 4-4"></path>
  </svg>
);

const AlertIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z"></path>
    <path d="M12 9v4"></path>
    <path d="m12 17 .01 0"></path>
  </svg>
);

const RefreshIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M21.5 2v6h-6M2.5 22v-6h6M2 11.5a10 10 0 0 1 18.8-4.3M22 12.5a10 10 0 0 1-18.8 4.2"></path>
  </svg>
);

export function InsightsCard({ insights, isLoading, hasError, onGenerate }: InsightsCardProps) {
  const formatAmount = (amount: number) => {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD',
      minimumFractionDigits: 0,
      maximumFractionDigits: 0,
    }).format(amount);
  };

  if (!insights && !isLoading && !hasError) {
    return (
      <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6 flex flex-col items-center justify-center text-center">
        <h3 className="text-lg font-semibold text-gray-900 mb-2">Budget Insights</h3>
        <p className="text-sm text-gray-500 mb-4">
          Generate an AI-powered analysis of your spending patterns.
        </p>
        <button
          onClick={onGenerate}
          className="inline-flex items-center gap-2 px-4 py-2 bg-blue-600 text-white text-sm font-medium rounded-lg hover:bg-blue-700 transition-colors"
        >
          <RefreshIcon />
          Generate Insights
        </button>
      </div>
    );
  }

  if (isLoading) {
    return (
      <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
        <div className="animate-pulse space-y-4">
          <div className="flex items-center justify-between">
            <div className="bg-neutral-200 rounded h-6 w-36" />
            <div className="bg-neutral-200 rounded-full h-6 w-20" />
          </div>
          <div className="space-y-3">
            <div className="bg-neutral-200 rounded h-4 w-full" />
            <div className="bg-neutral-200 rounded-full h-2 w-full" />
            <div className="bg-neutral-200 rounded h-4 w-full" />
            <div className="bg-neutral-200 rounded-full h-2 w-full" />
            <div className="bg-neutral-200 rounded h-4 w-full" />
            <div className="bg-neutral-200 rounded-full h-2 w-full" />
          </div>
          <div className="border-t border-gray-200 pt-4">
            <div className="bg-neutral-200 rounded h-4 w-full" />
            <div className="bg-neutral-200 rounded h-4 w-3/4 mt-2" />
          </div>
        </div>
      </div>
    );
  }

  if (hasError) {
    return (
      <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6 flex flex-col items-center justify-center text-center">
        <h3 className="text-lg font-semibold text-gray-900 mb-2">Budget Insights</h3>
        <p className="text-sm text-red-600 mb-4">
          Failed to generate insights. Please try again.
        </p>
        <button
          onClick={onGenerate}
          className="inline-flex items-center gap-2 px-4 py-2 bg-blue-600 text-white text-sm font-medium rounded-lg hover:bg-blue-700 transition-colors"
        >
          <RefreshIcon />
          Retry
        </button>
      </div>
    );
  }

  const { budgetBreakdown, summary, health } = insights!;

  return (
    <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-lg font-semibold text-gray-900">Budget Insights</h3>
        <div className="flex items-center gap-2">
          <button
            onClick={onGenerate}
            className="inline-flex items-center gap-1 px-2 py-1 text-xs font-medium text-gray-600 hover:text-gray-900 hover:bg-gray-100 rounded-lg transition-colors"
            title="Regenerate insights"
          >
            <RefreshIcon />
          </button>
          <div className={`inline-flex items-center px-2 py-1 rounded-full text-xs font-medium ${
            health.isHealthy
              ? 'bg-green-100 text-green-700'
              : 'bg-yellow-100 text-yellow-700'
          }`}>
            {health.isHealthy ? <InfoIcon /> : <AlertIcon />}
            <span className="ml-1">{health.status}</span>
          </div>
        </div>
      </div>

      {budgetBreakdown.totalExpenses > 0 && (
        <div className="space-y-4 mb-6">
          <div className="space-y-3">
            <div className="flex justify-between items-center">
              <span className="text-sm font-medium text-gray-700">Needs</span>
              <span className="text-sm text-gray-900">
                {budgetBreakdown.needsPercentage}% • {formatAmount(budgetBreakdown.needsAmount)}
              </span>
            </div>
            <div className="w-full bg-gray-200 rounded-full h-2">
              <div
                className="bg-blue-500 h-2 rounded-full"
                style={{ width: `${budgetBreakdown.needsPercentage}%` }}
              ></div>
            </div>
          </div>

          <div className="space-y-3">
            <div className="flex justify-between items-center">
              <span className="text-sm font-medium text-gray-700">Wants</span>
              <span className="text-sm text-gray-900">
                {budgetBreakdown.wantsPercentage}% • {formatAmount(budgetBreakdown.wantsAmount)}
              </span>
            </div>
            <div className="w-full bg-gray-200 rounded-full h-2">
              <div
                className="bg-purple-500 h-2 rounded-full"
                style={{ width: `${budgetBreakdown.wantsPercentage}%` }}
              ></div>
            </div>
          </div>

          <div className="space-y-3">
            <div className="flex justify-between items-center">
              <span className="text-sm font-medium text-gray-700">Savings</span>
              <span className="text-sm text-gray-900">
                {budgetBreakdown.savingsPercentage}% • {formatAmount(budgetBreakdown.savingsAmount)}
              </span>
            </div>
            <div className="w-full bg-gray-200 rounded-full h-2">
              <div
                className="bg-green-500 h-2 rounded-full"
                style={{ width: `${budgetBreakdown.savingsPercentage}%` }}
              ></div>
            </div>
          </div>
        </div>
      )}

      <div className="border-t border-gray-200 pt-4">
        <p className="text-sm text-gray-600 leading-relaxed">{summary}</p>

        {health.areas.length > 0 && (
          <div className="mt-3">
            <p className="text-xs font-medium text-gray-700 mb-2">Areas for improvement:</p>
            <ul className="space-y-1">
              {health.areas.map((area, index) => (
                <li key={index} className="text-xs text-gray-600 flex items-start">
                  <span className="text-yellow-500 mr-1">•</span>
                  {area}
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>
    </div>
  );
}
```

## Step 44.10: Create Summary Card Component

*Build a reusable summary card component for displaying key metrics.*

Create a flexible summary card component that can display various financial metrics with proper formatting, icons, and trend indicators.

Create `src/BudgetTracker.Web/src/features/analytics/components/SummaryCard.tsx`:

```tsx
import { ReactNode } from 'react';

interface SummaryCardProps {
  title: string;
  value: number;
  icon: ReactNode;
  valueColor?: string;
  trend?: string;
  isCurrency?: boolean;
}

export function SummaryCard({
  title,
  value,
  icon,
  valueColor = 'text-gray-900',
  trend,
  isCurrency = false
}: SummaryCardProps) {
  const formatValue = (val: number) => {
    if (isCurrency) {
      return new Intl.NumberFormat('en-US', {
        style: 'currency',
        currency: 'USD',
        minimumFractionDigits: 0,
        maximumFractionDigits: 0,
      }).format(Math.abs(val));
    }
    return val.toLocaleString();
  };

  return (
    <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center">
          <div className="flex-shrink-0">
            <div className="flex items-center justify-center w-8 h-8 bg-blue-100 rounded-lg">
              <div className="text-blue-600">
                {icon}
              </div>
            </div>
          </div>
          <div className="ml-4">
            <p className="text-sm font-medium text-gray-600">{title}</p>
            <p className={`text-2xl font-bold ${valueColor}`}>
              {isCurrency && value < 0 ? '-' : ''}{formatValue(value)}
            </p>
          </div>
        </div>
      </div>
      {trend && (
        <div className="mt-2">
          <p className="text-xs text-gray-500">{trend}</p>
        </div>
      )}
    </div>
  );
}
```

## Step 44.11: Create Analytics Feature Index

*Set up the main exports for the analytics feature.*

Create the index file that exports all analytics components and utilities, providing a clean interface for other parts of the application to import analytics functionality.

Create `src/BudgetTracker.Web/src/features/analytics/index.ts`:

```typescript
export { analyticsApi } from './api';
export { InsightsCard } from './components/InsightsCard';
export { SummaryCard } from './components/SummaryCard';
export type { BudgetInsights, BudgetBreakdown, BudgetHealth } from './types';
```

## Step 44.12: Update Dashboard with Analytics

*Integrate analytics insights into the main dashboard.*

Enhance the dashboard to load and display analytics insights alongside the query assistant from the previous lesson.

Update `src/BudgetTracker.Web/src/routes/dashboard.tsx`:

```tsx
import { useState } from 'react';
import { InsightsCard, analyticsApi } from '../features/analytics';
import Header from '../shared/components/layout/Header';
import { QueryAssistant } from '../features/intelligence';
import type { BudgetInsights } from '../features/analytics';

export default function Dashboard() {
  const [insights, setInsights] = useState<BudgetInsights | null>(null);
  const [isLoadingInsights, setIsLoadingInsights] = useState(false);
  const [insightsError, setInsightsError] = useState(false);

  const generateInsights = async () => {
    setIsLoadingInsights(true);
    setInsightsError(false);
    try {
      const data = await analyticsApi.getInsights();
      setInsights(data);
    } catch {
      setInsightsError(true);
    } finally {
      setIsLoadingInsights(false);
    }
  };

  return (
    <div className="px-4 py-6 sm:px-0">
      <Header
        title="Dashboard"
        subtitle="Analytics insights and query assistant"
      />

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <InsightsCard
          insights={insights}
          isLoading={isLoadingInsights}
          hasError={insightsError}
          onGenerate={generateInsights}
        />
        <QueryAssistant />
      </div>
    </div>
  );
}
```

## Step 44.13: Remove Dashboard Loader from Route Config

*Update the route configuration to match the new on-demand insights flow.*

Since the dashboard no longer exports a `loader` (insights are now fetched on button click instead of on page load), remove the loader import and references from the router configuration.

Update `src/BudgetTracker.Web/src/main.tsx`:

```diff
- import Dashboard, { loader as dashboardLoader } from './routes/dashboard'
+ import Dashboard from './routes/dashboard'
```

And remove the `loader` property from both dashboard routes:

```diff
      {
        index: true,
        element: <Dashboard />,
-       loader: dashboardLoader,
      },
      {
        path: 'dashboard',
        element: <Dashboard />,
-       loader: dashboardLoader,
      },
```

## Step 44.14: Test Analytics Insights

*Test the complete analytics insights functionality.*

### 44.14.1: Test Backend API

Test the analytics insights endpoint to ensure it's properly analyzing transaction data and returning meaningful insights:

```http
### Test analytics insights endpoint
GET http://localhost:5295/api/insights
X-API-Key: test-key-user1
```

**Expected Response Structure:**
```json
{
  "budgetBreakdown": {
    "needsPercentage": 45.2,
    "wantsPercentage": 34.8,
    "savingsPercentage": 20.0,
    "needsAmount": 1250.00,
    "wantsAmount": 962.50,
    "savingsAmount": 552.75,
    "totalExpenses": 2765.25
  },
  "summary": "Your budget shows a healthy balance with good savings discipline. Consider optimizing your discretionary spending to boost your savings rate even further.",
  "health": {
    "status": "Healthy",
    "isHealthy": true,
    "areas": []
  }
}
```

### 44.14.2: Test Dashboard Integration

Verify that the dashboard properly loads and displays analytics insights:

1. Navigate to the dashboard at `http://localhost:5173/dashboard`
2. Verify that the insights card shows a "Generate Insights" button on first load
3. Click the button and confirm the skeleton loader appears while data is fetched
4. After loading, verify the insights card displays budget breakdown with visual progress bars
5. Check that AI-generated summary appears in the insights card
6. Confirm that health status shows appropriate indicators
7. Click the refresh icon in the card header and verify insights are regenerated
8. Verify that the query assistant is displayed alongside the insights card

**Expected UI Elements:**
- **Generate button** on first visit (no automatic API call on page load)
- **Skeleton loader** while insights are being generated
- **Insights card** with budget breakdown percentages and amounts
- **Refresh button** in the card header to regenerate insights
- **Visual progress bars** for Needs, Wants, and Savings categories
- **Health status indicator** (green checkmark for healthy, yellow warning for needs attention)
- **AI-generated summary** providing contextual financial advice
- **Areas for improvement** list when health status needs attention
- **Query assistant** for natural language queries (from previous lesson)

### 44.14.3: Test Error Handling

Verify that the system gracefully handles errors:

1. Stop the backend API temporarily
2. Click "Generate Insights" on the dashboard
3. Confirm that the card shows an error message with a "Retry" button
4. Start the backend API again
5. Click "Retry" and verify insights load successfully

---

## Summary

You've successfully implemented a comprehensive analytics insights system for your budget tracker:

- **AI-Powered Analysis**: Built an intelligent insights service that analyzes spending patterns using Azure OpenAI
- **Budget Breakdown**: Implemented the 50/30/20 budgeting rule with automatic categorization
- **Health Assessment**: Created budget health scoring with actionable improvement recommendations
- **REST API**: Built a clean insights endpoint that integrates with existing authentication
- **Dashboard Integration**: Enhanced the dashboard with rich analytics visualizations
- **Error Resilience**: Implemented graceful error handling that doesn't break the user experience

**Key Features Implemented**:
- **Smart Categorization**: Automatically categorizes expenses into Needs, Wants, and Savings
- **Visual Breakdown**: Interactive progress bars showing budget allocation percentages
- **AI Insights**: Contextual financial advice generated by analyzing spending patterns
- **Health Monitoring**: Real-time budget health assessment with specific improvement areas
- **Responsive Design**: Mobile-friendly analytics cards that work across all devices

**Technical Achievements**:
- **Service-Oriented Architecture**: Clean separation between analytics logic and API endpoints
- **Type Safety**: Full TypeScript integration ensuring type safety across frontend and backend
- **Performance Optimization**: Efficient database queries with proper user isolation
- **Extensible Design**: Architecture that can easily accommodate additional analytics features
- **Error Boundaries**: Robust error handling that maintains application stability

**What Users Get**:
- **Financial Awareness**: Clear understanding of spending patterns and budget health
- **Actionable Insights**: AI-generated advice for improving financial habits
- **Visual Progress**: Easy-to-understand charts showing budget breakdown
- **Real-Time Analysis**: Up-to-date insights based on latest transaction data
- **Natural Language Queries**: Ask questions about transactions using the query assistant

The analytics insights system now provides users with powerful financial intelligence that helps them understand their spending habits and make informed budgeting decisions!

---

## Extra Mile

The dashboard works, but it still looks like a prototype. Below are five self-directed challenges to make it feel closer to a real product. No full solutions are provided — use the hints to guide your implementation.

### 1. Summary Metric Cards

Add a row of four `SummaryCard` components at the top of the dashboard showing **Total Income**, **Total Expenses**, **Net Balance**, and **Transaction Count**.

**Hints:**
- The `SummaryCard` component already exists in `src/BudgetTracker.Web/src/features/analytics/components/SummaryCard.tsx` but is not used anywhere yet.
- Expand `BudgetInsights` (backend and frontend types) with `TotalIncome` and `TransactionCount` fields.
- Compute `TotalIncome` from transactions where `Amount > 0` inside `AzureAiInsightsService`.
- Net Balance = Total Income - Total Expenses.
- Render the four cards in a responsive grid above the existing insights and query assistant cards.

### 2. Spending by Category

Add a card that breaks down expenses per individual category (e.g., "Groceries $320", "Entertainment $180") rather than only the Needs/Wants/Savings grouping.

**Hints:**
- Group transactions by `Category` in `AzureAiInsightsService` and return a `List<CategoryBreakdown>` with `Name`, `Amount`, and `Percentage` fields.
- Add the new list to `BudgetInsights` and mirror the type on the frontend.
- Render as a sorted list with progress bars (reuse the bar styling from `InsightsCard`).
- Consider capping the list at the top 8 categories and grouping the rest under "Other".

### 3. Recent Transactions

Show the last 5 transactions on the dashboard with a "View all" link that navigates to the transactions page.

**Hints:**
- Call the existing `GET /api/transactions?page=1&pageSize=5` endpoint from the dashboard loader — no new backend endpoint needed.
- Display each transaction's date, description, category, and amount in a compact list or table.
- Use React Router's `<Link>` component for the "View all" navigation.

### 4. Date Range Context

Display the time period being analyzed (e.g., "Jan 1 – Jan 29, 2026") in the dashboard header so users know what data the insights cover.

**Hints:**
- Return `StartDate` and `EndDate` from the insights API (derive from `Min`/`Max` of transaction dates).
- Add corresponding fields to `BudgetInsights` on both backend and frontend.
- Format the dates with `date-fns` (already available in the frontend) and display them in the dashboard subtitle.

### 5. Background Summary Generation

The "Generate Insights" button works well for on-demand use, but in a production app you'd want insights ready before the user even opens the dashboard. Replace the on-demand flow with a background process.

**Hints:**
- Create a `BackgroundService` (or `IHostedService`) that runs on a timer (e.g., every 30 minutes) and calls `GenerateInsightsAsync` for each active user.
- Store the generated `BudgetInsights` in a cache — an `IMemoryCache` entry keyed by user ID is the simplest option; a dedicated database table works too.
- Change the `/insights` endpoint to read from the cache first. If there is no cached result yet, either fall back to on-demand generation or return an "insights are being prepared" response.
- Invalidate the cache for a user when new transactions are imported (hook into the CSV import flow or use a `POST /api/insights/refresh` endpoint).
- Keep the refresh button in the header so users can force a regeneration, but load cached data automatically on page load instead of starting with an empty card.
