# Workshop Step 052: Recommendation Agent UI

## Mission

In this step, you'll build the **frontend** for the recommendation system created in Step 051. You'll create React components to display AI-powered financial recommendations with priority-based styling and integrate them into the dashboard.

**Your goal**: Build the RecommendationsCard component, verify the API client, and integrate recommendations into the dashboard.

**Learning Objectives**:
- Building React components for displaying recommendation data
- Implementing priority-based visual styling
- Integrating new features into existing dashboard loaders
- Handling loading states and error boundaries

---

## Prerequisites

Before starting, ensure you completed:
- [051-recommendation-agent.md](051-recommendation-agent.md) - Recommendation Agent Backend (this step)

---

## Step 52.1: Verify Frontend Types and API Client

*Verify TypeScript interfaces and API client for recommendations.*

The intelligence feature already has an existing `api.ts` file. Verify that it includes the recommendation types and API methods.

Verify `src/BudgetTracker.Web/src/features/intelligence/api.ts` contains:

```typescript
// This interface should already exist in api.ts
export interface ProactiveRecommendation {
  id: string;
  title: string;
  message: string;
  type: 'SpendingAlert' | 'SavingsOpportunity' | 'BehavioralInsight' | 'BudgetWarning';
  priority: 'Low' | 'Medium' | 'High' | 'Critical';
  generatedAt: string;
  expiresAt: string;
}

// This method should already exist in intelligenceApi
export const intelligenceApi = {
  // ... existing methods ...

  async getRecommendations(): Promise<ProactiveRecommendation[]> {
    const response = await api.get<ProactiveRecommendation[]>('/recommendations');
    return response.data;
  }
};
```

If these are missing, add them to the existing `api.ts` file. The `ProactiveRecommendation` interface and `getRecommendations` method are required for the recommendation system to work.

## Step 52.2: Build RecommendationsCard Component

*Create the React component to display recommendations.*

Build a comprehensive UI component that displays recommendations with proper styling and visual indicators. This component will receive recommendations as props from the dashboard, following the same pattern as the InsightsCard.

Create `src/BudgetTracker.Web/src/features/intelligence/components/RecommendationsCard.tsx`:

```tsx
import type { ProactiveRecommendation } from '../api';

interface RecommendationsCardProps {
  recommendations: ProactiveRecommendation[];
}

const AlertTriangleIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z"></path>
    <path d="M12 9v4"></path>
    <path d="M12 17h.01"></path>
  </svg>
);

const DollarSignIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <line x1="12" y1="2" x2="12" y2="22"></line>
    <path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"></path>
  </svg>
);

const LightbulbIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <path d="M15 14c.2-1 .7-1.7 1.5-2.5 1-.9 1.5-2.2 1.5-3.5A6 6 0 0 0 6 8c0 1 .2 2.2 1.5 3.5.7.7 1.3 1.5 1.5 2.5"></path>
    <path d="M9 18h6"></path>
    <path d="M10 22h4"></path>
  </svg>
);

const TrendingDownIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
    <polyline points="22 17 13.5 8.5 8.5 13.5 2 7"></polyline>
    <polyline points="16 17 22 17 22 11"></polyline>
  </svg>
);

export function RecommendationsCard({ recommendations }: RecommendationsCardProps) {
  const getIcon = (type: ProactiveRecommendation['type']) => {
    switch (type) {
      case 'SpendingAlert':
        return <AlertTriangleIcon />;
      case 'SavingsOpportunity':
        return <DollarSignIcon />;
      case 'BehavioralInsight':
        return <LightbulbIcon />;
      case 'BudgetWarning':
        return <TrendingDownIcon />;
      default:
        return <LightbulbIcon />;
    }
  };

  const getPriorityStyles = (priority: ProactiveRecommendation['priority']) => {
    switch (priority) {
      case 'Critical':
        return 'border-red-500 bg-red-50 text-red-900';
      case 'High':
        return 'border-orange-500 bg-orange-50 text-orange-900';
      case 'Medium':
        return 'border-yellow-500 bg-yellow-50 text-yellow-900';
      case 'Low':
        return 'border-blue-500 bg-blue-50 text-blue-900';
      default:
        return 'border-gray-500 bg-gray-50 text-gray-900';
    }
  };

  const formatType = (type: string) => {
    return type.replace(/([A-Z])/g, ' $1').trim();
  };

  if (recommendations.length === 0) {
    return (
      <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
        <h3 className="text-lg font-semibold text-gray-900 mb-4">Financial Recommendations</h3>
        <p className="text-sm text-gray-600">
          Import more transactions to receive personalized financial recommendations.
        </p>
      </div>
    );
  }

  return (
    <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
      <h3 className="text-lg font-semibold text-gray-900 mb-4">Financial Recommendations</h3>
      <div className="space-y-3">
        {recommendations.map((recommendation) => (
          <div
            key={recommendation.id}
            className={`border-l-4 rounded-lg p-4 ${getPriorityStyles(recommendation.priority)}`}
          >
            <div className="flex items-start gap-3">
              <div className="flex-shrink-0 mt-0.5">
                {getIcon(recommendation.type)}
              </div>
              <div className="flex-1 min-w-0">
                <h4 className="font-semibold text-sm mb-1">{recommendation.title}</h4>
                <p className="text-sm opacity-90 leading-relaxed">{recommendation.message}</p>
                <div className="mt-2 flex items-center gap-2 text-xs opacity-75">
                  <span>{recommendation.priority} priority</span>
                  <span>·</span>
                  <span>{formatType(recommendation.type)}</span>
                </div>
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
```

