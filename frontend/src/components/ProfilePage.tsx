import PasswordInput from './ui/PasswordInput'
import Icon from './ui/Icon'
import { useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { apiFetch, readApiResponse } from '../api/apiFetch'
import type { AuthUser } from '../types/auth'

type Props = {
  user: AuthUser
  onUpdated: (user: AuthUser) => void
  onSignedOut: (message: string) => void
}

function getError(error: unknown) {
  return error instanceof Error
    ? error.message
    : 'Unable to complete the operation.'
}

export default function ProfilePage({
  user,
  onUpdated,
  onSignedOut,
}: Props) {
  const [fullName, setFullName] = useState(user.fullName)
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')

  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')

  const busyRef = useRef(false)

  async function saveProfile(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (busyRef.current) return

    setError('')
    setSuccess('')

    if (!fullName.trim()) {
      setError('Enter your full name.')
      return
    }

    busyRef.current = true
    setBusy(true)

    try {
      const response = await apiFetch('/api/profile', {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          fullName: fullName.trim(),
        }),
      })

      if (response.status === 401) {
        onSignedOut('Your session ended. Please sign in again.')
        return
      }

      const updated = await readApiResponse<AuthUser>(response)

      onUpdated(updated)
      setFullName(updated.fullName)
      setSuccess('Your profile was updated.')
    } catch (saveError) {
      setError(getError(saveError))
    } finally {
      busyRef.current = false
      setBusy(false)
    }
  }

  async function changePassword(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (busyRef.current) return

    setError('')
    setSuccess('')

    if (newPassword !== confirmPassword) {
      setError('The new passwords do not match.')
      return
    }

    if (currentPassword === newPassword) {
      setError('Choose a password different from your current password.')
      return
    }

    if (
      newPassword.length < 12 ||
      !/[A-Z]/.test(newPassword) ||
      !/[a-z]/.test(newPassword) ||
      !/[0-9]/.test(newPassword) ||
      !/[^a-zA-Z0-9]/.test(newPassword)
    ) {
      setError(
        'Use at least 12 characters, including uppercase, lowercase, a number, and a symbol.',
      )
      return
    }

    busyRef.current = true
    setBusy(true)

    try {
      const response = await apiFetch('/api/profile/change-password', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          currentPassword,
          newPassword,
          confirmPassword,
        }),
      })

      if (response.status === 401) {
        onSignedOut(
          'Your session ended or the account is locked. Try signing in again later.',
        )
        return
      }

      const result = await readApiResponse<{ message: string }>(response)

      setCurrentPassword('')
      setNewPassword('')
      setConfirmPassword('')

      onSignedOut(result.message)
    } catch (changeError) {
      setError(getError(changeError))
    } finally {
      busyRef.current = false
      setBusy(false)
    }
  }

  return (
    <>
      <header className="page-header">
        <span className="workspace-page-emblem" aria-hidden="true"><Icon name="profile" /></span>
        <h1>
          {user.mustChangePassword
            ? 'Change your temporary password'
            : 'My Profile'}
        </h1>
        <p>
          {user.mustChangePassword
            ? 'Choose your own password before accessing SiteFlow.'
            : 'Manage your name and password.'}
        </p>
      </header>

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
        <h2>Account details</h2>
        <p><strong>Email:</strong> {user.email}</p>
        <p><strong>Roles:</strong> {user.roles.join(', ')}</p>

        {!user.mustChangePassword && (
          <form onSubmit={saveProfile}>
            <fieldset className="profile-fields" disabled={busy}>
              <label>
                Full name
                <input
                  type="text"
                  value={fullName}
                  onChange={event => setFullName(event.target.value)}
                  maxLength={150}
                  autoComplete="name"
                  required
                />
              </label>

              <button type="submit" className="primary-button">
                {busy ? 'Please wait...' : 'Save profile'}
              </button>
            </fieldset>
          </form>
        )}
      </section>

      <section className="panel">
        <h2>Change password</h2>

        <form onSubmit={changePassword}>
          <fieldset className="profile-fields" disabled={busy}>
            <PasswordInput label={user.mustChangePassword
                ? 'Temporary password'
                : 'Current password'}
                value={currentPassword}
                onChange={event =>
                  setCurrentPassword(event.target.value)
                }
                autoComplete="current-password"
                maxLength={1024}
                required
               />

            <PasswordInput label="New password"
                value={newPassword}
                onChange={event => setNewPassword(event.target.value)}
                autoComplete="new-password"
                minLength={12}
                maxLength={128}
                required
               />

            <PasswordInput label="Confirm new password"
                value={confirmPassword}
                onChange={event =>
                  setConfirmPassword(event.target.value)
                }
                autoComplete="new-password"
                minLength={12}
                maxLength={128}
                required
               />

            <p>
              Use at least 12 characters, including uppercase,
              lowercase, a number, and a symbol.
            </p>

            <p>
              After changing your password, sign in again.
              Your other sessions will also end on their next request.
            </p>

            <button type="submit" className="primary-button">
              {busy ? 'Please wait...' : 'Change password'}
            </button>
          </fieldset>
        </form>
      </section>
    </>
  )
}