export interface BudgetInsights {
  budgetBreakdown: BudgetBreakdown;
  summary: string;
  health: BudgetHealth;
}

export interface BudgetBreakdown {
  needsPercentage: number;
  wantsPercentage: number;
  savingsPercentage: number;
  needsAmount: number;
  wantsAmount: number;
  savingsAmount: number;
  totalExpenses: number;
}

export interface BudgetHealth {
  status: string;
  isHealthy: boolean;
  areas: string[];
}
