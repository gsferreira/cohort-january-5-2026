# Workshop Step 022: React AI Enhancement UI

## Mission

In this step, you'll build a sophisticated multi-step upload workflow that lets users preview and control AI-generated enhancements. Users will see exactly what the AI suggests before deciding to apply changes.

**Your goal**: Create a complete upload → preview → apply flow that gives users full control over AI suggestions.

**Learning Objectives**:
- Building multi-step user workflows in React
- Managing complex state transitions and UI flows
- Creating transparent AI preview interfaces
- Implementing confidence-based filtering
- User decision controls for AI suggestions

---

## Prerequisites

Before starting, ensure you completed:
- [021-ai-transaction-enhancement-backend.md](021-ai-transaction-enhancement-backend.md) - AI backend implementation

---

## Step 22.1: Update API Client Types

*Add types for the enhancement workflow.*

Update `src/BudgetTracker.Web/src/features/transactions/types.ts`:

```typescript
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
```

## Step 22.2: Add Enhancement API Function

*Add the API function to apply enhancements.*

Update `src/BudgetTracker.Web/src/features/transactions/api.ts`:

```typescript
async enhanceImport(request: EnhanceImportRequest): Promise<EnhanceImportResult> {
  const response = await apiClient.post<EnhanceImportResult>(
    '/transactions/import/enhance',
    request
  );
  return response.data;
}
```

## Step 22.3: Add Multi-Step State Management

*Transform the FileUpload component to support multiple steps.*

Update the imports and add new state in `src/BudgetTracker.Web/src/features/transactions/components/FileUpload.tsx`:

```tsx
import { transactionsApi, type EnhanceImportResult, type ImportResult } from '../api';

type Step = 'upload' | 'preview' | 'complete';

// Add these state variables after existing ones
const [currentStep, setCurrentStep] = useState<Step>('upload');
const [importResult, setImportResult] = useState<ImportResult | null>(null);
const [minConfidenceScore, setMinConfidenceScore] = useState(0.7);
const [enhanceResult, setEnhanceResult] = useState<EnhanceImportResult | null>(null);
const [currentPhase, setCurrentPhase] = useState<'uploading' | 'parsing' | 'enhancing' | 'complete'>('uploading');
```

## Step 22.4: Update Import Handler

*Update the import function to handle the multi-step flow.*

Replace the existing `handleImport` function:

```tsx
const handleImport = useCallback(async () => {
  if (!selectedFile || !account.trim()) {
    return;
  }

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
        if (progress < 30) {
          setCurrentPhase('uploading');
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
    setCurrentStep('preview');

    showSuccess(`Imported ${result.importedCount} transactions - review AI enhancements below`);
  } catch (error) {
    console.error('Import error:', error);
    let errorMessage = 'Failed to import the CSV file';
    if (error && typeof error === 'object' && 'message' in error) {
      errorMessage = (error as Error).message;
    }
    showError('Import Failed', errorMessage);
  } finally {
    setIsUploading(false);
    setUploadProgress(0);
    setCurrentPhase('uploading');
  }
}, [selectedFile, account, showError, showSuccess]);
```

## Step 22.5: Add Enhancement Handler

*Create the function to apply or skip AI enhancements.*

Add this function after `handleImport`:

```tsx
const handleEnhance = useCallback(async (applyEnhancements: boolean) => {
  if (!importResult) return;

  setIsUploading(true);

  try {
    const result = await transactionsApi.enhanceImport({
      importSessionHash: importResult.importSessionHash,
      enhancements: importResult.enhancements,
      minConfidenceScore,
      applyEnhancements
    });

    setEnhanceResult(result);
    setCurrentStep('complete');

    if (applyEnhancements) {
      showSuccess(
        `Enhanced ${result.enhancedCount} of ${result.totalTransactions} transactions`
      );
    } else {
      showSuccess('Import complete - original descriptions kept');
    }

    // Redirect after delay
    setTimeout(() => {
      navigate('/transactions');
    }, 4000);
  } catch (error) {
    showError('Enhancement Failed', 'Failed to apply enhancements');
    console.error('Enhancement error:', error);
  } finally {
    setIsUploading(false);
  }
}, [importResult, minConfidenceScore, showSuccess, showError, navigate]);
```

## Step 22.6: Update Clear Handler

*Update the clear function to reset all state.*

```tsx
const handleClearFile = useCallback(() => {
  setSelectedFile(null);
  setImportResult(null);
  setEnhanceResult(null);
  setCurrentStep('upload');
  setCurrentPhase('uploading');
  setUploadProgress(0);
  if (fileInputRef.current) {
    fileInputRef.current.value = '';
  }
}, []);
```

## Step 22.7: Build Step Indicator

*Create a visual progress indicator showing the current step.*

Add at the top of the return statement:

