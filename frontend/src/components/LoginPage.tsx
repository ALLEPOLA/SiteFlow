import { useState } from 'react'
import type { FormEvent } from 'react'
import type { AuthUser } from '../types/auth'

interface Props {
  onLoggedIn: (user: AuthUser) => void
}

export default function LoginPage({ onLoggedIn }: Props) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (loading) return

    setError('')
    setLoading(true)

    try {
      const csrfResponse = await fetch('/api/auth/csrf', {
        cache: 'no-store',
      })

      if (!csrfResponse.ok) {
        throw new Error('Unable to initialize login. Please try again.')
      }

      const csrf: { token: string } = await csrfResponse.json()

      const response = await fetch('/api/auth/login', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'X-CSRF-TOKEN': csrf.token,
        },
        body: JSON.stringify({
          email: email.trim(),
          password,
        }),
      })

      if (!response.ok) {
        const problem = await response.json().catch(() => null)

        throw new Error(
          problem?.message || 'Unable to sign in. Please try again.',
        )
      }

      const user: AuthUser = await response.json()
      setPassword('')
      onLoggedIn(user)
    } catch (error) {
      setError(
        error instanceof Error ? error.message : 'Login failed.',
      )
    } finally {
      setLoading(false)
    }
  }

  return (
    <main className="login-layout">
      <section className="panel login-panel">
        <p className="brand">SiteFlow ERP</p>
        <h1>Welcome back</h1>
        <p className="section-description">
          Sign in to manage your construction operations.
        </p>

        <form onSubmit={handleSubmit}>
          <fieldset className="form-fields" disabled={loading}>
            <div className="login-fields">
              <label htmlFor="login-email">
                Email
                <input
                  id="login-email"
                  type="email"
                  autoComplete="username"
                  value={email}
                  onChange={(event) => setEmail(event.target.value)}
                  required
                />
              </label>

              <label htmlFor="login-password">
                Password
                <input
                  id="login-password"
                  type="password"
                  autoComplete="current-password"
                  value={password}
                  onChange={(event) => setPassword(event.target.value)}
                  required
                />
              </label>

              <button className="primary-button" type="submit">
                {loading ? 'Signing in...' : 'Sign In'}
              </button>
            </div>
          </fieldset>

          {error && (
            <p className="error-message" role="alert">
              {error}
            </p>
          )}
        </form>
      </section>
    </main>
  )
}