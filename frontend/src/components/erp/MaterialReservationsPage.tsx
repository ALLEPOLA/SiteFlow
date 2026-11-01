import StorageLocationSelect from '../ui/StorageLocationSelect'
import { useRef, useState } from 'react'
import type { AuthUser } from '../../types/auth'
import Icon from '../ui/Icon'
import { send, useCommand, useData } from './ErpUi'
import { Feedback } from './Feedback'

type Project = { id: number; code: string; name: string; status: string }
type Eligible = { materialRequestId: number; projectId: number; projectName: string; requiredDate: string; materialId: number; materialCode: string; materialName: string; unit: string; approvedQuantity: number; allocatedQuantity: number }
type Reservation = { storageLocationId: number; storageLocationName: string; id: number; projectId: number; projectName: string; materialRequestId: number; requestNumber: string; materialId: number; materialCode: string; materialName: string; unit: string; quantity: number; consumedQuantity: number; releasedQuantity: number; remainingQuantity: number; reason: string; releaseReason: string; createdByName: string; createdAtUtc: string; canRelease: boolean }
type Balance = { storageLocationId: number; projectId: number; materialId: number; availableQuantity: number }
const quantity = (value: number) => new Intl.NumberFormat('en-LK', { maximumFractionDigits: 3 }).format(value)

