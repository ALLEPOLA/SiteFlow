import StorageLocationSelect from './ui/StorageLocationSelect'
import Icon from './ui/Icon'
import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { apiFetch, readApiResponse } from '../api/apiFetch'
import type { AuthUser } from '../types/auth'

type StockBalance = {
  projectId: number
  projectCode: string
  projectName: string
  materialId: number
  materialCode: string
  materialName: string
  unit: string
  reservedQuantity: number
  availableQuantity: number
  quantityOnHand: number
}

type IssueLine = {
  id: number
  materialId: number
  materialCode: string
  materialName: string
  unit: string
  quantity: number
  unitCost: number
  lineTotal: number
}

type MaterialIssue = {
  storageLocationId: number
  storageLocationName: string
  id: number
  issueNumber: string
  clientRequestId: string
  projectId: number
  projectCode: string
  projectName: string
  issueDate: string
  purpose: string
  receivedBy: string
  notes: string
  status: string
  createdByName: string
  createdAtUtc: string
  totalValue: number
  lines: IssueLine[]
}

type Hold = { id: number; projectId: number; materialId: number; requestNumber: string; remainingQuantity: number }

type DraftLine = {
  reservationId: string
  materialId: number
  quantity: string
}

function localToday() {
  const date = new Date()

  return [
    date.getFullYear(),
    String(date.getMonth() + 1).padStart(2, '0'),
    String(date.getDate()).padStart(2, '0'),
  ].join('-')
}

function formatQuantity(value: number) {
  return new Intl.NumberFormat('en-LK', {
    maximumFractionDigits: 3,
  }).format(value)
}

function formatMoney(value: number) {
  return new Intl.NumberFormat('en-LK', {
    style: 'currency',
    currency: 'LKR',
  }).format(value)
}

function getError(error: unknown) {
  return error instanceof Error
    ? error.message
    : 'Something went wrong. Please try again.'
}

