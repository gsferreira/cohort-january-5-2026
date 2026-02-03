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
