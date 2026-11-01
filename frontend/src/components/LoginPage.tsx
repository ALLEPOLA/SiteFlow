import PasswordInput from './ui/PasswordInput'
import { useState } from 'react'
import type { FormEvent } from 'react'
import type { AuthUser } from '../types/auth'
import Brand from './ui/Brand'
import Icon from './ui/Icon'
import ConstructionAnimation from './ConstructionAnimation'

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
        <ConstructionAnimation />
        <div className="login-workflow"><span><Icon name="requests" />Request</span><i /><span><Icon name="orders" />Purchase</span><i /><span><Icon name="inventory" />Track</span></div></div>
        <p className="login-story-footer">Purpose-built for construction materials management.</p>
      </section>
      <div className="login-form-area">
      <div className="login-form-atmosphere" aria-hidden="true">
        {[0, 1, 2, 3, 4].map(i => <i key={i} className="sf-site-particle" />)}
        <span className="sf-ambient-glow sf-ambient-glow-one" />
        <span className="sf-ambient-glow sf-ambient-glow-two" />
        <span className="sf-blueprint-orbit" />
        <span className="sf-floating-symbol sf-floating-symbol-one"><Icon name="projects" /></span>
        <span className="sf-floating-symbol sf-floating-symbol-two"><Icon name="inventory" /></span>
        <span className="sf-floating-symbol sf-floating-symbol-three"><Icon name="orders" /></span>
        <svg className="sf-form-skyline" viewBox="0 0 600 160" fill="none" focusable="false">
          <path d="M0 145H600M40 145V88H95V145M55 88V60H81V88M120 145V40H195V145M134 55H146M163 55H176M134 77H146M163 77H176M134 99H146M163 99H176M230 145V78H295V145M245 92H255M271 92H281M330 145V22H348V145M310 22H460L348 4L310 22M432 22V68M424 68H440M470 145V92H540V145M490 92V65H520V92" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      </div>
      <section className="panel login-panel">
        <div className="login-mobile-brand"><Brand /></div>
        <span className="login-welcome-icon" aria-hidden="true">
          <svg className="sf-login-mascot" viewBox="0 0 64 64" fill="none" focusable="false">
            <path d="M9 64C9 43 55 43 55 64" fill="#2d7358" />
            <path d="M20 53V64M44 53V64" stroke="#dce9a8" strokeWidth="5" />
            <rect x="16" y="14" width="32" height="37" rx="15" fill="#e8b58c" />
            <g className="sf-mascot-eyes">
              <circle cx="25" cy="33" r="2" fill="#173d33" />
              <circle cx="39" cy="33" r="2" fill="#173d33" />
            </g>
            <path d="M27 41Q32 45 37 41" stroke="#995f43" strokeWidth="2" strokeLinecap="round" />
            <path d="M13 24C13-2 51-2 51 24" fill="#efb84b" />
            <path d="M32 6V19" stroke="#ffe2a1" strokeWidth="5" strokeLinecap="round" />
            <rect x="9" y="21" width="46" height="7" rx="3.5" fill="#ffcf70" />
          </svg>
          <span className="sf-welcome-spark">✦</span><span className="sf-welcome-spark">✦</span>
        </span>
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

              <PasswordInput label="Password"
                  id="login-password"
                  autoComplete="current-password"
                  placeholder="Enter your password"
                  value={password}
                  onChange={(event) => setPassword(event.target.value)}
                  required
                 />

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
