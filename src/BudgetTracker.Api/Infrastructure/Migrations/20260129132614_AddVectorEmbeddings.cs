using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace BudgetTracker.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVectorEmbeddings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_Date",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_ImportedAt",
                table: "Transactions");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.AddColumn<Vector>(
                name: "Embedding",
                table: "Transactions",
                type: "vector(1536)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Category",
                table: "Transactions",
                column: "Category",
                filter: "\"Category\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Embedding",
                table: "Transactions",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_RagContext",
                table: "Transactions",
                columns: new[] { "UserId", "Account", "Date" },
                descending: new[] { false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_Category",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_Embedding",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_RagContext",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Embedding",
                table: "Transactions");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Date",
                table: "Transactions",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ImportedAt",
                table: "Transactions",
                column: "ImportedAt");
        }
    }
}
