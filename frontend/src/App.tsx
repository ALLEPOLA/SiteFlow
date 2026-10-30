import { useEffect, useState } from 'react'
import ProjectsPage from './components/ProjectsPage'
import MaterialsPage from './components/MaterialsPage'
import SuppliersPage from './components/SuppliersPage'
import MaterialRequestsPage from './components/MaterialRequestsPage'
import LoginPage from './components/LoginPage'
import type { AuthUser } from './types/auth'
import './App.css'

type Page = 'projects' | 'materials' | 'suppliers' | 'requests'

const navigation: { page: Page; label: string }[] = [
  { page: 'projects', label: 'Projects' },
  { page: 'materials', label: 'Materials' },
  { page: 'suppliers', label: 'Suppliers' },
  { page: 'requests', label: 'Material Requests' },
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
        const response = await fetch('/api/auth/me', {
          signal: controller.signal,
          cache: 'no-store',
        })

        if (response.status === 401) {
          return
        }

        if (!response.ok) {
          throw new Error('Unable to check your session.')
        }

        const currentUser: AuthUser = await response.json()

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
      // Get a fresh token for the currently authenticated user.
      const csrfResponse = await fetch('/api/auth/csrf', {
        cache: 'no-store',
      })

      if (!csrfResponse.ok) {
        throw new Error('Unable to initialize logout.')
      }

      const csrf: { token: string } = await csrfResponse.json()

      const response = await fetch('/api/auth/logout', {
        method: 'POST',
        headers: {
          'X-CSRF-TOKEN': csrf.token,
        },
      })

      if (!response.ok && response.status !== 401) {
        throw new Error('Unable to sign out. Please try again.')
      }

      setUser(null)
      setPage('projects')
    } catch (error) {
      setSessionError(
        error instanceof Error ? error.message : 'Logout failed.',
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
            {sessionError} Check that the backend is running.
          </p>
          <button
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
        onLoggedIn={(currentUser) => {
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

      <nav className="page-navigation" aria-label="Main navigation">
        {navigation.map((item) => (
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
    </main>
  )
}

export default App