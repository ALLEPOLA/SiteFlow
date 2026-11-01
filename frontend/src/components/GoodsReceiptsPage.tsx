import Icon from './ui/Icon'
import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { apiFetch, readApiResponse } from '../api/apiFetch'
import type { AuthUser } from '../types/auth'

type ReceivableLine = {
  purchaseOrderLineId: number
  materialCode: string
  materialName: string
  unit: string
  orderedQuantity: number
  receivedQuantity: number
  remainingQuantity: number
  unitCost: number
}

type ReceivableOrder = {
  id: number
  orderNumber: string
  projectName: string
  supplierName: string
  orderDate: string
  status: string
  lines: ReceivableLine[]
}

type ReceiptLine = {
  id: number
  purchaseOrderLineId: number
  materialCode: string
  materialName: string
  unit: string
  quantity: number
  unitCost: number
  lineTotal: number
}

type GoodsReceipt = {
  id: number
  receiptNumber: string
  clientRequestId: string
  purchaseOrderId: number
  orderNumber: string
  projectName: string
  supplierName: string
  receivedDate: string
  deliveryNoteNumber: string
  notes: string
  status: string
  createdByName: string
  createdAtUtc: string
  totalValue: number
  lines: ReceiptLine[]
}

type DraftLine = {
  purchaseOrderLineId: number
  quantity: string
}

function localToday() {
  const date = new Date()
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')

  return `${year}-${month}-${day}`
}

function money(value: number) {
  return new Intl.NumberFormat('en-LK', {
    style: 'currency',
    currency: 'LKR',
  }).format(value)
}

function quantity(value: number) {
  return new Intl.NumberFormat('en-LK', {
    maximumFractionDigits: 3,
  }).format(value)
}

function errorMessage(error: unknown) {
  return error instanceof Error
    ? error.message
    : 'Something went wrong. Please try again.'
}

