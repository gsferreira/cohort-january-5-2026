using BudgetTracker.Api.Auth;
using BudgetTracker.Api.Features.Transactions;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace BudgetTracker.Api.Infrastructure;

public class BudgetTrackerContext : IdentityDbContext<ApplicationUser>
{
    public BudgetTrackerContext(DbContextOptions<BudgetTrackerContext> options) : base(options)
    {
    }

    public DbSet<Transaction> Transactions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()");

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_Transactions_UserId");

            entity.HasIndex(e => new { e.UserId, e.Account, e.Date })
                .HasDatabaseName("IX_Transactions_RagContext")
                .IsDescending(false, false, true);

            entity.HasIndex(e => e.Category)
                .HasDatabaseName("IX_Transactions_Category")
                .HasFilter("\"Category\" IS NOT NULL");

            entity.Property(e => e.Embedding)
                .HasColumnType("vector(1536)");

            entity.HasIndex(e => e.Embedding)
                .HasDatabaseName("IX_Transactions_Embedding")
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops");

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .HasPrincipalKey(u => u.Id);
        });
    }
}
