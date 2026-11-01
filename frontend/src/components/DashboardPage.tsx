import { useEffect, useState } from 'react'
import { apiFetch, readApiResponse } from '../api/apiFetch'
import Icon from './ui/Icon'
import ConstructionAnimation from './ConstructionAnimation'

type DashboardDestination =
  | 'projects'
  | 'requests'
  | 'orders'
  | 'receipts'
  | 'inventory'
  | 'issues'

type DashboardData = {
  generatedAtUtc: string

  summary: {
    activeProjects: number
    pendingRequests: number
    requestsAwaitingPurchase: number
    draftPurchaseOrders: number
    ordersAwaitingDelivery: number
    postedReceipts: number
    postedMaterialIssues: number
  }

  recentReceipts: {
    id: number
    purchaseOrderId: number
    projectName: string
    supplierName: string
    receivedDate: string
    deliveryNoteNumber: string
    createdByName: string
    createdAtUtc: string
    lineCount: number
  }[]

  recentIssues: {
    id: number
    projectName: string
    issueDate: string
    purpose: string
    receivedBy: string
    createdByName: string
    createdAtUtc: string
    lineCount: number
  }[]

  requestsToReview: {
    id: number
    projectName: string
    requiredDate: string
    createdByName: string
    submittedAtUtc: string | null
  }[]

  outstandingDeliveries: {
    id: number
    projectName: string
    supplierName: string
    expectedDeliveryDate: string
    status: string
  }[]
}

type Props = {
  onNavigate: (page: DashboardDestination) => void
}

function reference(prefix: string, id: number) {
  return `${prefix}-${String(id).padStart(6, '0')}`
}

