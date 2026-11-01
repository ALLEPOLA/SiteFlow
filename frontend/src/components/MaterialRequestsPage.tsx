import Icon from './ui/Icon'
import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { apiFetch, readApiResponse } from '../api/apiFetch'

type Project = {
  id: number
  code: string
  name: string
  status: string
}

type Material = {
  id: number
  code: string
  name: string
  unit: string
  isActive: boolean
}

type RequestLine = {
  id: number
  materialId: number
  materialCode: string
  materialName: string
  unit: string
  quantity: number
  estimatedUnitPrice: number
  lineTotal: number
}

type MaterialRequest = {
  id: number
  requestNumber: string
  projectName: string
  requiredDate: string
  reason: string
  status: string
  createdByUserId: string | null
  createdByName: string
  submittedAtUtc: string | null
  reviewedByName: string | null
  reviewedAtUtc: string | null
  reviewComment: string | null
  canSubmit: boolean
  canReview: boolean
  totalEstimatedCost: number
  lines: RequestLine[]
}

type DraftLine = {
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

  if (!Number.isFinite(quantity) || !Number.isFinite(price)) {
    return 0
  }

  return Math.round((quantity * price + Number.EPSILON) * 100) / 100
}

function getError(error: unknown) {
  return error instanceof Error
    ? error.message
    : 'Unable to complete the request.'
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
  const [reviewComment, setReviewComment] = useState('')
  const [actionBusy, setActionBusy] = useState(false)
  const [actionError, setActionError] = useState('')
  const [actionSuccess, setActionSuccess] = useState('')

  const savingRef = useRef(false)
  const actionRef = useRef(false)

  useEffect(() => {
    const controller = new AbortController()
    const options = {
      signal: controller.signal,
      cache: 'no-store' as RequestCache,
    }

    async function load() {
      try {
        const [projectData, materialData, requestData, user] =
          await Promise.all([
            apiFetch('/api/projects', options).then(response =>
              readApiResponse<Project[]>(response),
            ),
            apiFetch('/api/materials', options).then(response =>
              readApiResponse<Material[]>(response),
            ),
            apiFetch('/api/material-requests', options).then(response =>
              readApiResponse<MaterialRequest[]>(response),
            ),
            apiFetch('/api/auth/me', options).then(response =>
              readApiResponse<{ roles: string[] }>(response),
            ),
          ])

        if (controller.signal.aborted) return

        setProjects(projectData.filter(project => project.status === 'Active'))
        setMaterials(materialData.filter(material => material.isActive))
        setRequests(requestData)

        setCanCreate(
          user.roles.some(role =>
            ['Administrator', 'SiteEngineer'].includes(role),
          ),
        )
      } catch (loadFailure) {
        if (!controller.signal.aborted) {
          setLoadError(getError(loadFailure))
        }
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false)
        }
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
    setLines(current =>
      current.map(line =>
        line.key === key ? { ...line, [field]: value } : line,
      ),
    )
  }

  function openDetails(request: MaterialRequest) {
    if (actionRef.current) return

    setSelected(request)
    setReviewComment('')
    setActionError('')
    setActionSuccess('')
  }

  function updateSavedRequest(updated: MaterialRequest) {
    setRequests(current =>
      current.map(request =>
        request.id === updated.id ? updated : request,
      ),
    )

    setSelected(current =>
      current?.id === updated.id ? updated : current,
    )
  }

  async function handleCreate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (savingRef.current || actionRef.current) return

    setError('')
    setSuccess('')

    if (!projectId || !requiredDate || !reason.trim()) {
      setError('Project, required date, and reason are required.')
      return
    }

    const materialIds = lines.map(line => line.materialId)

    if (
      materialIds.some(id => !id) ||
      new Set(materialIds).size !== materialIds.length
    ) {
      setError('Select a different material for every line.')
      return
    }

    for (const line of lines) {
      const quantity = Number(line.quantity)
      const price = Number(line.estimatedUnitPrice)

      if (
        !line.quantity.trim() ||
        !Number.isFinite(quantity) ||
        quantity <= 0 ||
        quantity > 999999999.999 ||
        Math.abs(quantity * 1000 - Math.round(quantity * 1000)) > 0.0001
      ) {
        setError('Enter positive quantities with up to 3 decimal places.')
        return
      }

      if (
        !line.estimatedUnitPrice.trim() ||
        !Number.isFinite(price) ||
        price < 0 ||
        price > 999999999.99 ||
        Math.abs(price * 100 - Math.round(price * 100)) > 0.0001
      ) {
        setError('Enter nonnegative prices with up to 2 decimal places.')
        return
      }
    }

    savingRef.current = true
    setSaving(true)

    try {
      const response = await apiFetch('/api/material-requests', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          projectId: Number(projectId),
          requiredDate,
          reason: reason.trim(),
          lines: lines.map(line => ({
            materialId: Number(line.materialId),
            quantity: Number(line.quantity),
            estimatedUnitPrice: Number(line.estimatedUnitPrice),
          })),
        }),
      })

      const created = await readApiResponse<MaterialRequest>(response)

      setRequests(current => [created, ...current])
      setSelected(created)
      setReviewComment('')
      setActionError('')
      setActionSuccess('')

      setProjectId('')
      setRequiredDate('')
      setReason('')
      setLines([emptyLine()])
      setSuccess(`${created.requestNumber} saved as a draft.`)
    } catch (saveFailure) {
      setError(getError(saveFailure))
    } finally {
      savingRef.current = false
      setSaving(false)
    }
  }

  async function handleRequestAction(
    request: MaterialRequest,
    action: 'submit' | 'approve' | 'reject',
  ) {
    if (actionRef.current || savingRef.current) return

    setActionError('')
    setActionSuccess('')

    const comment = reviewComment.trim()

    if (action === 'reject' && !comment) {
      setActionError('Enter a reason before rejecting the request.')
      return
    }

    const confirmation = {
      submit: 'Submit this request for approval?',
      approve: 'Approve this material request?',
      reject: 'Reject this material request?',
    }

    if (!window.confirm(confirmation[action])) return

    actionRef.current = true
    setActionBusy(true)

    try {
      const options: RequestInit = {
        method: 'POST',
      }

      if (action !== 'submit') {
        options.headers = {
          'Content-Type': 'application/json',
        }
        options.body = JSON.stringify({
          comment: comment || null,
        })
      }

      const response = await apiFetch(
        `/api/material-requests/${request.id}/${action}`,
        options,
      )

      const updated = await readApiResponse<MaterialRequest>(response)

      updateSavedRequest(updated)
      setReviewComment('')

      const result = {
        submit: 'submitted for approval',
        approve: 'approved',
        reject: 'rejected',
      }

      setActionSuccess(
        `${updated.requestNumber} ${result[action]}.`,
      )
    } catch (actionFailure) {
      setActionError(getError(actionFailure))
    } finally {
      actionRef.current = false
      setActionBusy(false)
    }
  }

  if (loading) {
    return <p role="status">Loading material requests...</p>
  }

  if (loadError) {
    return (
      <p className="error-message" role="alert">
        {loadError}
      </p>
    )
  }

  const total = lines.reduce(
    (sum, line) => sum + lineTotal(line),
    0,
  )

  return (
    <>
      <header className="page-header">
        <span className="workspace-page-emblem" aria-hidden="true"><Icon name="requests" /></span>
        <h1>Material Requests</h1>
        <p>Request construction materials for your projects.</p>
      </header>

      {canCreate && (
        <section className="panel">
          <h2>Create Material Request</h2>

          {(!projects.length || !materials.length) && (
            <p>Create an active project and material first.</p>
          )}

          <form onSubmit={handleCreate}>
            <fieldset
              className="form-fields"
              disabled={
                saving ||
                actionBusy ||
                !projects.length ||
                !materials.length
              }
            >
              <div className="form-grid">
                <label>
                  Project
                  <select
                    value={projectId}
                    onChange={event => setProjectId(event.target.value)}
                    required
                  >
                    <option value="">Select a project</option>
                    {projects.map(project => (
                      <option key={project.id} value={project.id}>
                        {project.code} — {project.name}
                      </option>
                    ))}
                  </select>
                </label>

                <label>
                  Required date
                  <input
                    type="date"
                    value={requiredDate}
                    onChange={event =>
                      setRequiredDate(event.target.value)
                    }
                    required
                  />
                </label>
              </div>

              <label>
                Reason
                <textarea
                  value={reason}
                  onChange={event => setReason(event.target.value)}
                  maxLength={1000}
                  rows={3}
                  required
                />
              </label>

              <h3>Material Lines</h3>

              {lines.map((line, index) => (
                <div className="request-line" key={line.key}>
                  <h4>Line {index + 1}</h4>

                  <div className="form-grid">
                    <label>
                      Material
                      <select
                        value={line.materialId}
                        onChange={event =>
                          updateLine(
                            line.key,
                            'materialId',
                            event.target.value,
                          )
                        }
                        required
                      >
                        <option value="">Select a material</option>

                        {materials.map(material => (
                          <option
                            key={material.id}
                            value={material.id}
                            disabled={lines.some(
                              other =>
                                other.key !== line.key &&
                                other.materialId === String(material.id),
                            )}
                          >
                            {material.code} — {material.name}
                            {' '}({material.unit})
                          </option>
                        ))}
                      </select>
                    </label>

                    <label>
                      Quantity
                      <input
                        type="number"
                        min="0.001"
                        max="999999999.999"
                        step="0.001"
                        value={line.quantity}
                        onChange={event =>
                          updateLine(
                            line.key,
                            'quantity',
                            event.target.value,
                          )
                        }
                        required
                      />
                    </label>

                    <label>
                      Estimated unit price (LKR)
                      <input
                        type="number"
                        min="0"
                        max="999999999.99"
                        step="0.01"
                        value={line.estimatedUnitPrice}
                        onChange={event =>
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
                      setLines(current =>
                        current.filter(item => item.key !== line.key),
                      )
                    }
                  >
                    Remove line {index + 1}
                  </button>
                </div>
              ))}

              <button
                type="button"
                className="secondary-button"
                disabled={
                  lines.length >= 100 ||
                  lines.length >= materials.length
                }
                onClick={() =>
                  setLines(current => [...current, emptyLine()])
                }
              >
                + Add Material
              </button>

              <p className="request-total">
                Total estimated cost: {money.format(total)}
              </p>

              <button type="submit" className="primary-button">
                {saving ? 'Saving...' : 'Save Draft Request'}
              </button>
            </fieldset>

            {error && (
              <p className="error-message" role="alert">{error}</p>
            )}

            {success && (
              <p className="success-message" role="status">{success}</p>
            )}
          </form>
        </section>
      )}

      <section className="panel">
        <h2>Saved Requests</h2>

        {requests.length === 0 ? (
          <p>No requests found.</p>
        ) : (
          <div className="table-container">
            <table>
              <thead>
                <tr>
                  <th>Request</th>
                  <th>Project</th>
                  <th>Required date</th>
                  <th>Estimated cost</th>
                  <th>Status</th>
                  <th>Action</th>
                </tr>
              </thead>

              <tbody>
                {requests.map(request => (
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
                        disabled={actionBusy || saving}
                        onClick={() => openDetails(request)}
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

      {selected && (
        <section className="panel">
          <div className="section-toolbar">
            <h2>{selected.requestNumber}</h2>

            <button
              type="button"
              className="secondary-button"
              disabled={actionBusy}
              onClick={() => {
                setSelected(null)
                setReviewComment('')
                setActionError('')
                setActionSuccess('')
              }}
            >
              Close details
            </button>
          </div>

          <p><strong>Project:</strong> {selected.projectName}</p>
          <p><strong>Required date:</strong> {selected.requiredDate}</p>
          <p><strong>Status:</strong> {selected.status}</p>
          <p><strong>Created by:</strong> {selected.createdByName}</p>

          {selected.submittedAtUtc && (
            <p>
              <strong>Submitted:</strong>{' '}
              {new Date(selected.submittedAtUtc).toLocaleString()}
            </p>
          )}

          <p className="request-reason">
            <strong>Reason:</strong> {selected.reason}
          </p>

          <div className="table-container">
            <table>
              <thead>
                <tr>
                  <th>Material</th>
                  <th>Unit</th>
                  <th>Quantity</th>
                  <th>Unit price</th>
                  <th>Total</th>
                </tr>
              </thead>

              <tbody>
                {selected.lines.map(line => (
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

          {actionError && (
            <p className="error-message" role="alert">
              {actionError}
            </p>
          )}

          {actionSuccess && (
            <p className="success-message" role="status">
              {actionSuccess}
            </p>
          )}

          {selected.canSubmit && (
            <button
              type="button"
              className="primary-button"
              disabled={actionBusy || saving}
              onClick={() =>
                handleRequestAction(selected, 'submit')
              }
            >
              {actionBusy ? 'Submitting...' : 'Submit Request'}
            </button>
          )}

          {selected.canReview && (
            <div className="request-review">
              <h3>Review request</h3>

              <label htmlFor="review-comment">
                Review comment — required for rejection
              </label>

              <textarea
                id="review-comment"
                value={reviewComment}
                onChange={event =>
                  setReviewComment(event.target.value)
                }
                maxLength={1000}
                rows={3}
                disabled={actionBusy || saving}
              />

              <div className="request-review-actions">
                <button
                  type="button"
                  className="primary-button"
                  disabled={actionBusy || saving}
                  onClick={() =>
                    handleRequestAction(selected, 'approve')
                  }
                >
                  {actionBusy ? 'Processing...' : 'Approve'}
                </button>

                <button
                  type="button"
                  className="secondary-button"
                  disabled={actionBusy || saving}
                  onClick={() =>
                    handleRequestAction(selected, 'reject')
                  }
                >
                  Reject
                </button>
              </div>
            </div>
          )}

          {selected.reviewedAtUtc && (
            <div className="review-summary">
              <h3>Review decision</h3>

              <p>
                <strong>Decision:</strong> {selected.status}
              </p>

              <p>
                <strong>Reviewed by:</strong>{' '}
                {selected.reviewedByName ?? 'Unknown'}
              </p>

              <p>
                <strong>Reviewed at:</strong>{' '}
                {new Date(selected.reviewedAtUtc).toLocaleString()}
              </p>

              {selected.reviewComment && (
                <p>
                  <strong>Comment:</strong> {selected.reviewComment}
                </p>
              )}
            </div>
          )}
        </section>
      )}
    </>
  )
}