```tsx
return (
  <div className={`space-y-8 ${className}`}>
    {/* Step Indicator */}
    <div className="flex items-center justify-center space-x-4">
      {/* Step 1: Upload */}
      <div className={`flex items-center space-x-2 ${
        currentStep === 'upload' ? 'text-blue-600' : 'text-green-600'
      }`}>
        <div className={`w-8 h-8 rounded-full flex items-center justify-center text-sm font-medium ${
          currentStep === 'upload' ? 'bg-blue-100 text-blue-600' : 'bg-green-100 text-green-600'
        }`}>
          {currentStep === 'upload' ? '1' : '✓'}
        </div>
        <span className="text-sm font-medium">Upload</span>
      </div>

      <div className={`w-12 h-0.5 ${
        currentStep !== 'upload' ? 'bg-green-600' : 'bg-gray-200'
      }`} />

      {/* Step 2: Preview */}
      <div className={`flex items-center space-x-2 ${
        currentStep === 'preview' ? 'text-blue-600' :
        currentStep === 'complete' ? 'text-green-600' : 'text-gray-400'
      }`}>
        <div className={`w-8 h-8 rounded-full flex items-center justify-center text-sm font-medium ${
          currentStep === 'preview' ? 'bg-blue-100 text-blue-600' :
          currentStep === 'complete' ? 'bg-green-100 text-green-600' : 'bg-gray-100 text-gray-400'
        }`}>
          {currentStep === 'complete' ? '✓' : '2'}
        </div>
        <span className="text-sm font-medium">Preview</span>
      </div>

      <div className={`w-12 h-0.5 ${
        currentStep === 'complete' ? 'bg-green-600' : 'bg-gray-200'
      }`} />

      {/* Step 3: Complete */}
      <div className={`flex items-center space-x-2 ${
        currentStep === 'complete' ? 'text-green-600' : 'text-gray-400'
      }`}>
        <div className={`w-8 h-8 rounded-full flex items-center justify-center text-sm font-medium ${
          currentStep === 'complete' ? 'bg-green-100 text-green-600' : 'bg-gray-100 text-gray-400'
        }`}>
          {currentStep === 'complete' ? '✓' : '3'}
        </div>
        <span className="text-sm font-medium">Complete</span>
      </div>
    </div>

    {/* Step Content */}
    {/* ... rest of component ... */}
  </div>
);
```

## Step 22.8: Build Upload Step

*Update the upload form with confidence threshold selection.*

Inside the step content area, add the upload step:

```tsx
{/* Step 1: Upload */}
{currentStep === 'upload' && (
  <>
    {/* Existing drag-and-drop area */}
    <div
      className={`relative border-2 border-dashed rounded-2xl p-10 text-center transition-all ${
        isDragOver ? 'border-blue-400 bg-blue-50' : 'border-gray-300 hover:border-blue-300'
      }`}
      onDragOver={handleDragOver}
      onDragLeave={handleDragLeave}
      onDrop={handleDrop}
    >
      <input
        ref={fileInputRef}
        type="file"
        accept=".csv"
        onChange={handleFileInputChange}
        className="hidden"
      />

      <div className="space-y-4">
        <div className="mx-auto h-12 w-12 text-gray-400">
          <svg fill="none" stroke="currentColor" viewBox="0 0 24 24" strokeWidth={1.5}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5m-13.5-9L12 3m0 0l4.5 4.5M12 3v13.5" />
          </svg>
        </div>

        <div>
          <p className="text-lg font-medium text-gray-900">
            {selectedFile ? selectedFile.name : 'Upload Bank Statement'}
          </p>
          <p className="text-sm text-gray-500">
            {selectedFile
              ? `${(selectedFile.size / 1024).toFixed(1)} KB`
              : 'Drag and drop or click to browse'}
          </p>
        </div>

        {!selectedFile && (
          <button
            onClick={handleBrowseClick}
            className="px-4 py-2 text-sm font-medium text-white bg-blue-600 rounded-lg hover:bg-blue-700"
          >
            Browse Files
          </button>
        )}
      </div>
    </div>

    {/* File selected - show options */}
    {selectedFile && (
      <div className="bg-gray-50 rounded-xl p-6 space-y-4">
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              Account Name
            </label>
            <input
              type="text"
              value={account}
              onChange={(e) => setAccount(e.target.value)}
              placeholder="e.g., Checking, Savings"
              className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
            />
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              AI Confidence Threshold
            </label>
            <select
              value={minConfidenceScore}
              onChange={(e) => setMinConfidenceScore(Number(e.target.value))}
              className="w-full px-3 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500"
            >
              <option value={0.3}>Low (30%) - More changes</option>
              <option value={0.5}>Medium (50%) - Balanced</option>
              <option value={0.7}>High (70%) - Conservative</option>
            </select>
          </div>
        </div>

        <div className="flex justify-end space-x-3">
          <button
            onClick={handleClearFile}
            disabled={isUploading}
            className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50"
          >
            Remove
          </button>
          <button
            onClick={handleImport}
            disabled={isUploading || !account.trim()}
            className="px-4 py-2 text-sm font-medium text-white bg-blue-600 rounded-lg hover:bg-blue-700 disabled:opacity-50"
          >
            {isUploading ? (
              <span className="flex items-center">
                <LoadingSpinner size="sm" />
                <span className="ml-2">
                  {currentPhase === 'uploading' ? 'Uploading...' :
                   currentPhase === 'parsing' ? 'Parsing...' :
                   currentPhase === 'enhancing' ? 'Enhancing...' : 'Processing...'}
                </span>
              </span>
            ) : (
              'Import Transactions'
            )}
          </button>
        </div>
      </div>
    )}

    {/* Upload progress */}
    {isUploading && uploadProgress > 0 && (
      <div className="space-y-2">
        <div className="flex justify-between text-sm">
          <span className="text-gray-600">
            {currentPhase === 'uploading' ? 'Uploading file...' :
             currentPhase === 'parsing' ? 'Parsing transactions...' :
             currentPhase === 'enhancing' ? 'Enhancing with AI...' : 'Finalizing...'}
          </span>
          <span className="text-blue-600 font-medium">{uploadProgress}%</span>
        </div>
        <div className="w-full bg-gray-200 rounded-full h-2">
          <div
            className="h-2 rounded-full bg-blue-600 transition-all"
            style={{ width: `${uploadProgress}%` }}
          />
        </div>
      </div>
    )}
  </>
)}
```

