import { useState } from 'react'
import type { AuthUser } from '../../types/auth'
import { send, useCommand, useData } from './ErpUi'
import { Feedback } from './Feedback'
import type { Material } from './ErpUi'

type Notification = { key: string; message: string; page: string; isRead: boolean }
export default function NotificationsPage({ user, onNavigate }: { user: AuthUser; onNavigate: (page: string) => void }) {
  const notifications = useData<Notification[]>('/api/notifications')
  const materials = useData<Material[]>('/api/materials')
  const command = useCommand()
  const [material, setMaterial] = useState('')
  const [quantity, setQuantity] = useState('')
  const [unreadOnly, setUnreadOnly] = useState(false)
  const canConfigure = user.roles.some(r => ['Administrator', 'ProcurementOfficer'].includes(r))
  const rows = notifications.data?.filter(n => !unreadOnly || !n.isRead) ?? []
  return <>
    <header className="page-header"><h1>Notifications</h1><p>Pending approvals, overdue deliveries, low stock, and overdue supplier invoices.</p></header>
    <Feedback error={command.error || notifications.error || materials.error} message={command.message} />
    <section className="panel"><div className="section-toolbar"><h2>{notifications.data?.filter(n => !n.isRead).length ?? 0} unread</h2><button type="button" className="secondary-button" onClick={notifications.refresh}>Refresh</button></div>
      <label className="checkbox-label"><input type="checkbox" checked={unreadOnly} onChange={e => setUnreadOnly(e.target.checked)} />Unread only</label>
      {notifications.loading ? <p role="status">Loading notifications...</p> : rows.length === 0 ? <p>No current notifications.</p> : <ul className="notification-list">{rows.map(n => <li key={n.key}><p>{n.message} {n.isRead && <small>Read</small>}</p>
        <button type="button" className="secondary-button" onClick={() => onNavigate(n.page)}>Open</button>{' '}
        {!n.isRead && <button type="button" className="secondary-button" disabled={command.busy} onClick={async () => { if (await command.run(() => send('/api/notifications/read', { key: n.key }))) notifications.refresh() }}>Mark read</button>}
      </li>)}</ul>}
    </section>
    {canConfigure && <section className="panel"><h2>Material reorder levels</h2><p>The same material threshold applies to each project's existing stock balance. Zero disables low-stock alerts for that material.</p>
      <form onSubmit={async e => { e.preventDefault(); if (await command.run(() => send(`/api/notifications/reorder-level/${material}`, { quantity: Number(quantity) }, 'PUT'))) { materials.refresh(); notifications.refresh() } }}>
        <fieldset className="form-fields" disabled={command.busy}><div className="form-grid"><label>Material<select value={material} onChange={e => { setMaterial(e.target.value); setQuantity(String(materials.data?.find(m => m.id === Number(e.target.value))?.reorderLevel ?? 0)) }} required><option value="">Select material</option>{materials.data?.map(m => <option key={m.id} value={m.id}>{m.code} — {m.name} (level {m.reorderLevel})</option>)}</select></label>
          <label>Reorder quantity<input type="number" min="0" max="999999999.999" step="0.001" value={quantity} onChange={e => setQuantity(e.target.value)} required /></label></div><button className="primary-button">Save reorder level</button></fieldset>
      </form>
    </section>}
  </>
}
