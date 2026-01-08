import FileUpload from '../features/transactions/components/FileUpload';

export default function ImportPage() {
  return (
    <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
      <div className="mb-8">
        <h1 className="text-3xl font-bold text-gray-900">Import Transactions</h1>
        <p className="mt-2 text-gray-600">
          Upload your bank statement CSV file or image to import transactions with AI-powered enhancements.
        </p>
      </div>

      <FileUpload />
    </div>
  );
}