import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'

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
  projectId: number
  projectName: string
  requiredDate: string
  reason: string
  status: string
  createdAtUtc: string
  totalEstimatedCost: number
  lines: RequestLine[]
}

interface DraftLine {
  key: string
  materialId: string
  quantity: string
  estimatedUnitPrice: string
}

interface ApiError {
  message?: string
  title?: string
  errors?: Record<string, string[]>
}

const money = new Intl.NumberFormat('en-LK', {
  style: 'currency',
  currency: 'LKR',
})

function createEmptyLine(): DraftLine {
  return {
    key: crypto.randomUUID(),
    materialId: '',
    quantity: '',
    estimatedUnitPrice: '',
  }
}

function getLineTotal(line: DraftLine): number {
  const quantity = Number(line.quantity)
  const price = Number(line.estimatedUnitPrice)

  if (!Number.isFinite(quantity) || !Number.isFinite(price)) {
    return 0
  }

  return Math.round((quantity * price + Number.EPSILON) * 100) / 100
}

async function readResponse<T>(response: Response): Promise<T> {
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
        `Request failed (${response.status}).`,
    )
  }

  return response.json() as Promise<T>
}

export default function MaterialRequestsPage() {
  const [projects, setProjects] = useState<Project[]>([])
  const [materials, setMaterials] = useState<Material[]>([])
  const [requests, setRequests] = useState<MaterialRequest[]>([])

  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState('')

  const [projectId, setProjectId] = useState('')
  const [requiredDate, setRequiredDate] = useState('')
  const [reason, setReason] = useState('')
  const [lines, setLines] = useState<DraftLine[]>(() => [
    createEmptyLine(),
  ])

  const [saving, setSaving] = useState(false)
  const [formError, setFormError] = useState('')
  const [success, setSuccess] = useState('')

  const [selectedRequest, setSelectedRequest] =
    useState<MaterialRequest | null>(null)

  useEffect(() => {
    const controller = new AbortController()

    async function loadData() {
      try {
        const [projectData, materialData, requestData] =
          await Promise.all([
            fetch('/api/projects', {
              signal: controller.signal,
            }).then((response) => readResponse<Project[]>(response)),

            fetch('/api/materials', {
              signal: controller.signal,
            }).then((response) => readResponse<Material[]>(response)),

            fetch('/api/material-requests', {
              signal: controller.signal,
            }).then((response) =>
              readResponse<MaterialRequest[]>(response),
            ),
          ])

        if (controller.signal.aborted) return

        setProjects(
          projectData.filter((project) => project.status === 'Active'),
        )
        setMaterials(
          materialData.filter((material) => material.isActive),
        )
        setRequests(requestData)
      } catch (error) {
        if (controller.signal.aborted) return

        setLoadError(
          error instanceof Error
            ? error.message
            : 'Unable to load material request data.',
        )
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false)
        }
      }
    }

    void loadData()

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

  function removeLine(key: string) {
    setLines((current) =>
      current.length > 1
        ? current.filter((line) => line.key !== key)
        : current,
    )
  }

  const estimatedTotal = lines.reduce(
    (total, line) => total + getLineTotal(line),
    0,
  )

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (saving) return

    setFormError('')
    setSuccess('')

    if (!projectId || !requiredDate || !reason.trim()) {
      setFormError('Select a project, required date, and reason.')
      return
    }

    const selectedIds = lines.map((line) => line.materialId)

    if (selectedIds.some((id) => !id)) {
      setFormError('Select a material for every line.')
      return
    }

    if (new Set(selectedIds).size !== selectedIds.length) {
      setFormError('Each material can appear only once.')
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
        setFormError('Enter a valid positive quantity for every line.')
        return
      }

      if (
        !line.estimatedUnitPrice.trim() ||
        !Number.isFinite(price) ||
        price < 0 ||
        price > 999999999.99
      ) {
        setFormError('Enter a valid nonnegative price for every line.')
        return
      }
    }

    setSaving(true)

    try {
      const response = await fetch('/api/material-requests', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
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

      const request =
        await readResponse<MaterialRequest>(response)

      setRequests((current) => [request, ...current])
      setSelectedRequest(request)

      setProjectId('')
      setRequiredDate('')
      setReason('')
      setLines([createEmptyLine()])

      setSuccess(`${request.requestNumber} saved as a draft.`)
    } catch (error) {
      setFormError(
        error instanceof Error
          ? error.message
          : 'Unable to save the request.',
      )
    } finally {
      setSaving(false)
    }
  }

  if (loading) {
    return <p role="status">Loading material requests...</p>
  }

  if (loadError) {
    return (
      <p className="error-message" role="alert">
        {loadError} Check that your API is running, then refresh.
      </p>
    )
  }

  return (
    <>
      <header className="page-header">
        <h1>Material Requests</h1>
        <p>Request construction materials for your projects.</p>
      </header>

      <section className="panel">
        <h2>Create Material Request</h2>

        {(projects.length === 0 || materials.length === 0) && (
          <p className="error-message">
            Create at least one active project and material first.
          </p>
        )}

        <form onSubmit={handleSubmit}>
          <fieldset
            className="form-fields"
            disabled={
              saving ||
              projects.length === 0 ||
              materials.length === 0
            }
          >
            <div className="form-grid">
              <label htmlFor="request-project">
                Project
                <select
                  id="request-project"
                  value={projectId}
                  onChange={(event) =>
                    setProjectId(event.target.value)
                  }
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
                  onChange={(event) =>
                    setRequiredDate(event.target.value)
                  }
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
                placeholder="Materials required for foundation work"
                maxLength={1000}
                rows={3}
                required
              />
            </label>

            <h3>Material Lines</h3>

            {lines.map((line, index) => {
              const material = materials.find(
                (item) => item.id === Number(line.materialId),
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
                          updateLine(
                            line.key,
                            'materialId',
                            event.target.value,
                          )
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
                          updateLine(
                            line.key,
                            'quantity',
                            event.target.value,
                          )
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
                      <strong>
                        {money.format(getLineTotal(line))}
                      </strong>
                    </div>
                  </div>

                  <button
                    className="secondary-button"
                    type="button"
                    disabled={lines.length === 1}
                    onClick={() => removeLine(line.key)}
                  >
                    Remove line {index + 1}
                  </button>
                </div>
              )
            })}

            <button
              className="secondary-button"
              type="button"
              disabled={
                lines.length >= 100 ||
                lines.length >= materials.length
              }
              onClick={() =>
                setLines((current) => [
                  ...current,
                  createEmptyLine(),
                ])
              }
            >
              + Add Material
            </button>

            <p className="request-total">
              Total estimated cost: {money.format(estimatedTotal)}
            </p>

            <button className="primary-button" type="submit">
              {saving ? 'Saving...' : 'Save Draft Request'}
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

      <section className="panel">
        <h2>Saved Requests</h2>

        {requests.length === 0 ? (
          <p>No material requests yet.</p>
        ) : (
          <div className="table-container">
            <table>
              <thead>
                <tr>
                  <th scope="col">Request</th>
                  <th scope="col">Project</th>
                  <th scope="col">Required date</th>
                  <th scope="col" className="number-cell">
                    Estimated cost
                  </th>
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
                    <td>
                      <span className="status-badge">
                        {request.status}
                      </span>
                    </td>
                    <td>
                      <button
                        className="secondary-button"
                        type="button"
                        onClick={() => setSelectedRequest(request)}
                        aria-label={`View ${request.requestNumber}`}
                      >
                        View
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      {selectedRequest && (
        <section className="panel" aria-label="Request details">
          <div className="section-toolbar">
            <h2>{selectedRequest.requestNumber}</h2>
            <button
              className="secondary-button"
              type="button"
              onClick={() => setSelectedRequest(null)}
            >
              Close details
            </button>
          </div>

          <p><strong>Project:</strong> {selectedRequest.projectName}</p>
          <p><strong>Required date:</strong> {selectedRequest.requiredDate}</p>
          <p><strong>Status:</strong> {selectedRequest.status}</p>
          <p className="request-reason">
            <strong>Reason:</strong> {selectedRequest.reason}
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
                {selectedRequest.lines.map((line) => (
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
            Total: {money.format(selectedRequest.totalEstimatedCost)}
          </p>
        </section>
      )}
    </>
  )
}