import { useState } from 'react'
import ProjectsPage from './components/ProjectsPage'
import MaterialsPage from './components/MaterialsPage'
import SuppliersPage from './components/SuppliersPage'
import MaterialRequestsPage from './components/MaterialRequestsPage'
import './App.css'

type Page = 'projects' | 'materials' | 'suppliers' | 'requests'

const navigation: { page: Page; label: string }[] = [
  { page: 'projects', label: 'Projects' },
  { page: 'materials', label: 'Materials' },
  { page: 'suppliers', label: 'Suppliers' },
  { page: 'requests', label: 'Material Requests' },
]

function App() {
  const [page, setPage] = useState<Page>('projects')

  return (
    <main className="app">
      <p className="brand">SiteFlow ERP</p>

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