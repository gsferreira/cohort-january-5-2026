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
