import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'

interface Material {
  id: number
  code: string
  name: string
  category: string
  unit: string
  isActive: boolean
}

interface ApiError {
  message?: string
  title?: string
  errors?: Record<string, string[]>
}

export default function MaterialsPage() {
  const [materials, setMaterials] = useState<Material[]>([])
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState('')

  const [code, setCode] = useState('')
  const [name, setName] = useState('')
  const [category, setCategory] = useState('')
  const [unit, setUnit] = useState('bag')

  const [saving, setSaving] = useState(false)
  const [formError, setFormError] = useState('')
  const [success, setSuccess] = useState('')

  useEffect(() => {
    const controller = new AbortController()

    async function loadMaterials() {
      try {
        const response = await fetch('/api/materials', {
          signal: controller.signal,
        })

        if (!response.ok) {
          throw new Error(
            `Unable to load materials (${response.status}).`,
          )
        }

        const data: Material[] = await response.json()

        if (!controller.signal.aborted) {
          setMaterials(data)
        }
      } catch (error) {
        if (controller.signal.aborted) return

        setLoadError(
          error instanceof Error
            ? error.message
            : 'Unable to load materials.',
        )
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false)
        }
      }
    }

    void loadMaterials()

    return () => controller.abort()
  }, [])

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (saving) return

    setFormError('')
    setSuccess('')

    if (!code.trim() || !name.trim() || !category.trim()) {
      setFormError('Please complete all fields.')
      return
    }

    setSaving(true)

    try {
      const response = await fetch('/api/materials', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          code: code.trim(),
          name: name.trim(),
          category: category.trim(),
          unit,
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
            `Unable to create material (${response.status}).`,
        )
      }

      const material: Material = await response.json()

      setMaterials((current) =>
        [...current, material].sort((a, b) =>
          a.code.localeCompare(b.code),
        ),
      )

      setCode('')
      setName('')
      setCategory('')
      setUnit('bag')
      setSuccess(`Material ${material.code} created successfully.`)
    } catch (error) {
      setFormError(
        error instanceof Error
          ? error.message
          : 'Unable to create material.',
      )
    } finally {
      setSaving(false)
    }
  }

  return (
    <>
      <header className="page-header">
        <h1>Materials</h1>
        <p>Manage the materials used across your construction sites.</p>
      </header>

      <section className="panel" aria-labelledby="create-material-heading">
        <h2 id="create-material-heading">Create Material</h2>

        <form onSubmit={handleSubmit}>
          <fieldset className="form-fields" disabled={saving}>
            <div className="form-grid">
              <label htmlFor="material-code">
                Material code
                <input
                  id="material-code"
                  value={code}
                  onChange={(event) => setCode(event.target.value)}
                  placeholder="MAT-002"
                  maxLength={30}
                  required
                />
              </label>

              <label htmlFor="material-name">
                Material name
                <input
                  id="material-name"
                  value={name}
                  onChange={(event) => setName(event.target.value)}
                  placeholder="Steel Rod"
                  maxLength={200}
                  required
                />
              </label>

              <label htmlFor="material-category">
                Category
                <input
                  id="material-category"
                  value={category}
                  onChange={(event) => setCategory(event.target.value)}
                  placeholder="Building Materials"
                  maxLength={100}
                  required
                />
              </label>

              <label htmlFor="material-unit">
                Unit
                <select
                  id="material-unit"
                  value={unit}
                  onChange={(event) => setUnit(event.target.value)}
                  required
                >
                  <option value="bag">Bag</option>
                  <option value="kg">Kilogram (kg)</option>
                  <option value="piece">Piece</option>
                  <option value="m">Metre (m)</option>
                  <option value="m2">Square metre (m²)</option>
                  <option value="m3">Cubic metre (m³)</option>
                  <option value="litre">Litre</option>
                </select>
              </label>
            </div>

            <button className="primary-button" type="submit">
              {saving ? 'Saving...' : 'Create Material'}
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

      <section className="panel" aria-labelledby="materials-heading">
        <h2 id="materials-heading">Material List</h2>

        {loading && <p role="status">Loading materials...</p>}

        {loadError && (
          <p className="error-message" role="alert">
            {loadError}
          </p>
        )}

        {!loading && !loadError && materials.length === 0 && (
          <p>No materials yet. Create your first material above.</p>
        )}

        {!loading && !loadError && materials.length > 0 && (
          <div className="table-container">
            <table>
              <thead>
                <tr>
                  <th scope="col">Code</th>
                  <th scope="col">Name</th>
                  <th scope="col">Category</th>
                  <th scope="col">Unit</th>
                  <th scope="col">Status</th>
                </tr>
              </thead>

              <tbody>
                {materials.map((material) => (
                  <tr key={material.id}>
                    <td className="project-code">{material.code}</td>
                    <td>{material.name}</td>
                    <td>{material.category}</td>
                    <td>{material.unit}</td>
                    <td>
                      <span className="status-badge">
                        {material.isActive ? 'Active' : 'Inactive'}
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