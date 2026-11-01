import StorageLocationSelect from '../ui/StorageLocationSelect'
import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { apiFetch, readApiResponse } from '../../api/apiFetch'
import type { AuthUser } from '../../types/auth'
import Icon from '../ui/Icon'

type Project = { id: number; code: string; name: string; status: string }
type Material = { id: number; code: string; name: string; unit: string; isActive: boolean }
type InputLine = { key: string; materialId: string; quantity: string; safetyStock: string; leadTimeDays: string; estimatedUnitPrice: string }
type PlanLine = {
  materialId: number; code: string; name: string; unit: string; demand: number; safetyStock: number
  leadTimeDays: number; estimatedUnitPrice: number; stock: number; physicalStock: number; reservedStock: number; incoming: number
  overdueIncoming: number; lateIncoming: number; shortage: number; estimatedCost: number
  orderByDate: string; orderIsLate: boolean; status: string
}
type Plan = {
  projectId: number; projectName: string; requiredDate: string; generatedAtUtc: string; asOfDate: string
  budget: number; committedPurchaseValue: number; budgetAfterCommitments: number
  estimatedCost: number; budgetAfterSuggestedPurchase: number; missingPriceCount: number
  lines: PlanLine[]; assumptions: string[]
}
const newLine = (): InputLine => ({ key: crypto.randomUUID(), materialId: '', quantity: '', safetyStock: '0', leadTimeDays: '7', estimatedUnitPrice: '' })
const money = (n: number) => new Intl.NumberFormat('en-LK', { style: 'currency', currency: 'LKR' }).format(n)
const qty = (n: number) => new Intl.NumberFormat('en-LK', { maximumFractionDigits: 3 }).format(n)
const today = () => { const d = new Date(); return new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 10) }

