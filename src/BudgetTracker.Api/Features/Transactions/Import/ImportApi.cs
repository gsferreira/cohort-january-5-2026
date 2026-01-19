using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BudgetTracker.Api.Auth;
using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Features.Transactions.Import.Processing;
using BudgetTracker.Api.Features.Transactions.Import.Enhancement;
using BudgetTracker.Api.Features.Transactions.Import.Detection;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Api.Features.Transactions.Import;

public static class ImportApi
{
    public static IEndpointRouteBuilder MapTransactionImportEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/import", ImportAsync)
            .DisableAntiforgery();

        routes.MapPost("/import/enhance", EnhanceImportAsync);

        return routes;
    }

    private static async Task<Results<Ok<ImportResult>, BadRequest<string>>> ImportAsync(
        IFormFile file,
        [FromForm] string account,
        CsvImporter csvImporter,
        IImageImporter imageImporter,
        ITransactionEnhancer enhancer,
        ICsvStructureDetector structureDetector,
        BudgetTrackerContext context,
        ClaimsPrincipal claimsPrincipal)
    {
        const double MinConfidenceThreshold = 0.85;

        var validationResult = ValidateFileInput(file, account);
        if (validationResult != null)
        {
            return validationResult;
        }

        try
        {
            var userId = claimsPrincipal.GetUserId();
            var sessionHash = GenerateSessionHash(file.FileName, DateTime.UtcNow);

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            stream.Position = 0;

            ImportResult result;
            List<Transaction> transactions;

            if (IsImageFile(file.FileName))
            {
                (result, transactions) = await imageImporter.ProcessImageAsync(
                    stream, file.FileName, userId, account);
            }
            else
            {
                var detectionResult = await structureDetector.DetectStructureAsync(stream);

                if (detectionResult.ConfidenceScore < MinConfidenceThreshold)
                {
                    return TypedResults.BadRequest(
                        $"Unable to detect CSV structure with sufficient confidence. " +
                        $"Detection method: {detectionResult.DetectionMethod}, " +
                        $"Confidence: {detectionResult.ConfidenceScore:P0}. " +
                        $"Please ensure the CSV has recognizable column headers.");
                }

                stream.Position = 0;
                (result, transactions) = await csvImporter.ParseCsvAsync(
                    stream, file.FileName, userId, account, detectionResult);
            }

            if (transactions.Any())
            {
                // Extract descriptions for AI enhancement
                var descriptions = transactions.Select(t => t.Description).ToList();

                // Enhance descriptions with AI (includes categories)
                var enhancements = await enhancer.EnhanceDescriptionsAsync(
                    descriptions, account, userId, sessionHash);

                // Create enhancement results for preview
                var enhancementResults = new List<TransactionEnhancementResult>();

                for (var i = 0; i < transactions.Count; i++)
                {
                    var transaction = transactions[i];
                    var enhancement = enhancements.FirstOrDefault(e =>
                        e.OriginalDescription == transaction.Description) ?? enhancements[i];

                    // Set session hash for tracking
                    transaction.ImportSessionHash = sessionHash;

                    enhancementResults.Add(new TransactionEnhancementResult
                    {
                        TransactionId = transaction.Id,
                        ImportSessionHash = sessionHash,
                        TransactionIndex = i,
                        OriginalDescription = enhancement.OriginalDescription,
                        EnhancedDescription = enhancement.EnhancedDescription,
                        SuggestedCategory = enhancement.SuggestedCategory,
                        ConfidenceScore = enhancement.ConfidenceScore
                    });
                }

                // Save transactions with original descriptions
                // (enhancements applied later via enhance endpoint)
                await context.Transactions.AddRangeAsync(transactions);
                await context.SaveChangesAsync();

                result.ImportSessionHash = sessionHash;
                result.Enhancements = enhancementResults;
            }

            return TypedResults.Ok(result);
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest($"Import failed: {ex.Message}");
        }
    }

    private static async Task<Results<Ok<EnhanceImportResult>, BadRequest<string>>> EnhanceImportAsync(
        [FromBody] EnhanceImportRequest request,
        BudgetTrackerContext context,
        ClaimsPrincipal claimsPrincipal)
    {
        try
        {
            var userId = claimsPrincipal.GetUserId();
            if (string.IsNullOrEmpty(userId))
                return TypedResults.BadRequest("User not authenticated");

            var enhancedCount = 0;

            if (request.ApplyEnhancements)
            {
                var transactions = await context.Transactions
                    .Where(t => t.UserId == userId && t.ImportSessionHash == request.ImportSessionHash)
                    .ToListAsync();

                foreach (var enhancement in request.Enhancements)
                {
                    if (enhancement.ConfidenceScore < request.MinConfidenceScore)
                        continue;

                    var transaction = transactions.FirstOrDefault(t => t.Id == enhancement.TransactionId);
                    if (transaction == null)
                        continue;

                    transaction.Description = enhancement.EnhancedDescription;

                    if (!string.IsNullOrEmpty(enhancement.SuggestedCategory))
                    {
                        transaction.Category = enhancement.SuggestedCategory;
                    }

                    enhancedCount++;
                }

                if (enhancedCount > 0)
                {
                    await context.SaveChangesAsync();
                }
            }

            return TypedResults.Ok(new EnhanceImportResult
            {
                ImportSessionHash = request.ImportSessionHash,
                TotalTransactions = request.Enhancements.Count,
                EnhancedCount = enhancedCount,
                SkippedCount = request.Enhancements.Count - enhancedCount
            });
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest($"Enhancement failed: {ex.Message}");
        }
    }

    private static string GenerateSessionHash(string fileName, DateTime timestamp)
    {
        var input = $"{fileName}_{timestamp:yyyyMMddHHmmss}_{Guid.NewGuid()}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash)[..12];
    }

    private static BadRequest<string>? ValidateFileInput(IFormFile file, string account)
    {
        if (file == null || file.Length == 0)
            return TypedResults.BadRequest("No file uploaded");

        var allowedExtensions = new[] { ".csv", ".png", ".jpg", ".jpeg" };
        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(fileExtension))
            return TypedResults.BadRequest("Only CSV files and images (PNG, JPG, JPEG) are supported");

        if (file.Length > 10 * 1024 * 1024)
            return TypedResults.BadRequest("File size exceeds 10MB limit");

        if (string.IsNullOrWhiteSpace(account))
            return TypedResults.BadRequest("Account name is required");

        return null;
    }

    private static bool IsImageFile(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension is ".png" or ".jpg" or ".jpeg";
    }
}
