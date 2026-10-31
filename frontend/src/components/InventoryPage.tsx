import { useEffect, useState } from 'react'
import { apiFetch, readApiResponse } from '../api/apiFetch'

type StockBalance = {
  projectId: number
  projectCode: string
  projectName: string
  materialId: number
  materialCode: string
  materialName: string
  unit: string
  quantityOnHand: number
}

type StockMovement = {
  id: number
  projectId: number
  projectCode: string
  projectName: string
  materialId: number
  materialCode: string
  materialName: string
  unit: string
  movementType: string
  quantity: number
  unitCost: number
  movementDate: string
  goodsReceiptLineId: number | null
  goodsReceiptId: number | null
  materialIssueLineId: number | null
  materialIssueId: number | null
  inventoryOperationId: number | null
  reversesMovementId: number | null
  createdByName: string
  createdAtUtc: string
}

type MovementPage = {
  page: number
  pageSize: number
  totalCount: number
  items: StockMovement[]
}

const PAGE_SIZE = 20

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
    : 'Unable to load inventory.'
}

function getSourceReference(movement: StockMovement) {
  if (movement.reversesMovementId != null) return `Reverses movement ${movement.reversesMovementId}`
  if (movement.inventoryOperationId != null) return `ST-${movement.inventoryOperationId}`
  if (movement.goodsReceiptId != null) {
    return `GR-${String(movement.goodsReceiptId).padStart(6, '0')}`
  }

  if (movement.materialIssueId != null) {
    return `MI-${String(movement.materialIssueId).padStart(6, '0')}`
  }

  return '—'
}

