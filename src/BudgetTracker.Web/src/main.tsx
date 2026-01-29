import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { createBrowserRouter, RouterProvider } from 'react-router-dom'
import { Login, Register } from './features/auth'
import ErrorBoundary from './shared/components/ErrorBoundary'
import ToastContainer from './shared/components/ToastContainer'
import { ToastProvider } from './shared/contexts/ToastContext'
import './index.css'
import { authLoader } from './routes/authLoader'
import Dashboard from './routes/dashboard'
import Import from './routes/import'
import Root from './routes/root'
import Transactions, { loader as transactionsLoader } from './routes/transactions'

const router = createBrowserRouter([
  {
    path: '/login',
    element: <Login />,
  },
  {
    path: '/register',
    element: <Register />,
  },
  {
    path: '/',
    element: <Root />,
    errorElement: <ErrorBoundary><div /></ErrorBoundary>,
    loader: authLoader,
    children: [
      {
        index: true,
        element: <Dashboard />,
      },
      {
        path: 'dashboard',
        element: <Dashboard />,
      },
      {
        path: 'transactions',
        element: <Transactions />,
        loader: transactionsLoader,
      },
      {
        path: 'import',
        element: <Import />,
      },
    ],
  },
], {
  future: {
    v7_partialHydration: true,
  },
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ToastProvider>
      <RouterProvider router={router} />
      <ToastContainer />
    </ToastProvider>
  </StrictMode>,
)