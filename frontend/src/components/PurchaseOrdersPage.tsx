import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { apiFetch, readApiResponse } from '../api/apiFetch'

interface Supplier {
  id: number
  code: string
  name: string
  isActive: boolean
}

interface AvailableLine {
  materialRequestLineId: number
  materialCode: string
  materialName: string
  unit: string
  requestedQuantity: number
  orderedQuantity: number
  remainingQuantity: number
  estimatedUnitPrice: number
}

interface AvailableRequest {
  id: number
  requestNumber: string
  projectName: string
  requiredDate: string
  lines: AvailableLine[]
}

interface OrderLine {
  id: number
  materialRequestLineId: number
  materialCode: string
  materialName: string
  unit: string
  quantity: number
  unitPrice: number
  lineTotal: number
}

interface PurchaseOrder {
  id: number
  orderNumber: string
  requestNumber: string
  projectName: string
  supplierName: string
  orderDate: string
  expectedDeliveryDate: string
  notes: string
  status: string
  createdByName: string
  totalAmount: number
  lines: OrderLine[]
}

interface DraftLine {
  requestLineId: number
  included: boolean
  quantity: string
  unitPrice: string
}

const money = new Intl.NumberFormat('en-LK', {
  style: 'currency',
  currency: 'LKR',
})

function todayLocal(): string {
  const date = new Date()

  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')

  return `${year}-${month}-${day}`
}

function calculateTotal(line: DraftLine): number {
  const quantity = Number(line.quantity)
  const price = Number(line.unitPrice)

  if (!Number.isFinite(quantity) || !Number.isFinite(price)) {
    return 0
  }

  return Math.round(
    (quantity * price + Number.EPSILON) * 100,
  ) / 100
}

