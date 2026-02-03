using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using BudgetTracker.Api.Auth;

namespace BudgetTracker.Api.Features.Intelligence.Recommendations;

public class Recommendation
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    [Required]
    public RecommendationType Type { get; set; }

    [Required]
    public RecommendationPriority Priority { get; set; }

    [Required]
    [Column(TypeName = "timestamptz")]
    public DateTime GeneratedAt { get; set; }

    [Required]
    [Column(TypeName = "timestamptz")]
    public DateTime ExpiresAt { get; set; }

    [Required]
    public RecommendationStatus Status { get; set; } = RecommendationStatus.Active;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RecommendationType
{
    SpendingAlert,
    SavingsOpportunity,
    BehavioralInsight,
    BudgetWarning
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RecommendationPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum RecommendationStatus
{
    Active,
    Expired
}

public class RecommendationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public RecommendationType Type { get; set; }
    public RecommendationPriority Priority { get; set; }
    public DateTime GeneratedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

internal static class RecommendationExtensions
{
    public static RecommendationDto MapToDto(this Recommendation recommendation)
    {
        return new RecommendationDto
        {
            Id = recommendation.Id,
            Title = recommendation.Title,
            Message = recommendation.Message,
            Type = recommendation.Type,
            Priority = recommendation.Priority,
            GeneratedAt = recommendation.GeneratedAt,
            ExpiresAt = recommendation.ExpiresAt
        };
    }
}
