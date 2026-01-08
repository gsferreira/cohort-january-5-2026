using System.Security.Claims;
using BudgetTracker.Api.Auth;
using BudgetTracker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Transactions.List;

public static class TransactionListApi
{
    public static IEndpointRouteBuilder MapTransactionListEndpoint(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/",
                async (BudgetTrackerContext db, ClaimsPrincipal claimsPrincipal, int page = 1, int pageSize = 20) =>
                {
                    if (page < 1) page = 1;
                    if (pageSize < 1 || pageSize > 100) pageSize = 20;

                    var query = db.Transactions.Where(t => t.UserId == claimsPrincipal.GetUserId());
                    var totalCount = await query.CountAsync();

                    var items = await query
                        .OrderByDescending(t => t.Date)
                        .ThenByDescending(t => t.ImportedAt)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToListAsync();

                    var result = new PagedResult<Transaction>
                    {
                        Items = items,
                        TotalCount = totalCount,
                        Page = page,
                        PageSize = pageSize
                    };

                    return Results.Ok(result);
                });

        return routes;
    }
}