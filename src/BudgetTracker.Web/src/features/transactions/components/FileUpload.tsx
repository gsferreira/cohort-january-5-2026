import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router';
import { useToast } from '../../../shared/contexts/ToastContext';
import { apiClient } from '../../../api';
import { transactionsApi } from '../api';
import type { ImportResult, EnhanceImportResult } from '../types';
import { LoadingSpinner } from '../../../shared/components/LoadingSpinner';
import { DetectionProgressIndicator } from './DetectionProgressIndicator';
import { DetectionMethodBadge } from './DetectionMethodBadge';

interface FileUploadProps {
  className?: string;
}

type Step = 'upload' | 'preview' | 'complete';
type ImportPhase = 'uploading' | 'detecting' | 'parsing' | 'extracting' | 'enhancing' | 'complete';

const isImageFile = (fileName: string): boolean => {
  const name = fileName.toLowerCase();
  return name.endsWith('.png') || name.endsWith('.jpg') || name.endsWith('.jpeg');
};

const getFileTypeIcon = (fileName: string): string => {
  if (isImageFile(fileName)) {
    return '🖼️';
  }
  return '📊';
};

const getFileTypeLabel = (fileName: string): string => {
  if (isImageFile(fileName)) {
    return 'Bank Statement Image';
  }
  return 'CSV Bank Statement';
};

const getErrorMessage = (error: unknown, fileName: string): string => {
  const message = error && typeof error === 'object' && 'message' in error
    ? (error as Error).message
    : 'Failed to import the file';
  const isImage = isImageFile(fileName);

  if (isImage) {
    if (message.includes('confidence')) {
      return 'The image quality may be too low to extract transactions accurately. Try a clearer image.';
    }
    if (message.includes('processing error')) {
      return 'Could not process the bank statement image. Ensure it shows a clear list of transactions.';
    }
  }

  if (message.includes('Unable to detect CSV structure')) {
    return 'Could not detect the CSV format. Please ensure your file has clear column headers.';
  }

  return message;
};

