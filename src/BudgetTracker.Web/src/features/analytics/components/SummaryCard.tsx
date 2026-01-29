import type { ReactNode } from 'react';

interface SummaryCardProps {
  title: string;
  value: number;
  icon: ReactNode;
  valueColor?: string;
  trend?: string;
  isCurrency?: boolean;
}

export function SummaryCard({
  title,
  value,
  icon,
  valueColor = 'text-gray-900',
  trend,
  isCurrency = false
}: SummaryCardProps) {
  const formatValue = (val: number) => {
    if (isCurrency) {
      return new Intl.NumberFormat('en-US', {
        style: 'currency',
        currency: 'USD',
        minimumFractionDigits: 0,
        maximumFractionDigits: 0,
      }).format(Math.abs(val));
    }
    return val.toLocaleString();
  };

  return (
    <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center">
          <div className="flex-shrink-0">
            <div className="flex items-center justify-center w-8 h-8 bg-blue-100 rounded-lg">
              <div className="text-blue-600">
                {icon}
              </div>
            </div>
          </div>
          <div className="ml-4">
            <p className="text-sm font-medium text-gray-600">{title}</p>
            <p className={`text-2xl font-bold ${valueColor}`}>
              {isCurrency && value < 0 ? '-' : ''}{formatValue(value)}
            </p>
          </div>
        </div>
      </div>
      {trend && (
        <div className="mt-2">
          <p className="text-xs text-gray-500">{trend}</p>
        </div>
      )}
    </div>
  );
}
