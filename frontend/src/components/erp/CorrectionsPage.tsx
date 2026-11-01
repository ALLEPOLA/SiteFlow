import Icon from '../ui/Icon'
import { useState } from 'react'
import type { AuthUser } from '../../types/auth'
import { send, useCommand, useData } from './ErpUi'
import { Feedback } from './Feedback'
import type { Material } from './ErpUi'

type Line = { id: number; materialId: number; materialRequestLineId: number; materialName: string; quantity: number; estimatedUnitPrice: number; unitPrice: number }
type Document = { id: number; requestNumber?: string; orderNumber?: string; receiptNumber?: string; issueNumber?: string; status: string; reason?: string; requiredDate?: string; supplierId?: number; orderDate?: string; expectedDeliveryDate?: string; notes?: string; canSubmit?: boolean; lines: Line[] }
type DraftLine = { key: string; materialId: number; label: string; quantity: string; price: string }

export default function CorrectionsPage({ user }: { user: AuthUser }) {
  const [kind, setKind] = useState('requests')
  const urls: Record<string, string> = { requests: '/api/material-requests', orders: '/api/purchase-orders', receipts: '/api/goods-receipts', issues: '/api/material-issues' }
  const documents = useData<Document[]>(urls[kind])
  const materials = useData<Material[]>('/api/materials')
  const suppliers = useData<{ id: number; name: string; isActive: boolean }[]>('/api/suppliers')
  const command = useCommand()
  const [selected, setSelected] = useState<Document | null>(null)
  const [reason, setReason] = useState('')
  const [text, setText] = useState('')
  const [date, setDate] = useState('')
  const [delivery, setDelivery] = useState('')
  const [supplier, setSupplier] = useState('')
  const [lines, setLines] = useState<DraftLine[]>([])
  const allowed = user.roles.includes('Administrator') || user.roles.includes(kind === 'requests' ? 'SiteEngineer' : kind === 'orders' ? 'ProcurementOfficer' : 'Storekeeper')
  const editable = allowed && selected?.status === 'Draft' && (kind === 'orders' || (kind === 'requests' && selected.canSubmit))

  function choose(id: string) {
    const doc = documents.data?.find(d => String(d.id) === id) ?? null
    setSelected(doc); setReason(''); setText(doc?.reason ?? doc?.notes ?? ''); setDate(doc?.requiredDate ?? doc?.orderDate ?? '')
    setDelivery(doc?.expectedDeliveryDate ?? ''); setSupplier(String(doc?.supplierId ?? ''))
    setLines(doc?.lines.map(l => ({ key: crypto.randomUUID(), materialId: kind === 'orders' ? l.materialRequestLineId : l.materialId,
      label: l.materialName, quantity: String(l.quantity), price: String(kind === 'requests' ? l.estimatedUnitPrice : l.unitPrice) })) ?? [])
  }
  async function action(action: string) {
    if (!selected || !window.confirm(`Confirm ${action} for this document?`)) return
    if (await command.run(() => send(`/api/corrections/${kind}/${selected.id}/${action}`, { reason }))) { setSelected(null); documents.refresh() }
  }
  return <>
    <header className="page-header">
        <span className="workspace-page-emblem" aria-hidden="true"><Icon name="corrections" /></span><h1>Corrections</h1><p>Edit drafts and correct posted transactions with a recorded reason.</p></header>
    <Feedback error={command.error || documents.error || materials.error || suppliers.error} message={command.message} />
    <section className="panel"><div className="form-grid">
      <label>Document type<select value={kind} disabled={command.busy} onChange={e => { setKind(e.target.value); setSelected(null) }}><option value="requests">Material requests</option><option value="orders">Purchase orders</option><option value="receipts">Goods receipts</option><option value="issues">Material issues</option></select></label>
      <label>Document<select value={selected?.id ?? ''} disabled={documents.loading || command.busy} onChange={e => choose(e.target.value)}><option value="">Select document</option>{!documents.loading && documents.data?.map(d => <option key={d.id} value={d.id}>{kind === 'requests' ? d.requestNumber : kind === 'orders' ? d.orderNumber : kind === 'receipts' ? d.receiptNumber : d.issueNumber} — {d.status}</option>)}</select></label>
    </div></section>
    {selected && <section className="panel"><h2>Document {selected.id} · {selected.status}</h2>
      {!allowed ? <p>Your role can view this document but cannot correct it.</p> : <>
        <label>Correction reason<textarea value={reason} onChange={e => setReason(e.target.value)} maxLength={1000} rows={3} required /></label>
        {editable && <form onSubmit={async e => {
          e.preventDefault()
          if (await command.run(() => send(`/api/corrections/${kind}/${selected.id}`, {
            reason, ...(kind === 'requests' ? { requiredDate: date, reasonText: text } : { supplierId: Number(supplier), orderDate: date, deliveryDate: delivery, notes: text }),
            lines: lines.map(l => ({ materialId: l.materialId, quantity: Number(l.quantity), price: Number(l.price) })),
          }, 'PUT'))) { setSelected(null); documents.refresh() }
        }}><fieldset className="form-fields" disabled={command.busy}><div className="form-grid">
          <label>{kind === 'requests' ? 'Required date' : 'Order date'}<input type="date" value={date} onChange={e => setDate(e.target.value)} required /></label>
          {kind === 'orders' && <><label>Expected delivery<input type="date" min={date} value={delivery} onChange={e => setDelivery(e.target.value)} required /></label><label>Supplier<select value={supplier} onChange={e => setSupplier(e.target.value)} required><option value="">Select supplier</option>{suppliers.data?.filter(s => s.isActive).map(s => <option key={s.id} value={s.id}>{s.name}</option>)}</select></label></>}
          <label>{kind === 'requests' ? 'Request reason' : 'Notes'}<textarea value={text} onChange={e => setText(e.target.value)} required={kind === 'requests'} maxLength={1000} /></label>
        </div>
        {lines.map((line, i) => <div className="request-line" key={line.key}><div className="form-grid">
          {kind === 'requests' ? <label>Material<select value={line.materialId || ''} required onChange={e => setLines(current => current.map((l, j) => j === i ? { ...l, materialId: Number(e.target.value) } : l))}><option value="">Select material</option>{materials.data?.filter(m => m.isActive).map(m => <option key={m.id} value={m.id}>{m.code} — {m.name}</option>)}</select></label> : <p>{line.label}</p>}
          <label>Quantity<input type="number" min="0.001" max="999999999.999" step="0.001" value={line.quantity} required onChange={e => setLines(current => current.map((l, j) => j === i ? { ...l, quantity: e.target.value } : l))} /></label>
          <label>Unit price LKR<input type="number" min="0" max="999999999.99" step="0.01" value={line.price} required onChange={e => setLines(current => current.map((l, j) => j === i ? { ...l, price: e.target.value } : l))} /></label>
        </div><button type="button" className="secondary-button" disabled={lines.length === 1} onClick={() => setLines(current => current.filter(l => l.key !== line.key))}>Remove line</button></div>)}
        {kind === 'requests' && <button type="button" className="secondary-button" disabled={lines.length >= 100} onClick={() => setLines(current => [...current, { key: crypto.randomUUID(), materialId: 0, label: '', quantity: '', price: '' }])}>Add material</button>}
        <p><button className="primary-button" disabled={!reason.trim()}>Save draft changes</button></p></fieldset></form>}
        {kind === 'orders' && ['Draft', 'Issued'].includes(selected.status) && <p><button type="button" className="secondary-button" disabled={command.busy || !reason.trim()} onClick={() => void action('cancel')}>Cancel purchase order</button></p>}
        {['receipts', 'issues'].includes(kind) && selected.status === 'Posted' && <p><button type="button" className="secondary-button" disabled={command.busy || !reason.trim()} onClick={() => void action('reverse')}>Reverse posting</button></p>}
        <p>Receipt reversals require enough stock and no active dependent stock activity, invoices, or returns. Issue reversals restore stock. Original records remain in history.</p>
      </>}
    </section>}
  </>
}
