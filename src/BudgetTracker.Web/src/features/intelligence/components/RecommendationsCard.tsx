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
