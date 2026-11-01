import Icon from './ui/Icon'
import { useEffect, useState } from 'react'
import { apiFetch, readApiResponse } from '../api/apiFetch'

type ProjectCost = {
  id: number
  code: string
  name: string
  status: string
  budget: number
  draftOrderValue: number
  committedPurchaseValue: number
  receivedMaterialValue: number
  consumedMaterialCost: number
  remainingMaterialBudget: number
  budgetAfterCommitments: number
}

type CostReport = {
  generatedAtUtc: string
  projects: ProjectCost[]
}

const money = new Intl.NumberFormat('en-LK', {
  style: 'currency', currency: 'LKR',
})

export default function ProjectCostsPage() {
  const [report, setReport] = useState<CostReport | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [refreshKey, setRefreshKey] = useState(0)
  const [projectId, setProjectId] = useState('')

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      setLoading(true)
      setError('')
      try {
        const response = await apiFetch('/api/project-costs', {
          signal: controller.signal, cache: 'no-store',
        })
        const data = await readApiResponse<CostReport>(response)
        if (!controller.signal.aborted) setReport(data)
      } catch (failure) {
        if (!controller.signal.aborted) {
          setError(failure instanceof Error ? failure.message : 'Unable to load project costs.')
        }
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }
    void load()
    return () => controller.abort()
  }, [refreshKey])

  const projects = report?.projects.filter(p => !projectId || String(p.id) === projectId) ?? []
  const total = (field: keyof Pick<ProjectCost, 'budget' | 'committedPurchaseValue' | 'receivedMaterialValue' | 'consumedMaterialCost'>) =>
    projects.reduce((sum, project) => sum + project[field], 0)

  return (
    <>
      <header className="page-header">
        <span className="workspace-page-emblem" aria-hidden="true"><Icon name="costs" /></span>
        <h1>Project Costs</h1>
        <p>Compare project budgets with material purchases and consumption.</p>
      </header>
      <section className="panel">
        <div className="section-toolbar">
          <label>
            Project
            <select value={projectId} onChange={event => setProjectId(event.target.value)} disabled={loading}>
              <option value="">All projects</option>
              {report?.projects.map(p => <option key={p.id} value={p.id}>{p.code} — {p.name}</option>)}
            </select>
          </label>
          <button type="button" className="secondary-button" disabled={loading} onClick={() => setRefreshKey(key => key + 1)}>
            {loading ? 'Refreshing...' : 'Refresh'}
          </button>
        </div>
        {report && !loading && !error && <p>Updated {new Date(report.generatedAtUtc).toLocaleString()}</p>}
      </section>
      {error && <p className="error-message" role="alert">{error}</p>}
      {loading ? <p role="status">Loading project costs...</p> : !error && report ? (
        <>
          <div className="cost-summary">
            {([
              ['Project budgets', 'budget'],
              ['Confirmed purchases', 'committedPurchaseValue'],
              ['Materials received', 'receivedMaterialValue'],
              ['Materials consumed', 'consumedMaterialCost'],
            ] as const).map(([label, field]) => (
              <div className="panel" key={field}><span>{label}</span><strong>{money.format(total(field))}</strong></div>
            ))}
          </div>
          <section className="panel">
            <h2>Budget and material costs</h2>
            {projects.length === 0 ? <p>No projects found. Create a project to start tracking its costs.</p> : (
              <div className="table-container">
                <table>
                  <thead><tr>
                    <th scope="col">Project</th><th scope="col">Status</th>
                    <th scope="col">Budget</th><th scope="col">Draft orders</th>
                    <th scope="col">Confirmed purchases</th><th scope="col">Received value</th>
                    <th scope="col">Consumed cost</th><th scope="col">Budget less consumed</th>
                    <th scope="col">Budget less purchases</th>
                  </tr></thead>
                  <tbody>{projects.map(p => (
                    <tr key={p.id}>
                      <td><strong>{p.code}</strong><br />{p.name}</td><td>{p.status}</td>
                      <td className="number-cell">{money.format(p.budget)}</td>
                      <td className="number-cell">{money.format(p.draftOrderValue)}</td>
                      <td className="number-cell">{money.format(p.committedPurchaseValue)}</td>
                      <td className="number-cell">{money.format(p.receivedMaterialValue)}</td>
                      <td className="number-cell">{money.format(p.consumedMaterialCost)}</td>
                      <td className={`number-cell ${p.remainingMaterialBudget < 0 ? 'cost-over-budget' : ''}`}>
                        {money.format(p.remainingMaterialBudget)}{p.remainingMaterialBudget < 0 && <small>Over budget</small>}
                      </td>
                      <td className={`number-cell ${p.budgetAfterCommitments < 0 ? 'cost-over-budget' : ''}`}>
                        {money.format(p.budgetAfterCommitments)}{p.budgetAfterCommitments < 0 && <small>Over budget</small>}
                      </td>
                    </tr>
                  ))}</tbody>
                </table>
              </div>
            )}
          </section>
          <section className="panel">
            <h2>How to read these values</h2>
            <p>Draft orders are planned purchases. Confirmed purchases include the full value of issued, partially received, and received orders; cancelled orders are excluded.</p>
            <p>Received value comes from posted goods receipts less supplier returns. Reversed postings are excluded. Consumed cost comes from posted material issues at their saved issue costs. These show different stages of the same materials and must not be added together.</p>
            <p>Budget less consumed is the project budget minus material issue costs. Budget less purchases is the project budget minus confirmed purchase value. Both comparisons cover materials only; labour, equipment, invoices, and payments are not included.</p>
          </section>
        </>
      ) : null}
    </>
  )
}