export default function InventoryPage() {
  const [balances, setBalances] = useState<StockBalance[]>([])
  const [movements, setMovements] = useState<MovementPage | null>(null)

  const [projectId, setProjectId] = useState('')
  const [materialId, setMaterialId] = useState('')
  const [page, setPage] = useState(1)
  const [refreshKey, setRefreshKey] = useState(0)

  const [balancesLoading, setBalancesLoading] = useState(true)
  const [movementsLoading, setMovementsLoading] = useState(true)

  const [balanceError, setBalanceError] = useState('')
  const [movementError, setMovementError] = useState('')

  useEffect(() => {
    const controller = new AbortController()

    async function loadBalances() {
      setBalancesLoading(true)
      setBalanceError('')

      try {
        const response = await apiFetch('/api/inventory/balances', {
          signal: controller.signal,
          cache: 'no-store',
        })

        const data = await readApiResponse<StockBalance[]>(response)

        if (!controller.signal.aborted) {
          setBalances(data)
        }
      } catch (error) {
        if (!controller.signal.aborted) {
          setBalanceError(getError(error))
        }
      } finally {
        if (!controller.signal.aborted) {
          setBalancesLoading(false)
        }
      }
    }

    void loadBalances()

    return () => controller.abort()
  }, [refreshKey])

  useEffect(() => {
    const controller = new AbortController()

    async function loadMovements() {
      setMovementsLoading(true)
      setMovementError('')
      setMovements(null)

      const parameters = new URLSearchParams({
        page: String(page),
        pageSize: String(PAGE_SIZE),
      })

      if (projectId) {
        parameters.set('projectId', projectId)
      }

      if (materialId) {
        parameters.set('materialId', materialId)
      }

      try {
        const response = await apiFetch(
          `/api/inventory/movements?${parameters.toString()}`,
          {
            signal: controller.signal,
            cache: 'no-store',
          },
        )

        const data = await readApiResponse<MovementPage>(response)

        if (!controller.signal.aborted) {
          setMovements(data)
        }
      } catch (error) {
        if (!controller.signal.aborted) {
          setMovementError(getError(error))
        }
      } finally {
        if (!controller.signal.aborted) {
          setMovementsLoading(false)
        }
      }
    }

    void loadMovements()

    return () => controller.abort()
  }, [projectId, materialId, page, refreshKey])

  const projects = Array.from(
    new Map(
      balances.map(balance => [
        balance.projectId,
        {
          id: balance.projectId,
          code: balance.projectCode,
          name: balance.projectName,
        },
      ]),
    ).values(),
  ).sort((a, b) => a.code.localeCompare(b.code))

  const materials = Array.from(
    new Map(
      balances.map(balance => [
        balance.materialId,
        {
          id: balance.materialId,
          code: balance.materialCode,
          name: balance.materialName,
        },
      ]),
    ).values(),
  ).sort((a, b) => a.code.localeCompare(b.code))

  const filteredBalances = balances.filter(balance => {
    const matchesProject =
      !projectId || balance.projectId === Number(projectId)

    const matchesMaterial =
      !materialId || balance.materialId === Number(materialId)

    return matchesProject && matchesMaterial
  })

  const projectCount = new Set(
    filteredBalances.map(balance => balance.projectId),
  ).size

  const materialCount = new Set(
    filteredBalances.map(balance => balance.materialId),
  ).size

  const totalPages = movements
    ? Math.max(1, Math.ceil(movements.totalCount / PAGE_SIZE))
    : 1

  function clearFilters() {
    setProjectId('')
    setMaterialId('')
    setPage(1)
  }

  function refreshInventory() {
    setPage(1)
    setRefreshKey(current => current + 1)
  }

  function viewMovements(balance: StockBalance) {
    setProjectId(String(balance.projectId))
    setMaterialId(String(balance.materialId))
    setPage(1)
  }

  return (
    <>
      <header className="page-header">
        <h1>Inventory</h1>
        <p>View project stock balances and material movements.</p>
      </header>

      <section className="panel">
        <div className="section-toolbar">
          <h2>Filter inventory</h2>

          <button
            type="button"
            className="secondary-button"
            onClick={refreshInventory}
            disabled={balancesLoading || movementsLoading}
          >
            Refresh
          </button>
        </div>

        <div className="inventory-filters">
          <label>
            Project
            <select
              value={projectId}
              disabled={balancesLoading}
              onChange={event => {
                setProjectId(event.target.value)
                setPage(1)
              }}
            >
              <option value="">All projects</option>

              {projects.map(project => (
                <option key={project.id} value={project.id}>
                  {project.code} — {project.name}
                </option>
              ))}
            </select>
          </label>

          <label>
            Material
            <select
              value={materialId}
              disabled={balancesLoading}
              onChange={event => {
                setMaterialId(event.target.value)
                setPage(1)
              }}
            >
              <option value="">All materials</option>

              {materials.map(material => (
                <option key={material.id} value={material.id}>
                  {material.code} — {material.name}
                </option>
              ))}
            </select>
          </label>

          <button
            type="button"
            className="secondary-button"
            onClick={clearFilters}
          >
            Clear filters
          </button>
        </div>
      </section>

      {balanceError && (
        <p className="error-message" role="alert">
          {balanceError}
        </p>
      )}

      {!balancesLoading && !balanceError && (
        <div className="inventory-summary">
          <div className="panel">
            <span>Projects in selection</span>
            <strong>{projectCount}</strong>
          </div>

          <div className="panel">
            <span>Materials in selection</span>
            <strong>{materialCount}</strong>
          </div>

          <div className="panel">
            <span>Project/material balances</span>
            <strong>{filteredBalances.length}</strong>
          </div>
        </div>
      )}

      <section className="panel">
        <h2>Stock balances</h2>

        {balancesLoading ? (
          <p role="status">Loading stock balances...</p>
        ) : balanceError ? (
          <p>Unable to display balances. Click Refresh to try again.</p>
        ) : filteredBalances.length === 0 ? (
          <p>
            No stock balances match this selection. Stock appears after
            deliveries are posted or existing receipts are imported.
          </p>
        ) : (
          <div className="table-container">
            <table>
              <thead>
                <tr>
                  <th>Project</th>
                  <th>Material code</th>
                  <th>Material</th>
                  <th>Unit</th>
                  <th>Quantity on hand</th>
                  <th>Action</th>
                </tr>
              </thead>

              <tbody>
                {filteredBalances.map(balance => (
                  <tr
                    key={`${balance.projectId}-${balance.materialId}`}
                  >
                    <td>
                      <strong>{balance.projectCode}</strong>
                      <br />
                      {balance.projectName}
                    </td>

                    <td>{balance.materialCode}</td>
                    <td>{balance.materialName}</td>
                    <td>{balance.unit}</td>

                    <td className="number-cell">
                      {formatQuantity(balance.quantityOnHand)}
                    </td>

                    <td>
                      <button
                        type="button"
                        className="secondary-button"
                        onClick={() => viewMovements(balance)}
                      >
                        View movements
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section className="panel">
        <h2>Stock movement history</h2>

        {movementError && (
          <p className="error-message" role="alert">
            {movementError}
          </p>
        )}

        {movementsLoading ? (
          <p role="status">Loading stock movements...</p>
        ) : movements && movements.items.length > 0 ? (
          <>
            <div className="table-container">
              <table>
                <thead>
                  <tr>
                    <th>Date</th>
                    <th>Project</th>
                    <th>Material</th>
                    <th>Type</th>
                    <th>Quantity change</th>
                    <th>Unit</th>
                    <th>Unit cost</th>
                    <th>Source</th>
                    <th>Recorded by</th>
                  </tr>
                </thead>

                <tbody>
                  {movements.items.map(movement => (
                    <tr key={movement.id}>
                      <td>{movement.movementDate}</td>

                      <td>
                        <strong>{movement.projectCode}</strong>
                        <br />
                        {movement.projectName}
                      </td>

                      <td>
                        <strong>{movement.materialCode}</strong>
                        <br />
                        {movement.materialName}
                      </td>

                      <td>
                        <span className="status-badge">
                          {movement.movementType}
                        </span>
                      </td>

                      <td
                        className={
                          movement.quantity > 0
                            ? 'number-cell inventory-in'
                            : 'number-cell inventory-out'
                        }
                      >
                        {movement.quantity > 0 ? '+' : ''}
                        {formatQuantity(movement.quantity)}
                      </td>

                      <td>{movement.unit}</td>

                      <td>{formatMoney(movement.unitCost)}</td>

                      <td>{getSourceReference(movement)}</td>

                      <td>{movement.createdByName}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="inventory-pagination">
              <button
                type="button"
                className="secondary-button"
                disabled={page <= 1}
                onClick={() => setPage(current => current - 1)}
              >
                Previous
              </button>

              <span>
                Page {page} of {totalPages} · {movements.totalCount}{' '}
                movements
              </span>

              <button
                type="button"
                className="secondary-button"
                disabled={page >= totalPages}
                onClick={() => setPage(current => current + 1)}
              >
                Next
              </button>
            </div>
          </>
        ) : !movementError ? (
          <>
            <p>No stock movements found.</p>

            {page > 1 && (
              <button
                type="button"
                className="secondary-button"
                onClick={() => setPage(1)}
              >
                Return to first page
              </button>
            )}
          </>
        ) : null}
      </section>
    </>
  )
}
