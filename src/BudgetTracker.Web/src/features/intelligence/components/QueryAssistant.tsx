import { useState } from 'react';
import { useToast } from '../../../shared/contexts/ToastContext';
import { intelligenceApi, type QueryResponse } from '../api';
import Card from '../../../shared/components/Card';
import { LoadingSpinner } from '../../../shared/components/LoadingSpinner';
import { formatCurrency, formatDate } from '../../../shared/utils/formatters';

const MessageIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="lucide lucide-message-circle">
    <path d="M7.9 20A9 9 0 1 0 4 16.1L2 22Z" />
  </svg>
);

const SendIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="lucide lucide-send">
    <path d="m22 2-7 20-4-9-9-4Z" />
    <path d="M22 2 11 13" />
  </svg>
);

interface QueryAssistantProps {
  className?: string;
}

export default function QueryAssistant({ className = "" }: QueryAssistantProps) {
  const [query, setQuery] = useState('');
  const [response, setResponse] = useState<QueryResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const { showError } = useToast();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!query.trim() || isLoading) return;

    setIsLoading(true);
    try {
      const result = await intelligenceApi.askQuery(query.trim());
      setResponse(result);
    } catch (error) {
      console.error('Query failed:', error);
      showError('Failed to process your query. Please try again.');
    } finally {
      setIsLoading(false);
    }
  };

  const suggestedQueries = [
    "What was my biggest expense last week?",
    "How much did I spend on groceries this month?",
    "Show me all transactions over $100",
    "What's my average daily spending?",
    "Which category do I spend the most on?",
    "When did I last go to Starbucks?"
  ];

  return (
    <Card className={`p-6 ${className}`}>
      <div className="flex items-center gap-3 mb-4">
        <div className="p-2 bg-indigo-100 rounded-lg">
          <MessageIcon />
        </div>
        <div>
          <h3 className="font-semibold text-gray-900">Ask about your finances</h3>
          <p className="text-sm text-gray-600">Ask me anything about your spending and transactions</p>
        </div>
      </div>

      <form onSubmit={handleSubmit} className="mb-4">
        <div className="flex gap-2">
          <div className="flex-1 relative">
            <input
              type="text"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder={isLoading ? "Processing your question..." : "Ask a question about your finances..."}
              className={`w-full px-3 py-2 border rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent ${
                isLoading
                  ? 'border-indigo-300 bg-indigo-50 placeholder:text-indigo-400'
                  : 'border-gray-300 placeholder:text-gray-500'
              }`}
              disabled={isLoading}
            />
            {isLoading && (
              <div className="absolute right-3 top-1/2 transform -translate-y-1/2">
                <LoadingSpinner size="sm" />
              </div>
            )}
          </div>
          <button
            type="submit"
            disabled={!query.trim() || isLoading}
            className="px-4 py-2 bg-indigo-600 text-white rounded-md text-sm font-medium hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2 min-w-[80px] justify-center"
          >
            {isLoading ? <LoadingSpinner size="sm" /> : (
              <>
                <SendIcon />
                Ask
              </>
            )}
          </button>
        </div>
      </form>

      {!response && !isLoading && (
        <div className="space-y-2">
          <p className="text-sm text-gray-600 mb-2">Try asking:</p>
          <div className="flex flex-wrap gap-2">
            {suggestedQueries.map((suggestion, index) => (
              <button
                key={index}
                onClick={() => setQuery(suggestion)}
                className="text-xs px-3 py-1 bg-gray-100 text-gray-600 rounded-full hover:bg-gray-200 transition-colors"
              >
                {suggestion}
              </button>
            ))}
          </div>
        </div>
      )}

      {response && (
        <div className="mt-4 p-4 bg-gray-50 rounded-lg">
          <p className="text-sm text-gray-900 mb-3">{response.answer}</p>

          {response.transactions && response.transactions.length > 0 && (
            <div className="mt-3 space-y-2">
              {response.transactions.slice(0, 3).map((transaction) => (
                <div key={transaction.id} className="p-3 bg-white rounded-md border">
                  <div className="flex justify-between items-start">
                    <div className="flex-1">
                      <p className="font-medium text-sm">{transaction.description}</p>
                      <p className="text-xs text-gray-500 mt-1">
                        {formatDate(transaction.date)} • {transaction.account}
                      </p>
                      {transaction.category && (
                        <span className="inline-block mt-1 px-2 py-1 bg-indigo-100 text-indigo-700 text-xs rounded-full">
                          {transaction.category}
                        </span>
                      )}
                    </div>
                    <div className="text-right">
                      <p className={`font-medium text-sm ${transaction.amount < 0 ? 'text-red-600' : 'text-green-600'}`}>
                        {transaction.amount < 0 ? '-' : '+'}{formatCurrency(Math.abs(transaction.amount))}
                      </p>
                    </div>
                  </div>
                </div>
              ))}
              {response.transactions.length > 3 && (
                <p className="text-xs text-gray-500 text-center">
                  ... and {response.transactions.length - 3} more transactions
                </p>
              )}
            </div>
          )}
        </div>
      )}
    </Card>
  );
}