export default function GoodsReceiptsPage() {
  const [receipts, setReceipts] = useState<GoodsReceipt[]>([])
  const [orders, setOrders] = useState<ReceivableOrder[]>([])
  const [canReceive, setCanReceive] = useState(false)

  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState('')
  const [success, setSuccess] = useState('')

  const [orderId, setOrderId] = useState('')
  const [receivedDate, setReceivedDate] = useState(localToday)
  const [deliveryNoteNumber, setDeliveryNoteNumber] = useState('')
  const [notes, setNotes] = useState('')
  const [draftLines, setDraftLines] = useState<DraftLine[]>([])
  const [selectedReceipt, setSelectedReceipt] =
    useState<GoodsReceipt | null>(null)

  // Keep the same submission ID until the server confirms success.
  const submissionId = useRef(crypto.randomUUID())
  const savingRef = useRef(false)

  useEffect(() => {
    const controller = new AbortController()

    async function loadPage() {
      setLoading(true)
      setError('')

      try {
        const [receiptData, user] = await Promise.all([
          apiFetch('/api/goods-receipts', {
            signal: controller.signal,
            cache: 'no-store',
          }).then(response =>
            readApiResponse<GoodsReceipt[]>(response),
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

        let availableOrders: ReceivableOrder[] = []

        if (allowed) {
          const response = await apiFetch(
            '/api/goods-receipts/receivable-orders',
            {
              signal: controller.signal,
              cache: 'no-store',
            },
          )

          availableOrders =
            await readApiResponse<ReceivableOrder[]>(response)
        }

        if (controller.signal.aborted) return

        setReceipts(receiptData)
        setCanReceive(allowed)
        setOrders(availableOrders)
      } catch (loadError) {
        if (!controller.signal.aborted) {
          setError(errorMessage(loadError))
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

  const selectedOrder = orders.find(
    order => order.id === Number(orderId),
  )

  const previewTotal = selectedOrder
    ? selectedOrder.lines.reduce((total, line) => {
        const draft = draftLines.find(
          item =>
            item.purchaseOrderLineId === line.purchaseOrderLineId,
        )

        const amount = Number(draft?.quantity || 0)

        return total + Math.round(amount * line.unitCost * 100) / 100
      }, 0)
    : 0

  function changeOrder(value: string) {
    setOrderId(value)

    const order = orders.find(item => item.id === Number(value))

    setDraftLines(
      order?.lines.map(line => ({
        purchaseOrderLineId: line.purchaseOrderLineId,
        quantity: '',
      })) ?? [],
    )
  }

  function changeQuantity(lineId: number, value: string) {
    setDraftLines(current =>
      current.map(line =>
        line.purchaseOrderLineId === lineId
          ? { ...line, quantity: value }
          : line,
      ),
    )
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()

    if (savingRef.current) return

    setError('')
    setSuccess('')

    if (!selectedOrder) {
      setError('Select a purchase order.')
      return
    }

    if (!deliveryNoteNumber.trim()) {
      setError('Enter the supplier delivery note number.')
      return
    }

    if (!receivedDate || receivedDate < selectedOrder.orderDate) {
      setError('The received date must be on or after the order date.')
      return
    }

    const lines: {
      purchaseOrderLineId: number
      quantity: number
    }[] = []

    for (const draft of draftLines) {
      if (draft.quantity.trim() === '') continue

      const amount = Number(draft.quantity)

      const orderLine = selectedOrder.lines.find(
        line =>
          line.purchaseOrderLineId === draft.purchaseOrderLineId,
      )

      if (
        !orderLine ||
        !Number.isFinite(amount) ||
        amount <= 0 ||
        amount > orderLine.remainingQuantity ||
        amount > 999999999.999 ||
        Math.abs(amount * 1000 - Math.round(amount * 1000)) > 0.0001
      ) {
        setError(
          'Enter positive quantities within the remaining amounts, using up to 3 decimal places.',
        )
        return
      }

      lines.push({
        purchaseOrderLineId: draft.purchaseOrderLineId,
        quantity: amount,
      })
    }

    if (lines.length === 0) {
      setError('Enter a received quantity for at least one material.')
      return
    }

    const payload = {
      clientRequestId: submissionId.current,
      purchaseOrderId: selectedOrder.id,
      receivedDate,
      deliveryNoteNumber: deliveryNoteNumber.trim(),
      notes: notes.trim(),
      lines,
    }

    savingRef.current = true
    setSaving(true)

    try {
      const response = await apiFetch('/api/goods-receipts', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(payload),
      })

      const saved = await readApiResponse<GoodsReceipt>(response)

      setReceipts(current =>
        [saved, ...current.filter(item => item.id !== saved.id)].sort(
          (a, b) => b.id - a.id,
        ),
      )

      setSelectedReceipt(saved)
      setSuccess(`${saved.receiptNumber} was recorded successfully.`)

      // Start a new submission only after confirmed success.
      submissionId.current = crypto.randomUUID()

      setOrderId('')
      setReceivedDate(localToday())
      setDeliveryNoteNumber('')
      setNotes('')
      setDraftLines([])

      try {
        const ordersResponse = await apiFetch(
          '/api/goods-receipts/receivable-orders',
          { cache: 'no-store' },
        )

        setOrders(
          await readApiResponse<ReceivableOrder[]>(ordersResponse),
        )
      } catch (refreshError) {
        setOrders([])
        setError(
          `The receipt was saved, but available orders could not refresh. Reload this page before entering another delivery. ${errorMessage(refreshError)}`,
        )
      }
    } catch (saveError) {
      setError(errorMessage(saveError))
    } finally {
      savingRef.current = false
      setSaving(false)
    }
  }

  if (loading) {
    return <p role="status">Loading goods receipts...</p>
  }

  return (
    <>
      <header className="page-header">
        <span className="workspace-page-emblem" aria-hidden="true"><Icon name="receipts" /></span>
        <h1>Goods Receipts</h1>
        <p>Record materials delivered against issued purchase orders.</p>
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

      {canReceive && (
        <section className="panel">
          <h2>Record a delivery</h2>

          {orders.length === 0 ? (
            <p>
              No purchase orders are available for receiving. Issue a
              purchase order with outstanding quantities first.
            </p>
          ) : (
            <form onSubmit={handleSubmit}>
              <fieldset className="receipt-fieldset" disabled={saving}>
                <div className="form-grid">
                  <label>
                    Purchase order
                    <select
                      value={orderId}
                      onChange={event => changeOrder(event.target.value)}
                      required
                    >
                      <option value="">Select a purchase order</option>

                      {orders.map(order => (
                        <option key={order.id} value={order.id}>
                          {order.orderNumber} — {order.supplierName}
                        </option>
                      ))}
                    </select>
                  </label>

                  <label>
                    Received date
                    <input
                      type="date"
                      value={receivedDate}
                      min={selectedOrder?.orderDate}
                      onChange={event =>
                        setReceivedDate(event.target.value)
                      }
                      required
                    />
                  </label>

                  <label>
                    Supplier delivery note number
                    <input
                      type="text"
                      value={deliveryNoteNumber}
                      onChange={event =>
                        setDeliveryNoteNumber(event.target.value)
                      }
                      maxLength={100}
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

                {selectedOrder && (
                  <>
                    <p>
                      <strong>Project:</strong>{' '}
                      {selectedOrder.projectName}
                      {' | '}
                      <strong>Supplier:</strong>{' '}
                      {selectedOrder.supplierName}
                    </p>

                    <p>
                      Enter the quantities delivered. Leave materials
                      that were not delivered blank.
                    </p>

                    <div className="table-container">
                      <table>
                        <thead>
                          <tr>
                            <th>Material</th>
                            <th>Unit</th>
                            <th>Ordered</th>
                            <th>Previously received</th>
                            <th>Remaining</th>
                            <th>Receive now</th>
                            <th>Unit cost</th>
                          </tr>
                        </thead>

                        <tbody>
                          {selectedOrder.lines.map(line => {
                            const draft = draftLines.find(
                              item =>
                                item.purchaseOrderLineId ===
                                line.purchaseOrderLineId,
                            )

                            return (
                              <tr key={line.purchaseOrderLineId}>
                                <td>
                                  <strong>{line.materialCode}</strong>
                                  <br />
                                  {line.materialName}
                                </td>
                                <td>{line.unit}</td>
                                <td>{quantity(line.orderedQuantity)}</td>
                                <td>{quantity(line.receivedQuantity)}</td>
                                <td>{quantity(line.remainingQuantity)}</td>
                                <td>
                                  <input
                                    aria-label={`Quantity received for ${line.materialName}`}
                                    type="number"
                                    min="0.001"
                                    max={Math.min(
                                      line.remainingQuantity,
                                      999999999.999,
                                    )}
                                    step="0.001"
                                    value={draft?.quantity ?? ''}
                                    placeholder="Not delivered"
                                    onChange={event =>
                                      changeQuantity(
                                        line.purchaseOrderLineId,
                                        event.target.value,
                                      )
                                    }
                                  />
                                </td>
                                <td>{money(line.unitCost)}</td>
                              </tr>
                            )
                          })}
                        </tbody>
                      </table>
                    </div>

                    <p className="request-total">
                      Delivery value: {money(previewTotal)}
                    </p>

                    <button type="submit" className="primary-button">
                      {saving ? 'Recording...' : 'Record delivery'}
                    </button>
                  </>
                )}
              </fieldset>
            </form>
          )}
        </section>
      )}

      <section className="panel">
        <h2>Recorded deliveries</h2>

        {receipts.length === 0 ? (
          <p>No goods receipts have been recorded.</p>
        ) : (
          <div className="table-container">
            <table>
              <thead>
                <tr>
                  <th>Receipt</th>
                  <th>Purchase order</th>
                  <th>Supplier</th>
                  <th>Received date</th>
                  <th>Delivery note</th>
                  <th>Value</th>
                  <th>Action</th>
                </tr>
              </thead>

              <tbody>
                {receipts.map(receipt => (
                  <tr key={receipt.id}>
                    <td>{receipt.receiptNumber}</td>
                    <td>{receipt.orderNumber}</td>
                    <td>{receipt.supplierName}</td>
                    <td>{receipt.receivedDate}</td>
                    <td>{receipt.deliveryNoteNumber}</td>
                    <td className="number-cell">
                      {money(receipt.totalValue)}
                    </td>
                    <td>
                      <button
                        type="button"
                        className="secondary-button"
                        onClick={() => setSelectedReceipt(receipt)}
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

      {selectedReceipt && (
        <section className="panel">
          <div className="section-toolbar">
            <h2>{selectedReceipt.receiptNumber}</h2>
            <button
              type="button"
              className="secondary-button"
              onClick={() => setSelectedReceipt(null)}
            >
              Close details
            </button>
          </div>

          <p>
            <strong>Purchase order:</strong>{' '}
            {selectedReceipt.orderNumber}
          </p>
          <p>
            <strong>Project:</strong> {selectedReceipt.projectName}
          </p>
          <p>
            <strong>Supplier:</strong> {selectedReceipt.supplierName}
          </p>
          <p>
            <strong>Received date:</strong>{' '}
            {selectedReceipt.receivedDate}
          </p>
          <p>
            <strong>Delivery note:</strong>{' '}
            {selectedReceipt.deliveryNoteNumber}
          </p>
          <p>
            <strong>Recorded by:</strong>{' '}
            {selectedReceipt.createdByName}
          </p>
          <p>
            <strong>Recorded at:</strong>{' '}
            {new Date(selectedReceipt.createdAtUtc).toLocaleString()}
          </p>
          <p>
            <strong>Status:</strong> {selectedReceipt.status}
          </p>

          {selectedReceipt.notes && (
            <p>
              <strong>Notes:</strong> {selectedReceipt.notes}
            </p>
          )}

          <div className="table-container">
            <table>
              <thead>
                <tr>
                  <th>Code</th>
                  <th>Material</th>
                  <th>Unit</th>
                  <th>Received quantity</th>
                  <th>Unit cost</th>
                  <th>Line value</th>
                </tr>
              </thead>

              <tbody>
                {selectedReceipt.lines.map(line => (
                  <tr key={line.id}>
                    <td>{line.materialCode}</td>
                    <td>{line.materialName}</td>
                    <td>{line.unit}</td>
                    <td>{quantity(line.quantity)}</td>
                    <td>{money(line.unitCost)}</td>
                    <td>{money(line.lineTotal)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <p className="request-total">
            Total: {money(selectedReceipt.totalValue)}
          </p>
        </section>
      )}
    </>
  )
}