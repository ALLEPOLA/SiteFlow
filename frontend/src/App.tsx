import { useState } from 'react'
import ProjectsPage from './components/ProjectsPage'
import MaterialsPage from './components/MaterialsPage'
import './App.css'

function App() {
  const [page, setPage] = useState<'projects' | 'materials'>('projects')

  return (
    <main className="app">
      <p className="brand">SiteFlow ERP</p>

      <nav className="page-navigation" aria-label="Main navigation">
        <button
          type="button"
          className={page === 'projects' ? 'nav-button active' : 'nav-button'}
          aria-current={page === 'projects' ? 'page' : undefined}
          onClick={() => setPage('projects')}
        >
          Projects
        </button>

        <button
          type="button"
          className={page === 'materials' ? 'nav-button active' : 'nav-button'}
          aria-current={page === 'materials' ? 'page' : undefined}
          onClick={() => setPage('materials')}
        >
          Materials
        </button>
      </nav>

      {page === 'projects' ? <ProjectsPage /> : <MaterialsPage />}
    </main>
  )
}

export default App