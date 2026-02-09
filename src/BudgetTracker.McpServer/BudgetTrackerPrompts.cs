using System.ComponentModel;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

[McpServerPromptType]
public static class BudgetTrackerPrompts
{
    [McpServerPrompt(Name = "Import"), Description("Import a csv file of transactions into the budget tracker")]
    public static ChatMessage Import([Description("Account name to import.")] string account)
    {
        return new(ChatRole.User, $"Import this csv file of transactions for account: {account}");
    }
}
