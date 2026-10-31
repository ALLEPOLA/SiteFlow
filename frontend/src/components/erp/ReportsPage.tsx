import { useState } from 'react'
import { download, useCommand, useData } from './ErpUi'
import { Feedback } from './Feedback'
import type { Project } from './ErpUi'

export default function ReportsPage() {
  const projects = useData<Project[]>('/api/projects')
  const orders = useData<{ id: number; orderNumber: string; projectName: string }[]>('/api/purchase-orders')
  const command = useCommand()
  const [kind, setKind] = useState('costs')
  const [project, setProject] = useState('')
  const [order, setOrder] = useState('')
  return <>
    <header className="page-header"><h1>Reports &amp; Exports</h1><p>Download purchase orders, stock ledgers, and project material cost summaries.</p></header>
    <Feedback error={command.error || projects.error || orders.error} />
    <section className="panel"><div className="form-grid">
      <label>Report<select value={kind} onChange={e => { setKind(e.target.value); setOrder('') }}><option value="costs">Project cost summary</option><option value="orders">Purchase orders</option><option value="stock">Stock movement ledger</option></select></label>
      <label>Project<select value={project} onChange={e => { setProject(e.target.value); setOrder('') }}><option value="">All assigned projects</option>{projects.data?.map(p => <option key={p.id} value={p.id}>{p.code} — {p.name}</option>)}</select></label>
      {kind === 'orders' && <label>Purchase order (optional)<select value={order} onChange={e => setOrder(e.target.value)}><option value="">All orders in project selection</option>{orders.data?.map(o => <option key={o.id} value={o.id}>{o.orderNumber} — {o.projectName}</option>)}</select></label>}
    </div><p>Amounts are in LKR. Cost stages are separate and must not be added together. Excel preserves Unicode text; PDF currently uses basic Latin text.</p>
      <div className="request-review-actions">{['pdf', 'xlsx'].map(format => <button type="button" className="primary-button" key={format} disabled={command.busy} onClick={() => {
        const params = new URLSearchParams(); if (project) params.set('projectId', project); if (kind === 'orders' && order) params.set('orderId', order)
        void command.run(() => download(`/api/reports/${kind}/${format}?${params}`, `SiteFlow-${kind}.${format}`))
      }}>Download {format === 'pdf' ? 'PDF' : 'Excel'}</button>)}</div>
    </section>
  </>
}
