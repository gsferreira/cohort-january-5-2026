namespace BudgetTracker.Api.Features.Transactions.Import.Detection;

public static class ColumnMappingDictionary
{
    public static readonly string[] DateColumns =
    [
        "Date",
        "Transaction Date",
        "Posting Date",
        "Posted Date",
        "Trans Date",
        "Value Date"
    ];

    public static readonly string[] DescriptionColumns =
    [
        "Description",
        "Memo",
        "Details",
        "Narrative",
        "Transaction Description",
        "Payee",
        "Name"
    ];

    public static readonly string[] AmountColumns =
    [
        "Amount",
        "Transaction Amount",
        "Debit",
        "Credit",
        "Value",
        "Sum"
    ];

    public static readonly string[] BalanceColumns =
    [
        "Balance",
        "Running Balance",
        "Account Balance",
        "Available Balance",
        "Closing Balance"
    ];

    public static readonly string[] CategoryColumns =
    [
        "Category",
        "Type",
        "Transaction Type",
        "Trans Type"
    ];

    public static string? FindMatchingColumn(IEnumerable<string> headers, string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            var match = headers.FirstOrDefault(h =>
                string.Equals(h.Trim(), pattern, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                return match;
        }
        return null;
    }
}
