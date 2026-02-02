import api from '../../api/client';

export interface QueryRequest {
  query: string;
}

export interface TransactionDto {
  id: string;
  date: string;
  description: string;
  amount: number;
  balance?: number;
  category?: string;
  labels?: string;
  importedAt: string;
  account: string;
}

export interface QueryResponse {
  answer: string;
  amount?: number;
  transactions?: TransactionDto[];
}

export interface ProactiveRecommendation {
  id: string;
  title: string;
  message: string;
  type: 'SpendingAlert' | 'SavingsOpportunity' | 'BehavioralInsight' | 'BudgetWarning';
  priority: 'Low' | 'Medium' | 'High' | 'Critical';
  generatedAt: string;
  expiresAt: string;
}

export const intelligenceApi = {
  askQuery: async (query: string): Promise<QueryResponse> => {
    const response = await api.post<QueryResponse>('/query/ask', { query });
    return response.data;
  },

  async getRecommendations(): Promise<ProactiveRecommendation[]> {
    const response = await api.get<ProactiveRecommendation[]>('/recommendations');
    return response.data;
  }
};