export default function PurchaseOrdersPage() {
  const [orders, setOrders] = useState<PurchaseOrder[]>([])
  const [availableRequests, setAvailableRequests] =
    useState<AvailableRequest[]>([])
  const [suppliers, setSuppliers] = useState<Supplier[]>([])
  const [canCreate, setCanCreate] = useState(false)

  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState('')

  const [requestId, setRequestId] = useState('')
  const [supplierId, setSupplierId] = useState('')
  const [orderDate, setOrderDate] = useState(todayLocal)
  const [deliveryDate, setDeliveryDate] = useState('')
  const [notes, setNotes] = useState('')
  const [draftLines, setDraftLines] = useState<DraftLine[]>([])

  const [saving, setSaving] = useState(false)
  const [formError, setFormError] = useState('')
  const [success, setSuccess] = useState('')
  const [warning, setWarning] = useState('')

  const [selectedOrder, setSelectedOrder] =
    useState<PurchaseOrder | null>(null)

  useEffect(() => {
    const controller = new AbortController()
    const options = { signal: controller.signal }

    async function load() {
      try {
        const [orderData, user] = await Promise.all([
          apiFetch('/api/purchase-orders', options).then(
            (response) =>
              readApiResponse<PurchaseOrder[]>(response),
          ),

          apiFetch('/api/auth/me', {
            ...options,
            cache: 'no-store',
          }).then((response) =>
            readApiResponse<{ roles: string[] }>(response),
          ),
        ])

        const allowed = user.roles.some((role) =>
          ['Administrator', 'ProcurementOfficer'].includes(role),
        )

        let requestData: AvailableRequest[] = []
        let supplierData: Supplier[] = []

        if (allowed) {
          ;[requestData, supplierData] = await Promise.all([
            apiFetch(
              '/api/purchase-orders/available-requests',
              options,
            ).then((response) =>
              readApiResponse<AvailableRequest[]>(response),
            ),

            apiFetch('/api/suppliers', options).then(
              (response) =>
                readApiResponse<Supplier[]>(response),
            ),
          ])
        }

        if (controller.signal.aborted) return

        setOrders(orderData)
        setCanCreate(allowed)
        setAvailableRequests(requestData)
        setSuppliers(
          supplierData.filter((supplier) => supplier.isActive),
        )
      } catch (error) {
        if (controller.signal.aborted) return

        setLoadError(
          error instanceof Error
            ? error.message
            : 'Unable to load purchase orders.',
        )
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false)
        }
      }
    }

    void load()

    return () => controller.abort()
  }, [])

  const selectedRequest = availableRequests.find(
    (request) => request.id === Number(requestId),
  )

  function selectRequest(value: string) {
    setRequestId(value)
    setFormError('')
    setSuccess('')

    const request = availableRequests.find(
      (item) => item.id === Number(value),
    )

    setDraftLines(
      request
        ? request.lines.map((line) => ({
            requestLineId: line.materialRequestLineId,
            included: true,
            quantity: String(line.remainingQuantity),
            unitPrice: String(line.estimatedUnitPrice),
          }))
        : [],
    )
  }

  function updateLine(
    id: number,
    changes: Partial<Omit<DraftLine, 'requestLineId'>>,
  ) {
    setDraftLines((current) =>
      current.map((line) =>
        line.requestLineId === id
          ? { ...line, ...changes }
          : line,
      ),
    )
  }

  const total = draftLines
    .filter((line) => line.included)
    .reduce((sum, line) => sum + calculateTotal(line), 0)

  async function handleCreate(
    event: FormEvent<HTMLFormElement>,
  ) {
    event.preventDefault()

    if (saving) return

    setFormError('')
    setSuccess('')
    setWarning('')

    if (
      !selectedRequest ||
      !supplierId ||
      !orderDate ||
      !deliveryDate
    ) {
      setFormError(
        'Select a request, supplier, order date, and delivery date.',
      )
      return
    }

    if (deliveryDate < orderDate) {
      setFormError(
        'Delivery date cannot be before the order date.',
      )
      return
    }

    const includedLines = draftLines.filter(
      (line) => line.included,
    )

    if (includedLines.length === 0) {
      setFormError('Include at least one material line.')
      return
    }

    for (const line of includedLines) {
      const available = selectedRequest.lines.find(
        (item) =>
          item.materialRequestLineId === line.requestLineId,
      )

      const quantity = Number(line.quantity)
      const price = Number(line.unitPrice)

      if (
        !available ||
        !line.quantity.trim() ||
        !Number.isFinite(quantity) ||
        quantity <= 0 ||
        quantity > available.remainingQuantity ||
        quantity > 999999999.999
      ) {
        setFormError(
          'Each quantity must be positive and within the remaining quantity.',
        )
        return
      }

      if (
        !line.unitPrice.trim() ||
        !Number.isFinite(price) ||
        price < 0 ||
        price > 999999999.99
      ) {
        setFormError('Enter a valid nonnegative unit price.')
        return
      }
    }

    setSaving(true)

    try {
      const response = await apiFetch('/api/purchase-orders', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          materialRequestId: selectedRequest.id,
          supplierId: Number(supplierId),
          orderDate,
          expectedDeliveryDate: deliveryDate,
          notes: notes.trim() || null,

          lines: includedLines.map((line) => ({
            materialRequestLineId: line.requestLineId,
            quantity: Number(line.quantity),
            unitPrice: Number(line.unitPrice),
          })),
        }),
      })

      const created =
        await readApiResponse<PurchaseOrder>(response)

      setOrders((current) => [created, ...current])
      setSelectedOrder(created)

      setRequestId('')
      setSupplierId('')
      setDeliveryDate('')
      setNotes('')
      setDraftLines([])

      setSuccess(`${created.orderNumber} saved as a draft.`)

      // Refresh availability after the saved order reserves quantities.
      try {
        const availableResponse = await apiFetch(
          '/api/purchase-orders/available-requests',
        )

        const updated =
          await readApiResponse<AvailableRequest[]>(
            availableResponse,
          )

        setAvailableRequests(updated)
      } catch {
        setAvailableRequests([])
        setWarning(
          'The order was saved, but available quantities could not be refreshed. Refresh the page before creating another order.',
        )
      }
    } catch (error) {
      setFormError(
        error instanceof Error
          ? error.message
          : 'Unable to create purchase order.',
      )
    } finally {
      setSaving(false)
    }
  }

  if (loading) {
    return <p role="status">Loading purchase orders...</p>
  }

  if (loadError) {
    return (
      <p className="error-message" role="alert">
        {loadError}
      </p>
    )
  }

  return (
    <>
      <header className="page-header">
        <h1>Purchase Orders</h1>
        <p>
          Purchase approved materials from your suppliers.
        </p>
      </header>

      {canCreate && (
        <section className="panel">
          <h2>Create Purchase Order</h2>

          {availableRequests.length === 0 && (
            <p>
              No approved requests with available quantities.
            </p>
          )}

          {suppliers.length === 0 && (
            <p className="error-message">
              Create an active supplier first.
            </p>
          )}

          <form onSubmit={handleCreate}>
            <fieldset
              className="form-fields"
              disabled={
                saving ||
                availableRequests.length === 0 ||
                suppliers.length === 0
              }
            >
              <div className="form-grid">
                <label htmlFor="po-request">
                  Approved request
                  <select
                    id="po-request"
                    value={requestId}
                    onChange={(event) =>
                      selectRequest(event.target.value)
                    }
                    required
                  >
                    <option value="">Select a request</option>

                    {availableRequests.map((request) => (
                      <option
                        key={request.id}
                        value={request.id}
                      >
                        {request.requestNumber} —{' '}
                        {request.projectName}
                      </option>
                    ))}
                  </select>
                </label>

                <label htmlFor="po-supplier">
                  Supplier
                  <select
                    id="po-supplier"
                    value={supplierId}
                    onChange={(event) =>
                      setSupplierId(event.target.value)
                    }
                    required
                  >
                    <option value="">Select a supplier</option>

                    {suppliers.map((supplier) => (
                      <option
                        key={supplier.id}
                        value={supplier.id}
                      >
                        {supplier.code} — {supplier.name}
                      </option>
                    ))}
                  </select>
                </label>

                <label htmlFor="po-order-date">
                  Order date
                  <input
                    id="po-order-date"
                    type="date"
                    value={orderDate}
                    onChange={(event) =>
                      setOrderDate(event.target.value)
                    }
                    required
                  />
                </label>

                <label htmlFor="po-delivery-date">
                  Expected delivery date
                  <input
                    id="po-delivery-date"
                    type="date"
                    value={deliveryDate}
                    min={orderDate}
                    onChange={(event) =>
                      setDeliveryDate(event.target.value)
                    }
                    required
                  />
                </label>
              </div>

              <label htmlFor="po-notes">
                Notes
                <textarea
                  id="po-notes"
                  value={notes}
                  onChange={(event) =>
                    setNotes(event.target.value)
                  }
                  maxLength={1000}
                  rows={3}
                />
              </label>

              {selectedRequest && (
                <>
                  <h3>Material Lines</h3>

                  <p>
                    Uncheck materials you are not purchasing
                    from this supplier.
                  </p>

                  {draftLines.map((line) => {
                    const material =
                      selectedRequest.lines.find(
                        (item) =>
                          item.materialRequestLineId ===
                          line.requestLineId,
                      )

                    if (!material) return null

                    return (
                      <div
                        className="request-line"
                        key={line.requestLineId}
                      >
                        <label className="checkbox-label">
                          <input
                            type="checkbox"
                            checked={line.included}
                            onChange={(event) =>
                              updateLine(
                                line.requestLineId,
                                {
                                  included:
                                    event.target.checked,
                                },
                              )
                            }
                          />

                          {material.materialCode} —{' '}
                          {material.materialName}
                        </label>

                        <p>
                          Approved:{' '}
                          {material.requestedQuantity}{' '}
                          {material.unit} · Ordered:{' '}
                          {material.orderedQuantity} ·
                          Remaining:{' '}
                          {material.remainingQuantity}
                        </p>

                        <div className="form-grid">
                          <label
                            htmlFor={`po-quantity-${line.requestLineId}`}
                          >
                            Order quantity ({material.unit})
                            <input
                              id={`po-quantity-${line.requestLineId}`}
                              type="number"
                              min="0.001"
                              max={Math.min(
                                material.remainingQuantity,
                                999999999.999,
                              )}
                              step="0.001"
                              value={line.quantity}
                              disabled={!line.included}
                              required={line.included}
                              onChange={(event) =>
                                updateLine(
                                  line.requestLineId,
                                  {
                                    quantity:
                                      event.target.value,
                                  },
                                )
                              }
                            />
                          </label>

                          <label
                            htmlFor={`po-price-${line.requestLineId}`}
                          >
                            Agreed unit price (LKR)
                            <input
                              id={`po-price-${line.requestLineId}`}
                              type="number"
                              min="0"
                              max="999999999.99"
                              step="0.01"
                              value={line.unitPrice}
                              disabled={!line.included}
                              required={line.included}
                              onChange={(event) =>
                                updateLine(
                                  line.requestLineId,
                                  {
                                    unitPrice:
                                      event.target.value,
                                  },
                                )
                              }
                            />
                          </label>
                        </div>

                        <p>
                          Line total:{' '}
                          <strong>
                            {money.format(
                              line.included
                                ? calculateTotal(line)
                                : 0,
                            )}
                          </strong>
                        </p>
                      </div>
                    )
                  })}

                  <p className="request-total">
                    Order total: {money.format(total)}
                  </p>
                </>
              )}

              <button
                className="primary-button"
                type="submit"
                disabled={!selectedRequest}
              >
                {saving
                  ? 'Saving...'
                  : 'Save Draft Purchase Order'}
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

            {warning && (
              <p className="error-message" role="alert">
                {warning}
              </p>
            )}
          </form>
        </section>
      )}

      <section className="panel">
        <h2>Purchase Order List</h2>

        {orders.length === 0 ? (
          <p>No purchase orders yet.</p>
        ) : (
          <div className="table-container">
            <table>
              <thead>
                <tr>
                  <th scope="col">Order</th>
                  <th scope="col">Request</th>
                  <th scope="col">Supplier</th>
                  <th scope="col">Order date</th>
                  <th
                    scope="col"
                    className="number-cell"
                  >
                    Total
                  </th>
                  <th scope="col">Status</th>
                  <th scope="col">Action</th>
                </tr>
              </thead>

              <tbody>
                {orders.map((order) => (
                  <tr key={order.id}>
                    <td>{order.orderNumber}</td>
                    <td>{order.requestNumber}</td>
                    <td>{order.supplierName}</td>
                    <td>{order.orderDate}</td>
                    <td className="number-cell">
                      {money.format(order.totalAmount)}
                    </td>
                    <td>
                      <span className="status-badge">
                        {order.status}
                      </span>
                    </td>
                    <td>
                      <button
                        type="button"
                        className="secondary-button"
                        onClick={() =>
                          setSelectedOrder(order)
                        }
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

      {selectedOrder && (
        <section className="panel">
          <div className="section-toolbar">
            <h2>{selectedOrder.orderNumber}</h2>

            <button
              type="button"
              className="secondary-button"
              onClick={() => setSelectedOrder(null)}
            >
              Close details
            </button>
          </div>

          <p>
            <strong>Request:</strong>{' '}
            {selectedOrder.requestNumber}
          </p>

          <p>
            <strong>Project:</strong>{' '}
            {selectedOrder.projectName}
          </p>

          <p>
            <strong>Supplier:</strong>{' '}
            {selectedOrder.supplierName}
          </p>

          <p>
            <strong>Order date:</strong>{' '}
            {selectedOrder.orderDate}
          </p>

          <p>
            <strong>Expected delivery:</strong>{' '}
            {selectedOrder.expectedDeliveryDate}
          </p>

          <p>
            <strong>Created by:</strong>{' '}
            {selectedOrder.createdByName}
          </p>

          <p>
            <strong>Status:</strong>{' '}
            {selectedOrder.status}
          </p>

          {selectedOrder.notes && (
            <p className="request-reason">
              <strong>Notes:</strong>{' '}
              {selectedOrder.notes}
            </p>
          )}

          <div className="table-container">
            <table>
              <thead>
                <tr>
                  <th scope="col">Material</th>
                  <th scope="col">Unit</th>
                  <th
                    scope="col"
                    className="number-cell"
                  >
                    Quantity
                  </th>
                  <th
                    scope="col"
                    className="number-cell"
                  >
                    Unit price
                  </th>
                  <th
                    scope="col"
                    className="number-cell"
                  >
                    Total
                  </th>
                </tr>
              </thead>

              <tbody>
                {selectedOrder.lines.map((line) => (
                  <tr key={line.id}>
                    <td>
                      {line.materialCode} —{' '}
                      {line.materialName}
                    </td>
                    <td>{line.unit}</td>
                    <td className="number-cell">
                      {line.quantity}
                    </td>
                    <td className="number-cell">
                      {money.format(line.unitPrice)}
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
            Total:{' '}
            {money.format(selectedOrder.totalAmount)}
          </p>
        </section>
      )}
    </>
  )
}