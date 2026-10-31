import { useRef, useState } from 'react'
import { money, send, today, useCommand, useData } from './ErpUi'
import { Feedback } from './Feedback'

type Billable = { id: number; goodsReceiptId: number; purchaseOrderId: number; supplierName: string; materialName: string; unitCost: number; remainingQuantity: number; receivedDate: string }
type Invoice = { id: number; number: string; purchaseOrderId: number; supplierName: string; invoiceDate: string; dueDate: string; status: string; total: number; paid: number; outstanding: number; payments: { id: number; amount: number; date: string; reference: string }[] }

export default function SupplierAccountsPage() {
  const invoices = useData<Invoice[]>('/api/supplier-accounts')
  const billable = useData<Billable[]>('/api/supplier-accounts/billable-lines')
  const command = useCommand()
  const invoiceKey = useRef(crypto.randomUUID())
  const paymentKey = useRef(crypto.randomUUID())
  const [order, setOrder] = useState('')
  const [number, setNumber] = useState('')
  const [date, setDate] = useState(today)
  const [due, setDue] = useState('')
  const [quantities, setQuantities] = useState<Record<number, string>>({})
  const [selectedId, setSelectedId] = useState('')
  const [amount, setAmount] = useState('')
  const [paymentDate, setPaymentDate] = useState(today)
  const [reference, setReference] = useState('')
  const orders = [...new Map(billable.data?.map(l => [l.purchaseOrderId, l])).values()]
  const lines = billable.data?.filter(l => l.purchaseOrderId === Number(order)) ?? []
  const selected = invoices.data?.find(i => i.id === Number(selectedId))
  function refresh() { invoices.refresh(); billable.refresh() }
  return <>
    <header className="page-header"><h1>Supplier Accounts</h1><p>Match invoices to received materials and record supplier payments in LKR.</p></header>
    <Feedback error={command.error || invoices.error || billable.error} message={command.message} />
    <section className="panel"><h2>Record matched invoice</h2><p>Invoice quantities cannot exceed unreturned, uninvoiced receipts. Unit prices must match the purchase order. This register covers material values before tax.</p>
      <form onSubmit={async e => { e.preventDefault(); if (await command.run(() => send('/api/supplier-accounts/invoices', {
        clientRequestId: invoiceKey.current, purchaseOrderId: Number(order), number, invoiceDate: date, dueDate: due,
        lines: lines.filter(l => quantities[l.id]?.trim()).map(l => ({ receiptLineId: l.id, quantity: Number(quantities[l.id]), unitPrice: l.unitCost })),
      }))) { invoiceKey.current = crypto.randomUUID(); setNumber(''); setOrder(''); setQuantities({}); refresh() } }}>
        <fieldset className="form-fields" disabled={command.busy}><div className="form-grid">
          <label>Purchase order<select value={order} onChange={e => { setOrder(e.target.value); setQuantities({}) }} required><option value="">Select order with billable receipts</option>{orders.map(o => <option key={o.purchaseOrderId} value={o.purchaseOrderId}>PO-{o.purchaseOrderId} — {o.supplierName}</option>)}</select></label>
          <label>Supplier invoice number<input value={number} onChange={e => setNumber(e.target.value)} maxLength={100} required /></label>
          <label>Invoice date<input type="date" value={date} onChange={e => setDate(e.target.value)} required /></label>
          <label>Due date<input type="date" value={due} min={date} onChange={e => setDue(e.target.value)} required /></label>
        </div>
        {lines.map(l => <div className="request-line" key={l.id}><p>GR-{l.goodsReceiptId} · {l.materialName} · Received {l.receivedDate} · Available to invoice: {l.remainingQuantity} · Unit price {money(l.unitCost)}</p><label>Quantity to invoice (leave blank to exclude)<input type="number" min="0.001" max={l.remainingQuantity} step="0.001" value={quantities[l.id] ?? ''} onChange={e => setQuantities(current => ({ ...current, [l.id]: e.target.value }))} /></label></div>)}
        <button className="primary-button" disabled={!lines.length}>Record invoice</button></fieldset>
      </form>
    </section>
    <section className="panel"><div className="section-toolbar"><h2>Invoices and outstanding balances</h2><button type="button" className="secondary-button" onClick={refresh}>Refresh</button></div>
      <div className="table-container"><table><thead><tr><th>Invoice</th><th>Supplier</th><th>Order</th><th>Due</th><th>Status</th><th>Total</th><th>Paid</th><th>Outstanding</th><th>Action</th></tr></thead><tbody>{invoices.data?.map(i => <tr key={i.id}><td>{i.number}</td><td>{i.supplierName}</td><td>PO-{i.purchaseOrderId}</td><td>{i.dueDate}</td><td>{i.status}</td><td>{money(i.total)}</td><td>{money(i.paid)}</td><td>{money(i.outstanding)}</td><td><button type="button" className="secondary-button" disabled={command.busy} onClick={() => { setSelectedId(String(i.id)); setAmount(''); setReference('') }}>View / pay</button></td></tr>)}</tbody></table></div>
    </section>
    {selected && <section className="panel"><h2>Invoice {selected.number}</h2><p>Outstanding: {money(selected.outstanding)}</p>
      {selected.status === 'Posted' && selected.outstanding > 0 && <form onSubmit={async e => { e.preventDefault(); if (!window.confirm('Record this supplier payment?')) return
        if (await command.run(() => send(`/api/supplier-accounts/invoices/${selected.id}/payments`, { clientRequestId: paymentKey.current, amount: Number(amount), date: paymentDate, reference }))) { paymentKey.current = crypto.randomUUID(); setAmount(''); setReference(''); refresh() }
      }}><fieldset className="form-fields" disabled={command.busy}><div className="form-grid">
        <label>Amount LKR<input type="number" min="0.01" max={selected.outstanding} step="0.01" value={amount} onChange={e => setAmount(e.target.value)} required /></label>
        <label>Payment date<input type="date" min={selected.invoiceDate} value={paymentDate} onChange={e => setPaymentDate(e.target.value)} required /></label>
        <label>Bank / payment reference<input value={reference} onChange={e => setReference(e.target.value)} maxLength={150} required /></label>
      </div><button className="primary-button">Record payment</button></fieldset></form>}
      <h3>Payment history</h3>{selected.payments.length === 0 ? <p>No payments recorded.</p> : <ul>{selected.payments.map(p => <li key={p.id}>{p.date} · {money(p.amount)} · {p.reference}</li>)}</ul>}
      {selected.status === 'Posted' && selected.paid === 0 && <button type="button" className="secondary-button" disabled={command.busy} onClick={async () => {
        const reason = window.prompt('Reason for cancelling this unpaid invoice:'); if (!reason?.trim()) return
        if (await command.run(() => send(`/api/supplier-accounts/invoices/${selected.id}/cancel`, { reason }))) refresh()
      }}>Cancel unpaid invoice</button>}
    </section>}
  </>
}
