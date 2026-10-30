import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'


interface Project {
  id: number
  code: string
  name: string
  location: string
  budget: number
  status: string
}

interface ApiError {
  message?: string
  title?: string
  errors?: Record<string, string[]>
}

const currencyFormatter = new Intl.NumberFormat('en-LK', {
  style: 'currency',
  currency: 'LKR',
})

function ProjectsPage() {
  const [projects, setProjects] = useState<Project[]>([])
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState('')

  const [code, setCode] = useState('')
  const [name, setName] = useState('')
  const [location, setLocation] = useState('')
  const [budget, setBudget] = useState('')

  const [saving, setSaving] = useState(false)
  const [formError, setFormError] = useState('')
  const [success, setSuccess] = useState('')

  useEffect(() => {
    const controller = new AbortController()

    async function loadProjects() {
      try {
        const response = await fetch('/api/projects', {
          signal: controller.signal,
        })

        if (!response.ok) {
          throw new Error(
            `Unable to load projects (${response.status}).`,
          )
        }

        const data: Project[] = await response.json()

        if (!controller.signal.aborted) {
          setProjects(data)
        }
      } catch (error) {
        if (controller.signal.aborted) return

        setLoadError(
          error instanceof Error
            ? error.message
            : 'Unable to load projects.',
        )
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false)
        }
      }
    }

    void loadProjects()

    return () => controller.abort()
  }, [])

  async function handleCreateProject(
    event: FormEvent<HTMLFormElement>,
  ) {
    event.preventDefault()

    if (saving) return

    setFormError('')
    setSuccess('')

    if (!code.trim() || !name.trim() || !location.trim()) {
      setFormError('Please complete all text fields.')
      return
    }

    const amount = Number(budget)

    if (
      budget.trim() === '' ||
      !Number.isFinite(amount) ||
      amount < 0
    ) {
      setFormError('Enter a valid budget of zero or more.')
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

      const project: Project = await response.json()

      setProjects((current) =>
        [...current, project].sort((a, b) =>
          a.code.localeCompare(b.code),
        ),
      )

      setCode('')
      setName('')
      setLocation('')
      setBudget('')
      setSuccess(`Project ${project.code} created successfully.`)
    } catch (error) {
      setFormError(
        error instanceof Error
          ? error.message
          : 'Unable to create project.',
      )
    } finally {
      setSaving(false)
    }
  }

  return (
    <>
      <header className="page-header">
       
        <h1>Construction Projects</h1>
        <p>Manage your construction sites and project budgets.</p>
      </header>

      <section className="panel" aria-labelledby="create-heading">
        <h2 id="create-heading">Create Project</h2>
        <p className="section-description">
          Enter the details of your new construction project.
        </p>

        <form onSubmit={handleCreateProject}>
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
                  onChange={(event) =>
                    setLocation(event.target.value)
                  }
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

          {formError && (
            <p className="error-message" role="alert">
              {formError}
            </p>
          )}

          {success && (
            <p className="success-message" role="status">
              {success}
            </p>
          )}
        </form>
      </section>

      <section className="panel" aria-labelledby="projects-heading">
        <h2 id="projects-heading">Projects</h2>

        {loading && <p role="status">Loading projects...</p>}

        {loadError && (
          <p className="error-message" role="alert">
            {loadError} Check that your backend is running, then
            refresh this page.
          </p>
        )}

        {!loading && !loadError && projects.length === 0 && (
          <p>No projects yet. Create your first project above.</p>
        )}

        {!loading && !loadError && projects.length > 0 && (
          <div className="table-container">
            <table>
              <thead>
                <tr>
                  <th scope="col">Code</th>
                  <th scope="col">Project name</th>
                  <th scope="col">Location</th>
                  <th scope="col" className="number-cell">
                    Budget
                  </th>
                  <th scope="col">Status</th>
                </tr>
              </thead>

              <tbody>
                {projects.map((project) => (
                  <tr key={project.id}>
                    <td className="project-code">{project.code}</td>
                    <td>{project.name}</td>
                    <td>{project.location}</td>
                    <td className="number-cell">
                      {currencyFormatter.format(project.budget)}
                    </td>
                    <td>
                      <span className="status-badge">
                        {project.status}
                      </span>
                    </td>
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

export default ProjectsPage