This component provides:
- Props-based design that receives recommendations from the dashboard loader
- Priority-based styling with distinct color schemes for each priority level
- Type-specific inline SVG icons (no external dependencies)
- Responsive design with proper accessibility
- Empty state handling when no recommendations are available

## Step 52.3: Update Intelligence Feature Index

*Update the exports for the intelligence feature.*

Update `src/BudgetTracker.Web/src/features/intelligence/index.ts` to include the RecommendationsCard export:

```typescript
export { intelligenceApi } from './api';
export { RecommendationsCard } from './components/RecommendationsCard';
export { default as QueryAssistant } from './components/QueryAssistant';
export type { ProactiveRecommendation } from './api';
```

Note: `ProactiveRecommendation` is exported from `'./api'` since the type is defined there alongside the API methods.

## Step 52.4: Integrate Recommendations into Dashboard

*Add recommendations to the dashboard with proper loading and error handling.*

Enhance the dashboard to load and display recommendation data alongside existing analytics. The dashboard will fetch recommendations on mount using `useEffect` and display them above the existing insights and query assistant sections.

Update `src/BudgetTracker.Web/src/routes/dashboard.tsx`:

```tsx
import { useState, useEffect } from 'react';
import { InsightsCard, analyticsApi } from '../features/analytics';
import Header from '../shared/components/layout/Header';
import { QueryAssistant, RecommendationsCard, intelligenceApi } from '../features/intelligence';
import type { BudgetInsights } from '../features/analytics';
import type { ProactiveRecommendation } from '../features/intelligence';

export default function Dashboard() {
  const [insights, setInsights] = useState<BudgetInsights | null>(null);
  const [isLoadingInsights, setIsLoadingInsights] = useState(false);
  const [insightsError, setInsightsError] = useState(false);
  const [recommendations, setRecommendations] = useState<ProactiveRecommendation[]>([]);

  useEffect(() => {
    intelligenceApi.getRecommendations().then(setRecommendations).catch(() => []);
  }, []);

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

      {recommendations.length > 0 && (
        <div className="mb-6">
          <RecommendationsCard recommendations={recommendations} />
        </div>
      )}

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

Key integration points:

1. **useEffect**: Fetches recommendations on component mount, with graceful error handling via `.catch()`
2. **Props Passing**: Passes `recommendations` array as props to `RecommendationsCard`
3. **Conditional Rendering**: Only displays recommendations section when recommendations exist
4. **Layout**: Places recommendations prominently above the insights/query sections
5. **Existing Patterns**: Preserves the existing `InsightsCard` on-demand generation pattern

## Step 52.5: Test the Recommendation UI

*Test the frontend integration.*

### 52.5.1: Test Dashboard Integration

1. Navigate to the dashboard at `http://localhost:5173/dashboard`
2. Verify that recommendations appear prominently on the page
3. Confirm that recommendations are displayed with appropriate priority styling
4. Verify that recommendations update automatically after importing new transactions

### 52.5.2: Test Different Priority Levels

Verify that each priority level displays correctly:
- **Critical**: Red border and background
- **High**: Orange border and background
- **Medium**: Yellow border and background
- **Low**: Blue border and background

### 52.5.3: Test Empty State

Verify the empty state message when no recommendations exist.

---

## Summary

You've successfully implemented the **frontend for the recommendation system**:

- **RecommendationsCard Component**: Priority-based visual styling with type-specific icons
- **Dashboard Integration**: Recommendations load alongside other dashboard data
- **Error Handling**: Graceful degradation if recommendation API fails
- **User-Friendly Interface**: Intuitive recommendation cards with clear visual hierarchy

The recommendation system frontend is now complete and integrated with the backend services from Step 051!