export default function MaterialIssuesPage() {
  const [issues, setIssues] = useState<MaterialIssue[]>([])
  const [balances, setBalances] = useState<StockBalance[]>([])
  const [holds, setHolds] = useState<Hold[]>([])
  const [holdsLoading, setHoldsLoading] = useState(false)
  const [canIssue, setCanIssue] = useState(false)

  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')

  const [projectId, setProjectId] = useState('')
  const [storageLocationId, setStorageLocationId] = useState('')
  const [locationBalances, setLocationBalances] = useState<StockBalance[]>([])
  const [issueDate, setIssueDate] = useState(localToday)
  const [purpose, setPurpose] = useState('')
  const [receivedBy, setReceivedBy] = useState('')
  const [notes, setNotes] = useState('')
  const [draftLines, setDraftLines] = useState<DraftLine[]>([])
  const [selectedIssue, setSelectedIssue] =
    useState<MaterialIssue | null>(null)

  const submissionId = useRef(crypto.randomUUID())
  const savingRef = useRef(false)

  useEffect(() => {
    const controller = new AbortController()

    async function loadPage() {
      try {
        const [issueData, user] = await Promise.all([
          apiFetch('/api/material-issues', {
            signal: controller.signal,
            cache: 'no-store',
          }).then(response =>
            readApiResponse<MaterialIssue[]>(response),
          ),

          apiFetch('/api/auth/me', {
            signal: controller.signal,
            cache: 'no-store',
          }).then(response =>
            readApiResponse<AuthUser>(response),
          ),
        ])

        const allowed = user.roles.some(role =>
          ['Administrator', 'Storekeeper'].includes(role),
        )

        let stock: StockBalance[] = []

        if (allowed) {
          const response = await apiFetch('/api/inventory/balances', {
            signal: controller.signal,
            cache: 'no-store',
          })

          stock = await readApiResponse<StockBalance[]>(response)
        }

        if (controller.signal.aborted) return

        setIssues(issueData)
        setCanIssue(allowed)
        setBalances(stock)
      } catch (loadError) {
        if (!controller.signal.aborted) {
          setError(getError(loadError))
        }
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false)
        }
      }
    }

    void loadPage()

    return () => controller.abort()
  }, [])

  useEffect(() => {
    if (!canIssue || !projectId || !storageLocationId) return
    const controller = new AbortController()
    async function loadLocation() {
      try {
        const query = '?projectId=' + projectId + '&storageLocationId=' + storageLocationId
        const [stock, holdData] = await Promise.all([
          apiFetch('/api/inventory/balances' + query, { signal: controller.signal, cache: 'no-store' }).then(readApiResponse<StockBalance[]>),
          apiFetch('/api/material-reservations' + query, { signal: controller.signal, cache: 'no-store' }).then(readApiResponse<Hold[]>),
        ])
        if (!controller.signal.aborted) { setLocationBalances(stock); setHolds(holdData); setDraftLines(stock.filter(b => b.quantityOnHand > 0).map(b => ({ materialId: b.materialId, quantity: '', reservationId: '' }))) }
      } catch (e) { if (!controller.signal.aborted) setError(getError(e)) }
      finally { if (!controller.signal.aborted) setHoldsLoading(false) }
    }
    void loadLocation()
    return () => controller.abort()
  }, [projectId, storageLocationId, canIssue])

  function issueLimit(stock: StockBalance, reservationId: string) {
    return reservationId ? holds.find(h => h.id === Number(reservationId) && h.materialId === stock.materialId)?.remainingQuantity ?? 0 : stock.availableQuantity
  }

  const availableStock = balances.filter(
    balance => balance.quantityOnHand > 0,
  )

  const projects = Array.from(
    new Map(
      availableStock.map(balance => [
        balance.projectId,
        {
          id: balance.projectId,
          code: balance.projectCode,
          name: balance.projectName,
        },
      ]),
    ).values(),
  ).sort((a, b) => a.code.localeCompare(b.code))

  const projectStock = locationBalances.filter(
    balance => balance.projectId === Number(projectId) && balance.quantityOnHand > 0,
  )

  function changeProject(value: string) {
    setProjectId(value); setStorageLocationId(''); setLocationBalances([]); setDraftLines([]); setHolds([]); setHoldsLoading(false)
  }
  function changeLocation(value: string) {
    setStorageLocationId(value); setLocationBalances([]); setDraftLines([]); setHolds([]); setHoldsLoading(Boolean(value))
  }

  function changeQuantity(materialId: number, value: string) {
    setDraftLines(current =>
      current.map(line =>
        line.materialId === materialId
          ? { ...line, quantity: value }
          : line,
      ),
    )
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (savingRef.current || holdsLoading) return

    setError('')
    setSuccess('')

    if (!projectId || projectStock.length === 0) {
      setError('Select a project with available stock.')
      return
    }

    if (!issueDate || !purpose.trim() || !receivedBy.trim()) {
      setError('Enter the issue date, purpose, and recipient name.')
      return
    }

    const lines: {
      materialId: number
      quantity: number
      reservationId: number | null
    }[] = []

    for (const draft of draftLines) {
      if (!draft.quantity.trim()) continue

      const amount = Number(draft.quantity)

      const stock = projectStock.find(
        balance => balance.materialId === draft.materialId,
      )

      if (
        !stock ||
        !Number.isFinite(amount) ||
        amount <= 0 ||
        amount > issueLimit(stock, draft.reservationId) ||
        amount > 999999999.999 ||
        Math.abs(amount * 1000 - Math.round(amount * 1000)) > 0.0001
      ) {
        setError(
          'Enter positive quantities within available stock, using up to 3 decimal places.',
        )
        return
      }

      lines.push({
        materialId: draft.materialId,
        reservationId: draft.reservationId ? Number(draft.reservationId) : null,
        quantity: amount,
      })
    }

    if (lines.length === 0 || lines.length > 100) {
      setError('Enter quantities for between 1 and 100 materials.')
      return
    }

    const payload = {
      clientRequestId: submissionId.current,
      projectId: Number(projectId),
      storageLocationId: Number(storageLocationId),
      issueDate,
      purpose: purpose.trim(),
      receivedBy: receivedBy.trim(),
      notes: notes.trim(),
      lines,
    }

    savingRef.current = true
    setSaving(true)

    try {
      const response = await apiFetch('/api/material-issues', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(payload),
      })

      const saved = await readApiResponse<MaterialIssue>(response)

      setIssues(current =>
        [saved, ...current.filter(issue => issue.id !== saved.id)].sort(
          (a, b) => b.id - a.id,
        ),
      )

      setSelectedIssue(saved)
      setSuccess(`${saved.issueNumber} was posted successfully.`)

      submissionId.current = crypto.randomUUID()

      setProjectId('')
      setStorageLocationId('')
      setLocationBalances([])
      setIssueDate(localToday())
      setPurpose('')
      setReceivedBy('')
      setNotes('')
      setDraftLines([])

      try {
        const stockResponse = await apiFetch(
          '/api/inventory/balances',
          { cache: 'no-store' },
        )

        setBalances(
          await readApiResponse<StockBalance[]>(stockResponse),
        )
      } catch (refreshError) {
        setBalances([])
        setError(
          `The issue was saved, but stock could not refresh. Reload before creating another issue. ${getError(refreshError)}`,
        )
      }
    } catch (saveError) {
      setError(getError(saveError))
    } finally {
      savingRef.current = false
      setSaving(false)
    }
  }

  if (loading) {
    return <p role="status">Loading material issues...</p>
  }

  return (
    <>
      <header className="page-header">
        <span className="workspace-page-emblem" aria-hidden="true"><Icon name="issues" /></span>
        <h1>Material Issues</h1>
        <p>Record materials issued for construction work.</p>
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

      {canIssue && (
        <section className="panel">
          <h2>Issue materials</h2>
          {holdsLoading && <p role="status">Loading project reservations...</p>}

          {projects.length === 0 ? (
            <p>
              No stock is available. Record deliveries or import existing
              receipts first.
            </p>
          ) : (
            <form onSubmit={handleSubmit}>
              <fieldset className="receipt-fieldset" disabled={saving || holdsLoading}>
                <div className="form-grid">
                  <label>
                    Project
                    <select
                      value={projectId}
                      onChange={event =>
                        changeProject(event.target.value)
                      }
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

                  <StorageLocationSelect projectId={projectId} value={storageLocationId} onChange={changeLocation} label="Issue from store / location" />
                  <label>
                    Issue date
                    <input
                      type="date"
                      value={issueDate}
                      onChange={event =>
                        setIssueDate(event.target.value)
                      }
                      required
                    />
                  </label>

                  <label>
                    Received by
                    <input
                      type="text"
                      value={receivedBy}
                      onChange={event =>
                        setReceivedBy(event.target.value)
                      }
                      placeholder="Person receiving the materials"
                      maxLength={150}
                      required
                    />
                  </label>

                  <label>
                    Purpose
                    <textarea
                      value={purpose}
                      onChange={event => setPurpose(event.target.value)}
                      placeholder="For example: Foundation concrete work"
                      maxLength={1000}
                      rows={3}
                      required
                    />
                  </label>

                  <label>
                    Notes
                    <textarea
                      value={notes}
                      onChange={event => setNotes(event.target.value)}
                      maxLength={1000}
                      rows={3}
                    />
                  </label>
                </div>

                {projectId && storageLocationId && !holdsLoading && projectStock.length === 0 && <p>No stock is available in this location. Receive stock or transfer it here first.</p>}
                {projectId && projectStock.length > 0 && (
                  <>
                    <p>
                      Select a reservation to fulfill approved work, or use unreserved stock. Enter quantities for the materials being issued.
                      Leave other materials blank. The issue date must
                      be on or after each selected material’s latest
                      stock movement.
                    </p>

                    <div className="table-container">
                      <table>
                        <thead>
                          <tr>
                            <th>Code</th>
                            <th>Material</th>
                            <th>Unit</th>
                            <th>On hand / unreserved</th><th>Issue from reservation</th>
                            <th>Issue quantity</th>
                          </tr>
                        </thead>

                        <tbody>
                          {projectStock.map(stock => {
                            const draft = draftLines.find(
                              line =>
                                line.materialId === stock.materialId,
                            )

                            return (
                              <tr key={stock.materialId}>
                                <td>{stock.materialCode}</td>
                                <td>{stock.materialName}</td>
                                <td>{stock.unit}</td>
                                <td>
                                  {formatQuantity(stock.quantityOnHand)} / {formatQuantity(stock.availableQuantity)}
                                </td>
                                <td><select aria-label={'Reservation for ' + stock.materialName} value={draft?.reservationId ?? ''} onChange={e => setDraftLines(current => current.map(l => l.materialId === stock.materialId ? { ...l, reservationId: e.target.value, quantity: '' } : l))}><option value="">Unreserved stock</option>{holds.filter(h => h.materialId === stock.materialId).map(h => <option key={h.id} value={h.id}>RS-{String(h.id).padStart(6, '0')} / {h.requestNumber} / {formatQuantity(h.remainingQuantity)} {stock.unit}</option>)}</select></td>
                                <td>
                                  <input
                                    type="number"
                                    aria-label={`Issue quantity for ${stock.materialName}`}
                                    min="0.001"
                                    max={Math.min(
                                      issueLimit(stock, draft?.reservationId ?? ''),
                                      999999999.999,
                                    )}
                                    step="0.001"
                                    value={draft?.quantity ?? ''}
                                    placeholder="Not issued"
                                    onChange={event =>
                                      changeQuantity(
                                        stock.materialId,
                                        event.target.value,
                                      )
                                    }
                                  />
                                </td>
                              </tr>
                            )
                          })}
                        </tbody>
                      </table>
                    </div>

                    <p>
                      Posting immediately reduces stock. Check the
                      project, recipient, and quantities before saving.
                    </p>

                    <button type="submit" className="primary-button">
                      {saving ? 'Posting...' : 'Post material issue'}
                    </button>
                  </>
                )}
              </fieldset>
            </form>
          )}
        </section>
      )}

      <section className="panel">
        <h2>Posted material issues</h2>

        {issues.length === 0 ? (
          <p>No material issues have been posted.</p>
        ) : (
          <div className="table-container">
            <table>
              <thead>
                <tr>
                  <th>Issue</th>
                  <th>Project</th>
                  <th>Date</th>
                  <th>Received by</th>
                  <th>Value</th>
                  <th>Action</th>
                </tr>
              </thead>

              <tbody>
                {issues.map(issue => (
                  <tr key={issue.id}>
                    <td>{issue.issueNumber}<br /><small>{issue.storageLocationName}</small></td>
                    <td>{issue.projectName}</td>
                    <td>{issue.issueDate}</td>
                    <td>{issue.receivedBy}</td>
                    <td className="number-cell">
                      {formatMoney(issue.totalValue)}
                    </td>
                    <td>
                      <button
                        type="button"
                        className="secondary-button"
                        onClick={() => setSelectedIssue(issue)}
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

      {selectedIssue && (
        <section className="panel">
          <div className="section-toolbar">
            <h2>{selectedIssue.issueNumber}</h2>

            <button
              type="button"
              className="secondary-button"
              onClick={() => setSelectedIssue(null)}
            >
              Close details
            </button>
          </div>

          <p>
            <strong>Project:</strong> {selectedIssue.projectCode}
            {' — '}
            {selectedIssue.projectName} / {selectedIssue.storageLocationName}
          </p>

          <p>
            <strong>Issue date:</strong> {selectedIssue.issueDate}
          </p>

          <p>
            <strong>Received by:</strong> {selectedIssue.receivedBy}
          </p>

          <p>
            <strong>Purpose:</strong> {selectedIssue.purpose}
          </p>

          {selectedIssue.notes && (
            <p>
              <strong>Notes:</strong> {selectedIssue.notes}
            </p>
          )}

          <p>
            <strong>Posted by:</strong> {selectedIssue.createdByName}
          </p>

          <p>
            <strong>Posted at:</strong>{' '}
            {new Date(selectedIssue.createdAtUtc).toLocaleString()}
          </p>

          <p>
            <strong>Status:</strong> {selectedIssue.status}
          </p>

          <div className="table-container">
            <table>
              <thead>
                <tr>
                  <th>Code</th>
                  <th>Material</th>
                  <th>Unit</th>
                  <th>Issued quantity</th>
                  <th>Unit cost</th>
                  <th>Line value</th>
                </tr>
              </thead>

              <tbody>
                {selectedIssue.lines.map(line => (
                  <tr key={line.id}>
                    <td>{line.materialCode}</td>
                    <td>{line.materialName}</td>
                    <td>{line.unit}</td>
                    <td>{formatQuantity(line.quantity)}</td>
                    <td>{formatMoney(line.unitCost)}</td>
                    <td>{formatMoney(line.lineTotal)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <p className="request-total">
            Total: {formatMoney(selectedIssue.totalValue)}
          </p>
        </section>
      )}
    </>
  )
}