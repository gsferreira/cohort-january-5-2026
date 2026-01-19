using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Features.Transactions.Import.Detection;

namespace BudgetTracker.Api.Features.Transactions.Import.Processing;

public class CsvImporter
{
    public async Task<(ImportResult Result, List<Transaction> Transactions)> ParseCsvAsync(
        Stream csvStream, string sourceFileName, string userId, string account)
    {
        return await ParseCsvAsync(csvStream, sourceFileName, userId, account, null);
    }

    public async Task<(ImportResult Result, List<Transaction> Transactions)> ParseCsvAsync(
        Stream csvStream,
        string sourceFileName,
        string userId,
        string account,
        CsvStructureDetectionResult? detectionResult)
    {
        var result = new ImportResult
        {
            SourceFile = sourceFileName,
            ImportedAt = DateTime.UtcNow
        };

        if (detectionResult != null)
        {
            result.DetectionMethod = detectionResult.DetectionMethod.ToString();
            result.DetectionConfidence = detectionResult.ConfidenceScore;
        }

        var transactions = new List<Transaction>();

        try
        {
            var culture = GetCultureInfo(detectionResult?.CultureCode);
            var delimiter = detectionResult?.Delimiter ?? ',';
            var columnMappings = detectionResult?.ColumnMappings ?? new Dictionary<string, string>();

            using var reader = new StreamReader(csvStream, Encoding.UTF8);
            using var csv = new CsvReader(reader, new CsvConfiguration(culture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                BadDataFound = null,
                Delimiter = delimiter.ToString()
            });

            var rowNumber = 0;

            await foreach (var record in csv.GetRecordsAsync<dynamic>())
            {
                rowNumber++;
                result.TotalRows++;

                try
                {
                    var transaction = ParseTransactionRow(record, columnMappings, culture);
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

    private static CultureInfo GetCultureInfo(string? cultureCode)
    {
        if (string.IsNullOrEmpty(cultureCode))
            return CultureInfo.InvariantCulture;

        try
        {
            return CultureInfo.GetCultureInfo(cultureCode);
        }
        catch
        {
            return CultureInfo.InvariantCulture;
        }
    }

    private Transaction? ParseTransactionRow(
        dynamic record,
        Dictionary<string, string> columnMappings,
        CultureInfo culture)
    {
        try
        {
            var recordDict = (IDictionary<string, object>)record;

            var description = GetMappedColumnValue(recordDict, columnMappings, "Description") ??
                              GetColumnValue(recordDict, "Description", "Memo", "Details");

            var dateStr = GetMappedColumnValue(recordDict, columnMappings, "Date") ??
                          GetColumnValue(recordDict, "Date", "Transaction Date", "Posting Date");

            var amountStr = GetMappedColumnValue(recordDict, columnMappings, "Amount") ??
                            GetColumnValue(recordDict, "Amount", "Transaction Amount", "Debit", "Credit");

            var balanceStr = GetMappedColumnValue(recordDict, columnMappings, "Balance") ??
                             GetColumnValue(recordDict, "Balance", "Running Balance", "Account Balance");

            var category = GetMappedColumnValue(recordDict, columnMappings, "Category") ??
                           GetColumnValue(recordDict, "Category", "Type", "Transaction Type");

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

            if (!TryParseDate(dateStr, culture, out var date))
            {
                throw new ArgumentException($"Invalid date format: {dateStr}");
            }

            if (!TryParseAmount(amountStr, culture, out var amount))
            {
                throw new ArgumentException($"Invalid amount format: {amountStr}");
            }

            decimal? balance = null;
            if (!string.IsNullOrWhiteSpace(balanceStr))
            {
                if (TryParseAmount(balanceStr, culture, out var parsedBalance))
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

    private static string? GetMappedColumnValue(
        IDictionary<string, object> record,
        Dictionary<string, string> columnMappings,
        string standardField)
    {
        var sourceColumn = columnMappings
            .FirstOrDefault(m => m.Value == standardField).Key;

        if (!string.IsNullOrEmpty(sourceColumn) &&
            record.TryGetValue(sourceColumn, out var value) &&
            value != null)
        {
            return value.ToString()?.Trim();
        }

        return null;
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

    private static bool TryParseDate(string dateStr, CultureInfo culture, out DateTime date)
    {
        date = default;

        if (DateTime.TryParse(dateStr.Trim(), culture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date))
        {
            return true;
        }

        if (DateTime.TryParse(dateStr.Trim(), CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date))
        {
            return true;
        }

        return false;
    }

    private static bool TryParseAmount(string amountStr, CultureInfo culture, out decimal amount)
    {
        amount = 0;

        if (string.IsNullOrWhiteSpace(amountStr))
            return false;

        var cleanAmount = amountStr.Trim();
        cleanAmount = cleanAmount
            .Replace("$", "")
            .Replace("€", "")
            .Replace("£", "")
            .Replace("¥", "")
            .Replace("R$", "")
            .Trim();

        if (decimal.TryParse(cleanAmount, NumberStyles.Currency, culture, out amount))
        {
            return true;
        }

        return decimal.TryParse(cleanAmount, NumberStyles.Currency, CultureInfo.InvariantCulture, out amount);
    }
}
