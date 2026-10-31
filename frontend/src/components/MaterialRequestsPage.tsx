import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { apiFetch, readApiResponse } from '../api/apiFetch'

interface Project {
  id: number
  code: string
  name: string
  status: string
}

interface Material {
  id: number
  code: string
  name: string
  unit: string
  isActive: boolean
}

interface RequestLine {
  id: number
  materialId: number
  materialCode: string
  materialName: string
  unit: string
  quantity: number
  estimatedUnitPrice: number
  lineTotal: number
}

interface MaterialRequest {
  id: number
  requestNumber: string
  projectName: string
  requiredDate: string
  reason: string
  status: string
  createdByUserId: string | null
  createdByName: string
  submittedAtUtc: string | null
  canSubmit: boolean
  totalEstimatedCost: number
  lines: RequestLine[]
}

interface DraftLine {
  key: string
  materialId: string
  quantity: string
  estimatedUnitPrice: string
}

const money = new Intl.NumberFormat('en-LK', {
  style: 'currency',
  currency: 'LKR',
})

function emptyLine(): DraftLine {
  return {
    key: crypto.randomUUID(),
    materialId: '',
    quantity: '',
    estimatedUnitPrice: '',
  }
}

function lineTotal(line: DraftLine) {
  const quantity = Number(line.quantity)
  const price = Number(line.estimatedUnitPrice)

  if (!Number.isFinite(quantity) || !Number.isFinite(price)) return 0

  return Math.round((quantity * price + Number.EPSILON) * 100) / 100
}

