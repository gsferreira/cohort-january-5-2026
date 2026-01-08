import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate } from 'react-router';
import { useToast } from '../../../shared/contexts/ToastContext';
import { apiClient } from '../../../api';
import { transactionsApi } from '../api';
import type { ImportResult } from '../types';
import { LoadingSpinner } from '../../../shared/components/LoadingSpinner';

interface FileUploadProps {
  className?: string;
}

function FileUpload({ className = '' }: FileUploadProps) {
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [isDragOver, setIsDragOver] = useState(false);
  const [isUploading, setIsUploading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [account, setAccount] = useState('');
  const [importResult, setImportResult] = useState<ImportResult | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const navigate = useNavigate();
  const { showSuccess, showError } = useToast();

  const MAX_FILE_SIZE = 10 * 1024 * 1024; // 10MB

  useEffect(() => {
    // Fetch XSRF token when component mounts to enable API calls
    const fetchXsrfToken = async () => {
      try {
        await apiClient.get('/antiforgery/token');
      } catch (error) {
        console.error('Failed to fetch XSRF token:', error);
      }
    };
    fetchXsrfToken();
  }, []);

  const validateFile = (file: File): string | null => {
    const fileName = file.name.toLowerCase();

    if (!fileName.endsWith('.csv')) {
      return 'Please select a CSV file';
    }
    if (file.size > MAX_FILE_SIZE) {
      return 'File size must be less than 10MB';
    }
    return null;
  };

  const handleFileSelect = useCallback((file: File) => {
    const error = validateFile(file);
    if (error) {
      showError('Invalid File', error);
      return;
    }
    setSelectedFile(file);
    setImportResult(null);
  }, [showError]);

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

    try {
      const formData = new FormData();
      formData.append('file', selectedFile);
      formData.append('account', account.trim());

      const result = await transactionsApi.importTransactions({
        formData,
        onUploadProgress: (progressEvent) => {
          const progress = Math.round((progressEvent.loaded * 100) / progressEvent.total);
          setUploadProgress(progress);
        }
      });

      setImportResult(result);
      showSuccess(`Successfully imported ${result.importedCount} transactions`);

      // Clear form and redirect after a delay
      setTimeout(() => {
        setSelectedFile(null);
        setAccount('');
        if (fileInputRef.current) {
          fileInputRef.current.value = '';
        }
        navigate('/transactions');
      }, 3000);

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
    }
  }, [selectedFile, account, showError, showSuccess, navigate]);

  const handleClearFile = useCallback(() => {
    setSelectedFile(null);
    setImportResult(null);
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  }, []);

  const formatFileSize = (bytes: number): string => {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  };

  return (
    <div className={`space-y-6 ${className}`}>
      {/* Upload Area */}
      <div
        className={`relative border-2 border-dashed rounded-lg p-8 text-center transition-colors ${
          isDragOver
            ? 'border-blue-400 bg-blue-50'
            : 'border-gray-300 hover:border-gray-400'
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
          <div className={`mx-auto h-12 w-12 ${isDragOver ? 'text-blue-500' : 'text-gray-400'}`}>
            <svg fill="none" stroke="currentColor" viewBox="0 0 24 24" strokeWidth={1.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5m-13.5-9L12 3m0 0l4.5 4.5M12 3v13.5" />
            </svg>
          </div>

          <div>
            <p className="text-lg font-medium text-gray-900">
              {selectedFile ? 'File Selected' : 'Upload CSV File'}
            </p>
            <p className="text-sm text-gray-600">
              {selectedFile
                ? `${selectedFile.name} (${formatFileSize(selectedFile.size)})`
                : 'Drag and drop your CSV file here, or click to browse'
              }
            </p>
          </div>

          {!selectedFile && (
            <button
              onClick={handleBrowseClick}
              className="inline-flex items-center px-4 py-2 border border-transparent text-sm font-medium rounded-md text-white bg-blue-600 hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500"
            >
              Browse Files
            </button>
          )}
        </div>
      </div>

      {/* File Details and Upload */}
      {selectedFile && (
        <div className="bg-gray-50 rounded-lg p-4">
          <div className="space-y-4">
            <div className="flex items-center space-x-3">
              <div className="flex-shrink-0">
                <span className="text-green-600 text-lg">📊</span>
              </div>
              <div>
                <p className="text-sm font-medium text-gray-900">{selectedFile.name}</p>
                <p className="text-xs text-gray-600">CSV File • {formatFileSize(selectedFile.size)}</p>
              </div>
            </div>

            <div>
              <label htmlFor="account" className="block text-sm font-medium text-gray-700 mb-1">
                Account Name
              </label>
              <input
                type="text"
                id="account"
                value={account}
                onChange={(e) => setAccount(e.target.value)}
                placeholder="Enter account name (e.g. Checking, Savings)"
                className="w-full px-3 py-2 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
              />
            </div>

            <div className="flex items-center justify-between">
              <button
                onClick={handleClearFile}
                disabled={isUploading}
                className="px-3 py-2 border border-gray-300 text-sm font-medium rounded-md text-gray-700 bg-white hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 disabled:opacity-50"
              >
                Remove
              </button>
              <button
                onClick={handleImport}
                disabled={isUploading || !account.trim()}
                className="inline-flex items-center px-4 py-2 border border-transparent text-sm font-medium rounded-md text-white bg-blue-600 hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-offset-2 focus:ring-blue-500 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {isUploading ? (
                  <>
                    <LoadingSpinner size="sm" />
                    <span className="ml-2">Uploading...</span>
                  </>
                ) : (
                  'Import Transactions'
                )}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Upload Progress */}
      {isUploading && uploadProgress > 0 && (
        <div className="bg-blue-50 rounded-lg p-4">
          <div className="flex items-center justify-between mb-2">
            <span className="text-sm font-medium text-blue-900">Uploading...</span>
            <span className="text-sm text-blue-600">{uploadProgress}%</span>
          </div>
          <div className="w-full bg-blue-200 rounded-full h-2">
            <div
              className="bg-blue-600 h-2 rounded-full transition-all duration-300"
              style={{ width: `${uploadProgress}%` }}
            ></div>
          </div>
        </div>
      )}

      {/* Import Result */}
      {importResult && (
        <div className="bg-green-50 border border-green-200 rounded-lg p-4">
          <div className="flex">
            <div className="flex-shrink-0">
              <span className="text-green-600 text-xl">✓</span>
            </div>
            <div className="ml-3">
              <h3 className="text-sm font-medium text-green-800">Import Successful</h3>
              <div className="mt-2 text-sm text-green-700">
                <p>
                  Successfully imported {importResult.importedCount} transactions
                  {importResult.failedCount > 0 && ` (${importResult.failedCount} failed)`}
                </p>
                {importResult.errors.length > 0 && (
                  <div className="mt-2">
                    <p className="font-medium">Errors:</p>
                    <ul className="list-disc list-inside">
                      {importResult.errors.slice(0, 5).map((error: string, index: number) => (
                        <li key={index}>{error}</li>
                      ))}
                      {importResult.errors.length > 5 && (
                        <li>... and {importResult.errors.length - 5} more errors</li>
                      )}
                    </ul>
                  </div>
                )}
              </div>
              <p className="mt-2 text-xs text-green-600">Redirecting to transactions list...</p>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default FileUpload;