export default function MaterialReservationsPage({ user }: { user: AuthUser }) {
  const canCreate = user.roles.some(r => ['Administrator', 'SiteEngineer', 'ProjectManager'].includes(r))
  const [projectId, setProjectId] = useState('')
  const [activeOnly, setActiveOnly] = useState(true)
  const [selection, setSelection] = useState('')
  const [storageLocationId, setStorageLocationId] = useState('')
  const [amount, setAmount] = useState('')
  const [reason, setReason] = useState('')
  const [releaseId, setReleaseId] = useState<number | null>(null)
  const [releaseReason, setReleaseReason] = useState('')
  const key = useRef(crypto.randomUUID())
  const submitted = useRef(false)
  const command = useCommand()
  const projects = useData<Project[]>('/api/projects')
  const reservations = useData<Reservation[]>('/api/material-reservations?activeOnly=' + activeOnly + (projectId ? '&projectId=' + projectId : ''))
  const eligible = useData<Eligible[]>('/api/material-reservations/eligible-requests', canCreate)
  const balances = useData<Balance[]>('/api/inventory/location-balances', canCreate)
  const choices = eligible.data?.filter(l => !projectId || l.projectId === Number(projectId)) ?? []
  const chosen = choices.find(l => l.materialRequestId + ':' + l.materialId === selection)
  const freeStock = chosen ? balances.data?.find(b => b.projectId === chosen.projectId && b.storageLocationId === Number(storageLocationId) && b.materialId === chosen.materialId)?.availableQuantity ?? 0 : 0
  const limit = chosen ? Math.max(0, Math.min(freeStock, chosen.approvedQuantity - chosen.allocatedQuantity)) : 0
  function refresh() { reservations.refresh(); eligible.refresh(); balances.refresh() }
  function editable() { submitted.current = false; key.current = crypto.randomUUID() }

  return <>
    <header className="page-header"><span className="workspace-page-emblem" aria-hidden="true"><Icon name="reservations" /></span><h1>Material Reservations</h1><p>Hold existing stock for approved work and keep other teams' available quantities accurate.</p></header>
    <div className="planning-intro"><Icon name="reservations" /><div><strong>On hand − reserved = available</strong><p>A reservation holds stock without moving it. Storekeepers select its reference when issuing materials. Holds remain until issued or explicitly released.</p></div></div>
    <Feedback error={command.error || projects.error || reservations.error || eligible.error || balances.error} message={command.message} />
    <section className="panel"><div className="section-toolbar"><h2>Reservation register</h2><button type="button" className="secondary-button" disabled={command.busy} onClick={refresh}>Refresh</button></div>
      <div className="form-grid"><label>Project<select value={projectId} disabled={command.busy} onChange={e => { setProjectId(e.target.value); setSelection(''); setStorageLocationId(''); setReleaseId(null); editable() }}><option value="">All assigned projects</option>{projects.data?.map(p => <option key={p.id} value={p.id}>{p.code} — {p.name}</option>)}</select></label><label className="checkbox-label"><input type="checkbox" checked={activeOnly} onChange={e => { setActiveOnly(e.target.checked); setReleaseId(null) }} />Only reservations with quantity remaining</label></div>
      {reservations.loading ? <p role="status">Loading reservations...</p> : !reservations.data?.length ? <p>No reservations match this selection.</p> : <div className="table-container"><table><thead><tr><th>Reference / request</th><th>Project / material</th><th>Reserved originally</th><th>Issued</th><th>Released</th><th>Still held</th><th>Created by</th><th>Action</th></tr></thead><tbody>{reservations.data.map(r => <tr key={r.id}>
        <td><strong>RS-{String(r.id).padStart(6, '0')}</strong><br />{r.requestNumber}</td><td>{r.projectName}<br /><small>{r.storageLocationName}</small><br />{r.materialCode} — {r.materialName}</td><td className="number-cell">{quantity(r.quantity)} {r.unit}</td><td className="number-cell">{quantity(r.consumedQuantity)}</td><td className="number-cell">{quantity(r.releasedQuantity)}</td><td className="number-cell"><strong>{quantity(r.remainingQuantity)} {r.unit}</strong></td><td>{r.createdByName}<br /><small>{new Date(r.createdAtUtc).toLocaleDateString()}</small></td>
        <td>{r.canRelease ? <button type="button" className="secondary-button" disabled={command.busy} onClick={() => { setReleaseId(r.id); setReleaseReason('') }}>Release remaining</button> : <span>{r.remainingQuantity > 0 ? 'Held' : 'Closed'}</span>}<details><summary>Details</summary><p>{r.reason}</p>{r.releaseReason && <p>Release: {r.releaseReason}</p>}</details></td>
      </tr>)}</tbody></table></div>}
      <p className="planning-note">Shows up to 500 records. Select a project to narrow the register. Closed reservations remain in history.</p>
    </section>
    {releaseId !== null && <section className="panel"><h2>Release remaining quantity: RS-{String(releaseId).padStart(6, '0')}</h2><form onSubmit={async e => { e.preventDefault(); if (await command.run(() => send('/api/material-reservations/' + releaseId + '/release', { reason: releaseReason }))) { setReleaseId(null); setReleaseReason(''); refresh() } }}><fieldset className="form-fields" disabled={command.busy}><label>Release reason<textarea value={releaseReason} maxLength={1000} required onChange={e => setReleaseReason(e.target.value)} /></label><div className="request-review-actions"><button className="primary-button" disabled={!releaseReason.trim()}>Release unused stock</button><button type="button" className="secondary-button" onClick={() => setReleaseId(null)}>Cancel</button></div></fieldset></form></section>}
    {canCreate && <section className="panel"><h2>Reserve approved materials</h2><p>Engineers can reserve their own approved requests. Project Managers and Administrators can reserve approved requests within their project access.</p>
      <form onSubmit={async e => { e.preventDefault(); if (!chosen || !storageLocationId || submitted.current) return; submitted.current = true
        if (await command.run(() => send('/api/material-reservations', { clientRequestId: key.current, materialRequestId: chosen.materialRequestId, storageLocationId: Number(storageLocationId), materialId: chosen.materialId, quantity: Number(amount), reason }))) { setAmount(''); setReason(''); setSelection(''); setStorageLocationId(''); editable(); refresh() } else submitted.current = false
      }}><fieldset className="form-fields" disabled={command.busy || eligible.loading || balances.loading}>
        <div className="form-grid"><label>Approved request and material<select value={selection} required onChange={e => { setSelection(e.target.value); setStorageLocationId(''); setAmount(''); editable() }}><option value="">Select approved work</option>{choices.map(l => <option key={l.materialRequestId + ':' + l.materialId} value={l.materialRequestId + ':' + l.materialId}>MR-{String(l.materialRequestId).padStart(6, '0')} · {l.projectName} · {l.materialName} ({l.unit})</option>)}</select></label>
          <StorageLocationSelect projectId={chosen?.projectId} value={storageLocationId} onChange={value => { setStorageLocationId(value); setAmount(''); editable() }} label="Reserve in store / location" />
          <label>Quantity to reserve<input type="number" min="0.001" max={limit} step="0.001" required value={amount} onChange={e => { setAmount(e.target.value); editable() }} /></label>
        </div>
        {chosen && <p className="planning-warning">Unreserved stock: {quantity(freeStock)} {chosen.unit} · Approved quantity not reserved/consumed: {quantity(chosen.approvedQuantity - chosen.allocatedQuantity)} {chosen.unit} · Maximum new hold: {quantity(limit)} {chosen.unit}. Required on {chosen.requiredDate}.</p>}
        <label>Work / reservation reason<textarea value={reason} maxLength={1000} required onChange={e => { setReason(e.target.value); editable() }} /></label>
        <div className="request-review-actions"><button className="primary-button" disabled={!chosen || limit <= 0 || !reason.trim()}>Reserve stock</button></div>
      </fieldset></form>
      {!eligible.loading && choices.length === 0 && <p>No eligible approved request lines. Submit a material request and have another authorized account approve it first.</p>}
    </section>}
  </>
}