export default function MaterialPlanningPage({ user }: { user: AuthUser }) {
  const [projects, setProjects] = useState<Project[]>([])
  const [materials, setMaterials] = useState<Material[]>([])
  const [projectId, setProjectId] = useState('')
  const [storageLocationId, setStorageLocationId] = useState('')
  const [requiredDate, setRequiredDate] = useState(today)
  const [reason, setReason] = useState('')
  const [lines, setLines] = useState<InputLine[]>([newLine()])
  const [result, setResult] = useState<Plan | null>(null)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [createdId, setCreatedId] = useState<number | null>(null)
  const [saveAttempted, setSaveAttempted] = useState(false)
  const canCreate = user.roles.some(r => ['Administrator', 'SiteEngineer'].includes(r))

  useEffect(() => {
    const controller = new AbortController()
    async function load() {
      try {
        const [p, m] = await Promise.all([
          apiFetch('/api/projects', { signal: controller.signal, cache: 'no-store' }).then(readApiResponse<Project[]>),
          apiFetch('/api/materials', { signal: controller.signal, cache: 'no-store' }).then(readApiResponse<Material[]>),
        ])
        if (!controller.signal.aborted) { setProjects(p.filter(x => x.status === 'Active')); setMaterials(m.filter(x => x.isActive)) }
      } catch (e) {
        if (!controller.signal.aborted) setError(e instanceof Error ? e.message : 'Unable to load planning data.')
      } finally { if (!controller.signal.aborted) setLoading(false) }
    }
    void load()
    return () => controller.abort()
  }, [])

  function invalidate() { if (createdId) setSaveAttempted(false); setResult(null); setNotice(''); setCreatedId(null); setError('') }
  function changeLine(key: string, name: keyof Omit<InputLine, 'key'>, value: string) {
    invalidate()
    setLines(current => current.map(l => l.key === key ? { ...l, [name]: value } : l))
  }
  function payload() {
    return { projectId: Number(projectId), storageLocationId: storageLocationId ? Number(storageLocationId) : null, requiredDate, lines: lines.map(l => ({
      materialId: Number(l.materialId), quantity: Number(l.quantity), safetyStock: Number(l.safetyStock),
      leadTimeDays: Number(l.leadTimeDays), estimatedUnitPrice: Number(l.estimatedUnitPrice),
    })) }
  }
  async function analyze() {
    return readApiResponse<Plan>(await apiFetch('/api/material-planning/analyze', {
      method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload()),
    }))
  }
  async function preview(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (busy) return
    setBusy(true); setError(''); setNotice(''); setResult(null); setCreatedId(null)
    try {
      const ids = lines.map(l => l.materialId)
      if (new Set(ids).size !== ids.length) throw new Error('Choose each material only once.')
      setResult(await analyze())
    } catch (e) { setError(e instanceof Error ? e.message : 'Planning failed.') }
    finally { setBusy(false) }
  }
  async function createDraft() {
    if (!result || busy || createdId || saveAttempted) return
    if (!reason.trim()) { setError('Enter a work description before creating the draft.'); return }
    setBusy(true); setError(''); setNotice('')
    try {
      // Recheck live supply before converting a reviewed preview into a request.
      const fresh = await analyze()
      if (JSON.stringify(fresh.lines) !== JSON.stringify(result.lines) ||
          fresh.budgetAfterCommitments !== result.budgetAfterCommitments) {
        setResult(fresh)
        setNotice('Stock, deliveries, or commitments changed. Review the updated calculation, then create the draft.')
        return
      }
      const shortage = fresh.lines.filter(l => l.shortage > 0)
      if (!shortage.length) { setResult(fresh); setNotice('There is no shortage to request.'); return }
      setSaveAttempted(true)
      const created = await readApiResponse<{ id: number }>(await apiFetch('/api/material-requests', {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ projectId: fresh.projectId, requiredDate: fresh.requiredDate,
          reason: (storageLocationId ? 'Material planning (location #' + storageLocationId + '): ' : 'Material planning: ') + reason.trim(),
          lines: shortage.map(l => ({ materialId: l.materialId, quantity: l.shortage, estimatedUnitPrice: l.estimatedUnitPrice })),
        }),
      }))
      setCreatedId(created.id)
      setNotice('Draft MR-' + String(created.id).padStart(6, '0') + ' created. Open Material Requests to review and submit it.')
    } catch (e) {
      setError((e instanceof Error ? e.message : 'Unable to create draft.') +
        ' If saving was interrupted, check Material Requests before creating another request.')
    } finally { setBusy(false) }
  }

  return (
    <>
      <header className="page-header">
        <span className="workspace-page-emblem" aria-hidden="true"><Icon name="planning" /></span>
        <h1>Material Planning</h1>
        <p>Plan upcoming work, explain shortages, and prepare requests before materials are needed.</p>
      </header>
      <div className="planning-intro"><Icon name="planning" /><div><strong>Plan ahead. No usage history required.</strong><p>Enter additional upcoming demand. We compare it with unreserved stock and deliveries expected in time. This is a what-if preview, not a stock reservation.</p></div></div>
      {error && <p className="error-message" role="alert">{error}</p>}
      {notice && <p className="success-message" role="status">{notice}</p>}
      {loading ? <p role="status">Loading projects and materials...</p> : (
        <section className="panel">
          <h2>Upcoming work requirements</h2>
          <form onSubmit={preview}>
            <fieldset className="form-fields" disabled={busy}>
              <div className="form-grid">
                <label>Project<select value={projectId} onChange={e => { invalidate(); setProjectId(e.target.value); setStorageLocationId('') }} required><option value="">Select an active project</option>{projects.map(p => <option key={p.id} value={p.id}>{p.code} — {p.name}</option>)}</select></label>
                <StorageLocationSelect projectId={projectId} value={storageLocationId} onChange={value => { invalidate(); setStorageLocationId(value) }} label="Planning location (optional)" optional />
                <label>Required on<input type="date" min={today()} value={requiredDate} onChange={e => { invalidate(); setRequiredDate(e.target.value) }} required /></label>
              </div>
              <p className="section-description">A specific-location plan excludes incoming orders and other locations. Consider a stock transfer before purchasing more. Safety stock is the extra quantity you want left after the work. Lead time is your expected supplier turnaround, in calendar days.</p>
              {lines.map((line, index) => (
                <div className="request-line" key={line.key}>
                  <div className="section-toolbar"><h3>Material {index + 1}</h3>{lines.length > 1 && <button type="button" className="secondary-button" onClick={() => { invalidate(); setLines(current => current.filter(l => l.key !== line.key)) }}>Remove</button>}</div>
                  <div className="form-grid planning-input-grid">
                    <label>Material<select value={line.materialId} onChange={e => changeLine(line.key, 'materialId', e.target.value)} required><option value="">Select a material</option>{materials.map(m => <option key={m.id} value={m.id}>{m.code} — {m.name} ({m.unit})</option>)}</select></label>
                    <label>Planned quantity<input type="number" min="0.001" max="999999999.999" step="0.001" value={line.quantity} onChange={e => changeLine(line.key, 'quantity', e.target.value)} required /></label>
                    <label>Safety stock<input type="number" min="0" max="999999999.999" step="0.001" value={line.safetyStock} onChange={e => changeLine(line.key, 'safetyStock', e.target.value)} required /></label>
                    <label>Supplier lead time (days)<input type="number" min="0" max="365" step="1" value={line.leadTimeDays} onChange={e => changeLine(line.key, 'leadTimeDays', e.target.value)} required /></label>
                    <label>Estimated unit price (LKR)<input type="number" min="0" max="999999999.99" step="0.01" value={line.estimatedUnitPrice} onChange={e => changeLine(line.key, 'estimatedUnitPrice', e.target.value)} required /></label>
                  </div>
                </div>
              ))}
              <div className="request-review-actions">
                <button type="button" className="secondary-button" disabled={lines.length >= 100} onClick={() => { invalidate(); setLines(current => [...current, newLine()]) }}>Add material</button>
                <button type="submit" className="primary-button"><Icon name={busy ? 'refresh' : 'planning'} />{busy ? 'Checking...' : 'Calculate requirements'}</button>
              </div>
            </fieldset>
          </form>
        </section>
      )}
      {result && (
        <>
          <div className="inventory-summary">
            <section className="panel"><span>Materials with a shortage</span><strong>{result.lines.filter(l => l.shortage > 0).length} / {result.lines.length}</strong></section>
            <section className="panel"><span>Estimated replenishment</span><strong>{money(result.estimatedCost)}</strong></section>
            <section className="panel"><span>Budget after suggested purchase</span><strong className={result.budgetAfterSuggestedPurchase < 0 ? 'cost-over-budget' : ''}>{money(result.budgetAfterSuggestedPurchase)}</strong></section>
          </div>
          <section className="panel">
            <div className="section-toolbar"><h2>Requirement breakdown</h2><small>Snapshot: {new Date(result.generatedAtUtc).toLocaleString()} · dates use UTC</small></div>
            <p className="section-description">Shortage = max(0, planned demand + safety stock − unreserved stock − incoming in time). Amounts are not reserved.</p>
            {result.missingPriceCount > 0 && <p className="planning-warning">{result.missingPriceCount} shortage line(s) have a zero price. The cost estimate is incomplete.</p>}
            <div className="table-container"><table><thead><tr><th>Material / coverage</th><th>Demand + safety</th><th>Available stock</th><th>Incoming in time</th><th>Shortage</th><th>Order by</th><th>Estimated cost</th></tr></thead>
              <tbody>{result.lines.map(l => (
                <tr key={l.materialId}>
                  <td><strong>{l.code} — {l.name}</strong><div className="planning-coverage"><progress aria-label={l.name + ' demand coverage'} max={l.demand + l.safetyStock} value={Math.min(l.stock + l.incoming, l.demand + l.safetyStock)} /><span className="status-badge" data-status={l.shortage === 0 ? 'Approved' : l.orderIsLate ? 'Rejected' : 'Submitted'}>{l.shortage === 0 ? 'Covered' : l.orderIsLate ? 'Urgent' : 'Order needed'}</span></div></td>
                  <td className="number-cell">{qty(l.demand)} + {qty(l.safetyStock)} {l.unit}</td>
                  <td className="number-cell">{qty(l.stock)}<small className="planning-note"> ({qty(l.reservedStock)} reserved)</small></td><td className="number-cell">{qty(l.incoming)}</td>
                  <td className="number-cell"><strong>{qty(l.shortage)} {l.unit}</strong></td>
                  <td>{l.shortage > 0 ? l.orderByDate : 'No purchase needed'}{l.orderIsLate && <small className="planning-late">Lead time exceeds time remaining</small>}</td>
                  <td className="number-cell">{money(l.estimatedCost)}</td>
                </tr>
              ))}</tbody></table></div>
            {result.lines.some(l => l.overdueIncoming > 0 || l.lateIncoming > 0) && <div className="planning-warning"><strong>Deliveries excluded from coverage</strong>{result.lines.filter(l => l.overdueIncoming > 0 || l.lateIncoming > 0).map(l => <p key={l.materialId}>{l.name}: {qty(l.overdueIncoming)} {l.unit} overdue; {qty(l.lateIncoming)} {l.unit} expected after the required date. Confirm or reschedule these orders before buying again.</p>)}</div>}
          </section>
          <section className="panel"><h2>Review and prepare a request</h2><p>Project budget: {money(result.budget)} · Issued purchase commitments: {money(result.committedPurchaseValue)} · Budget after commitments: {money(result.budgetAfterCommitments)}. Draft orders, tax, and delivery charges are excluded.</p>
            {result.budgetAfterSuggestedPurchase < 0 && <p className="planning-warning">The suggested purchase exceeds the uncommitted project budget. Discuss funding or quantities before submitting a request.</p>}
            {canCreate ? <><label>Upcoming work description<textarea maxLength={950} value={reason} disabled={busy || !!createdId || saveAttempted} onChange={e => setReason(e.target.value)} placeholder="For example: second-floor slab work" /></label><div className="request-review-actions"><button type="button" className="primary-button" disabled={busy || !!createdId || saveAttempted || !result.lines.some(l => l.shortage > 0)} onClick={createDraft}>{busy ? 'Rechecking...' : createdId ? 'Draft created' : 'Recheck & create shortage draft'}</button></div>
              {saveAttempted && !createdId && <p className="planning-warning">A save was attempted. Check Material Requests for the outcome before starting a new plan. Reload this page only after confirming the result.</p>}
            </> : <p>Administrators and Site Engineers can create shortage drafts. Share this calculation with your project engineer.</p>}
            <p className="planning-note">Drafts require normal submission and review. This page keeps your what-if inputs only while it is open; the resulting material request is saved.</p>
          </section>
          <section className="panel"><h2>How this plan was calculated</h2><ul className="planning-assumptions">{result.assumptions.map(a => <li key={a}>{a}</li>)}</ul></section>
        </>
      )}
    </>
  )
}
