using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BudgetTracker.Api.Auth;

namespace BudgetTracker.Api.Features.Transactions;

public class Transaction
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [Column(TypeName = "timestamptz")]
    public DateTime Date { get; set; }

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? Balance { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    [MaxLength(200)]
    public string? Labels { get; set; }

    [Required]
    [Column(TypeName = "timestamptz")]
    public DateTime ImportedAt { get; set; }

    [Required]
    [MaxLength(100)]
    public string Account { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? ImportSessionHash { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
}

public class TransactionDto
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? Balance { get; set; }
    public string? Category { get; set; }
    public string? Labels { get; set; }
    public DateTime ImportedAt { get; set; }
    public string Account { get; set; } = string.Empty;
}

internal static class TransactionExtensions
{
    public static TransactionDto MapToDto(this Transaction transaction)
    {
        return new TransactionDto
        {
            Id = transaction.Id,
            Date = transaction.Date,
            Description = transaction.Description,
            Amount = transaction.Amount,
            Balance = transaction.Balance,
            Category = transaction.Category,
            Labels = transaction.Labels,
            ImportedAt = transaction.ImportedAt,
            Account = transaction.Account
        };
    }
}
