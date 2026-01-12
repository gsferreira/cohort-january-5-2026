export interface Transaction {
  id: string;
  date: string;
  description: string;
  amount: number;
  balance?: number;
  category?: string;
  labels?: string;
  importedAt: string;
  sourceFile?: string;
  account: string;
}

export interface TransactionListDto {
  items: Transaction[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface GetTransactionsParams {
  page?: number;
  pageSize?: number;
}

export interface ImportTransactionsParams {
  formData: FormData;
  onUploadProgress?: (progressEvent: any) => void;
}

export interface TransactionEnhancement {
  transactionId: string;
  importSessionHash: string;
  transactionIndex: number;
  originalDescription: string;
  enhancedDescription: string;
  suggestedCategory?: string;
  confidenceScore: number;
}

export interface ImportResult {
  totalRows: number;
  importedCount: number;
  failedCount: number;
  errors: string[];
  sourceFile?: string;
  importedAt: string;
  importSessionHash: string;
  enhancements: TransactionEnhancement[];
}

export interface EnhanceImportRequest {
  importSessionHash: string;
  enhancements: TransactionEnhancement[];
  minConfidenceScore: number;
  applyEnhancements: boolean;
}

export interface EnhanceImportResult {
  importSessionHash: string;
  totalTransactions: number;
  enhancedCount: number;
  skippedCount: number;
}