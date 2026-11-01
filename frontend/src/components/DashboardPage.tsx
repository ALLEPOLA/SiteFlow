import type { AuthUser } from '../types/auth'
import { canAccessPage } from '../security/permissions'
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
  | 'reservations'

type DashboardData = {
  generatedAtUtc: string

  summary: {
    activeProjects: number | null
    activeReservations: number
    pendingRequests: number | null
    requestsAwaitingPurchase: number | null
    draftPurchaseOrders: number | null
    ordersAwaitingDelivery: number | null
    postedReceipts: number | null
    postedMaterialIssues: number | null
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
  user: AuthUser
  onNavigate: (page: DashboardDestination) => void
}

function reference(prefix: string, id: number) {
  return `${prefix}-${String(id).padStart(6, '0')}`
}

export default function DashboardPage({ onNavigate, user }: Props) {
  const [data, setData] = useState<DashboardData | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [refreshKey, setRefreshKey] = useState(0)

  const can = (page: DashboardDestination) => canAccessPage(user.roles, page)
  const reviewer = user.roles.some(role => ['Administrator', 'ProjectManager'].includes(role))
  const engineer = user.roles.includes('SiteEngineer')
  const purchaser = user.roles.some(role => ['Administrator', 'ProcurementOfficer'].includes(role))
  const primary: DashboardDestination = reviewer || engineer ? 'requests' : purchaser ? 'orders' : 'inventory'
  const primaryLabel = primary === 'requests' ? reviewer ? 'Review material requests' : 'Open my material requests' : primary === 'orders' ? 'Manage purchase orders' : 'Explore your inventory'
  const metrics: { key: keyof DashboardData['summary']; label: string; page: DashboardDestination; icon: string; show: boolean }[] = [
    { key: 'activeReservations', label: 'Active material reservations', page: 'reservations', icon: 'reservations', show: can('reservations') },
    { key: 'activeProjects', label: 'Active assigned projects', page: 'projects', icon: 'projects', show: can('projects') },
    { key: 'pendingRequests', label: reviewer ? 'Requests I can review' : 'My submitted requests', page: 'requests', icon: 'requests', show: reviewer || engineer },
    { key: 'requestsAwaitingPurchase', label: 'Approved requests to order', page: 'orders', icon: 'orders', show: purchaser },
    { key: 'draftPurchaseOrders', label: 'Draft purchase orders', page: 'orders', icon: 'orders', show: purchaser },
    { key: 'ordersAwaitingDelivery', label: 'Orders awaiting delivery', page: 'receipts', icon: 'receipts', show: can('receipts') },
    { key: 'postedReceipts', label: 'Posted goods receipts', page: 'receipts', icon: 'inventory', show: can('receipts') },
    { key: 'postedMaterialIssues', label: 'Posted material issues', page: 'issues', icon: 'issues', show: can('issues') },
  ]
  const shortcuts: { page: DashboardDestination; label: string; hint: string }[] = [
    { page: 'requests', label: reviewer ? 'Review requests' : 'Material requests', hint: reviewer ? 'Review requests from other creators' : 'Plan the next site requirement' },
    { page: 'orders', label: 'Purchase orders', hint: purchaser ? 'Keep procurement moving' : 'View assigned project orders' },
    { page: 'receipts', label: 'Goods receipts', hint: 'Review incoming materials' },
    { page: 'inventory', label: 'Site inventory', hint: 'Trace stock and movements' },
    { page: 'issues', label: 'Material issues', hint: 'Track materials issued to the site' },
  ]

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
        <p>Your assigned workspaces and project activity, based on your current role permissions.</p>
      </header>

      <section className="dashboard-hero">
        <div className="relative z-10"><span className="hero-label"><span />CONNECTED MATERIALS MANAGEMENT</span><h2>Keep every site<br /><span>moving forward.</span></h2><p>From the first request to the final issue.<br className="hidden sm:block" /> Everything you need to keep materials flowing.</p><button type="button" className="hero-action" onClick={() => onNavigate(primary)}>{primaryLabel}<Icon name="arrow" /></button></div>
        <div className="dashboard-hero-visual"><ConstructionAnimation /><div className="hero-flow" aria-label="Materials workflow"><div><span className="hero-flow-icon"><Icon name="requests" /></span><span>01</span><strong>Request</strong></div><i /><div><span className="hero-flow-icon"><Icon name="orders" /></span><span>02</span><strong>Purchase</strong></div><i /><div><span className="hero-flow-icon"><Icon name="inventory" /></span><span>03</span><strong>Receive & track</strong></div></div>
      </div></section>

      <nav className="dashboard-shortcuts" aria-label="Your role's quick access">
        {shortcuts.filter(item => can(item.page)).map(item => <button key={item.page} type="button" onClick={() => onNavigate(item.page)}><span><Icon name={item.page} /></span><div><strong>{item.label}</strong><small>{item.hint}</small></div><Icon name="arrow" /></button>)}
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
        <div role="status" aria-label="Loading summary and recent activity"><div className="dashboard-cards skeleton-grid" aria-hidden="true">{Array.from({ length: metrics.filter(item => item.show).length }, (_, index) => <div className="skeleton-card" key={index}><span /><strong /><small /></div>)}</div><span className="sr-only">Loading summary and recent activity...</span></div>
      ) : !error && data ? (
        <>
          <div className="dashboard-cards">
            {metrics.filter(item => item.show).map(item => <button key={item.key} type="button" className="dashboard-card" onClick={() => onNavigate(item.page)}><span className="dashboard-metric-icon" aria-hidden="true"><Icon name={item.icon} /></span><span>{item.label}</span><strong>{data.summary[item.key] ?? 0}</strong><small>Open workspace</small></button>)}
          </div>

          <div className="dashboard-activity">
          {reviewer && (<section className="panel">
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
          </section>)}

          {can('orders') && can('receipts') && (<section className="panel">
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
          </section>)}

          {can('receipts') && (<section className="panel">
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
          </section>)}

          {can('issues') && (<section className="panel">
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
          </section>)}

          {can('inventory') && (<section className="panel">
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
          </section>)}
          </div>
        </>
      ) : null}
    </div>
  )
}
