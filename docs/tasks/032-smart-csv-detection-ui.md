# Workshop Step 032: Smart CSV Detection UI

## Mission

In this step, you'll update the frontend to display CSV detection information and progress indicators. Users will see whether their CSV was processed using rule-based detection or AI analysis, along with confidence scores and appropriate progress feedback.

**Your goal**: Enhance the import UI to provide transparency about how CSV files are analyzed and processed.

**Learning Objectives**:
- Displaying detection method and confidence in the UI
- Creating progress indicators for different processing phases
- Updating TypeScript types to match backend responses
- Building informative success states with detection details

---

## Prerequisites

Before starting, ensure you completed:
- [031-smart-csv-detection-backend.md](031-smart-csv-detection-backend.md) - CSV detection backend

---

## Step 32.1: Update Frontend Types

*Add detection information to the frontend TypeScript types.*

The frontend needs to understand the new detection information to provide appropriate feedback to users about how their CSV files were processed.

Update `src/BudgetTracker.Web/src/features/transactions/types.ts`:

```tsx
export interface ImportResult {
  importedCount: number;
  failedCount: number;
  errors: string[];
  importSessionHash: string;
  enhancements: TransactionEnhancement[];
  detectionMethod?: string; // "RuleBased" | "AI"
  detectionConfidence?: number; // 0-100
}
```

## Step 32.2: Update Detection Progress Indicator

*Modify the progress indicator to show CSV detection phases.*

The progress indicator should display different messages during the detection phase to help users understand what's happening during import.

Create or update `src/BudgetTracker.Web/src/features/transactions/components/DetectionProgressIndicator.tsx`:

```tsx
interface DetectionProgressIndicatorProps {
  progress: number;
  currentPhase: 'uploading' | 'detecting' | 'parsing' | 'enhancing' | 'complete';
  fileName: string;
}

export function DetectionProgressIndicator({
  progress,
  currentPhase,
  fileName
}: DetectionProgressIndicatorProps) {
  const getPhaseDescription = (): string => {
    switch (currentPhase) {
      case 'uploading':
        return 'Uploading CSV file...';
      case 'detecting':
        return 'Detecting CSV structure...';
      case 'parsing':
        return 'Parsing transactions...';
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
    </div>
  );
}
```

## Step 32.3: Update FileUpload Component Progress Phases

*Update the file upload component to track and display detection phases.*

Update `src/BudgetTracker.Web/src/features/transactions/components/FileUpload.tsx` to include phase tracking:

```tsx
// Add phase state
const [currentPhase, setCurrentPhase] = useState<'uploading' | 'detecting' | 'parsing' | 'enhancing' | 'complete'>('uploading');

// Update the handleImport function to set phases
const handleImport = async () => {
  if (!selectedFile || !account.trim()) return;

  setIsUploading(true);
  setUploadProgress(0);
  setImportResult(null);
  setCurrentPhase('uploading');

  try {
    const formData = new FormData();
    formData.append('file', selectedFile);
    formData.append('account', account.trim());

    const result = await transactionsApi.importTransactions({
      formData,
      onUploadProgress: (progressEvent) => {
        const progress = Math.round((progressEvent.loaded * 100) / progressEvent.total);
        setUploadProgress(progress);

        // Update phase based on progress
        if (progress < 20) {
          setCurrentPhase('uploading');
        } else if (progress < 40) {
          setCurrentPhase('detecting');
        } else if (progress < 70) {
          setCurrentPhase('parsing');
        } else if (progress < 100) {
          setCurrentPhase('enhancing');
        } else {
          setCurrentPhase('complete');
        }
      }
    });

    setImportResult(result);
    setCurrentStep('imported');
    // ... rest of success handling
  } catch (error) {
    // ... error handling
  } finally {
    setIsUploading(false);
    setUploadProgress(0);
  }
};
```

## Step 32.4: Add Detection Information Display

*Update the success message to show detection method and confidence.*

Add detection information display to the import success state in `FileUpload.tsx`:

