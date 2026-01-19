# Workshop Step 034: Multimodal Image Import UI

## Mission

In this step, you'll update the frontend to support image uploads and display appropriate progress indicators for image processing. Users will be able to upload bank statement images directly, with visual feedback showing the AI extraction process.

**Your goal**: Enhance the import UI to accept image files and provide appropriate visual feedback during image processing.

**Learning Objectives**:
- Updating file upload components to accept images
- Creating file type detection and routing
- Building progress phases specific to image processing
- Displaying file type icons and labels
- Handling image-specific error states

---

## Prerequisites

Before starting, ensure you completed:
- [033-image-import-backend.md](033-image-import-backend.md) - Image import backend

---

## Step 34.1: Update File Input to Accept Images

*Modify the file upload component to accept image files.*

Update `src/BudgetTracker.Web/src/features/transactions/components/FileUpload.tsx`:

```tsx
// Update the file input to accept images
<input
  ref={fileInputRef}
  type="file"
  accept=".csv,.png,.jpg,.jpeg"
  onChange={handleFileInputChange}
  className="hidden"
/>
```

## Step 34.2: Update File Validation

*Update the validateFile function to accept image files.*

```tsx
const validateFile = (file: File): string | null => {
  const validExtensions = ['.csv', '.png', '.jpg', '.jpeg'];
  const fileName = file.name.toLowerCase();

  if (!validExtensions.some(ext => fileName.endsWith(ext))) {
    return 'Please select a CSV file or bank statement image (PNG, JPG, JPEG)';
  }

  const maxSize = 10 * 1024 * 1024; // 10MB
  if (file.size > maxSize) {
    return 'File size must be less than 10MB';
  }

  return null;
};
```

## Step 34.3: Create File Type Helper Functions

*Add helper functions to detect file type and return appropriate icons/labels.*

```tsx
// Add these helper functions to FileUpload.tsx

const isImageFile = (fileName: string): boolean => {
  const name = fileName.toLowerCase();
  return name.endsWith('.png') || name.endsWith('.jpg') || name.endsWith('.jpeg');
};

const getFileTypeIcon = (fileName: string): string => {
  if (isImageFile(fileName)) {
    return '🖼️'; // Image icon
  }
  return '📊'; // CSV/spreadsheet icon
};

const getFileTypeLabel = (fileName: string): string => {
  if (isImageFile(fileName)) {
    return 'Bank Statement Image';
  }
  return 'CSV Bank Statement';
};
```

## Step 34.4: Update Progress Phases for Image Processing

*Add image-specific progress phases.*

```tsx
// Update the phase type to include extracting
type ImportPhase = 'uploading' | 'detecting' | 'parsing' | 'extracting' | 'enhancing' | 'complete';

const [currentPhase, setCurrentPhase] = useState<ImportPhase>('uploading');

// Update phase progression in handleImport
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

    // Determine if this is an image file
    const isImage = isImageFile(selectedFile.name);

    const result = await transactionsApi.importTransactions({
      formData,
      onUploadProgress: (progressEvent) => {
        const progress = Math.round((progressEvent.loaded * 100) / progressEvent.total);
        setUploadProgress(progress);

        // Update phase based on progress and file type
        if (progress < 20) {
          setCurrentPhase('uploading');
        } else if (progress < 60) {
          setCurrentPhase(isImage ? 'extracting' : 'detecting');
        } else if (progress < 85 && !isImage) {
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

    showSuccess(
      `Successfully imported ${result.importedCount} transactions from ${getFileTypeLabel(selectedFile.name).toLowerCase()}`
    );
  } catch (error: any) {
    console.error('Import error:', error);
    const errorMessage = error?.message || 'Failed to import the file';
    showError('Import Failed', errorMessage);
  } finally {
    setIsUploading(false);
    setUploadProgress(0);
  }
};
```

## Step 34.5: Update Detection Progress Indicator for Images

*Modify the progress indicator to show appropriate phases for image processing.*

Update `src/BudgetTracker.Web/src/features/transactions/components/DetectionProgressIndicator.tsx`:

```tsx
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
```

## Step 34.6: Update File Selection Display

*Show file type information when a file is selected.*

