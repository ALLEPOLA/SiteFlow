import { useRef, useState } from 'react'
import type { AuthUser } from '../../types/auth'
import { apiFetch, readApiResponse } from '../../api/apiFetch'
import { download, send, useCommand, useData } from './ErpUi'
import { Feedback } from './Feedback'

type Resource = { id: number; name?: string; code?: string; number?: string; requestNumber?: string; orderNumber?: string; receiptNumber?: string; issueNumber?: string }
type Attachment = { id: number; fileName: string; size: number; createdAtUtc: string; uploadedByUserId: string }

function AttachmentList({ type, id, user, version }: { type: string; id: string; user: AuthUser; version: number }) {
  const attachments = useData<Attachment[]>(`/api/documents?resourceType=${encodeURIComponent(type)}&resourceId=${id}&v=${version}`)
  const command = useCommand()
  return <><Feedback error={attachments.error || command.error} message={command.message} />
    <ul>{attachments.data?.map(a => <li key={a.id}>
      <button type="button" className="secondary-button" disabled={command.busy} onClick={() => void command.run(() => download(`/api/documents/${a.id}`, a.fileName))}>{a.fileName}</button>
      {' '}{Math.ceil(a.size / 1024)} KB · {new Date(a.createdAtUtc).toLocaleString()}{' '}
      {(a.uploadedByUserId === user.id || user.roles.includes('Administrator')) && <button type="button" className="secondary-button" disabled={command.busy} onClick={async () => {
        if (!window.confirm('Remove this attachment?')) return
        if (await command.run(() => send(`/api/documents/${a.id}`, null, 'DELETE'))) attachments.refresh()
      }}>Remove</button>}
    </li>)}</ul>{!attachments.loading && attachments.data?.length === 0 && <p>No attachments yet.</p>}
  </>
}

export default function DocumentsPage({ user }: { user: AuthUser }) {
  const [type, setType] = useState('Project')
  const [id, setId] = useState('')
  const [version, setVersion] = useState(0)
  const command = useCommand()
  const input = useRef<HTMLInputElement>(null)
  const urls: Record<string, string> = { Project: '/api/projects', Request: '/api/material-requests', Order: '/api/purchase-orders', Receipt: '/api/goods-receipts', Issue: '/api/material-issues', Invoice: '/api/supplier-accounts' }
  const resources = useData<Resource[]>(urls[type])
  return <>
    <header className="page-header"><h1>Documents</h1><p>Attach quotations, delivery notes, invoices, and site photos to project records.</p></header>
    <Feedback error={command.error || resources.error} message={command.message} />
    <section className="panel"><div className="form-grid">
      <label>Record type<select value={type} onChange={e => { setType(e.target.value); setId('') }} disabled={command.busy}>{Object.keys(urls).filter(t => t !== 'Invoice' || user.roles.some(r => ['Administrator', 'ProcurementOfficer'].includes(r))).map(t => <option key={t}>{t}</option>)}</select></label>
      <label>Record<select value={id} onChange={e => setId(e.target.value)} disabled={resources.loading || command.busy}><option value="">Select record</option>{!resources.loading && resources.data?.map(r => <option key={r.id} value={r.id}>{type === 'Order' ? r.orderNumber : r.requestNumber ?? r.receiptNumber ?? r.issueNumber ?? r.number ?? `${r.code} — ${r.name}`}</option>)}</select></label>
    </div></section>
    {id && <section className="panel"><h2>Attachments</h2><form onSubmit={async e => { e.preventDefault()
      const file = input.current?.files?.[0]; if (!file) return
      const body = new FormData(); body.append('resourceType', type); body.append('resourceId', id); body.append('file', file)
      if (await command.run(() => apiFetch('/api/documents', { method: 'POST', body }).then(readApiResponse))) { setVersion(v => v + 1); if (input.current) input.current.value = '' }
    }}><fieldset className="form-fields" disabled={command.busy}><label>Document or image (PDF, PNG, JPG, TXT; maximum 5 MB)<input ref={input} type="file" accept=".pdf,.png,.jpg,.jpeg,.txt" required /></label><p><button className="primary-button">Upload attachment</button></p></fieldset></form>
      <AttachmentList key={`${type}-${id}`} type={type} id={id} user={user} version={version} />
    </section>}
  </>
}
