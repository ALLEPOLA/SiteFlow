import { useState } from 'react'
import type { FormEvent } from 'react'
import type { AuthUser } from '../types/auth'
import Brand from './ui/Brand'
import Icon from './ui/Icon'

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
      <section className="login-story">
        <Brand />
        <div className="login-story-content"><span className="eyebrow">THE FOUNDATION FOR BETTER PROJECTS</span><h2>Every material.<br />Every movement.<br /><span>One clear view.</span></h2><p>Bring your purchasing, site inventory, and project costs together in one connected workspace.</p>
        <div className="site-illustration" aria-hidden="true"><svg viewBox="0 0 480 230" fill="none"><path d="M20 210h440 M40 190l190-100 200 100-190 100L40 190Z" stroke="currentColor" opacity=".18" /><path d="m110 180 105 53 110-58-105-52-110 57Z" fill="#16483f" stroke="#70b7a2" /><path d="M110 180V95l110-57 105 53v84 M220 38v85 M110 95l110 56 105-60 M220 151v82" stroke="#86d3b9" strokeWidth="2" /><path d="M140 111v47 M169 126v47 M197 141v47 M245 139v48 M275 123v48 M303 109v47 M110 137l110 56 105-60" stroke="#70b7a2" /><path d="M360 180V35 M350 35h20 M280 35h140 M290 35l70-25 55 25 M300 35v44 M292 80h16 M355 180h18" stroke="#deb37d" strokeWidth="2" /><path d="m53 171 22-11v-36l-22 11v36Z M53 135l-19-10 22-11 19 10 M34 125v36l19 10" stroke="#70b7a2" /><circle cx="220" cy="38" r="5" fill="#e9b77c" /><circle cx="325" cy="175" r="4" fill="#e9b77c" /></svg></div>
        <div className="login-workflow"><span><Icon name="requests" />Request</span><i /><span><Icon name="orders" />Purchase</span><i /><span><Icon name="inventory" />Track</span></div></div>
        <p className="login-story-footer">Purpose-built for construction materials management.</p>
      </section>
      <div className="login-form-area">
      <section className="panel login-panel">
        <div className="login-mobile-brand"><Brand /></div>
        <span className="login-welcome-icon"><Icon name="lock" /></span>
        <p className="eyebrow text-teal-700">YOUR WORKSPACE AWAITS</p>
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
                  placeholder="you@company.com"
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
                  placeholder="Enter your password"
                  value={password}
                  onChange={(event) => setPassword(event.target.value)}
                  required
                />
              </label>

              <button className="primary-button" type="submit">
                {loading ? <><span className="loading-spinner" aria-hidden="true" />Signing in...</> : <>Sign in to your workspace<Icon name="arrow" /></>}
              </button>
            </div>
          </fieldset>

          {error && (
            <p className="error-message" role="alert">
              {error}
            </p>
          )}
        </form>
        <p className="login-help">Need access? Contact your SiteFlow administrator.</p>
      </section>
      <p className="login-form-footer"><Icon name="lock" />Your project workspace. Securely connected.</p>
      </div>
    </main>
  )
}
