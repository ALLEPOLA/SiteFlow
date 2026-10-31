import { useEffect, useState } from 'react'
import ProjectsPage from './components/ProjectsPage'
import MaterialsPage from './components/MaterialsPage'
import SuppliersPage from './components/SuppliersPage'
import MaterialRequestsPage from './components/MaterialRequestsPage'
import PurchaseOrdersPage from './components/PurchaseOrdersPage'
import GoodsReceiptsPage from './components/GoodsReceiptsPage'
import InventoryPage from './components/InventoryPage'
import MaterialIssuesPage from './components/MaterialIssuesPage'
import LoginPage from './components/LoginPage'
import type { AuthUser } from './types/auth'
import { apiFetch, readApiResponse } from './api/apiFetch'
import './App.css'

type Page =
  | 'projects'
  | 'materials'
  | 'suppliers'
  | 'requests'
  | 'orders'
  | 'receipts'
  | 'inventory'
  | 'issues'

const navigation: { page: Page; label: string }[] = [
  { page: 'projects', label: 'Projects' },
  { page: 'materials', label: 'Materials' },
  { page: 'suppliers', label: 'Suppliers' },
  { page: 'requests', label: 'Material Requests' },
  { page: 'orders', label: 'Purchase Orders' },
  { page: 'receipts', label: 'Goods Receipts' },
  { page: 'inventory', label: 'Inventory' },
  { page: 'issues', label: 'Material Issues' },
]

function App() {
  const [user, setUser] = useState<AuthUser | null>(null)
  const [checkingSession, setCheckingSession] = useState(true)
  const [sessionError, setSessionError] = useState('')
  const [page, setPage] = useState<Page>('projects')
  const [loggingOut, setLoggingOut] = useState(false)

  useEffect(() => {
    const controller = new AbortController()

    async function checkSession() {
      try {
        const response = await apiFetch('/api/auth/me', {
          signal: controller.signal,
          cache: 'no-store',
        })

        if (response.status === 401) return

        const currentUser = await readApiResponse<AuthUser>(response)

        if (!controller.signal.aborted) {
          setUser(currentUser)
        }
      } catch (error) {
        if (controller.signal.aborted) return

        setSessionError(
          error instanceof Error
            ? error.message
            : 'Unable to connect to the server.',
        )
      } finally {
        if (!controller.signal.aborted) {
          setCheckingSession(false)
        }
      }
    }

    void checkSession()

    return () => controller.abort()
  }, [])

  async function handleLogout() {
    if (loggingOut) return

    setLoggingOut(true)
    setSessionError('')

    try {
      const response = await apiFetch('/api/auth/logout', {
        method: 'POST',
      })

      if (!response.ok && response.status !== 401) {
        await readApiResponse<unknown>(response)
      }

      setUser(null)
      setPage('projects')
    } catch (error) {
      setSessionError(
        error instanceof Error
          ? error.message
          : 'Unable to sign out.',
      )
    } finally {
      setLoggingOut(false)
    }
  }

  if (checkingSession) {
    return (
      <main className="app">
        <p role="status">Checking your session...</p>
      </main>
    )
  }

  if (!user) {
    if (sessionError) {
      return (
        <main className="app">
          <p className="error-message" role="alert">
            {sessionError}
          </p>

          <button
            type="button"
            className="primary-button"
            onClick={() => window.location.reload()}
          >
            Retry
          </button>
        </main>
      )
    }

    return (
      <LoginPage
        onLoggedIn={currentUser => {
          setSessionError('')
          setUser(currentUser)
        }}
      />
    )
  }

  return (
    <main className="app">
      <header className="app-toolbar">
        <p className="brand">SiteFlow ERP</p>

        <div className="user-menu">
          <div>
            <strong>{user.fullName}</strong>
            <p className="user-role">{user.roles.join(', ')}</p>
          </div>

          <button
            type="button"
            className="secondary-button"
            disabled={loggingOut}
            onClick={handleLogout}
          >
            {loggingOut ? 'Signing out...' : 'Logout'}
          </button>
        </div>
      </header>

      {sessionError && (
        <p className="error-message" role="alert">
          {sessionError}
        </p>
      )}

      <nav
        className="page-navigation"
        aria-label="Main navigation"
      >
        {navigation.map(item => (
          <button
            key={item.page}
            type="button"
            className={
              page === item.page
                ? 'nav-button active'
                : 'nav-button'
            }
            aria-current={page === item.page ? 'page' : undefined}
            onClick={() => setPage(item.page)}
          >
            {item.label}
          </button>
        ))}
      </nav>

      {page === 'projects' && <ProjectsPage />}
      {page === 'materials' && <MaterialsPage />}
      {page === 'suppliers' && <SuppliersPage />}
      {page === 'requests' && <MaterialRequestsPage />}
      {page === 'orders' && <PurchaseOrdersPage />}
      {page === 'receipts' && <GoodsReceiptsPage />}
      {page === 'inventory' && <InventoryPage />}
      {page === 'issues' && <MaterialIssuesPage />}
    </main>
  )
}

export default App