export default function MaterialRequestsPage() {
  const [projects, setProjects] = useState<Project[]>([])
  const [materials, setMaterials] = useState<Material[]>([])
  const [requests, setRequests] = useState<MaterialRequest[]>([])
  const [canCreate, setCanCreate] = useState(false)

  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState('')
  const [projectId, setProjectId] = useState('')
  const [requiredDate, setRequiredDate] = useState('')
  const [reason, setReason] = useState('')
  const [lines, setLines] = useState<DraftLine[]>(() => [emptyLine()])

  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')
  const [selected, setSelected] = useState<MaterialRequest | null>(null)

  const [submittingId, setSubmittingId] = useState<number | null>(null)
  const [submitError, setSubmitError] = useState('')
  const [submitSuccess, setSubmitSuccess] = useState('')

  useEffect(() => {
    const controller = new AbortController()
    const options = { signal: controller.signal }

    async function load() {
      try {
        const [projectData, materialData, requestData, user] =
          await Promise.all([
            apiFetch('/api/projects', options).then((response) =>
              readApiResponse<Project[]>(response),
            ),
            apiFetch('/api/materials', options).then((response) =>
              readApiResponse<Material[]>(response),
            ),
            apiFetch('/api/material-requests', options).then((response) =>
              readApiResponse<MaterialRequest[]>(response),
            ),
            apiFetch('/api/auth/me', {
              ...options,
              cache: 'no-store',
            }).then((response) =>
              readApiResponse<{ roles: string[] }>(response),
            ),
          ])

        if (controller.signal.aborted) return

        setProjects(projectData.filter((p) => p.status === 'Active'))
        setMaterials(materialData.filter((m) => m.isActive))
        setRequests(requestData)
        setCanCreate(
          user.roles.some((role) =>
            ['Administrator', 'SiteEngineer'].includes(role),
          ),
        )
      } catch (error) {
        if (controller.signal.aborted) return
        setLoadError(
          error instanceof Error ? error.message : 'Unable to load data.',
        )
      } finally {
        if (!controller.signal.aborted) setLoading(false)
      }
    }

    void load()
    return () => controller.abort()
  }, [])

  function updateLine(
    key: string,
    field: 'materialId' | 'quantity' | 'estimatedUnitPrice',
    value: string,
  ) {
    setLines((current) =>
      current.map((line) =>
        line.key === key ? { ...line, [field]: value } : line,
      ),
    )
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (saving) return

    setError('')
    setSuccess('')

    if (!projectId || !requiredDate || !reason.trim()) {
      setError('Project, required date, and reason are required.')
      return
    }

    const ids = lines.map((line) => line.materialId)

    if (ids.some((id) => !id)) {
      setError('Select a material for every line.')
      return
    }

    if (new Set(ids).size !== ids.length) {
      setError('Each material can appear only once.')
      return
    }

    for (const line of lines) {
      const quantity = Number(line.quantity)
      const price = Number(line.estimatedUnitPrice)

      if (
        !line.quantity.trim() ||
        !Number.isFinite(quantity) ||
        quantity <= 0 ||
        quantity > 999999999.999
      ) {
        setError('Enter a valid positive quantity for every line.')
        return
      }

      if (
        !line.estimatedUnitPrice.trim() ||
        !Number.isFinite(price) ||
        price < 0 ||
        price > 999999999.99
      ) {
        setError('Enter a valid nonnegative price for every line.')
        return
      }
    }

    setSaving(true)

    try {
      const response = await apiFetch('/api/material-requests', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          projectId: Number(projectId),
          requiredDate,
          reason: reason.trim(),
          lines: lines.map((line) => ({
            materialId: Number(line.materialId),
            quantity: Number(line.quantity),
            estimatedUnitPrice: Number(line.estimatedUnitPrice),
          })),
        }),
      })

      const created = await readApiResponse<MaterialRequest>(response)

      setRequests((current) => [created, ...current])
      setSelected(created)
      setProjectId('')
      setRequiredDate('')
      setReason('')
      setLines([emptyLine()])
      setSuccess(`${created.requestNumber} saved as a draft.`)
    } catch (error) {
      setError(
        error instanceof Error ? error.message : 'Unable to save request.',
      )
    } finally {
      setSaving(false)
    }
  }

  async function handleSubmitRequest(id: number) {
    if (submittingId !== null) return
    if (!window.confirm('Submit this request for manager approval?')) return

    setSubmittingId(id)
    setSubmitError('')
    setSubmitSuccess('')

    try {
      const response = await apiFetch(`/api/material-requests/${id}/submit`, {
        method: 'POST',
      })
      const updated = await readApiResponse<MaterialRequest>(response)
      setRequests((current) => current.map((request) =>
        request.id === updated.id ? updated : request,
      ))
      setSelected((current) => current?.id === updated.id ? updated : current)
      setSubmitSuccess(`${updated.requestNumber} submitted for approval.`)
    } catch (error) {
      setSubmitError(error instanceof Error ? error.message : 'Unable to submit request.')
    } finally {
      setSubmittingId(null)
    }
  }

  if (loading) return <p role="status">Loading material requests...</p>

  if (loadError) {
    return <p className="error-message" role="alert">{loadError}</p>
  }

  const total = lines.reduce((sum, line) => sum + lineTotal(line), 0)

  return (
    <>
      <header className="page-header">
        <h1>Material Requests</h1>
        <p>Request construction materials for your projects.</p>
      </header>

      {canCreate && (
        <section className="panel">
          <h2>Create Material Request</h2>

          {(!projects.length || !materials.length) && (
            <p className="error-message">
              Create an active project and material first.
            </p>
          )}

          <form onSubmit={handleSubmit}>
            <fieldset
              className="form-fields"
              disabled={saving || !projects.length || !materials.length}
            >
              <div className="form-grid">
                <label htmlFor="request-project">
                  Project
                  <select
                    id="request-project"
                    value={projectId}
                    onChange={(event) => setProjectId(event.target.value)}
                    required
                  >
                    <option value="">Select a project</option>
                    {projects.map((project) => (
                      <option key={project.id} value={project.id}>
                        {project.code} — {project.name}
                      </option>
                    ))}
                  </select>
                </label>

                <label htmlFor="request-date">
                  Required date
                  <input
                    id="request-date"
                    type="date"
                    value={requiredDate}
                    onChange={(event) => setRequiredDate(event.target.value)}
                    required
                  />
                </label>
              </div>

              <label htmlFor="request-reason">
                Reason
                <textarea
                  id="request-reason"
                  value={reason}
                  onChange={(event) => setReason(event.target.value)}
                  maxLength={1000}
                  rows={3}
                  required
                />
              </label>

              <h3>Material Lines</h3>

              {lines.map((line, index) => {
                const material = materials.find(
                  (m) => m.id === Number(line.materialId),
                )

                return (
                  <div className="request-line" key={line.key}>
                    <h4>Line {index + 1}</h4>

                    <div className="form-grid">
                      <label htmlFor={`material-${line.key}`}>
                        Material
                        <select
                          id={`material-${line.key}`}
                          value={line.materialId}
                          onChange={(event) =>
                            updateLine(line.key, 'materialId', event.target.value)
                          }
                          required
                        >
                          <option value="">Select a material</option>
                          {materials.map((item) => (
                            <option
                              key={item.id}
                              value={item.id}
                              disabled={lines.some(
                                (other) =>
                                  other.key !== line.key &&
                                  other.materialId === String(item.id),
                              )}
                            >
                              {item.code} — {item.name} ({item.unit})
                            </option>
                          ))}
                        </select>
                      </label>

                      <label htmlFor={`quantity-${line.key}`}>
                        Quantity {material ? `(${material.unit})` : ''}
                        <input
                          id={`quantity-${line.key}`}
                          type="number"
                          min="0.001"
                          max="999999999.999"
                          step="0.001"
                          value={line.quantity}
                          onChange={(event) =>
                            updateLine(line.key, 'quantity', event.target.value)
                          }
                          required
                        />
                      </label>

                      <label htmlFor={`price-${line.key}`}>
                        Estimated unit price (LKR)
                        <input
                          id={`price-${line.key}`}
                          type="number"
                          min="0"
                          max="999999999.99"
                          step="0.01"
                          value={line.estimatedUnitPrice}
                          onChange={(event) =>
                            updateLine(
                              line.key,
                              'estimatedUnitPrice',
                              event.target.value,
                            )
                          }
                          required
                        />
                      </label>

                      <div className="line-total">
                        <span>Estimated line total</span>
                        <strong>{money.format(lineTotal(line))}</strong>
                      </div>
                    </div>

                    <button
                      type="button"
                      className="secondary-button"
                      disabled={lines.length === 1}
                      onClick={() =>
                        setLines((current) =>
                          current.filter((item) => item.key !== line.key),
                        )
                      }
                    >
                      Remove line {index + 1}
                    </button>
                  </div>
                )
              })}

              <button
                type="button"
                className="secondary-button"
                disabled={lines.length >= 100 || lines.length >= materials.length}
                onClick={() => setLines((current) => [...current, emptyLine()])}
              >
                + Add Material
              </button>

              <p className="request-total">
                Total estimated cost: {money.format(total)}
              </p>

              <button className="primary-button" type="submit">
                {saving ? 'Saving...' : 'Save Draft Request'}
              </button>
            </fieldset>

            {error && <p className="error-message" role="alert">{error}</p>}
            {success && <p className="success-message" role="status">{success}</p>}
          </form>
        </section>
      )}

      <section className="panel">
        <h2>Saved Requests</h2>
        {submitError && <p className="error-message" role="alert">{submitError}</p>}
        {submitSuccess && <p className="success-message" role="status">{submitSuccess}</p>}

        {!requests.length ? (
          <p>No requests found.</p>
        ) : (
          <div className="table-container">
            <table>
              <thead>
                <tr>
                  <th scope="col">Request</th>
                  <th scope="col">Project</th>
                  <th scope="col">Required date</th>
                  <th scope="col" className="number-cell">Estimated cost</th>
                  <th scope="col">Status</th>
                  <th scope="col">Action</th>
                </tr>
              </thead>
              <tbody>
                {requests.map((request) => (
                  <tr key={request.id}>
                    <td>{request.requestNumber}</td>
                    <td>{request.projectName}</td>
                    <td>{request.requiredDate}</td>
                    <td className="number-cell">
                      {money.format(request.totalEstimatedCost)}
                    </td>
                    <td>{request.status}</td>
                    <td>
                      <button
                        type="button"
                        className="secondary-button"
                        aria-label={`View ${request.requestNumber}`}
                        onClick={() => setSelected(request)}
                      >
                        View
                      </button>
                      {request.canSubmit && (
                        <button
                          type="button"
                          className="primary-button"
                          style={{ marginLeft: 8 }}
                          disabled={submittingId !== null}
                          onClick={() => handleSubmitRequest(request.id)}
                        >
                          {submittingId === request.id ? 'Submitting...' : 'Submit'}
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {selected && (
        <section className="panel">
          <div className="section-toolbar">
            <h2>{selected.requestNumber}</h2>
            <button
              type="button"
              className="secondary-button"
              onClick={() => setSelected(null)}
            >
              Close details
            </button>
          </div>

          <p><strong>Project:</strong> {selected.projectName}</p>
          <p><strong>Required date:</strong> {selected.requiredDate}</p>
          <p><strong>Status:</strong> {selected.status}</p>
          <p><strong>Created by:</strong> {selected.createdByName}</p>
          {selected.submittedAtUtc && (
            <p><strong>Submitted:</strong> {new Date(selected.submittedAtUtc).toLocaleString()}</p>
          )}
          <p className="request-reason">
            <strong>Reason:</strong> {selected.reason}
          </p>

          <div className="table-container">
            <table>
              <thead>
                <tr>
                  <th scope="col">Material</th>
                  <th scope="col">Unit</th>
                  <th scope="col" className="number-cell">Quantity</th>
                  <th scope="col" className="number-cell">Unit price</th>
                  <th scope="col" className="number-cell">Total</th>
                </tr>
              </thead>
              <tbody>
                {selected.lines.map((line) => (
                  <tr key={line.id}>
                    <td>{line.materialCode} — {line.materialName}</td>
                    <td>{line.unit}</td>
                    <td className="number-cell">{line.quantity}</td>
                    <td className="number-cell">
                      {money.format(line.estimatedUnitPrice)}
                    </td>
                    <td className="number-cell">
                      {money.format(line.lineTotal)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <p className="request-total">
            Total: {money.format(selected.totalEstimatedCost)}
          </p>
        </section>
      )}
    </>
  )
}