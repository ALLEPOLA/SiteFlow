import { useState } from 'react'
import type { FormEvent } from 'react'

interface CreatedProject {
  id: number
  code: string
  name: string
  location: string
  budget: number
  status: string
}

interface Props {
  onCreated: (project: CreatedProject) => void
}

interface ApiError {
  message?: string
  title?: string
  errors?: Record<string, string[]>
}

export default function CreateProjectForm({ onCreated }: Props) {
  const [code, setCode] = useState('')
  const [name, setName] = useState('')
  const [location, setLocation] = useState('')
  const [budget, setBudget] = useState('')
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (saving) return

    setError('')
    setSuccess('')

    const amount = Number(budget)

    if (!code.trim() || !name.trim() || !location.trim()) {
      setError('Please enter a project code, name, and location.')
      return
    }

    if (budget.trim() === '' || !Number.isFinite(amount) || amount < 0) {
      setError('Please enter a valid budget of zero or more.')
      return
    }

    setSaving(true)

    try {
      const response = await fetch('/api/projects', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          code: code.trim(),
          name: name.trim(),
          location: location.trim(),
          budget: amount,
        }),
      })

      if (!response.ok) {
        const problem: ApiError | null = await response
          .json()
          .catch(() => null)

        const validationMessages = problem?.errors
          ? Object.values(problem.errors).flat().join(' ')
          : ''

        throw new Error(
          validationMessages ||
            problem?.message ||
            problem?.title ||
            `Unable to create project (${response.status}).`,
        )
      }

      const project: CreatedProject = await response.json()
      onCreated(project)

      setCode('')
      setName('')
      setLocation('')
      setBudget('')
      setSuccess(`Project ${project.code} created successfully.`)
    } catch (error) {
      setError(
        error instanceof Error
          ? error.message
          : 'Unable to create project.',
      )
    } finally {
      setSaving(false)
    }
  }

  return (
    <section className="panel">
      <h2>Create Project</h2>

      <form onSubmit={handleSubmit}>
        <fieldset className="form-fields" disabled={saving}>
          <div className="form-grid">
            <label htmlFor="project-code">
              Project code
              <input
                id="project-code"
                value={code}
                onChange={(event) => setCode(event.target.value)}
                placeholder="PRJ-002"
                maxLength={30}
                required
              />
            </label>

            <label htmlFor="project-name">
              Project name
              <input
                id="project-name"
                value={name}
                onChange={(event) => setName(event.target.value)}
                placeholder="Colombo Apartment Building"
                maxLength={200}
                required
              />
            </label>

            <label htmlFor="project-location">
              Location
              <input
                id="project-location"
                value={location}
                onChange={(event) => setLocation(event.target.value)}
                placeholder="Colombo"
                maxLength={200}
                required
              />
            </label>

            <label htmlFor="project-budget">
              Budget (LKR)
              <input
                id="project-budget"
                type="number"
                value={budget}
                onChange={(event) => setBudget(event.target.value)}
                placeholder="7500000"
                min="0"
                step="0.01"
                required
              />
            </label>
          </div>

          <button className="primary-button" type="submit">
            {saving ? 'Saving...' : 'Create Project'}
          </button>
        </fieldset>

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
      </form>
    </section>
  )
}