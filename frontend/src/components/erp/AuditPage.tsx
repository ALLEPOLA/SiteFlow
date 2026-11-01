import Icon from '../ui/Icon'
import { useState } from 'react'
import { useData } from './ErpUi'
import { Feedback } from './Feedback'

type Audit = { id: number; createdAtUtc: string; actorName: string; action: string; resource: string; reason: string }
export default function AuditPage() {
  const [page, setPage] = useState(1)
  const [query, setQuery] = useState('')
  const [search, setSearch] = useState('')
  const audit = useData<{ totalCount: number; items: Audit[] }>(`/api/administration/audit?page=${page}&search=${encodeURIComponent(search)}`)
  return <>
    <header className="page-header">
        <span className="workspace-page-emblem" aria-hidden="true"><Icon name="audit" /></span><h1>Audit History</h1><p>Successful commands, responsible accounts, affected records, and correction reasons. Passwords and document contents are excluded.</p></header>
    <Feedback error={audit.error} />
    <section className="panel"><form onSubmit={e => { e.preventDefault(); setPage(1); setSearch(query); audit.refresh() }}><label>Search actor, action, or record<input value={query} onChange={e => setQuery(e.target.value)} maxLength={200} /></label><p><button className="secondary-button">Search / refresh</button></p></form>
      {audit.loading ? <p role="status">Loading audit history...</p> : <div className="table-container"><table><thead><tr><th>UTC time</th><th>Actor</th><th>Action</th><th>Record</th><th>Reason</th></tr></thead><tbody>{audit.data?.items.map(a => <tr key={a.id}><td>{a.createdAtUtc}</td><td>{a.actorName}</td><td>{a.action}</td><td>{a.resource}</td><td>{a.reason || '—'}</td></tr>)}</tbody></table></div>}
      <div className="inventory-pagination"><button type="button" className="secondary-button" disabled={page === 1 || audit.loading} onClick={() => setPage(p => p - 1)}>Previous</button><span>Page {page} · {audit.data?.totalCount ?? 0} records</span><button type="button" className="secondary-button" disabled={audit.loading || page * 50 >= (audit.data?.totalCount ?? 0)} onClick={() => setPage(p => p + 1)}>Next</button></div>
    </section>
  </>
}
