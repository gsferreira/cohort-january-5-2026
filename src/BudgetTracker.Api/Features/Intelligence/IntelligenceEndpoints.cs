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
