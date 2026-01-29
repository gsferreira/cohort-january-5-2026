import apiClient from '../../api/client';
import type { BudgetInsights } from './types';

export const analyticsApi = {
  async getInsights(): Promise<BudgetInsights> {
    const response = await apiClient.get<BudgetInsights>('/insights');
    return response.data;
  }
};
