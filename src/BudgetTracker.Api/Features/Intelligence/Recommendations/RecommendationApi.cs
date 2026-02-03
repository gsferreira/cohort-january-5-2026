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