## Step 22.9: Build Preview Step

*Create the enhancement preview interface with confidence filtering and category display.*

```tsx
{/* Step 2: Preview Enhancements */}
{currentStep === 'preview' && importResult && (
  <div className="space-y-6">
    <div className="bg-white rounded-xl border border-gray-200 p-6">
      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <h3 className="text-lg font-semibold text-gray-900">Review AI Enhancements</h3>
        <div className="flex items-center space-x-4 text-sm">
          <span className="text-green-600">
            {importResult.importedCount} imported
          </span>
          <span className="text-blue-600">
            {importResult.enhancements.filter(e => e.confidenceScore >= minConfidenceScore).length} will be enhanced
          </span>
        </div>
      </div>

      {/* Enhancement list */}
      <div className="space-y-3 max-h-96 overflow-y-auto">
        {importResult.enhancements.slice(0, 15).map((enhancement, index) => {
          const willEnhance = enhancement.confidenceScore >= minConfidenceScore;

          return (
            <div
              key={`${enhancement.importSessionHash}-${index}`}
              className={`border rounded-lg p-4 ${willEnhance ? 'border-green-200 bg-green-50' : 'border-gray-200'}`}
            >
              <div className="flex justify-between items-start">
                <div className="flex-1 space-y-2">
                  <div className="flex items-center justify-between">
                    <span className="text-xs text-gray-500">#{index + 1}</span>
                    <span className={`px-2 py-1 rounded-full text-xs font-medium ${
                      enhancement.confidenceScore >= 0.8 ? 'bg-green-100 text-green-800' :
                      enhancement.confidenceScore >= 0.5 ? 'bg-yellow-100 text-yellow-800' :
                      'bg-red-100 text-red-800'
                    }`}>
                      {Math.round(enhancement.confidenceScore * 100)}%
                    </span>
                  </div>

                  <div className="text-sm">
                    <span className="text-gray-500">Original:</span>{' '}
                    <span className="text-gray-700">{enhancement.originalDescription}</span>
                  </div>

                  {willEnhance && (
                    <div className="text-sm">
                      <span className="text-green-600 font-medium">Enhanced:</span>{' '}
                      <span className="text-gray-900 font-medium">{enhancement.enhancedDescription}</span>
                      {enhancement.suggestedCategory && (
                        <span className="ml-2 px-2 py-0.5 bg-blue-100 text-blue-700 text-xs rounded">
                          {enhancement.suggestedCategory}
                        </span>
                      )}
                    </div>
                  )}

                  {!willEnhance && (
                    <div className="text-sm text-gray-400 italic">
                      Below threshold ({Math.round(minConfidenceScore * 100)}%) - will keep original
                    </div>
                  )}
                </div>
              </div>
            </div>
          );
        })}

        {importResult.enhancements.length > 15 && (
          <div className="text-center py-3 text-sm text-gray-500">
            ... and {importResult.enhancements.length - 15} more
          </div>
        )}
      </div>

      {/* Confidence threshold control */}
      <div className="mt-6 pt-6 border-t border-gray-200">
        <div className="flex items-center justify-between">
          <div>
            <label className="block text-sm font-medium text-gray-700">
              Confidence Threshold
            </label>
            <p className="text-xs text-gray-500">
              Only apply enhancements above this confidence
            </p>
          </div>
          <div className="flex items-center space-x-3">
            <span className="text-sm text-gray-600">{Math.round(minConfidenceScore * 100)}%</span>
            <input
              type="range"
              min="0.3"
              max="0.9"
              step="0.1"
              value={minConfidenceScore}
              onChange={(e) => setMinConfidenceScore(parseFloat(e.target.value))}
              className="w-24"
            />
          </div>
        </div>
      </div>
    </div>

    {/* Decision buttons */}
    <div className="flex items-center justify-between">
      <button
        onClick={handleClearFile}
        className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50"
      >
        Start Over
      </button>

      <div className="flex items-center space-x-3">
        <button
          onClick={() => handleEnhance(false)}
          disabled={isUploading}
          className="px-4 py-2 text-sm font-medium text-gray-700 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 disabled:opacity-50"
        >
          Skip Enhancement
        </button>
        <button
          onClick={() => handleEnhance(true)}
          disabled={isUploading}
          className="px-4 py-2 text-sm font-medium text-white bg-green-600 rounded-lg hover:bg-green-700 disabled:opacity-50"
        >
          {isUploading ? (
            <span className="flex items-center">
              <LoadingSpinner size="sm" />
              <span className="ml-2">Applying...</span>
            </span>
          ) : (
            'Apply Enhancements'
          )}
        </button>
      </div>
    </div>
  </div>
)}
```

