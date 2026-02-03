using System.Text.Json;
using BudgetTracker.Api.Infrastructure;
using BudgetTracker.Api.Features.Intelligence.Search;
using BudgetTracker.Api.Features.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

namespace BudgetTracker.Api.Features.Intelligence.Query;

public class QueryAssistantService : IQueryAssistantService
{
    private readonly BudgetTrackerContext _context;
    private readonly ISemanticSearchService _semanticSearchService;
    private readonly IChatClient _chatClient;
    private readonly ILogger<QueryAssistantService> _logger;

    public QueryAssistantService(
        BudgetTrackerContext context,
        ISemanticSearchService semanticSearchService,
        IChatClient chatClient,
        ILogger<QueryAssistantService> logger)
    {
        _context = context;
        _semanticSearchService = semanticSearchService;
        _chatClient = chatClient;
        _logger = logger;
    }

    public async Task<QueryResponse> ProcessQueryAsync(string query, string userId)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new QueryResponse { Answer = "Please provide a question about your finances." };
        }

        if (query.Length > 500)
        {
            return new QueryResponse { Answer = "Your question is too long. Please keep it under 500 characters." };
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return new QueryResponse { Answer = "User authentication required." };
        }

        try
        {
            var userTransactions = GetUserTransactions(userId);

            if (!await userTransactions.AnyAsync())
            {
                return new QueryResponse
                {
                    Answer =
                        "You don't have any transactions yet. Import some transactions to start asking questions about your finances."
                };
            }

            var relevantTransactions = await _semanticSearchService.FindRelevantTransactionsAsync(
                query, userId, maxResults: 10);

            var recentTransactions = await userTransactions.Take(10).ToListAsync();

            return await ProcessQueryDirectlyWithAi(query, recentTransactions, relevantTransactions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process query: {Query} for user {UserId}", query, userId);
            return new QueryResponse
            {
                Answer = "I'm sorry, I couldn't process your question right now. Please try again later."
            };
        }
    }

    private IOrderedQueryable<Transaction> GetUserTransactions(string userId)
    {
        return _context.Transactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.Date);
    }

    private async Task<QueryResponse> ProcessQueryDirectlyWithAi(string query, List<Transaction> transactions,
        List<Transaction> relevantTransactions)
    {
        var systemPrompt = CreateSystemPrompt();
        var userPrompt = CreateUserPrompt(query, transactions, relevantTransactions);

        var response = await _chatClient.GetResponseAsync([
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, userPrompt)
        ]);

        var content = response.Text ?? string.Empty;
        return ParseAiResponse(content, transactions);
    }

    private static string CreateSystemPrompt()
    {
        return """
               You are a helpful financial assistant that answers questions about the user's spending and transactions.

               You can analyze spending patterns, find specific transactions, calculate totals, identify trends, and provide insights.
               Be conversational and helpful. Provide specific numbers, dates, and transaction details when relevant.

               The transactions provided to you have been semantically filtered to be most relevant to the user's query,
               so you're working with the most pertinent financial data for their question.

               When responding, provide:
               1. A clear, natural language answer to their question
               2. If relevant, include specific transaction details or amounts
               3. If showing multiple transactions, limit to the most relevant 3-5 items

               Always respond with JSON in this exact format:
               {
                 "answer": "Your natural language response here",
                 "amount": null or decimal value if relevant,
                 "transactions": null or array of relevant transaction objects
               }

               For transactions, use this format:
               {
                 "id": "transaction-guid",
                 "date": "YYYY-MM-DD",
                 "description": "transaction description",
                 "amount": decimal-value,
                 "category": "category-name-or-null",
                 "account": "account-name"
               }

               Examples of queries you can handle:
               - "What was my biggest expense last week?"
               - "Show me all Amazon purchases"
               - "What categories do I spend the most on?"
               - "Show me transactions over $100"
               - "When did I last go to Starbucks?"
               - "How much have I saved this year?"
               - "Find all my coffee-related expenses"
               - "Show me subscription services I'm paying for"
               - "What do I spend on transportation?"
               """;
    }

    private static string CreateUserPrompt(string query, List<Transaction> transactions,
        List<Transaction> relevantTransactions)
    {
        var earliestDate = transactions.Min(t => t.Date);
        var latestDate = transactions.Max(t => t.Date);
        var totalTransactions = transactions.Count;
        var totalIncome = transactions.Where(t => t.Amount > 0).Sum(t => t.Amount);
        var totalExpenses = Math.Abs(transactions.Where(t => t.Amount < 0).Sum(t => t.Amount));

        var categoryBreakdown = transactions
            .Where(t => t.Amount < 0 && !string.IsNullOrEmpty(t.Category))
            .GroupBy(t => t.Category!)
            .Select(g => new { Category = g.Key, Total = Math.Abs(g.Sum(t => t.Amount)) })
            .OrderByDescending(c => c.Total)
            .Take(10)
            .ToList();

        var recentTransactions = transactions
            .OrderByDescending(t => t.Date)
            .Take(10)
            .Select(t => new
            {
                Id = t.Id,
                Date = t.Date.ToString("yyyy-MM-dd"),
                Description = t.Description,
                Amount = t.Amount,
                Category = t.Category,
                Account = t.Account
            })
            .ToList();

        var relatedTransactions = relevantTransactions
            .OrderByDescending(t => t.Date)
            .Take(10)
            .Select(t => new
            {
                Id = t.Id,
                Date = t.Date.ToString("yyyy-MM-dd"),
                Description = t.Description,
                Amount = t.Amount,
                Category = t.Category,
                Account = t.Account
            })
            .ToList();

        var transactionsJson =
            JsonSerializer.Serialize(recentTransactions, new JsonSerializerOptions { WriteIndented = false });

        var relevantTransactionsJson =
            JsonSerializer.Serialize(relatedTransactions, new JsonSerializerOptions { WriteIndented = false });

        return $"""
                User query: "{query}"

                Transaction Summary:
                - Total transactions: {totalTransactions}
                - Date range: {earliestDate:yyyy-MM-dd} to {latestDate:yyyy-MM-dd}
                - Total income: €{totalIncome:F2}
                - Total expenses: €{totalExpenses:F2}
                - Net amount: €{(totalIncome - totalExpenses):F2}

                Top spending categories:
                {string.Join("\n", categoryBreakdown.Select(c => $"- {c.Category}: €{c.Total:F2}"))}

                Recent transactions (sample of {recentTransactions.Count}):
                {transactionsJson}

                Relevant transactions for the prompt:
                {relevantTransactionsJson}

                Please analyze this data and answer the user's query. Include specific transaction details in your response when relevant.
                """;
    }

    private QueryResponse ParseAiResponse(string content, List<Transaction> transactions)
    {
        try
        {
            var jsonResponse = JsonSerializer.Deserialize<AiQueryResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (jsonResponse == null)
            {
                return new QueryResponse { Answer = "I couldn't process your question. Please try rephrasing it." };
            }

            var response = new QueryResponse
            {
                Answer = jsonResponse.Answer ?? "I processed your query but couldn't generate a response.",
                Amount = jsonResponse.Amount
            };

            if (jsonResponse.Transactions == null || jsonResponse.Transactions.Count == 0) return response;

            var matchedTransactions = new List<TransactionDto>();

            foreach (var aiTransaction in jsonResponse.Transactions.Take(5))
            {
                if (Guid.TryParse(aiTransaction.Id, out var transactionId))
                {
                    var actualTransaction = transactions.FirstOrDefault(t => t.Id == transactionId);
                    if (actualTransaction != null)
                    {
                        matchedTransactions.Add(actualTransaction.MapToDto());
                        continue;
                    }
                }

                if (DateTime.TryParse(aiTransaction.Date, out var date))
                {
                    matchedTransactions.Add(new TransactionDto
                    {
                        Id = Guid.TryParse(aiTransaction.Id, out var id) ? id : Guid.NewGuid(),
                        Date = date,
                        Description = aiTransaction.Description ?? "Transaction",
                        Amount = aiTransaction.Amount,
                        Category = aiTransaction.Category,
                        Account = aiTransaction.Account ?? "Account",
                        ImportedAt = DateTime.UtcNow
                    });
                }
            }

            if (matchedTransactions.Count != 0)
            {
                response.Transactions = matchedTransactions;
            }

            return response;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI response: {Content}", content);
            return new QueryResponse
            {
                Answer =
                    "I processed your question but had trouble formatting the response. Please try asking in a different way."
            };
        }
    }

    private class AiQueryResponse
    {
        public string? Answer { get; set; }
        public decimal? Amount { get; set; }
        public List<AiTransactionReference>? Transactions { get; set; }
    }

    private class AiTransactionReference
    {
        public string Id { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public string? Category { get; set; }
        public string? Account { get; set; }
    }
}