export default function DashboardPage({ onNavigate }: Props) {
  const [data, setData] = useState<DashboardData | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [refreshKey, setRefreshKey] = useState(0)

  useEffect(() => {
    const controller = new AbortController()

    async function loadDashboard() {
      setLoading(true)
      setError('')

      try {
        const response = await apiFetch('/api/dashboard', {
          signal: controller.signal,
          cache: 'no-store',
        })

        const result = await readApiResponse<DashboardData>(response)

        if (!controller.signal.aborted) {
          setData(result)
        }
      } catch (loadError) {
        if (!controller.signal.aborted) {
          setError(
            loadError instanceof Error
              ? loadError.message
              : 'Unable to load the dashboard.',
          )
        }
      } finally {
        if (!controller.signal.aborted) {
          setLoading(false)
        }
      }
    }

    void loadDashboard()

    return () => controller.abort()
  }, [refreshKey])

  return (
    <div className="dashboard-page">
      <header className="page-header dashboard-heading">
        <p className="eyebrow">OPERATIONS OVERVIEW</p>
        <h1>Your operations, at a glance<span className="text-teal-600">.</span></h1>
        <p>Construction and materials activity for your accessible projects.</p>
      </header>

      <section className="dashboard-hero">
        <div className="relative z-10"><span className="hero-label"><span />CONNECTED MATERIALS MANAGEMENT</span><h2>Keep every site<br /><span>moving forward.</span></h2><p>From the first request to the final issue.<br className="hidden sm:block" /> Everything you need to keep materials flowing.</p><button type="button" className="hero-action" onClick={() => onNavigate('inventory')}>Explore your inventory<Icon name="arrow" /></button></div>
        <div className="dashboard-hero-visual"><ConstructionAnimation /><div className="hero-flow" aria-label="Materials workflow"><div><span className="hero-flow-icon"><Icon name="requests" /></span><span>01</span><strong>Request</strong></div><i /><div><span className="hero-flow-icon"><Icon name="orders" /></span><span>02</span><strong>Purchase</strong></div><i /><div><span className="hero-flow-icon"><Icon name="inventory" /></span><span>03</span><strong>Receive & track</strong></div></div>
      </div></section>

      <nav className="dashboard-shortcuts" aria-label="Dashboard quick access">
        <button type="button" onClick={() => onNavigate('requests')}><span><Icon name="requests" /></span><div><strong>Material requests</strong><small>Plan the next site requirement</small></div><Icon name="arrow" /></button>
        <button type="button" onClick={() => onNavigate('orders')}><span><Icon name="orders" /></span><div><strong>Purchase orders</strong><small>Keep procurement moving</small></div><Icon name="arrow" /></button>
        <button type="button" onClick={() => onNavigate('receipts')}><span><Icon name="receipts" /></span><div><strong>Goods receipts</strong><small>Review incoming materials</small></div><Icon name="arrow" /></button>
        <button type="button" onClick={() => onNavigate('inventory')}><span><Icon name="inventory" /></span><div><strong>Site inventory</strong><small>Trace stock and movements</small></div><Icon name="arrow" /></button>
      </nav>
      <div className="section-toolbar dashboard-toolbar">
        <p>
          {loading
            ? 'Loading dashboard...'
            : data
              ? `Updated ${new Date(data.generatedAtUtc).toLocaleString()}`
              : 'Dashboard unavailable'}
        </p>

        <button
          type="button"
          className="secondary-button"
          disabled={loading}
          onClick={() => setRefreshKey(current => current + 1)}
        >
          <Icon name="refresh" className={loading ? 'is-spinning' : ''} />{loading ? 'Refreshing...' : 'Refresh overview'}
        </button>
      </div>

      {error && (
        <p className="error-message" role="alert">
          {error}
        </p>
      )}

      {loading ? (
        <div role="status" aria-label="Loading summary and recent activity"><div className="dashboard-cards skeleton-grid" aria-hidden="true">{Array.from({ length: 7 }, (_, index) => <div className="skeleton-card" key={index}><span /><strong /><small /></div>)}</div><span className="sr-only">Loading summary and recent activity...</span></div>
      ) : !error && data ? (
        <>
          <div className="dashboard-cards">
            <button
              type="button"
              className="dashboard-card"
              onClick={() => onNavigate('projects')}
            >
              <span className="dashboard-metric-icon" aria-hidden="true"><Icon name="projects" /></span>
              <span>Active projects</span>
              <strong>{data.summary.activeProjects}</strong>
              <small>View projects</small>
            </button>

            <button
              type="button"
              className="dashboard-card"
              onClick={() => onNavigate('requests')}
            >
              <span className="dashboard-metric-icon" aria-hidden="true"><Icon name="requests" /></span>
              <span>Requests awaiting review</span>
              <strong>{data.summary.pendingRequests}</strong>
              <small>View material requests</small>
            </button>

            <button
              type="button"
              className="dashboard-card"
              onClick={() => onNavigate('orders')}
            >
              <span className="dashboard-metric-icon" aria-hidden="true"><Icon name="orders" /></span>
              <span>Approved requests to order</span>
              <strong>{data.summary.requestsAwaitingPurchase}</strong>
              <small>View purchase orders</small>
            </button>

            <button
              type="button"
              className="dashboard-card"
              onClick={() => onNavigate('orders')}
            >
              <span className="dashboard-metric-icon" aria-hidden="true"><Icon name="orders" /></span>
              <span>Draft purchase orders</span>
              <strong>{data.summary.draftPurchaseOrders}</strong>
              <small>View purchase orders</small>
            </button>

            <button
              type="button"
              className="dashboard-card"
              onClick={() => onNavigate('receipts')}
            >
              <span className="dashboard-metric-icon" aria-hidden="true"><Icon name="receipts" /></span>
              <span>Orders awaiting delivery</span>
              <strong>{data.summary.ordersAwaitingDelivery}</strong>
              <small>View goods receipts</small>
            </button>

            <button
              type="button"
              className="dashboard-card"
              onClick={() => onNavigate('receipts')}
            >
              <span className="dashboard-metric-icon" aria-hidden="true"><Icon name="inventory" /></span>
              <span>Posted goods receipts</span>
              <strong>{data.summary.postedReceipts}</strong>
              <small>View deliveries</small>
            </button>

            <button
              type="button"
              className="dashboard-card"
              onClick={() => onNavigate('issues')}
            >
              <span className="dashboard-metric-icon" aria-hidden="true"><Icon name="issues" /></span>
              <span>Posted material issues</span>
              <strong>{data.summary.postedMaterialIssues}</strong>
              <small>View material issues</small>
            </button>
          </div>

          <div className="dashboard-activity">
          <section className="panel">
            <div className="section-toolbar">
              <h2>Requests awaiting review</h2>
              <button
                type="button"
                className="secondary-button"
                onClick={() => onNavigate('requests')}
              >
                View requests
              </button>
            </div>

            {data.requestsToReview.length === 0 ? (
              <p>No requests are awaiting review.</p>
            ) : (
              <div className="table-container">
                <table>
                  <thead>
                    <tr>
                      <th>Request</th>
                      <th>Project</th>
                      <th>Required date</th>
                      <th>Created by</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.requestsToReview.map(request => (
                      <tr key={request.id}>
                        <td>{reference('MR', request.id)}</td>
                        <td>{request.projectName}</td>
                        <td>{request.requiredDate}</td>
                        <td>{request.createdByName}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          <section className="panel">
            <div className="section-toolbar">
              <h2>Outstanding deliveries</h2>
              <button
                type="button"
                className="secondary-button"
                onClick={() => onNavigate('orders')}
              >
                View orders
              </button>
            </div>

            {data.outstandingDeliveries.length === 0 ? (
              <p>No issued orders are awaiting delivery.</p>
            ) : (
              <div className="table-container">
                <table>
                  <thead>
                    <tr>
                      <th>Order</th>
                      <th>Project</th>
                      <th>Supplier</th>
                      <th>Expected delivery</th>
                      <th>Status</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.outstandingDeliveries.map(order => (
                      <tr key={order.id}>
                        <td>{reference('PO', order.id)}</td>
                        <td>{order.projectName}</td>
                        <td>{order.supplierName}</td>
                        <td>{order.expectedDeliveryDate}</td>
                        <td>
                          <span className="status-badge" data-status={order.status}>
                            {order.status === 'PartiallyReceived'
                              ? 'Partially received'
                              : order.status}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          <section className="panel">
            <div className="section-toolbar">
              <h2>Recent goods receipts</h2>
              <button
                type="button"
                className="secondary-button"
                onClick={() => onNavigate('receipts')}
              >
                View receipts
              </button>
            </div>

            {data.recentReceipts.length === 0 ? (
              <p>No posted goods receipts yet.</p>
            ) : (
              <div className="table-container">
                <table>
                  <thead>
                    <tr>
                      <th>Receipt</th>
                      <th>Order</th>
                      <th>Project</th>
                      <th>Supplier</th>
                      <th>Received date</th>
                      <th>Recorded by</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.recentReceipts.map(receipt => (
                      <tr key={receipt.id}>
                        <td>{reference('GR', receipt.id)}</td>
                        <td>
                          {reference('PO', receipt.purchaseOrderId)}
                        </td>
                        <td>{receipt.projectName}</td>
                        <td>{receipt.supplierName}</td>
                        <td>{receipt.receivedDate}</td>
                        <td>{receipt.createdByName}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          <section className="panel">
            <div className="section-toolbar">
              <h2>Recent material issues</h2>
              <button
                type="button"
                className="secondary-button"
                onClick={() => onNavigate('issues')}
              >
                View issues
              </button>
            </div>

            {data.recentIssues.length === 0 ? (
              <p>No posted material issues yet.</p>
            ) : (
              <div className="table-container">
                <table>
                  <thead>
                    <tr>
                      <th>Issue</th>
                      <th>Project</th>
                      <th>Issue date</th>
                      <th>Received by</th>
                      <th>Purpose</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.recentIssues.map(issue => (
                      <tr key={issue.id}>
                        <td>{reference('MI', issue.id)}</td>
                        <td>{issue.projectName}</td>
                        <td>{issue.issueDate}</td>
                        <td>{issue.receivedBy}</td>
                        <td>{issue.purpose}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          <section className="panel">
            <div className="section-toolbar">
              <h2>Project stock</h2>
              <button
                type="button"
                className="primary-button"
                onClick={() => onNavigate('inventory')}
              >
                Open inventory
              </button>
            </div>
            <p>
              Check available quantities and trace receipts and issues
              for each project.
            </p>
          </section>
          </div>
        </>
      ) : null}
    </div>
  )
}
