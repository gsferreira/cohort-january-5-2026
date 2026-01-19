interface DetectionProgressIndicatorProps {
  progress: number;
  currentPhase: 'uploading' | 'detecting' | 'parsing' | 'extracting' | 'enhancing' | 'complete';
  fileName: string;
}

export function DetectionProgressIndicator({
  progress,
  currentPhase,
  fileName
}: DetectionProgressIndicatorProps) {
  const isImage = fileName.toLowerCase().match(/\.(png|jpg|jpeg)$/);

  const getPhaseDescription = (): string => {
    switch (currentPhase) {
      case 'uploading':
        return `Uploading ${isImage ? 'bank statement image' : 'CSV file'}...`;
      case 'detecting':
        return isImage ? 'Preparing image for analysis...' : 'Detecting CSV structure...';
      case 'parsing':
        return 'Parsing CSV data...';
      case 'extracting':
        return 'Extracting transactions from image using AI...';
      case 'enhancing':
        return 'Enhancing transaction descriptions with AI...';
      case 'complete':
        return 'Import completed successfully!';
      default:
        return 'Processing...';
    }
  };

  const getPhaseIcon = (): string => {
    switch (currentPhase) {
      case 'uploading':
        return '⬆️';
      case 'detecting':
        return '🔍';
      case 'parsing':
        return '📊';
      case 'extracting':
        return '🖼️';
      case 'enhancing':
        return '✨';
      case 'complete':
        return '✅';
      default:
        return '⚙️';
    }
  };

  return (
    <div className="mt-4">
      <div className="flex justify-between text-sm text-gray-600 mb-1">
        <span className="flex items-center gap-2">
          <span>{getPhaseIcon()}</span>
          <span>{getPhaseDescription()}</span>
        </span>
        <span>{Math.round(progress)}%</span>
      </div>
      <div className="w-full bg-gray-200 rounded-full h-2">
        <div
          className="bg-blue-600 h-2 rounded-full transition-all duration-300"
          style={{ width: `${progress}%` }}
        />
      </div>

      {currentPhase === 'detecting' && (
        <div className="mt-2 text-xs text-gray-500 flex items-center">
          <svg className="animate-spin -ml-1 mr-2 h-3 w-3 text-gray-400" fill="none" viewBox="0 0 24 24">
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"/>
            <path className="opacity-75" fill="currentColor" d="m4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"/>
          </svg>
          Analyzing column structure and format...
        </div>
      )}

      {currentPhase === 'extracting' && (
        <div className="mt-2 text-xs text-gray-500 flex items-center">
          <svg className="animate-spin -ml-1 mr-2 h-3 w-3 text-gray-400" fill="none" viewBox="0 0 24 24">
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"/>
            <path className="opacity-75" fill="currentColor" d="m4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"/>
          </svg>
          AI vision analyzing bank statement...
        </div>
      )}
    </div>
  );
}