function FileUpload({ className = '' }: FileUploadProps) {
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [isDragOver, setIsDragOver] = useState(false);
  const [isUploading, setIsUploading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [account, setAccount] = useState('');
  const [currentStep, setCurrentStep] = useState<Step>('upload');
  const [importResult, setImportResult] = useState<ImportResult | null>(null);
  const [minConfidenceScore, setMinConfidenceScore] = useState(0.7);
  const [enhanceResult, setEnhanceResult] = useState<EnhanceImportResult | null>(null);
  const [currentPhase, setCurrentPhase] = useState<ImportPhase>('uploading');
  const fileInputRef = useRef<HTMLInputElement>(null);
  const navigate = useNavigate();
  const { showSuccess, showError } = useToast();

  const MAX_FILE_SIZE = 10 * 1024 * 1024; // 10MB

  useEffect(() => {
    const fetchXsrfToken = async () => {
      try {
        await apiClient.get('/antiforgery/token');
      } catch (error) {
        console.error('Failed to fetch XSRF token:', error);
      }
    };
    fetchXsrfToken();
  }, []);

  const validateFile = useCallback((file: File): string | null => {
    const validExtensions = ['.csv', '.png', '.jpg', '.jpeg'];
    const fileName = file.name.toLowerCase();

    if (!validExtensions.some(ext => fileName.endsWith(ext))) {
      return 'Please select a CSV file or bank statement image (PNG, JPG, JPEG)';
    }

    if (file.size > MAX_FILE_SIZE) {
      return 'File size must be less than 10MB';
    }
    return null;
  }, []);

  const handleFileSelect = useCallback((file: File) => {
    const error = validateFile(file);
    if (error) {
      showError('Invalid File', error);
      return;
    }
    setSelectedFile(file);
    setImportResult(null);
  }, [showError, validateFile]);

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDragOver(true);
  }, []);

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDragOver(false);
  }, []);

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDragOver(false);
    const files = Array.from(e.dataTransfer.files);
    if (files.length > 0) {
      handleFileSelect(files[0]);
    }
  }, [handleFileSelect]);

  const handleFileInputChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files;
    if (files && files.length > 0) {
      handleFileSelect(files[0]);
    }
  }, [handleFileSelect]);

  const handleBrowseClick = useCallback(() => {
    fileInputRef.current?.click();
  }, []);

  const handleImport = useCallback(async () => {
    if (!selectedFile || !account.trim()) {
      return;
    }

    setIsUploading(true);
    setUploadProgress(0);
    setImportResult(null);
    setCurrentPhase('uploading');

    const isImage = isImageFile(selectedFile.name);

    try {
      const formData = new FormData();
      formData.append('file', selectedFile);
      formData.append('account', account.trim());

      const result = await transactionsApi.importTransactions({
        formData,
        onUploadProgress: (progressEvent) => {
          const progress = Math.round((progressEvent.loaded * 100) / progressEvent.total);
          setUploadProgress(progress);

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
      setCurrentStep('preview');

      showSuccess(
        `Successfully imported ${result.importedCount} transactions from ${getFileTypeLabel(selectedFile.name).toLowerCase()}`
      );
    } catch (error) {
      console.error('Import error:', error);
      const errorMessage = getErrorMessage(error, selectedFile.name);
      showError('Import Failed', errorMessage);
    } finally {
      setIsUploading(false);
      setUploadProgress(0);
      setCurrentPhase('uploading');
    }
  }, [selectedFile, account, showError, showSuccess]);

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

      {/* Step 1: Upload */}
      {currentStep === 'upload' && (
        <>
          {/* Drag-and-drop area */}
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
              accept=".csv,.png,.jpg,.jpeg"
              onChange={handleFileInputChange}
              className="hidden"
            />

            <div className="space-y-4">
              {selectedFile ? (
                <>
                  <div className="text-4xl">{getFileTypeIcon(selectedFile.name)}</div>
                  <div>
                    <p className="text-lg font-medium text-gray-900">{selectedFile.name}</p>
                    <p className="text-sm text-gray-500">
                      {getFileTypeLabel(selectedFile.name)} • {(selectedFile.size / 1024).toFixed(1)} KB
                    </p>
                  </div>
                  {isImageFile(selectedFile.name) && (
                    <div className="mt-2 p-2 bg-blue-50 rounded text-xs text-blue-700 max-w-md mx-auto">
                      <strong>Tip:</strong> For best results, ensure the image clearly shows transaction details including dates, descriptions, and amounts.
                    </div>
                  )}
                </>
              ) : (
                <>
                  <div className="mx-auto h-12 w-12 text-gray-400">
                    <svg fill="none" stroke="currentColor" viewBox="0 0 48 48" strokeWidth={1.5}>
                      <path d="M28 8H12a4 4 0 00-4 4v20m32-12v8m0 0v8a4 4 0 01-4 4H12a4 4 0 01-4-4v-4m32-4l-3.172-3.172a4 4 0 00-5.656 0L28 28M8 32l9.172-9.172a4 4 0 015.656 0L28 28m0 0l4 4m4-24h8m-4-4v8m-12 4h.02" strokeLinecap="round" strokeLinejoin="round" />
                    </svg>
                  </div>
                  <div>
                    <p className="text-lg font-medium text-gray-900">Upload Bank Statement</p>
                    <p className="text-sm text-gray-500">Drag and drop or click to browse</p>
                    <p className="mt-1 text-xs text-gray-400">
                      CSV files or bank statement images (PNG, JPG)
                    </p>
                  </div>
                  <button
                    onClick={handleBrowseClick}
                    className="px-4 py-2 text-sm font-medium text-white bg-blue-600 rounded-lg hover:bg-blue-700"
                  >
                    Browse Files
                  </button>
                </>
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
                         currentPhase === 'detecting' ? 'Detecting...' :
                         currentPhase === 'parsing' ? 'Parsing...' :
                         currentPhase === 'extracting' ? 'Extracting...' :
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
          {isUploading && uploadProgress > 0 && selectedFile && (
            <DetectionProgressIndicator
              progress={uploadProgress}
              currentPhase={currentPhase}
              fileName={selectedFile.name}
            />
          )}
        </>
      )}

      {/* Step 2: Preview Enhancements */}
      {currentStep === 'preview' && importResult && (
        <div className="space-y-6">
          <div className="bg-white rounded-xl border border-gray-200 p-6">
            {/* Detection info */}
            {importResult.detectionMethod && (
              <div className="flex items-center justify-between p-3 bg-gray-50 rounded-lg mb-6">
                <span className="text-sm text-gray-600">CSV Structure Detection</span>
                <DetectionMethodBadge
                  method={importResult.detectionMethod}
                  confidence={importResult.detectionConfidence !== undefined ? importResult.detectionConfidence * 100 : undefined}
                />
              </div>
            )}

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
    </div>
  );
}

export default FileUpload;
