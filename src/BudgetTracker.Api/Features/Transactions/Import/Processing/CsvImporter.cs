using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using BudgetTracker.Api.Infrastructure;

namespace BudgetTracker.Api.Features.Transactions.Import.Processing;

public class CsvImporter
{
    public async Task<(ImportResult Result, List<Transaction> Transactions)> ParseCsvAsync(
        Stream csvStream, string sourceFileName, string userId, string account)
    {
        var result = new ImportResult
        {
            SourceFile = sourceFileName,
            ImportedAt = DateTime.UtcNow
        };

        var transactions = new List<Transaction>();

        try
        {
            using var reader = new StreamReader(csvStream, Encoding.UTF8);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                BadDataFound = null
            });

            var rowNumber = 0;

            await foreach (var record in csv.GetRecordsAsync<dynamic>())
            {
                rowNumber++;
                result.TotalRows++;

                try
                {
                    var transaction = ParseTransactionRow(record);
                    if (transaction != null)
                    {
                        transaction.UserId = userId;
                        transaction.Account = account;

                        transactions.Add(transaction);
                        result.ImportedCount++;
                    }
                    else
                    {
                        result.FailedCount++;
                        result.Errors.Add($"Row {rowNumber}: Failed to parse transaction");
                    }
                }
                catch (Exception ex)
                {
                    result.FailedCount++;
                    result.Errors.Add($"Row {rowNumber}: {ex.Message}");
                }
            }

            result.ImportedCount = transactions.Count;
            result.FailedCount = result.TotalRows - result.ImportedCount;

            return (result, transactions);
        }
        catch (Exception ex)
        {
            result.Errors.Add($"CSV parsing error: {ex.Message}");
            return (result, new List<Transaction>());
        }
    }

    private Transaction? ParseTransactionRow(dynamic record)
    {
        try
        {
            var recordDict = (IDictionary<string, object>)record;

            // Flexible column mapping - try common variations
            var description = GetColumnValue(recordDict, "Description", "Memo", "Details");
            var dateStr = GetColumnValue(recordDict, "Date", "Transaction Date", "Posting Date");
            var amountStr = GetColumnValue(recordDict, "Amount", "Transaction Amount", "Debit", "Credit");
            var balanceStr = GetColumnValue(recordDict, "Balance", "Running Balance", "Account Balance");
            var category = GetColumnValue(recordDict, "Category", "Type", "Transaction Type");

            // Validate required fields
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentException("Description is required");
            }

            if (string.IsNullOrWhiteSpace(dateStr))
            {
                throw new ArgumentException("Date is required");
            }

            if (string.IsNullOrWhiteSpace(amountStr))
            {
                throw new ArgumentException("Amount is required");
            }

            // Parse date with culture-aware parsing
            if (!TryParseDate(dateStr, out var date))
            {
                throw new ArgumentException($"Invalid date format: {dateStr}");
            }

            // Parse amount using culture-aware parsing
            if (!TryParseAmount(amountStr, out var amount))
            {
                throw new ArgumentException($"Invalid amount format: {amountStr}");
            }

            // Parse balance (optional)
            decimal? balance = null;
            if (!string.IsNullOrWhiteSpace(balanceStr))
            {
                if (TryParseAmount(balanceStr, out var parsedBalance))
                {
                    balance = parsedBalance;
                }
            }

            return new Transaction
            {
                Id = Guid.NewGuid(),
                Date = date,
                Description = description.Trim(),
                Amount = amount,
                Balance = balance,
                Category = !string.IsNullOrWhiteSpace(category?.Trim()) ? category.Trim() : "Uncategorized",
                ImportedAt = DateTime.UtcNow,
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? GetColumnValue(IDictionary<string, object> record, params string[] columnNames)
    {
        foreach (var columnName in columnNames)
        {
            if (record.TryGetValue(columnName, out var value) && value != null)
            {
                return value.ToString()?.Trim();
            }
        }

        return null;
    }

    private bool TryParseDate(string dateStr, out DateTime date)
    {
        date = default;

        // Try culture-aware parsing first
        if (DateTime.TryParse(dateStr.Trim(), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date))
        {
            return true;
        }

        return false;
    }

    private static bool TryParseAmount(string amountStr, out decimal amount)
    {
        amount = 0;

        if (string.IsNullOrWhiteSpace(amountStr))
            return false;

        var cleanAmount = amountStr.Trim();

        // Remove common currency symbols
        cleanAmount = cleanAmount.Replace("$", "").Replace("€", "").Replace("£", "").Replace("¥", "").Replace("R$", "").Trim();

        // Use culture-specific parsing - .NET handles decimal/thousand separators automatically
        return decimal.TryParse(cleanAmount, NumberStyles.Currency, CultureInfo.InvariantCulture, out amount);
    }
}