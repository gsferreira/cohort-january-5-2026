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
