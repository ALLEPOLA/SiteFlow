import { useState } from 'react'
import { send, useCommand, useData } from './ErpUi'
import { Feedback } from './Feedback'
import type { Project } from './ErpUi'

type Staff = { id: string; fullName: string; email: string; roles: string[]; isActive: boolean; mustChangePassword: boolean }
type Assignment = { projectId: number; userId: string; projectName: string; fullName: string }
const roles = ['SiteEngineer', 'ProjectManager', 'ProcurementOfficer', 'Storekeeper']

export default function StaffAdministration() {
  const staff = useData<Staff[]>('/api/users')
  const projects = useData<Project[]>('/api/projects')
  const assignments = useData<Assignment[]>('/api/administration/assignments')
  const command = useCommand()
  const [selected, setSelected] = useState<Staff | null>(null)
  const [name, setName] = useState('')
  const [role, setRole] = useState(roles[0])
  const [active, setActive] = useState(true)
  const [reason, setReason] = useState('')
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [projectId, setProjectId] = useState('')
  const [userId, setUserId] = useState('')
  const [assignmentReason, setAssignmentReason] = useState('')

  function edit(user: Staff) {
    setSelected(user); setName(user.fullName); setRole(user.roles[0] ?? roles[0]); setActive(user.isActive)
    setReason(''); setPassword(''); setConfirm('')
  }
  return <>
    <Feedback error={command.error || staff.error || projects.error || assignments.error} message={command.message} />
    <section className="panel">
      <div className="section-toolbar"><h2>All staff accounts</h2><button type="button" className="secondary-button" onClick={staff.refresh}>Refresh accounts</button></div>
      {staff.loading ? <p role="status">Loading staff...</p> : <div className="table-container"><table><thead><tr><th>Name</th><th>Email</th><th>Roles</th><th>Status</th><th>Action</th></tr></thead>
        <tbody>{staff.data?.map(u => <tr key={u.id}><td>{u.fullName}</td><td>{u.email}</td><td>{u.roles.join(', ')}</td><td>{u.isActive ? 'Active' : 'Disabled'}{u.mustChangePassword && ' · Password change required'}</td>
          <td>{!u.roles.includes('Administrator') && <button type="button" className="secondary-button" disabled={command.busy} onClick={() => edit(u)}>Manage</button>}</td></tr>)}</tbody></table></div>}
    </section>
    {selected && <section className="panel"><h2>Manage {selected.fullName}</h2>
      <form onSubmit={async e => { e.preventDefault(); if (await command.run(() => send(`/api/users/${selected.id}`, { fullName: name, role, isActive: active, reason }, 'PUT'))) { staff.refresh(); setSelected(null) } }}>
        <fieldset className="form-fields" disabled={command.busy}><div className="form-grid">
          <label>Full name<input value={name} onChange={e => setName(e.target.value)} maxLength={150} required /></label>
          <label>Role<select value={role} onChange={e => setRole(e.target.value)}>{roles.map(r => <option key={r}>{r}</option>)}</select></label>
          <label className="checkbox-label"><input type="checkbox" checked={active} onChange={e => setActive(e.target.checked)} />Account enabled</label>
          <label>Change reason<input value={reason} onChange={e => setReason(e.target.value)} maxLength={1000} required /></label>
        </div><button className="primary-button">Save staff changes</button></fieldset>
      </form>
      <h3>Reset temporary password</h3>
      <form onSubmit={async e => { e.preventDefault(); if (await command.run(async () => { if (password !== confirm) throw new Error('Passwords must match.'); return send(`/api/users/${selected.id}/reset-password`, { password, reason }) })) { setPassword(''); setConfirm(''); staff.refresh() } }}>
        <fieldset className="form-fields" disabled={command.busy}><div className="form-grid">
          <label>Temporary password<input type="password" value={password} onChange={e => setPassword(e.target.value)} minLength={12} maxLength={128} autoComplete="new-password" required /></label>
          <label>Confirm temporary password<input type="password" value={confirm} onChange={e => setConfirm(e.target.value)} minLength={12} maxLength={128} autoComplete="new-password" required /></label>
          <label>Reset reason<input value={reason} onChange={e => setReason(e.target.value)} maxLength={1000} required /></label>
        </div><p>Use uppercase, lowercase, a number, and a symbol. Previous sessions end and the user must choose a new password.</p><button className="secondary-button">Reset password</button></fieldset>
      </form>
    </section>}
    <section className="panel"><h2>Project staff assignments</h2><p>Administrators can access every project. Other staff can access only their assigned projects, using their account role.</p>
      <form onSubmit={async e => { e.preventDefault(); if (await command.run(() => send('/api/administration/assignments', { projectId: Number(projectId), userId, reason: assignmentReason }))) { assignments.refresh(); setAssignmentReason('') } }}>
        <fieldset className="form-fields" disabled={command.busy}><div className="form-grid">
          <label>Project<select value={projectId} onChange={e => setProjectId(e.target.value)} required><option value="">Select project</option>{projects.data?.map(p => <option key={p.id} value={p.id}>{p.code} — {p.name}</option>)}</select></label>
          <label>Staff<select value={userId} onChange={e => setUserId(e.target.value)} required><option value="">Select staff</option>{staff.data?.filter(u => u.isActive && !u.roles.includes('Administrator')).map(u => <option key={u.id} value={u.id}>{u.fullName} ({u.roles.join(', ')})</option>)}</select></label>
          <label>Assignment reason<input value={assignmentReason} onChange={e => setAssignmentReason(e.target.value)} maxLength={1000} required /></label>
        </div><button className="primary-button">Assign staff</button></fieldset>
      </form>
      <div className="table-container"><table><thead><tr><th>Project</th><th>Staff</th><th>Action</th></tr></thead><tbody>{assignments.data?.map(a => <tr key={`${a.projectId}-${a.userId}`}><td>{a.projectName}</td><td>{a.fullName}</td><td>
        <button type="button" className="secondary-button" disabled={command.busy} onClick={async () => {
          const reason = window.prompt('Reason for removing this assignment:'); if (!reason?.trim()) return
          if (await command.run(() => send('/api/administration/assignments/remove', { projectId: a.projectId, userId: a.userId, reason }))) assignments.refresh()
        }}>Remove assignment</button>
      </td></tr>)}</tbody></table></div>
    </section>
  </>
}