```tsx
{selectedFile && (
  <div className="mt-4 p-4 bg-gray-50 rounded-lg border border-gray-200">
    <div className="flex items-center justify-between">
      <div className="flex items-center gap-3">
        <span className="text-2xl">{getFileTypeIcon(selectedFile.name)}</span>
        <div>
          <p className="text-sm font-medium text-gray-900">{selectedFile.name}</p>
          <p className="text-xs text-gray-500">
            {getFileTypeLabel(selectedFile.name)} • {(selectedFile.size / 1024).toFixed(1)} KB
          </p>
        </div>
      </div>
      <button
        type="button"
        onClick={handleClearFile}
        className="text-gray-400 hover:text-gray-600"
      >
        <svg className="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor">
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
        </svg>
      </button>
    </div>

    {isImageFile(selectedFile.name) && (
      <div className="mt-3 p-2 bg-blue-50 rounded text-xs text-blue-700">
        <strong>Tip:</strong> For best results, ensure the image clearly shows transaction details including dates, descriptions, and amounts.
      </div>
    )}
  </div>
)}
```

## Step 34.7: Update Dropzone Text

*Update the dropzone to mention image support.*

```tsx
<div className="text-center">
  <svg className="mx-auto h-12 w-12 text-gray-400" stroke="currentColor" fill="none" viewBox="0 0 48 48">
    <path d="M28 8H12a4 4 0 00-4 4v20m32-12v8m0 0v8a4 4 0 01-4 4H12a4 4 0 01-4-4v-4m32-4l-3.172-3.172a4 4 0 00-5.656 0L28 28M8 32l9.172-9.172a4 4 0 015.656 0L28 28m0 0l4 4m4-24h8m-4-4v8m-12 4h.02" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
  </svg>
  <p className="mt-2 text-sm text-gray-600">
    <span className="font-medium text-blue-600 hover:text-blue-500 cursor-pointer">
      Click to upload
    </span>
    {' or drag and drop'}
  </p>
  <p className="mt-1 text-xs text-gray-500">
    CSV files or bank statement images (PNG, JPG)
  </p>
</div>
```

## Step 34.8: Handle Image-Specific Errors

*Add error handling for image-related issues.*

```tsx
const getErrorMessage = (error: any, fileName: string): string => {
  const message = error?.message || 'Failed to import the file';
  const isImage = isImageFile(fileName);

  if (isImage) {
    if (message.includes('confidence')) {
      return 'The image quality may be too low to extract transactions accurately. Try a clearer image.';
    }
    if (message.includes('processing error')) {
      return 'Could not process the bank statement image. Ensure it shows a clear list of transactions.';
    }
  }

  if (message.includes('Unable to automatically detect CSV structure')) {
    return 'Could not detect the CSV format. Please ensure your file has clear column headers.';
  }

  return message;
};

// Use in catch block:
catch (error: any) {
  console.error('Import error:', error);
  const errorMessage = getErrorMessage(error, selectedFile?.name || '');
  showError('Import Failed', errorMessage);
}
```

---

## Testing

### Test Image Upload Display

1. Select an image file (PNG or JPG)
2. Verify:
   - Image icon (🖼️) is displayed
   - "Bank Statement Image" label is shown
   - Tip about image quality appears

### Test Image Processing Progress

1. Upload a bank statement image
2. Verify:
   - Phase shows "Extracting transactions from image using AI..."
   - Spinner animation appears during extraction
   - Success message mentions image import

### Test CSV vs Image Distinction

1. Upload a CSV file, note the UI behavior
2. Upload an image file, compare:
   - Different icons and labels
   - Different progress phase text
   - Both complete successfully

### Test Error Handling

1. Upload a very small/blurry image
2. Verify appropriate error message about image quality

---

## Summary

You've successfully implemented:

- **File Type Support**: Upload accepts both CSV and image files
- **Visual Distinction**: Different icons and labels for file types
- **Progress Phases**: Image-specific extraction phase
- **User Guidance**: Tips for image quality
- **Error Handling**: Image-specific error messages

**Week 4 Complete!** You now have a budget tracker that can import transactions from any CSV format worldwide and from bank statement images.
