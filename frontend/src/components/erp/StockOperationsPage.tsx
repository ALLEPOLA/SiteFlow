import { useRef, useState } from 'react'
import { money, send, today, useCommand, useData } from './ErpUi'
import { Feedback } from './Feedback'
import type { Project, Material } from './ErpUi'

type Receipt = { id: number; projectId: number; receiptNumber: string; status: string; lines: { id: number; materialId: number; materialName: string; quantity: number }[] }
type Operation = { id: number; kind: string; status: string; projectName: string; destinationProjectId: number | null; materialName: string; quantity: number; unitCost: number; date: string; reason: string }

export default function StockOperationsPage() {
  const projects = useData<Project[]>('/api/projects')
  const materials = useData<Material[]>('/api/materials')
  const receipts = useData<Receipt[]>('/api/goods-receipts')
  const operations = useData<Operation[]>('/api/inventory-operations')
  const command = useCommand()
  const key = useRef(crypto.randomUUID())
  const [kind, setKind] = useState('Count')
  const [project, setProject] = useState('')
  const [destination, setDestination] = useState('')
  const [material, setMaterial] = useState('')
  const [quantity, setQuantity] = useState('')
  const [cost, setCost] = useState('0')
  const [date, setDate] = useState(today)
  const [reason, setReason] = useState('')
  const [receiptLine, setReceiptLine] = useState('')
  return <>
    <header className="page-header"><h1>Stock Operations</h1><p>Record physical counts, stock adjustments, project transfers, and supplier returns.</p></header>
    <Feedback error={command.error || projects.error || materials.error || receipts.error || operations.error} message={command.message} />
    <section className="panel"><h2>Post inventory operation</h2>
      <form onSubmit={async e => { e.preventDefault(); if (!window.confirm('Post this inventory operation? It changes stock immediately.')) return
        if (await command.run(() => send('/api/inventory-operations', {
          clientRequestId: key.current, kind, projectId: Number(project), materialId: Number(material), quantity: Number(quantity), unitCost: Number(cost), date, reason,
          destinationProjectId: kind === 'Transfer' ? Number(destination) : null, goodsReceiptLineId: kind === 'SupplierReturn' ? Number(receiptLine) : null,
        }))) { key.current = crypto.randomUUID(); setQuantity(''); setReason(''); operations.refresh() }
      }}><fieldset className="form-fields" disabled={command.busy}><div className="form-grid">
        <label>Operation<select value={kind} onChange={e => setKind(e.target.value)}><option value="Count">Physical stock count</option><option value="Adjustment">Stock adjustment</option><option value="Transfer">Project transfer</option><option value="SupplierReturn">Return to supplier</option></select></label>
        <label>{kind === 'Transfer' ? 'Source project' : 'Project'}<select value={project} onChange={e => setProject(e.target.value)} required><option value="">Select project</option>{projects.data?.filter(p => p.status === 'Active').map(p => <option key={p.id} value={p.id}>{p.code} — {p.name}</option>)}</select></label>
        {kind === 'Transfer' && <label>Destination project<select value={destination} onChange={e => setDestination(e.target.value)} required><option value="">Select destination</option>{projects.data?.filter(p => p.status === 'Active' && String(p.id) !== project).map(p => <option key={p.id} value={p.id}>{p.code} — {p.name}</option>)}</select></label>}
        <label>Material<select value={material} onChange={e => { setMaterial(e.target.value); setReceiptLine('') }} required><option value="">Select material</option>{materials.data?.filter(m => m.isActive).map(m => <option key={m.id} value={m.id}>{m.code} — {m.name}</option>)}</select></label>
        {kind === 'SupplierReturn' && <label>Original receipt line<select value={receiptLine} onChange={e => setReceiptLine(e.target.value)} required><option value="">Select receipt line</option>{receipts.data?.filter(r => r.status === 'Posted' && r.projectId === Number(project)).flatMap(r => r.lines.filter(l => l.materialId === Number(material)).map(l => <option key={l.id} value={l.id}>{r.receiptNumber} — {l.materialName} ({l.quantity} received)</option>))}</select></label>}
        <label>{kind === 'Count' ? 'Actual counted quantity' : kind === 'Adjustment' ? 'Quantity change (negative to reduce)' : 'Quantity'}<input type="number" step="0.001" min={kind === 'Adjustment' ? '-999999999.999' : kind === 'Count' ? '0' : '0.001'} max="999999999.999" value={quantity} onChange={e => setQuantity(e.target.value)} required /></label>
        {['Adjustment', 'Count'].includes(kind) && <label>Unit cost LKR for new incoming stock<input type="number" min="0" max="999999999.99" step="0.01" value={cost} onChange={e => setCost(e.target.value)} required /></label>}
        <label>Date<input type="date" value={date} onChange={e => setDate(e.target.value)} required /></label>
        <label>Reason<textarea value={reason} onChange={e => setReason(e.target.value)} maxLength={1000} required /></label>
      </div><p>Counts record the actual total on hand. Transfers move stock between two assigned projects. Outgoing stock uses its recorded cost; returns use the original receipt cost. Dates cannot precede the latest stock movement.</p><button className="primary-button">Post operation</button></fieldset></form>
    </section>
    <section className="panel"><div className="section-toolbar"><h2>Latest 200 operations</h2><button type="button" className="secondary-button" onClick={operations.refresh}>Refresh</button></div>
      <div className="table-container"><table><thead><tr><th>Operation</th><th>Project</th><th>Destination</th><th>Material</th><th>Quantity</th><th>Cost</th><th>Date</th><th>Reason</th><th>Status / action</th></tr></thead><tbody>{operations.data?.map(o => <tr key={o.id}><td>ST-{o.id} · {o.kind}</td><td>{o.projectName}</td><td>{o.destinationProjectId ?? '—'}</td><td>{o.materialName}</td><td>{o.quantity}</td><td>{money(o.unitCost)}</td><td>{o.date}</td><td>{o.reason}</td><td>{o.status}{o.status === 'Posted' && <button type="button" className="secondary-button" disabled={command.busy} onClick={async () => {
        const reason = window.prompt('Reason for reversing this operation:'); if (!reason?.trim()) return
        if (await command.run(() => send(`/api/inventory-operations/${o.id}/reverse`, { reason }))) operations.refresh()
      }}>Reverse</button>}</td></tr>)}</tbody></table></div>
    </section>
  </>
}
