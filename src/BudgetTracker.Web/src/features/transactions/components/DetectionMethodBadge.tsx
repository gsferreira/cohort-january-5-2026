interface DetectionMethodBadgeProps {
  method: 'RuleBased' | 'AI' | string;
  confidence?: number;
  showConfidence?: boolean;
}

export function DetectionMethodBadge({
  method,
  confidence,
  showConfidence = true
}: DetectionMethodBadgeProps) {
  const isAI = method === 'AI';

  return (
    <div className="inline-flex items-center gap-2">
      <span
        className={`inline-flex items-center px-2 py-1 rounded-full text-xs font-medium ${
          isAI
            ? 'bg-purple-100 text-purple-800'
            : 'bg-blue-100 text-blue-800'
        }`}
      >
        {isAI ? (
          <>
            <span className="mr-1">🤖</span>
            AI Detection
          </>
        ) : (
          <>
            <span className="mr-1">📋</span>
            Pattern Match
          </>
        )}
      </span>

      {showConfidence && confidence !== undefined && (
        <span
          className={`inline-flex items-center px-2 py-1 rounded-full text-xs font-medium ${
            confidence >= 90
              ? 'bg-green-100 text-green-800'
              : confidence >= 70
                ? 'bg-yellow-100 text-yellow-800'
                : 'bg-red-100 text-red-800'
          }`}
        >
          {Math.round(confidence)}% confident
        </span>
      )}
    </div>
  );
}