## Step 22.10: Build Complete Step

*Create the completion states.*

```tsx
{/* Step 3: Complete */}
{currentStep === 'complete' && (
  <div className="bg-white rounded-xl border border-gray-200 p-8">
    <div className="text-center space-y-6">
      <div className="mx-auto w-16 h-16 bg-green-100 rounded-full flex items-center justify-center">
        <svg className="w-8 h-8 text-green-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
        </svg>
      </div>

      <div>
        <h3 className="text-lg font-semibold text-gray-900 mb-2">
          {enhanceResult?.enhancedCount ? 'Enhancement Complete!' : 'Import Complete!'}
        </h3>
        <p className="text-gray-600">
          {enhanceResult?.enhancedCount
            ? `Enhanced ${enhanceResult.enhancedCount} of ${enhanceResult.totalTransactions} transactions.`
            : 'Your transactions have been imported successfully.'}
        </p>
      </div>

      <button
        onClick={() => navigate('/transactions')}
        className="px-6 py-3 text-sm font-medium text-white bg-blue-600 rounded-lg hover:bg-blue-700"
      >
        View Transactions
      </button>
    </div>
  </div>
)}
```

## Step 22.11: Test the Complete Workflow

*Test your multi-step enhancement workflow.*

1. **Start both servers**:
   ```bash
   # Backend
   cd src/BudgetTracker.Api/
   dotnet run

   # Frontend
   cd src/BudgetTracker.Web/
   npm run dev
   ```

2. **Test the workflow**:
   - Upload a CSV file
   - Review the AI enhancements in the preview
   - Adjust the confidence threshold slider
   - Note how category badges appear next to suggestions
   - Try both "Apply Enhancements" and "Skip Enhancement"
   - Verify transactions are updated correctly

**You should see:**
- Step indicator showing progress
- Processing phases during upload
- Enhancement preview with original vs enhanced
- Category badges (Shopping, Food & Drink, etc.)
- Confidence scores with color coding
- Working threshold slider
- Both decision paths work correctly

---

## Troubleshooting

**No enhancements in preview:**
- Check backend logs for AI response
- Verify IChatClient is configured correctly
- Ensure import API returns enhancements array

**Categories not showing:**
- Check TransactionEnhancement type includes suggestedCategory
- Verify backend returns category in response

**Threshold slider not filtering:**
- Check minConfidenceScore state is updating
- Verify filter logic uses correct comparison

**Apply not working:**
- Check browser console for API errors
- Verify enhance endpoint is registered
- Check session hash matches between import and enhance

---

## Summary

You've successfully built:

- **Multi-Step Wizard**: Upload → Preview → Complete flow
- **Step Indicator**: Visual progress through the workflow
- **Enhancement Preview**: Original vs enhanced with confidence
- **Category Display**: Badges showing AI-suggested categories
- **Confidence Control**: Interactive threshold filtering
- **User Decision**: Apply or skip enhancements

**Key Features:**
- Transparent AI workflow
- User control over suggestions
- Confidence-based filtering
- Professional UI with progress feedback

Your budget tracker now has a complete AI-powered transaction enhancement system that respects user control and provides transparency into AI decisions.