```tsx
{result && result.importedCount > 0 && (
  <div className="mt-4 p-4 bg-green-50 border border-green-200 rounded-md">
    <div className="flex items-center">
      <svg className="h-5 w-5 text-green-400 mr-2" fill="currentColor" viewBox="0 0 20 20">
        <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clipRule="evenodd" />
      </svg>
      <div className="flex-1">
        <span className="text-sm font-medium text-green-800">
          Import completed successfully!
        </span>
        <div className="text-xs text-green-700 mt-1">
          {result.importedCount} transactions imported
          {result.failedCount > 0 && `, ${result.failedCount} failed`}
        </div>

        {/* Detection information display */}
        {result.detectionMethod && (
          <div className="mt-2 p-2 bg-green-100 rounded border border-green-200">
            <div className="flex items-center justify-between">
              <span className="text-xs font-medium text-green-800">
                CSV Analysis: {result.detectionMethod === 'RuleBased' ? 'Pattern Matching' : 'AI Detection'}
              </span>
              {result.detectionConfidence !== undefined && (
                <span className="text-xs text-green-700 bg-green-200 px-2 py-1 rounded">
                  {Math.round(result.detectionConfidence)}% confidence
                </span>
              )}
            </div>
            <div className="text-xs text-green-600 mt-1">
              {result.detectionMethod === 'RuleBased'
                ? 'Standard format detected using pattern matching'
                : 'Complex format analyzed using AI detection'
              }
            </div>
          </div>
        )}
      </div>
    </div>
  </div>
)}
```

## Step 32.5: Create Detection Method Badge Component

*Create a reusable badge component for showing detection method.*

Create `src/BudgetTracker.Web/src/features/transactions/components/DetectionMethodBadge.tsx`:

```tsx
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
```

## Step 32.6: Update Import Results Display

*Enhance the import results to show detection details prominently.*

Update the import results section in `FileUpload.tsx` to use the new badge component:

```tsx
import { DetectionMethodBadge } from './DetectionMethodBadge';

// In the results display section:
{importResult && (
  <div className="space-y-4">
    {/* Detection method badge */}
    {importResult.detectionMethod && (
      <div className="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
        <span className="text-sm text-gray-600">Structure Detection</span>
        <DetectionMethodBadge
          method={importResult.detectionMethod}
          confidence={importResult.detectionConfidence}
        />
      </div>
    )}

    {/* Transaction counts */}
    <div className="grid grid-cols-2 gap-4">
      <div className="p-3 bg-green-50 rounded-lg text-center">
        <div className="text-2xl font-bold text-green-600">
          {importResult.importedCount}
        </div>
        <div className="text-xs text-green-700">Imported</div>
      </div>
      {importResult.failedCount > 0 && (
        <div className="p-3 bg-red-50 rounded-lg text-center">
          <div className="text-2xl font-bold text-red-600">
            {importResult.failedCount}
          </div>
          <div className="text-xs text-red-700">Failed</div>
        </div>
      )}
    </div>
  </div>
)}
```

## Step 32.7: Handle Detection Errors

*Update error handling to show helpful messages for detection failures.*

Add specific error handling for detection failures:

```tsx
// In the error handling section
const getErrorMessage = (error: any): string => {
  const message = error?.message || 'Failed to import the file';

  // Check for detection-related errors
  if (message.includes('Unable to automatically detect CSV structure')) {
    return 'Could not detect the CSV format. Please ensure your file has clear column headers (Date, Description, Amount).';
  }

  if (message.includes('AI analysis')) {
    return 'AI analysis could not determine the file structure. Try a CSV with standard column names.';
  }

  return message;
};

// In the catch block
catch (error: any) {
  console.error('Import error:', error);
  const errorMessage = getErrorMessage(error);
  showError('Import Failed', errorMessage);
}
```

---

## Testing

### Test Rule-Based Detection Display

1. Upload a standard CSV with English headers (Date, Description, Amount)
2. Verify:
   - Progress phases show correctly (uploading → detecting → parsing → enhancing)
   - Success message shows "Pattern Matching" method
   - Confidence badge shows high confidence (>85%)

### Test AI Detection Display

1. Upload a CSV with non-English headers (e.g., Portuguese: Data, Descrição, Valor)
2. Verify:
   - Detection phase shows "Analyzing column structure..."
   - Success message shows "AI Detection" method
   - Purple badge indicates AI was used

### Test Detection Failure

1. Upload a CSV with unrecognizable columns
2. Verify:
   - Appropriate error message is displayed
   - User is guided on how to fix the issue

---

## Summary

You've successfully implemented:

- **TypeScript Types**: Updated to include detection method and confidence
- **Progress Indicators**: Different phases for CSV detection flow
- **Detection Badges**: Visual indicators for rule-based vs AI detection
- **Confidence Display**: Clear presentation of detection confidence levels
- **Error Handling**: Helpful messages for detection failures

**Next Step**: Move to `033-image-import-backend.md` to add multimodal image import capabilities.
