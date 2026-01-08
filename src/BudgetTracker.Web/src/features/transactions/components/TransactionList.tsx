import { useLoaderData, useNavigation } from 'react-router-dom';
import type { TransactionListDto } from '../types';
import EmptyState from '../../../shared/components/EmptyState';
import Pagination from '../../../shared/components/Pagination';
import { SkeletonCardRow } from '../../../shared/components/Skeleton';
import { formatDate } from '../../../shared/utils/formatters';

export default function TransactionList() {
  const data = useLoaderData() as TransactionListDto;
  const navigation = useNavigation();
  const isLoading = navigation.state === 'loading';

  const formatAmount = (amount: number) => {
    const isPositive = amount >= 0;
    const colorClass = isPositive ? 'text-green-600' : 'text-red-600';
    const sign = isPositive ? '+' : '';
    return (
      <span className={`inline-flex items-center font-medium ${colorClass}`}>
        {sign}${Math.abs(amount).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
      </span>
    );
  };

  if (isLoading) {
    return (
      <div className="space-y-4">
        {Array.from({ length: 8 }).map((_, index) => (
          <SkeletonCardRow key={index} />
        ))}
      </div>
    );
  }

  if (!data.items || data.items.length === 0) {
    return (
      <EmptyState
        title="No transactions found"
        description="You haven't imported any transactions yet. Start by uploading a CSV file."
        action={{
          label: 'Import Transactions',
          onClick: () => {
            window.location.href = '/import';
          }
        }}
      />
    );
  }

  return (
    <div className="space-y-4">
      <div className="space-y-2">
        {data.items.map((transaction) => (
          <div key={transaction.id} className="bg-white rounded-lg border border-neutral-100 p-4 hover:shadow-sm transition-shadow duration-200">
            <div className="flex justify-between items-start gap-4">
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2">
                  <p className="text-sm font-medium text-gray-900 truncate">
                    {transaction.description}
                  </p>
                  {transaction.category && (
                    <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${transaction.category === 'Uncategorized'
                      ? 'bg-red-100 text-red-700'
                      : 'bg-neutral-100 text-neutral-600'
                      }`}>
                      {transaction.category}
                    </span>
                  )}
                  {transaction.account && (
                    <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-blue-100 text-blue-700">
                      {transaction.account}
                    </span>
                  )}
                </div>
                <p className="text-xs text-gray-500 mt-1">
                  {formatDate(transaction.date)}
                </p>
              </div>
              <div className="flex-shrink-0 text-sm">
                {formatAmount(transaction.amount)}
              </div>
            </div>
          </div>
        ))}
      </div>

      {/* Pagination */}
      {data.totalPages > 1 && (
        <Pagination
          currentPage={data.page}
          totalPages={data.totalPages}
          totalCount={data.totalCount}
          pageSize={data.pageSize}
          className="mt-6"
        />
      )}
    </div>
  );
}