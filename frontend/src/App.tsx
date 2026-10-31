import { useEffect, useState } from 'react'
import DashboardPage from './components/DashboardPage'
import ProjectsPage from './components/ProjectsPage'
import MaterialsPage from './components/MaterialsPage'
import SuppliersPage from './components/SuppliersPage'
import MaterialRequestsPage from './components/MaterialRequestsPage'
import PurchaseOrdersPage from './components/PurchaseOrdersPage'
import GoodsReceiptsPage from './components/GoodsReceiptsPage'
import InventoryPage from './components/InventoryPage'
import MaterialIssuesPage from './components/MaterialIssuesPage'
import UserManagementPage from './components/UserManagementPage'
import ProfilePage from './components/ProfilePage'
import LoginPage from './components/LoginPage'
import type { AuthUser } from './types/auth'
import { apiFetch, readApiResponse } from './api/apiFetch'
import './App.css'

type Page =
  | 'dashboard'
  | 'projects'
  | 'materials'
  | 'suppliers'
  | 'requests'
  | 'orders'
  | 'receipts'
  | 'inventory'
  | 'issues'
  | 'users'
  | 'profile'

const navigation: {
  page: Page
  label: string
  adminOnly?: boolean
}[] = [
  { page: 'dashboard', label: 'Dashboard' },
  { page: 'projects', label: 'Projects' },
  { page: 'materials', label: 'Materials' },
  { page: 'suppliers', label: 'Suppliers' },
  { page: 'requests', label: 'Material Requests' },
  { page: 'orders', label: 'Purchase Orders' },
  { page: 'receipts', label: 'Goods Receipts' },
  { page: 'inventory', label: 'Inventory' },
  { page: 'issues', label: 'Material Issues' },
  { page: 'users', label: 'User Management', adminOnly: true },
  { page: 'profile', label: 'My Profile' },
]

function App() {
  const [user, setUser] = useState<AuthUser | null>(null)
  const [checkingSession, setCheckingSession] = useState(true)
  const [sessionError, setSessionError] = useState('')
  const [loginNotice, setLoginNotice] = useState('')
  const [page, setPage] = useState<Page>('dashboard')
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
        if (!controller.signal.aborted) {
          setSessionError(
            error instanceof Error
              ? error.message
              : 'Unable to connect to the server.',
          )
        }
      } finally {
        if (!controller.signal.aborted) {
          setCheckingSession(false)
        }
      }
    }

    void checkSession()

    return () => controller.abort()
  }, [])

  function endSession(message: string) {
    setUser(null)
    setPage('dashboard')
    setSessionError('')
    setLoginNotice(message)
  }

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

      endSession('')
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
      <>
        {loginNotice && (
          <p className="profile-login-notice" role="status">
            {loginNotice}
          </p>
        )}

        <LoginPage
          onLoggedIn={currentUser => {
            setSessionError('')
            setLoginNotice('')
            setPage('dashboard')
            setUser(currentUser)
          }}
        />
      </>
    )
  }

  const isAdmin = user.roles.includes('Administrator')

  const visibleNavigation = navigation.filter(
    item => !item.adminOnly || isAdmin,
  )

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

      {user.mustChangePassword ? (
        <ProfilePage
          user={user}
          onUpdated={setUser}
          onSignedOut={endSession}
        />
      ) : (
        <>
          <nav
            className="page-navigation"
            aria-label="Main navigation"
          >
            {visibleNavigation.map(item => (
              <button
                key={item.page}
                type="button"
                className={
                  page === item.page
                    ? 'nav-button active'
                    : 'nav-button'
                }
                aria-current={
                  page === item.page ? 'page' : undefined
                }
                onClick={() => setPage(item.page)}
              >
                {item.label}
              </button>
            ))}
          </nav>

          {page === 'dashboard' && (
            <DashboardPage
              onNavigate={destination => setPage(destination)}
            />
          )}

          {page === 'projects' && <ProjectsPage />}
          {page === 'materials' && <MaterialsPage />}
          {page === 'suppliers' && <SuppliersPage />}
          {page === 'requests' && <MaterialRequestsPage />}
          {page === 'orders' && <PurchaseOrdersPage />}
          {page === 'receipts' && <GoodsReceiptsPage />}
          {page === 'inventory' && <InventoryPage />}
          {page === 'issues' && <MaterialIssuesPage />}
          {page === 'users' && isAdmin && <UserManagementPage />}

          {page === 'profile' && (
            <ProfilePage
              user={user}
              onUpdated={setUser}
              onSignedOut={endSession}
            />
          )}
        </>
      )}
    </main>
  )
}

export default App