import { useEffect, useState } from 'react'
import DashboardPage from './components/DashboardPage'
import ProjectsPage from './components/ProjectsPage'
import ProjectCostsPage from './components/ProjectCostsPage'
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
import CorrectionsPage from './components/erp/CorrectionsPage'
import StockOperationsPage from './components/erp/StockOperationsPage'
import SupplierAccountsPage from './components/erp/SupplierAccountsPage'
import DocumentsPage from './components/erp/DocumentsPage'
import NotificationsPage from './components/erp/NotificationsPage'
import ReportsPage from './components/erp/ReportsPage'
import AuditPage from './components/erp/AuditPage'
import Icon from './components/ui/Icon'
import Brand from './components/ui/Brand'
import type { AuthUser } from './types/auth'
import { apiFetch, readApiResponse } from './api/apiFetch'
import './App.css'

type Page =
  | 'dashboard'
  | 'projects'
  | 'costs'
  | 'materials'
  | 'suppliers'
  | 'requests'
  | 'orders'
  | 'receipts'
  | 'inventory'
  | 'issues'
  | 'users'
  | 'profile'
  | 'corrections'
  | 'stock-operations'
  | 'accounts'
  | 'documents'
  | 'notifications'
  | 'reports'
  | 'audit'

const navigation: {
  page: Page
  label: string
  adminOnly?: boolean
  roles?: string[]
}[] = [
  { page: 'dashboard', label: 'Dashboard' },
  { page: 'projects', label: 'Projects' },
  { page: 'costs', label: 'Project Costs' },
  { page: 'materials', label: 'Materials' },
  { page: 'suppliers', label: 'Suppliers' },
  { page: 'requests', label: 'Material Requests' },
  { page: 'orders', label: 'Purchase Orders' },
  { page: 'receipts', label: 'Goods Receipts' },
  { page: 'inventory', label: 'Inventory' },
  { page: 'issues', label: 'Material Issues' },
  { page: 'corrections', label: 'Corrections' },
  { page: 'stock-operations', label: 'Stock Operations', roles: ['Administrator', 'Storekeeper'] },
  { page: 'accounts', label: 'Supplier Accounts', roles: ['Administrator', 'ProcurementOfficer'] },
  { page: 'documents', label: 'Documents' },
  { page: 'notifications', label: 'Notifications' },
  { page: 'reports', label: 'Reports & Exports' },
  { page: 'audit', label: 'Audit History', adminOnly: true },
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
  const [menuOpen, setMenuOpen] = useState(false)

  useEffect(() => {
    if (!menuOpen) return
    const sidebar = document.getElementById('workspace-sidebar')
    const previousFocus = document.querySelector<HTMLButtonElement>('[aria-label="Open navigation"]')
    const previousOverflow = document.body.style.overflow
    const mobile = window.matchMedia('(max-width: 1023px)')
    if (mobile.matches) {
      document.body.style.overflow = 'hidden'
      sidebar?.querySelector<HTMLButtonElement>('button')?.focus()
    }
    function closeOnEscape(event: KeyboardEvent) {
      if (event.key === 'Escape') setMenuOpen(false)
      if (event.key !== 'Tab' || !mobile.matches) return
      const buttons = sidebar?.querySelectorAll<HTMLButtonElement>('button:not(:disabled)')
      if (!buttons?.length) return
      const first = buttons[0]
      const last = buttons[buttons.length - 1]
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus() }
      if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus() }
    }
    function closeOnDesktop() { if (!mobile.matches) setMenuOpen(false) }
    window.addEventListener('keydown', closeOnEscape)
    mobile.addEventListener('change', closeOnDesktop)
    return () => {
      window.removeEventListener('keydown', closeOnEscape)
      mobile.removeEventListener('change', closeOnDesktop)
      document.body.style.overflow = previousOverflow
      previousFocus?.focus()
    }
  }, [menuOpen])

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
      <main className="app session-screen">
        <Brand />
        <div className="session-loader" role="status"><span className="loading-spinner" aria-hidden="true" />Checking your session...</div>
      </main>
    )
  }

  if (!user) {
    if (sessionError) {
      return (
        <main className="app session-error">
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
    item => (!item.adminOnly || isAdmin) && (!item.roles || item.roles.some(role => user.roles.includes(role))),
  )

  const groups = [
    { label: 'Workspace', pages: ['dashboard', 'projects', 'costs'] },
    { label: 'Materials & purchasing', pages: ['materials', 'suppliers', 'requests', 'orders', 'receipts'] },
    { label: 'Stock & finance', pages: ['inventory', 'issues', 'stock-operations', 'accounts', 'corrections'] },
    { label: 'Tools & administration', pages: ['documents', 'notifications', 'reports', 'audit', 'users', 'profile'] },
  ]
  const currentLabel = user.mustChangePassword ? 'My Profile' : navigation.find(item => item.page === page)?.label
  const initials = user.fullName.split(' ').filter(Boolean).slice(0, 2).map(word => word[0]).join('')

  return (
    <div className="app app-shell">
      <a href="#workspace-content" className="skip-link">Skip to content</a>
      {menuOpen && <button className="sidebar-backdrop" type="button" aria-label="Close navigation" onClick={() => setMenuOpen(false)} />}
      <aside id="workspace-sidebar" className={`app-sidebar ${menuOpen ? 'is-open' : ''}`}>
        <div className="sidebar-brand"><Brand /><button type="button" className="icon-button mobile-menu-button" aria-label="Close navigation" onClick={() => setMenuOpen(false)}><Icon name="close" /></button></div>
        {!user.mustChangePassword && <nav className="page-navigation" aria-label="Main navigation">
          {groups.map(group => {
            const items = visibleNavigation.filter(item => group.pages.includes(item.page))
            return items.length > 0 && <div className="nav-group" key={group.label}><p className="nav-group-label">{group.label}</p>{items.map(item => <button key={item.page} type="button" className={`nav-button ${page === item.page ? 'active' : ''}`} aria-current={page === item.page ? 'page' : undefined} onClick={() => { setPage(item.page); setMenuOpen(false) }}><Icon name={item.page} /><span>{item.label}</span>{page === item.page && <span className="nav-active-dot" />}</button>)}</div>
          })}
        </nav>}
        <div className="sidebar-footer"><span className="workspace-emblem"><Icon name="projects" /></span><div><strong>Construction ERP</strong><span>Your materials, connected.</span></div></div>
      </aside>
      <div className="workspace-main" inert={menuOpen ? true : undefined}>
      <header className="app-toolbar">
        <div className="flex min-w-0 items-center gap-3"><button type="button" className="icon-button mobile-menu-button" aria-label="Open navigation" aria-expanded={menuOpen} aria-controls="workspace-sidebar" onClick={() => setMenuOpen(true)}><Icon name="menu" /></button><div className="toolbar-breadcrumb"><span>Workspace</span><span aria-hidden="true">/</span><strong>{currentLabel}</strong></div></div>
        <div className="user-menu">
          {!user.mustChangePassword && <button type="button" className="icon-button notification-shortcut" title="Notifications" aria-label="Open notifications" onClick={() => setPage('notifications')}><Icon name="notifications" /></button>}
          <span className="user-avatar" aria-hidden="true">{initials}</span>
          <div className="user-details">
            <strong>{user.fullName}</strong>
            <p className="user-role">{user.roles.map(role => role.replace(/([a-z])([A-Z])/g, '$1 $2')).join(', ')}</p>
          </div>

          <button
            type="button"
            className="icon-button logout-button"
            title="Sign out"
            aria-label={loggingOut ? 'Signing out' : 'Sign out'}
            disabled={loggingOut}
            onClick={handleLogout}
          >
            <Icon name="logout" />
          </button>
        </div>
      </header>
      <main id="workspace-content" className="workspace-content" tabIndex={-1}>
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
          <div key={page} className="page-transition">
          {page === 'dashboard' && (
            <DashboardPage
              onNavigate={destination => setPage(destination)}
            />
          )}

          {page === 'projects' && <ProjectsPage />}
          {page === 'costs' && <ProjectCostsPage />}
          {page === 'materials' && <MaterialsPage />}
          {page === 'suppliers' && <SuppliersPage />}
          {page === 'requests' && <MaterialRequestsPage />}
          {page === 'orders' && <PurchaseOrdersPage />}
          {page === 'receipts' && <GoodsReceiptsPage />}
          {page === 'inventory' && <InventoryPage />}
          {page === 'issues' && <MaterialIssuesPage />}
          {page === 'corrections' && <CorrectionsPage user={user} />}
          {page === 'stock-operations' && user.roles.some(role => ['Administrator', 'Storekeeper'].includes(role)) && <StockOperationsPage />}
          {page === 'accounts' && user.roles.some(role => ['Administrator', 'ProcurementOfficer'].includes(role)) && <SupplierAccountsPage />}
          {page === 'documents' && <DocumentsPage user={user} />}
          {page === 'notifications' && <NotificationsPage user={user} onNavigate={destination => {
            const item = visibleNavigation.find(entry => entry.page === destination)
            if (item) setPage(item.page)
          }} />}
          {page === 'reports' && <ReportsPage />}
          {page === 'audit' && isAdmin && <AuditPage />}
          {page === 'users' && isAdmin && <UserManagementPage />}

          {page === 'profile' && (
            <ProfilePage
              user={user}
              onUpdated={setUser}
              onSignedOut={endSession}
            />
          )}
          </div>
        </>
      )}
      <footer className="workspace-footer"><span>SiteFlow <span aria-hidden="true">/</span> Materials management</span><span>Built for better site operations</span></footer>
      </main>
      </div>
    </div>
  )
}

export default App
