using System.Security.Claims;
using BudgetTracker.Api.Auth;
using Microsoft.AspNetCore.Mvc;

namespace BudgetTracker.Api.Features.Intelligence.Query;

public static class QueryApi
{
    public static IEndpointRouteBuilder MapQueryEndpoints(this IEndpointRouteBuilder routes)
    {
        var queryGroup = routes.MapGroup("/query")
            .WithTags("Query Assistant")
            .RequireAuthorization();

        queryGroup.MapPost("/ask", async (
            [FromBody] QueryRequest request,
            IQueryAssistantService queryService,
            ClaimsPrincipal claimsPrincipal) =>
        {
            var userId = claimsPrincipal.GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Results.Unauthorized();
            }

            var response = await queryService.ProcessQueryAsync(request.Query, userId);
            return Results.Ok(response);
        })
        .WithName("AskQuery")
        .WithSummary("Ask a natural language question about your finances")
        .WithDescription("Process natural language queries like 'What was my biggest expense last week?' or 'How much did I spend on groceries this month?'")
        .Produces<QueryResponse>()
        .ProducesProblem(400)
        .ProducesProblem(401);

        return routes;
    }
}
