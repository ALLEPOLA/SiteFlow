import { useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { apiFetch, readApiResponse } from '../api/apiFetch'
import StaffAdministration from './erp/StaffAdministration'

type UserRole =
  | 'SiteEngineer'
  | 'ProjectManager'
  | 'ProcurementOfficer'
  | 'Storekeeper'

type CreatedUser = {
  id: string
  fullName: string
  email: string
  role: UserRole
}

const roles: {
  value: UserRole
  label: string
  description: string
}[] = [
  {
    value: 'SiteEngineer',
    label: 'Site Engineer',
    description: 'Creates and submits material requests.',
  },
  {
    value: 'ProjectManager',
    label: 'Project Manager',
    description: 'Approves or rejects requests created by other users.',
  },
  {
    value: 'ProcurementOfficer',
    label: 'Procurement Officer',
    description: 'Manages materials, suppliers, and purchase orders.',
  },
  {
    value: 'Storekeeper',
    label: 'Storekeeper',
    description: 'Records deliveries and issues materials from stock.',
  },
]

function roleLabel(role: UserRole) {
  return roles.find(item => item.value === role)?.label ?? role
}

export default function UserManagementPage() {
  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [role, setRole] = useState<UserRole>('SiteEngineer')
  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')

  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const [createdUsers, setCreatedUsers] = useState<CreatedUser[]>([])

  const savingRef = useRef(false)

  const selectedRole = roles.find(item => item.value === role)

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (savingRef.current) return

    setError('')
    setSuccess('')

    if (!fullName.trim() || !email.trim()) {
      setError('Enter the full name and email address.')
      return
    }

    if (password !== confirmPassword) {
      setError('The passwords do not match.')
      return
    }

    if (
      password.length < 12 ||
      !/[A-Z]/.test(password) ||
      !/[a-z]/.test(password) ||
      !/[0-9]/.test(password) ||
      !/[^a-zA-Z0-9]/.test(password)
    ) {
      setError(
        'Use at least 12 characters, including uppercase, lowercase, a number, and a symbol.',
      )
      return
    }

    savingRef.current = true
    setSaving(true)

    try {
      const response = await apiFetch('/api/users', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          fullName: fullName.trim(),
          email: email.trim(),
          password,
          role,
        }),
      })

      const created = await readApiResponse<CreatedUser>(response)

      setCreatedUsers(current => [
        created,
        ...current.filter(user => user.id !== created.id),
      ])

      setSuccess(
        `${created.fullName} was created as ${roleLabel(created.role)}. They can now sign in using their email and the password you entered.`,
      )

      setFullName('')
      setEmail('')
      setRole('SiteEngineer')
      setPassword('')
      setConfirmPassword('')
    } catch (saveError) {
      setError(
        saveError instanceof Error
          ? saveError.message
          : 'Unable to create the account.',
      )
    } finally {
      savingRef.current = false
      setSaving(false)
    }
  }

  return (
    <>
      <header className="page-header">
        <h1>User Management</h1>
        <p>Create staff accounts and assign their responsibilities.</p>
      </header>

      <StaffAdministration key={createdUsers.length} />

      {error && (
        <p className="error-message" role="alert">
          {error}
        </p>
      )}

      {success && (
        <p className="success-message" role="status">
          {success}
        </p>
      )}

      <section className="panel">
        <h2>Create staff account</h2>

        <form onSubmit={handleSubmit}>
          <fieldset className="user-management-fieldset" disabled={saving}>
            <div className="form-grid">
              <label>
                Full name
                <input
                  type="text"
                  value={fullName}
                  onChange={event => setFullName(event.target.value)}
                  maxLength={150}
                  autoComplete="off"
                  required
                />
              </label>

              <label>
                Email address
                <input
                  type="email"
                  value={email}
                  onChange={event => setEmail(event.target.value)}
                  maxLength={254}
                  autoComplete="off"
                  autoCapitalize="none"
                  spellCheck={false}
                  required
                />
              </label>

              <label>
                Role
                <select
                  value={role}
                  onChange={event =>
                    setRole(event.target.value as UserRole)
                  }
                  aria-describedby="user-role-description"
                  required
                >
                  {roles.map(item => (
                    <option key={item.value} value={item.value}>
                      {item.label}
                    </option>
                  ))}
                </select>

                <small id="user-role-description">
                  {selectedRole?.description}
                </small>
              </label>

              <label>
                Password
                <input
                  type="password"
                  value={password}
                  onChange={event => setPassword(event.target.value)}
                  minLength={12}
                  maxLength={128}
                  autoComplete="new-password"
                  aria-describedby="user-password-help"
                  required
                />
              </label>

              <label>
                Confirm password
                <input
                  type="password"
                  value={confirmPassword}
                  onChange={event =>
                    setConfirmPassword(event.target.value)
                  }
                  minLength={12}
                  maxLength={128}
                  autoComplete="new-password"
                  required
                />
              </label>
            </div>

            <p id="user-password-help">
              Passwords need at least 12 characters, including uppercase,
              lowercase, a number, and a symbol.
            </p>

            <button type="submit" className="primary-button">
              {saving ? 'Creating account...' : 'Create account'}
            </button>
          </fieldset>
        </form>
      </section>

      <section className="panel">
        <h2>Accounts created during this visit</h2>

        <p>
          This list shows accounts created while this page is open.
          Refreshing or leaving the page clears the list; the accounts
          remain saved.
        </p>

        {createdUsers.length === 0 ? (
          <p>No accounts created during this visit.</p>
        ) : (
          <div className="table-container">
            <table>
              <thead>
                <tr>
                  <th>Full name</th>
                  <th>Email</th>
                  <th>Role</th>
                </tr>
              </thead>

              <tbody>
                {createdUsers.map(user => (
                  <tr key={user.id}>
                    <td>{user.fullName}</td>
                    <td>{user.email}</td>
                    <td>{roleLabel(user.role)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </>
  )
}
